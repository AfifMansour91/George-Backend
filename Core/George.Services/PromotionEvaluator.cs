using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using George.DB;
using George.Services.Request;
using George.Services.Response;

namespace George.Services;

/// <summary>
/// Pure-function promotion engine. Given a list of candidate <see cref="Promotion"/> rows
/// and an evaluation request, returns the applied promotions per the spec in
/// <c>Sprint4/מבצעים.md</c>. Also surfaces "near-miss" promotions (scope matches, threshold
/// not yet met) so the storefront can render encouragement messages from the
/// "הודעות עידוד וחיסכון בסל הקניות" section.
///
/// All payload reads use the camelCase v1 shape that <c>PromotionPayloadValidator</c>
/// enforces. The evaluator is intentionally side-effect free — load promotions and
/// site defaults outside, then call <see cref="Evaluate"/> with everything it needs.
/// </summary>
public static class PromotionEvaluator
{
    /// <summary>Per-site defaults used when a promotion doesn't override them.</summary>
    public sealed class SiteEvaluationDefaults
    {
        public string OveragePolicyDefault { get; init; } = "full_price";
        public bool ApplyOnPhoneOrders { get; init; } = true;
        public bool ApplyOnDiscountedProducts { get; init; } = false;
    }

    /// <summary>
    /// Either an applied promotion (threshold met) or a near-miss promotion (scope matches,
    /// threshold not yet met). Both can be null when the promotion doesn't apply at all.
    /// </summary>
    private readonly record struct EvalOutcome(AppliedPromotion? Applied, NearbyPromotion? Nearby, NearbyPromotion? SecondaryNearby = null)
    {
        public static EvalOutcome None => new(null, null, null);
        public static EvalOutcome From(AppliedPromotion a, NearbyPromotion? secondaryNearby = null) => new(a, null, secondaryNearby);
        public static EvalOutcome NearMiss(NearbyPromotion n) => new(null, n, null);
    }

    public static EvaluatePromotionsRes Evaluate(
        IReadOnlyList<Promotion> candidates,
        EvaluatePromotionsReq req,
        SiteEvaluationDefaults siteDefaults,
        DateTime utcNow,
        IReadOnlyDictionary<int, int>? priorCustomerRedemptionsByPromotionId = null)
    {
        var res = new EvaluatePromotionsRes();
        if (candidates is null || candidates.Count == 0 || req?.Cart is null || req.Cart.Count == 0)
            return res;

        var channel = (req.Channel ?? string.Empty).Trim().ToLowerInvariant();
        var coupon = NormalizeCoupon(req.CouponCode);

        if (!siteDefaults.ApplyOnPhoneOrders && channel == "phone")
            return res;

        foreach (var p in candidates)
        {
            if (!IsBaselineEligible(p, utcNow)) continue;
            if (!ChannelAllows(p, channel)) continue;
            if (!CouponMatches(p, coupon)) continue;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(p.PayloadJson) ? "{}" : p.PayloadJson);
                if (!ScheduleAllowsByPayload(doc.RootElement, utcNow)) continue;

                var (_, perCustomer) = ReadLimits(doc.RootElement);
                if (IsCustomerLimitReached(p.Id, perCustomer, priorCustomerRedemptionsByPromotionId)) continue;

                var type = (p.PromotionType ?? "").Trim().ToLowerInvariant();
                EvalOutcome outcome = type switch
                {
                    "discount" => EvaluateDiscount(p, doc.RootElement, req, siteDefaults),
                    "buy_x_pay_y" => EvaluateBuyXPayY(p, doc.RootElement, req, siteDefaults),
                    "buy_x_get_y" => EvaluateBuyXGetY(p, doc.RootElement, req, siteDefaults),
                    _ => EvalOutcome.None,
                };

                if (outcome.Applied is not null)
                {
                    res.PromotionsApplied.Add(outcome.Applied);
                    res.TotalDiscount += outcome.Applied.DiscountAmount;
                }
                else if (outcome.Nearby is not null)
                {
                    res.PromotionsNearby.Add(outcome.Nearby);
                }

                if (outcome.SecondaryNearby is not null)
                {
                    res.PromotionsNearby.Add(outcome.SecondaryNearby);
                }
            }
            catch (JsonException)
            {
                continue;
            }
            finally
            {
                doc?.Dispose();
            }
        }

        ApplyCouponPromotionCap(candidates, res);
        ApplyMaxPerLineStacking(res);
        return res;
    }

    // ─── Common gates ────────────────────────────────────────────────────────────

    private static bool IsBaselineEligible(Promotion p, DateTime utcNow)
    {
        if (p.IsDeleted || p.IsDraft || !p.IsActive) return false;
        if (p.ScheduleStartDateUtc is { } start && start.Date > utcNow.Date) return false;
        if (p.ScheduleEndDateUtc is { } end && end.Date < utcNow.Date) return false;
        return true;
    }

    private static bool ChannelAllows(Promotion p, string channel)
    {
        if (string.IsNullOrEmpty(channel)) return true;
        if (string.IsNullOrWhiteSpace(p.ChannelsJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(p.ChannelsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return true;
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String) continue;
                var v = (el.GetString() ?? "").Trim().ToLowerInvariant();
                if (v == "all" || v == channel) return true;
                if (!string.IsNullOrEmpty(v)) found.Add(v);
            }
            // Legacy: editor "all channels" used to save web+mobile+store without phone.
            if (channel == "phone" && found.Contains("web") && found.Contains("mobile") && found.Contains("store"))
                return true;
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool CouponMatches(Promotion p, string? cartCoupon)
    {
        if (string.IsNullOrWhiteSpace(p.CouponCode)) return true;
        return string.Equals(NormalizeCoupon(p.CouponCode), cartCoupon, StringComparison.Ordinal);
    }

    private static bool ScheduleAllowsByPayload(JsonElement payload, DateTime utcNow)
    {
        if (!payload.TryGetProperty("daysOfWeek", out var d) || d.ValueKind != JsonValueKind.Array) return true;
        if (d.GetArrayLength() == 0) return true;
        var todayKey = utcNow.DayOfWeek switch
        {
            DayOfWeek.Sunday => "Sun",
            DayOfWeek.Monday => "Mon",
            DayOfWeek.Tuesday => "Tue",
            DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday => "Thu",
            DayOfWeek.Friday => "Fri",
            DayOfWeek.Saturday => "Sat",
            _ => string.Empty,
        };
        foreach (var el in d.EnumerateArray())
            if (el.ValueKind == JsonValueKind.String && string.Equals(el.GetString(), todayKey, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string? NormalizeCoupon(string? c) =>
        string.IsNullOrWhiteSpace(c) ? null : c.Trim().ToLowerInvariant();

    private static List<string> ProductIdsFromLines(IEnumerable<EvaluateCartLine> lines) =>
        lines.Select(l => l.ProductId).Distinct(StringComparer.Ordinal).ToList();

    // ─── DISCOUNT (% / ₪) ────────────────────────────────────────────────────────

    private static EvalOutcome EvaluateDiscount(
        Promotion p, JsonElement payload, EvaluatePromotionsReq req, SiteEvaluationDefaults defaults)
    {
        var kind = (p.ListDiscountKind ?? "percent").Trim().ToLowerInvariant();
        if (kind != "percent" && kind != "amount") return EvalOutcome.None;

        var value = ReadDecimal(payload, "value");
        if (value is null || value <= 0m) return EvalOutcome.None;
        if (kind == "percent" && value > 100m) return EvalOutcome.None;

        var scope = ReadString(payload, "applyScope") ?? "all";
        var productIds = ReadIntSet(payload, "productIds");
        var categoryIds = ReadIntSet(payload, "categoryIds");
        var excludedIds = ReadIntSet(payload, "excludedProductIds");
        var wholeCart = scope == "whole_cart" || ReadBool(payload, "appliesToWholeCart") == true;

        var eligible = new List<EligibleItem>();
        foreach (var line in req.Cart)
        {
            if (line.PricePerUnit is null || line.Quantity <= 0) continue;
            if (TryGetIntId(line.ProductId, out var pid) && excludedIds.Contains(pid)) continue;
            if (!defaults.ApplyOnDiscountedProducts && IsLineCatalogDiscounted(line)) continue;

            bool ok = wholeCart
                ? true
                : scope switch
                {
                    "all" => true,
                    "products" => TryGetIntId(line.ProductId, out var pp) && productIds.Contains(pp),
                    "categories" => LineMatchesAnyCategory(line, categoryIds),
                    _ => true,
                };
            if (!ok) continue;

            var lineTotal = line.PricePerUnit.Value * line.Quantity;
            eligible.Add(new EligibleItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                OriginalPrice = lineTotal,
                DiscountAmount = 0m,
                FinalPrice = lineTotal,
            });
        }
        if (eligible.Count == 0) return EvalOutcome.None;

        // Threshold check + near-miss capture.
        if (ReadBool(payload, "applyPurchaseCondition") == true)
        {
            var minQty = ReadInt(payload, "minPurchaseQuantity");
            var minAmt = ReadDecimal(payload, "minPurchaseAmount");
            decimal currentQty = eligible.Sum(e => e.Quantity);
            decimal cartTotal = ResolveDisplaySubtotal(req);
            if (minQty is { } mq && currentQty < mq)
            {
                return EvalOutcome.NearMiss(new NearbyPromotion
                {
                    PromotionId = p.Id,
                    PromotionType = "discount",
                    PromotionName = p.Name,
                    Missing = new MissingThreshold { Kind = "quantity", Current = currentQty, Required = mq },
                    PotentialSaving = EstimateDiscountSaving(eligible, kind, value!.Value),
                    WholeCart = false,
                    TriggerProductIds = eligible.Select(e => e.ProductId).Distinct().ToList(),
                });
            }
            if (minAmt is { } ma && cartTotal < ma)
            {
                return EvalOutcome.NearMiss(new NearbyPromotion
                {
                    PromotionId = p.Id,
                    PromotionType = "discount",
                    PromotionName = p.Name,
                    Missing = new MissingThreshold { Kind = "amount", Current = cartTotal, Required = ma },
                    PotentialSaving = EstimateDiscountSaving(eligible, kind, value!.Value),
                    WholeCart = true,
                });
            }
        }

        var perItems = ReadInt(payload, "limitPerItems");
        var targets = ApplyPerItemsLimit(eligible, perItems);

        decimal totalDiscount = 0m;
        for (int i = 0; i < eligible.Count; i++)
        {
            if (!targets.Contains(i)) continue;
            var item = eligible[i];
            decimal lineDiscount = kind == "percent"
                ? Math.Round(item.OriginalPrice * value!.Value / 100m, 2, MidpointRounding.AwayFromZero)
                : Math.Min(value!.Value * item.Quantity, item.OriginalPrice);
            if (lineDiscount > item.OriginalPrice) lineDiscount = item.OriginalPrice;
            item.DiscountAmount = lineDiscount;
            item.FinalPrice = item.OriginalPrice - lineDiscount;
            totalDiscount += lineDiscount;
        }
        if (totalDiscount <= 0m) return EvalOutcome.None;

        return EvalOutcome.From(new AppliedPromotion
        {
            PromotionId = p.Id,
            PromotionType = "discount",
            PromotionName = p.Name,
            DiscountType = kind == "percent" ? "percentage" : "amount",
            DiscountValue = value,
            DiscountAmount = totalDiscount,
            EligibleItems = eligible,
            WholeCart = wholeCart,
            TriggerProductIds = wholeCart
                ? null
                : eligible.Where(e => e.DiscountAmount > 0).Select(e => e.ProductId).Distinct().ToList(),
        });
    }

    private static decimal EstimateDiscountSaving(List<EligibleItem> eligible, string kind, decimal value)
    {
        decimal sum = 0m;
        foreach (var i in eligible)
            sum += kind == "percent"
                ? Math.Round(i.OriginalPrice * value / 100m, 2, MidpointRounding.AwayFromZero)
                : Math.Min(value * i.Quantity, i.OriginalPrice);
        return sum;
    }

    private static HashSet<int> ApplyPerItemsLimit(List<EligibleItem> lines, int? perItems)
    {
        var all = Enumerable.Range(0, lines.Count).ToHashSet();
        if (perItems is null || perItems <= 0 || perItems >= lines.Count) return all;

        var sortedAsc = Enumerable.Range(0, lines.Count)
            .OrderBy(i => lines[i].Quantity == 0 ? 0m : lines[i].OriginalPrice / lines[i].Quantity)
            .ThenBy(i => i)
            .ToList();

        var result = new HashSet<int>();
        if (perItems == 1)
        {
            result.Add(sortedAsc[0]);
            return result;
        }
        for (int i = 0; i < perItems.Value - 1 && i < sortedAsc.Count - 1; i++) result.Add(sortedAsc[i]);
        result.Add(sortedAsc[^1]);
        return result;
    }

    // ─── BUY X PAY Y ─────────────────────────────────────────────────────────────

    private static EvalOutcome EvaluateBuyXPayY(
        Promotion p, JsonElement payload, EvaluatePromotionsReq req, SiteEvaluationDefaults defaults)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return EvalOutcome.None;
        if (!payload.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object) return EvalOutcome.None;

        var scope = (ReadString(cond, "scope") ?? "product").ToLowerInvariant();
        var buyQty = ReadQuantity(cond, "quantity");
        var fixedPrice = ReadDecimal(pricing, "fixedPrice") ?? 0m;
        if (buyQty <= 0m || fixedPrice <= 0m) return EvalOutcome.None;

        var excludedIds = ReadIntSet(cond, "excludedProductIds");
        var eligibleLines = new List<EvaluateCartLine>();
        if (scope == "product")
        {
            var pid = ReadInt(cond, "productId");
            if (pid is null) return EvalOutcome.None;
            foreach (var line in req.Cart)
            {
                if (IsPromotionGiftLine(line)) continue;
                if (!defaults.ApplyOnDiscountedProducts && IsLineCatalogDiscounted(line)) continue;
                if (TryGetIntId(line.ProductId, out var lid) && lid == pid) eligibleLines.Add(line);
            }
        }
        else if (scope == "category")
        {
            var cid = ReadInt(cond, "categoryId");
            if (cid is null) return EvalOutcome.None;
            foreach (var line in req.Cart)
            {
                if (IsPromotionGiftLine(line)) continue;
                if (!defaults.ApplyOnDiscountedProducts && IsLineCatalogDiscounted(line)) continue;
                if (!LineMatchesCategory(line, cid.Value)) continue;
                if (TryGetIntId(line.ProductId, out var pid2) && excludedIds.Contains(pid2)) continue;
                eligibleLines.Add(line);
            }
        }
        if (eligibleLines.Count == 0) return EvalOutcome.None;

        decimal totalQty = eligibleLines.Sum(l => l.Quantity);
        if (totalQty < buyQty)
        {
            decimal perUnit = eligibleLines
                .Where(l => l.PricePerUnit.HasValue && l.Quantity > 0)
                .Select(l => l.PricePerUnit!.Value)
                .DefaultIfEmpty(0m)
                .Max();
            decimal? potential = perUnit > 0m ? Math.Max(0m, perUnit * buyQty - fixedPrice) : null;
            return EvalOutcome.NearMiss(new NearbyPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_pay_y",
                PromotionName = p.Name,
                Missing = new MissingThreshold { Kind = "quantity", Current = totalQty, Required = buyQty },
                PotentialSaving = potential,
                TriggerProductIds = ProductIdsFromLines(eligibleLines),
            });
        }

        var policy = (ReadString(payload, "overagePolicy") ?? defaults.OveragePolicyDefault).ToLowerInvariant();
        if (policy != "same_price" && policy != "full_price") policy = "full_price";

        var (perOrderLimit, _) = ReadLimits(payload);
        int bundlesApplied = (int)Math.Floor(totalQty / buyQty);
        if (perOrderLimit is > 0) bundlesApplied = Math.Min(bundlesApplied, perOrderLimit.Value);
        if (bundlesApplied < 1) return EvalOutcome.None;

        decimal bundledQty = bundlesApplied * buyQty;
        decimal overageQty = totalQty - bundledQty;

        var pricedLines = eligibleLines
            .Where(l => l.PricePerUnit.HasValue && l.Quantity > 0)
            .Select(l => (Line: l, Price: l.PricePerUnit!.Value))
            .OrderByDescending(x => x.Price)
            .ToList();

        decimal needed = bundledQty;
        var consumedValueByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal bundledNormalValue = 0m;
        var remainingByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var pl in pricedLines)
            remainingByProduct[pl.Line.ProductId] = pl.Line.Quantity;

        foreach (var pl in pricedLines)
        {
            if (needed <= 0m) break;
            if (!remainingByProduct.TryGetValue(pl.Line.ProductId, out var avail) || avail <= 0m) continue;
            var take = Math.Min(avail, needed);
            var value = take * pl.Price;
            bundledNormalValue += value;
            consumedValueByProduct[pl.Line.ProductId] =
                consumedValueByProduct.GetValueOrDefault(pl.Line.ProductId) + value;
            remainingByProduct[pl.Line.ProductId] = avail - take;
            needed -= take;
        }

        decimal promoCost = bundlesApplied * fixedPrice;
        decimal overageCost = 0m;
        if (overageQty > 0m)
        {
            var overagePool = pricedLines
                .Select(pl => (pl.Line, pl.Price, Remaining: remainingByProduct.GetValueOrDefault(pl.Line.ProductId)))
                .Where(x => x.Remaining > 0m)
                .OrderBy(x => x.Price)
                .ToList();
            decimal overLeft = overageQty;
            foreach (var entry in overagePool)
            {
                if (overLeft <= 0m) break;
                var take = Math.Min(entry.Remaining, overLeft);
                if (policy == "same_price")
                    overageCost += take * (buyQty == 0m ? 0m : fixedPrice / buyQty);
                else
                    overageCost += take * entry.Price;
                overLeft -= take;
            }
        }

        decimal originalEligibleCost = eligibleLines
            .Where(l => l.PricePerUnit.HasValue)
            .Sum(l => l.PricePerUnit!.Value * l.Quantity);
        decimal totalCost = promoCost + overageCost;
        decimal discountAmount = Math.Max(0m, originalEligibleCost - totalCost);
        if (discountAmount <= 0m) return EvalOutcome.None;

        var eligibleItems = new List<EligibleItem>();
        if (bundledNormalValue > 0m)
        {
            foreach (var pl in pricedLines)
            {
                if (!consumedValueByProduct.TryGetValue(pl.Line.ProductId, out var consumedVal) || consumedVal <= 0m)
                    continue;
                var lineQty = pl.Line.Quantity;
                var lineOriginal = pl.Price * lineQty;
                var lineDiscount = discountAmount * (consumedVal / bundledNormalValue);
                eligibleItems.Add(new EligibleItem
                {
                    ProductId = pl.Line.ProductId,
                    Quantity = lineQty,
                    OriginalPrice = lineOriginal,
                    DiscountAmount = lineDiscount,
                    FinalPrice = lineOriginal - lineDiscount,
                });
            }
        }

        decimal roundedDiscount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
        foreach (var ei in eligibleItems)
        {
            ei.DiscountAmount = Math.Round(ei.DiscountAmount, 2, MidpointRounding.AwayFromZero);
            ei.FinalPrice = Math.Round(ei.OriginalPrice - ei.DiscountAmount, 2, MidpointRounding.AwayFromZero);
        }

        NearbyPromotion? nextSet = null;
        decimal remainder = totalQty - bundledQty;
        if (remainder > 0.001m && !(perOrderLimit is > 0 && bundlesApplied >= perOrderLimit.Value))
        {
            nextSet = new NearbyPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_pay_y",
                PromotionName = p.Name,
                Missing = new MissingThreshold { Kind = "next_set", Current = remainder, Required = buyQty },
                TriggerProductIds = ProductIdsFromLines(eligibleLines),
            };
        }

        return EvalOutcome.From(new AppliedPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_pay_y",
            PromotionName = p.Name,
            DiscountType = "fixed_price",
            DiscountAmount = roundedDiscount,
            EligibleItems = eligibleItems.Count > 0 ? eligibleItems : null,
            PriceBreakdown = new BxpyPriceBreakdown
            {
                BaseQuantity = buyQty,
                ActualQuantity = bundledQty,
                OverageQuantity = overageQty,
                OveragePolicy = policy,
                PromotionPrice = promoCost,
                OveragePrice = Math.Round(overageCost, 2, MidpointRounding.AwayFromZero),
                Total = Math.Round(totalCost, 2, MidpointRounding.AwayFromZero),
                DisplayPricePerUnit = buyQty == 0m ? 0m : Math.Round(promoCost / bundledQty, 2, MidpointRounding.AwayFromZero),
            },
            TriggerProductIds = ProductIdsFromLines(eligibleLines),
        }, nextSet);
    }

    // ─── BUY X GET Y ─────────────────────────────────────────────────────────────

    private static EvalOutcome EvaluateBuyXGetY(
        Promotion p, JsonElement payload, EvaluatePromotionsReq req, SiteEvaluationDefaults defaults)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return EvalOutcome.None;
        if (!payload.TryGetProperty("reward", out var reward) || reward.ValueKind != JsonValueKind.Object) return EvalOutcome.None;

        var benefitAppliesTo = (ReadString(reward, "benefitAppliesTo") ?? "products").ToLowerInvariant();
        if (benefitAppliesTo is "product") benefitAppliesTo = "products";
        if (benefitAppliesTo is "category") benefitAppliesTo = "categories";

        var rewardIds = ReadIntSet(reward, "productIds").ToList();
        if (benefitAppliesTo == "products" && rewardIds.Count == 0) return EvalOutcome.None;

        var discountType = (ReadString(reward, "discountType") ?? "free").ToLowerInvariant();
        var discountValue = ReadDecimal(reward, "discountValue");
        var benefitQuantity = Math.Max(1, ReadInt(reward, "benefitQuantity") ?? ReadInt(reward, "maxQuantity") ?? 1);
        var applyToCheapest = ReadBool(reward, "applyToCheapest") == true;
        var autoAdd = ReadBool(reward, "autoAdd") != false; // default true like WP admin form

        var productScope = (ReadString(cond, "productScope") ?? "all").ToLowerInvariant();
        var buyProductIds = ReadIntSet(cond, "productIds");
        var buyCategoryIds = ReadIntSet(cond, "categoryIds");
        var excludedIds = ReadIntSet(cond, "excludedProductIds");

        var buyLines = CollectBxgyBuyLines(req.Cart, productScope, buyProductIds, buyCategoryIds, excludedIds, defaults);
        if (buyLines.Count == 0) return EvalOutcome.None;

        var minQty = ReadInt(cond, "minQuantity") ?? 1;
        var minAmt = ReadDecimal(cond, "minAmount");
        decimal buyQty = buyLines.Sum(l => l.Quantity);
        decimal buyAmt = buyLines.Where(l => l.PricePerUnit.HasValue).Sum(l => l.PricePerUnit!.Value * l.Quantity);
        decimal displaySubtotal = ResolveDisplaySubtotal(req);

        NearbyPromotion BuildNearby(string kind, decimal current, decimal required) => new NearbyPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_get_y",
            PromotionName = p.Name,
            Missing = new MissingThreshold { Kind = kind, Current = current, Required = required },
            RewardProductId = benefitAppliesTo == "products" && rewardIds.Count == 1 ? rewardIds[0] : null,
            RewardDiscountType = discountType,
            RewardDiscountValue = discountValue,
            TriggerProductIds = ProductIdsFromLines(buyLines),
        };

        if (buyQty < minQty) return EvalOutcome.NearMiss(BuildNearby("quantity", buyQty, minQty));
        if (minAmt is > 0 && displaySubtotal < minAmt) return EvalOutcome.NearMiss(BuildNearby("amount", displaySubtotal, minAmt.Value));

        var (perOrderLimit, _) = ReadLimits(payload);

        if (benefitAppliesTo != "products")
            return EvaluateBxgySharedPool(p, payload, req, buyLines, minQty, minAmt, benefitAppliesTo, reward, benefitQuantity, applyToCheapest, perOrderLimit, discountType, discountValue);

        return EvaluateBxgyDistinctProducts(p, req, buyLines, minQty, buyAmt, minAmt, displaySubtotal, rewardIds, discountType, discountValue, benefitQuantity, applyToCheapest, autoAdd, perOrderLimit);
    }

    private static List<EvaluateCartLine> CollectBxgyBuyLines(
        IReadOnlyList<EvaluateCartLine> cart,
        string productScope,
        HashSet<int> productIds,
        HashSet<int> categoryIds,
        HashSet<int> excludedIds,
        SiteEvaluationDefaults defaults)
    {
        var buy = new List<EvaluateCartLine>();
        foreach (var line in cart)
        {
            if (IsPromotionGiftLine(line)) continue;
            if (!defaults.ApplyOnDiscountedProducts && IsLineCatalogDiscounted(line)) continue;
            if (TryGetIntId(line.ProductId, out var lid) && excludedIds.Contains(lid)) continue;
            bool ok = productScope switch
            {
                "all" => true,
                "specific_products" => TryGetIntId(line.ProductId, out var pp) && productIds.Contains(pp),
                "specific_categories" => LineMatchesAnyCategory(line, categoryIds),
                _ => true,
            };
            if (ok) buy.Add(line);
        }
        return buy;
    }

    private static EvalOutcome EvaluateBxgyDistinctProducts(
        Promotion p,
        EvaluatePromotionsReq req,
        List<EvaluateCartLine> buyLines,
        int minQty,
        decimal buyAmt,
        decimal? minAmt,
        decimal cartSubtotal,
        List<int> rewardIds,
        string discountType,
        decimal? discountValue,
        int benefitQuantity,
        bool applyToCheapest,
        bool autoAdd,
        int? perOrderLimit)
    {
        decimal buyQty = buyLines.Sum(l => l.Quantity);
        int fulfillments = (int)Math.Floor(buyQty / minQty);
        if (fulfillments < 1) return EvalOutcome.None;

        int benefitUnits = fulfillments * benefitQuantity;
        if (perOrderLimit is > 0) benefitUnits = Math.Min(benefitUnits, perOrderLimit.Value);
        if (benefitUnits < 1) return EvalOutcome.None;

        if (rewardIds.Count > 1)
        {
            var nextSetMulti = BuildBxgyBuySideNextSetNudge(p, buyLines, buyQty, minQty, benefitQuantity, perOrderLimit, null, discountType, discountValue);
            return EvalOutcome.From(new AppliedPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_get_y",
                PromotionName = p.Name,
                DiscountType = discountType,
                DiscountValue = discountValue,
                DiscountAmount = 0m,
                RewardOptions = rewardIds.Select(id => new RewardOption { ProductId = id, ProductName = string.Empty }).ToList(),
                TriggerProductIds = ProductIdsFromLines(buyLines),
                AutoAddQuantity = benefitUnits,
            }, nextSetMulti);
        }

        var rewardId = rewardIds[0];
        var rewardLine = req.Cart.FirstOrDefault(l => TryGetIntId(l.ProductId, out var rid) && rid == rewardId);
        if (rewardLine is null || rewardLine.PricePerUnit is null)
        {
            var nextSetUnlock = BuildBxgyBuySideNextSetNudge(p, buyLines, buyQty, minQty, benefitQuantity, perOrderLimit, rewardId, discountType, discountValue);
            return EvalOutcome.From(new AppliedPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_get_y",
                PromotionName = p.Name,
                DiscountType = discountType,
                DiscountValue = discountValue,
                DiscountAmount = 0m,
                RewardProductId = rewardId,
                TriggerProductIds = ProductIdsFromLines(buyLines),
                AutoAddEligible = autoAdd && discountType == "free",
                AutoAddQuantity = benefitUnits,
            }, nextSetUnlock);
        }

        decimal discountableQty = Math.Min(rewardLine.Quantity, benefitUnits);
        if (discountableQty <= 0m) return EvalOutcome.None;

        decimal perUnitBenefit = ComputeBxgyPerUnitBenefit(rewardLine.PricePerUnit.Value, discountType, discountValue);
        if (perUnitBenefit <= 0m) return EvalOutcome.None;

        decimal rewardDiscount = Math.Round(perUnitBenefit * discountableQty, 2, MidpointRounding.AwayFromZero);
        if (rewardDiscount <= 0m) return EvalOutcome.None;

        var nextSetApplied = BuildBxgyBuySideNextSetNudge(p, buyLines, buyQty, minQty, benefitQuantity, perOrderLimit, rewardId, discountType, discountValue);
        return EvalOutcome.From(new AppliedPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_get_y",
            PromotionName = p.Name,
            RewardProductId = rewardId,
            DiscountType = discountType,
            DiscountValue = discountValue,
            DiscountAmount = rewardDiscount,
            EligibleItems =
            [
                new EligibleItem
                {
                    ProductId = rewardLine.ProductId,
                    Quantity = discountableQty,
                    OriginalPrice = rewardLine.PricePerUnit.Value * discountableQty,
                    DiscountAmount = rewardDiscount,
                    FinalPrice = rewardLine.PricePerUnit.Value * discountableQty - rewardDiscount,
                },
            ],
            TriggerProductIds = ProductIdsFromLines(buyLines),
        }, nextSetApplied);
    }

    private static EvalOutcome EvaluateBxgySharedPool(
        Promotion p,
        JsonElement payload,
        EvaluatePromotionsReq req,
        List<EvaluateCartLine> buyLines,
        int minQty,
        decimal? minAmt,
        string benefitAppliesTo,
        JsonElement reward,
        int benefitQuantity,
        bool applyToCheapest,
        int? perOrderLimit,
        string discountType,
        decimal? discountValue)
    {
        if (!payload.TryGetProperty("condition", out var cond)) return EvalOutcome.None;

        var productScope = (ReadString(cond, "productScope") ?? "all").ToLowerInvariant();
        var buyProductIds = ReadIntSet(cond, "productIds");
        var buyCategoryIds = ReadIntSet(cond, "categoryIds");
        var buyExcluded = ReadIntSet(cond, "excludedProductIds");
        var benefitCategoryIds = ReadIntSet(reward, "categoryIds");
        var benefitExcluded = ReadIntSet(reward, "benefitExcludedProductIds");

        var blines = new List<(EvaluateCartLine Line, decimal PerUnitBenefit)>();
        foreach (var line in req.Cart)
        {
            if (!BxgyBenefitLineMatches(line, benefitAppliesTo, productScope, buyProductIds, buyCategoryIds, buyExcluded, ReadIntSet(reward, "productIds"), benefitCategoryIds, benefitExcluded))
                continue;
            if (line.PricePerUnit is null || line.PricePerUnit <= 0) continue;
            var perUnit = ComputeBxgyPerUnitBenefit(line.PricePerUnit.Value, discountType, discountValue);
            if (perUnit <= 0m) continue;
            blines.Add((line, perUnit));
        }
        if (blines.Count == 0) return EvalOutcome.None;

        var units = new List<(string ProductId, decimal Value)>();
        var perUnitByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var qtyByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (line, perUnit) in blines)
        {
            int whole = (int)Math.Floor(line.Quantity);
            if (whole < 1) continue;
            perUnitByProduct[line.ProductId] = perUnit;
            qtyByProduct[line.ProductId] = line.Quantity;
            for (int i = 0; i < whole; i++)
                units.Add((line.ProductId, perUnit));
        }
        if (units.Count == 0) return EvalOutcome.None;

        int group = minQty + benefitQuantity;
        int fulfillments = group > 0 ? units.Count / group : 0;
        if (fulfillments < 1) return EvalOutcome.None;

        int benefitUnits = fulfillments * benefitQuantity;
        if (perOrderLimit is > 0) benefitUnits = Math.Min(benefitUnits, perOrderLimit.Value);
        if (benefitUnits < 1) return EvalOutcome.None;

        var chosen = SelectBxgyBenefitUnits(units, benefitUnits, applyToCheapest);
        if (chosen.Count == 0) return EvalOutcome.None;

        var freed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pid in chosen)
            freed[pid] = freed.TryGetValue(pid, out var c) ? c + 1 : 1;

        var eligibleItems = new List<EligibleItem>();
        decimal totalDiscount = 0m;
        foreach (var (pid, count) in freed)
        {
            if (!perUnitByProduct.TryGetValue(pid, out var pu) || !qtyByProduct.TryGetValue(pid, out var lineQty) || lineQty <= 0)
                continue;
            decimal lineDiscount = Math.Round(pu * count, 2, MidpointRounding.AwayFromZero);
            totalDiscount += lineDiscount;
            eligibleItems.Add(new EligibleItem
            {
                ProductId = pid,
                Quantity = count,
                OriginalPrice = pu * count,
                DiscountAmount = lineDiscount,
                FinalPrice = 0m,
            });
        }
        if (totalDiscount <= 0m) return EvalOutcome.None;

        decimal buyQty = buyLines.Sum(l => l.Quantity);
        var nextSetPool = BuildBxgyBuySideNextSetNudge(p, buyLines, buyQty, minQty, benefitQuantity, perOrderLimit, null, discountType, discountValue);
        return EvalOutcome.From(new AppliedPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_get_y",
            PromotionName = p.Name,
            DiscountType = discountType,
            DiscountValue = discountValue,
            DiscountAmount = totalDiscount,
            EligibleItems = eligibleItems,
            TriggerProductIds = ProductIdsFromLines(buyLines),
        }, nextSetPool);
    }

    private static NearbyPromotion? BuildBxgyBuySideNextSetNudge(
        Promotion p,
        IReadOnlyList<EvaluateCartLine> buyLines,
        decimal buyQty,
        int minQty,
        int benefitQuantity,
        int? perOrderLimit,
        int? rewardProductId,
        string discountType,
        decimal? discountValue)
    {
        if (minQty < 1) return null;
        int fulfillments = (int)Math.Floor(buyQty / minQty);
        if (fulfillments < 1) return null;
        decimal remainder = buyQty - fulfillments * minQty;
        if (remainder <= 0.001m) return null;
        if (perOrderLimit is > 0 && fulfillments * benefitQuantity >= perOrderLimit.Value) return null;

        return new NearbyPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_get_y",
            PromotionName = p.Name,
            Missing = new MissingThreshold { Kind = "next_set", Current = remainder, Required = minQty },
            RewardProductId = rewardProductId,
            RewardDiscountType = discountType,
            RewardDiscountValue = discountValue,
            TriggerProductIds = ProductIdsFromLines(buyLines),
        };
    }

    private static bool BxgyBenefitLineMatches(
        EvaluateCartLine line,
        string benefitAppliesTo,
        string buyScope,
        HashSet<int> buyProductIds,
        HashSet<int> buyCategoryIds,
        HashSet<int> buyExcluded,
        HashSet<int> benefitProductIds,
        HashSet<int> benefitCategoryIds,
        HashSet<int> benefitExcluded)
    {
        if (IsPromotionGiftLine(line)) return false;
        if (TryGetIntId(line.ProductId, out var pid) && benefitExcluded.Contains(pid)) return false;

        if (benefitAppliesTo == "products")
            return TryGetIntId(line.ProductId, out var pp) && benefitProductIds.Contains(pp);

        if (benefitAppliesTo == "categories")
            return LineMatchesAnyCategory(line, benefitCategoryIds);

        if (benefitAppliesTo == "same")
        {
            if (TryGetIntId(line.ProductId, out var spid) && buyExcluded.Contains(spid)) return false;
            return buyScope switch
            {
                "all" => true,
                "specific_products" => TryGetIntId(line.ProductId, out var pp) && buyProductIds.Contains(pp),
                "specific_categories" => LineMatchesAnyCategory(line, buyCategoryIds),
                _ => true,
            };
        }

        // all
        return true;
    }

    private static decimal ComputeBxgyPerUnitBenefit(decimal price, string discountType, decimal? discountValue) =>
        discountType switch
        {
            "free" => price,
            "percentage" when discountValue is > 0 and <= 100 => Math.Round(price * discountValue.Value / 100m, 2, MidpointRounding.AwayFromZero),
            "fixed_price" when discountValue is >= 0 => Math.Max(0m, price - discountValue.Value),
            _ => 0m,
        };

    /// <summary>Israeli-law default: spread free units across price tiers; optional cheapest-only mode.</summary>
    private static List<string> SelectBxgyBenefitUnits(List<(string ProductId, decimal Value)> units, int need, bool cheapest)
    {
        if (need < 1 || units.Count == 0) return new List<string>();

        if (cheapest)
        {
            return units.OrderBy(u => u.Value).Take(need).Select(u => u.ProductId).ToList();
        }

        var sorted = units.OrderByDescending(u => u.Value).ToList();
        if (need >= sorted.Count)
            return sorted.Select(u => u.ProductId).ToList();

        var picked = new List<string>();
        for (int g = 0; g < need; g++)
        {
            int end = (int)Math.Floor((g + 1) * sorted.Count / (double)need);
            int freeIdx = end - 1;
            if (freeIdx >= 0 && freeIdx < sorted.Count)
                picked.Add(sorted[freeIdx].ProductId);
        }
        return picked;
    }

    private static bool IsLineCatalogDiscounted(EvaluateCartLine line) =>
        line.IsCatalogDiscounted == true;

    /// <summary>
    /// Cart subtotal for minimum-spend gates — prefers client <see cref="EvaluatePromotionsReq.CartTotal"/>
    /// (display/checkout total, tax-inclusive when the storefront sends it) over a recomputed line sum.
    /// </summary>
    private static decimal ResolveDisplaySubtotal(EvaluatePromotionsReq req)
    {
        if (req.CartTotal is > 0m) return req.CartTotal.Value;
        return req.Cart
            .Where(l => !IsPromotionGiftLine(l) && l.PricePerUnit.HasValue)
            .Sum(l => l.PricePerUnit!.Value * l.Quantity);
    }

    private static bool IsPromotionGiftLine(EvaluateCartLine line) =>
        string.Equals(line.Source, "promotion_gift", StringComparison.OrdinalIgnoreCase);

    // ─── Stacking (max discount per product line — matches WP plugin engine) ─────

    /// <summary>
    /// When multiple coupon-gated promotions match the same cart coupon, keep the best discount only.
    /// Auto promotions (no coupon) are unaffected and stack via <see cref="ApplyMaxPerLineStacking"/>.
    /// </summary>
    private static void ApplyCouponPromotionCap(IReadOnlyList<Promotion> candidates, EvaluatePromotionsRes res)
    {
        if (res.PromotionsApplied.Count <= 1) return;

        var couponByPromoId = new Dictionary<int, string?>();
        foreach (var p in candidates)
            couponByPromoId[p.Id] = NormalizeCoupon(p.CouponCode);

        var couponGated = new List<(int Index, AppliedPromotion Applied)>();
        for (int i = 0; i < res.PromotionsApplied.Count; i++)
        {
            var a = res.PromotionsApplied[i];
            if (couponByPromoId.TryGetValue(a.PromotionId, out var code) && !string.IsNullOrEmpty(code))
                couponGated.Add((i, a));
        }
        if (couponGated.Count <= 1) return;

        var keepIndex = couponGated
            .OrderByDescending(x => x.Applied.DiscountAmount)
            .ThenBy(x => x.Index)
            .First().Index;

        var filtered = new List<AppliedPromotion>();
        decimal total = 0m;
        for (int i = 0; i < res.PromotionsApplied.Count; i++)
        {
            var a = res.PromotionsApplied[i];
            if (couponByPromoId.TryGetValue(a.PromotionId, out var code)
                && !string.IsNullOrEmpty(code)
                && i != keepIndex)
                continue;
            filtered.Add(a);
            total += a.DiscountAmount;
        }
        res.PromotionsApplied = filtered;
        res.TotalDiscount = total;
    }

    private static void ApplyMaxPerLineStacking(EvaluatePromotionsRes res)
    {
        if (res.PromotionsApplied.Count <= 1) return;

        var contributions = new List<Dictionary<string, decimal>>();
        foreach (var a in res.PromotionsApplied)
        {
            if (IsSignalOnlyPromotion(a))
            {
                contributions.Add(new Dictionary<string, decimal>(StringComparer.Ordinal));
                continue;
            }
            contributions.Add(ExtractProductDiscounts(a));
        }

        var bestByProduct = new Dictionary<string, (int PromoIndex, decimal Amount)>(StringComparer.Ordinal);
        for (int i = 0; i < contributions.Count; i++)
        {
            foreach (var (pid, amt) in contributions[i])
            {
                if (amt <= 0m) continue;
                if (!bestByProduct.TryGetValue(pid, out var cur) || amt > cur.Amount)
                    bestByProduct[pid] = (i, amt);
            }
        }

        var kept = new HashSet<int>();
        for (int i = 0; i < res.PromotionsApplied.Count; i++)
        {
            var a = res.PromotionsApplied[i];
            if (IsSignalOnlyPromotion(a))
            {
                kept.Add(i);
                continue;
            }
            var map = contributions[i];
            bool hasWinning = false;
            foreach (var pid in map.Keys)
            {
                if (bestByProduct.TryGetValue(pid, out var win) && win.PromoIndex == i && win.Amount > 0m)
                {
                    hasWinning = true;
                    break;
                }
            }
            if (a.WholeCart && a.DiscountAmount > 0m) hasWinning = true;
            if (hasWinning) kept.Add(i);
        }

        var filtered = new List<AppliedPromotion>();
        decimal total = 0m;
        for (int i = 0; i < res.PromotionsApplied.Count; i++)
        {
            if (!kept.Contains(i)) continue;
            var a = res.PromotionsApplied[i];
            if (!IsSignalOnlyPromotion(a) && a.EligibleItems?.Count > 0)
            {
                decimal promoTotal = 0m;
                foreach (var ei in a.EligibleItems)
                {
                    if (bestByProduct.TryGetValue(ei.ProductId, out var win) && win.PromoIndex == i)
                    {
                        ei.DiscountAmount = win.Amount;
                        ei.FinalPrice = ei.OriginalPrice - win.Amount;
                        promoTotal += win.Amount;
                    }
                    else
                    {
                        ei.DiscountAmount = 0m;
                        ei.FinalPrice = ei.OriginalPrice;
                    }
                }
                a.DiscountAmount = a.WholeCart ? a.DiscountAmount : promoTotal;
            }
            else if (!IsSignalOnlyPromotion(a) && a.RewardProductId is int rid && a.DiscountAmount > 0m)
            {
                var pid = rid.ToString();
                if (bestByProduct.TryGetValue(pid, out var win) && win.PromoIndex == i)
                    a.DiscountAmount = win.Amount;
                else
                    continue;
            }
            else if (!IsSignalOnlyPromotion(a) && a.PromotionType == "buy_x_pay_y")
            {
                // BxPY discount is bundle-level — keep if any trigger product won or no overlap.
                var triggers = a.TriggerProductIds ?? new List<string>();
                bool ok = triggers.Count == 0 || triggers.Any(t => bestByProduct.ContainsKey(t));
                if (!ok) continue;
            }

            filtered.Add(a);
            total += a.DiscountAmount;
        }

        res.PromotionsApplied = filtered;
        res.TotalDiscount = total;
    }

    private static bool IsSignalOnlyPromotion(AppliedPromotion a) =>
        a.DiscountAmount <= 0m &&
        (a.RewardProductId != null || (a.RewardOptions?.Count ?? 0) > 0);

    private static Dictionary<string, decimal> ExtractProductDiscounts(AppliedPromotion a)
    {
        var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (a.EligibleItems?.Count > 0)
        {
            foreach (var ei in a.EligibleItems)
            {
                if (ei.DiscountAmount <= 0) continue;
                map[ei.ProductId] = map.TryGetValue(ei.ProductId, out var cur) ? cur + ei.DiscountAmount : ei.DiscountAmount;
            }
            return map;
        }
        if (a.RewardProductId is int rid && a.DiscountAmount > 0m)
            map[rid.ToString()] = a.DiscountAmount;
        else if (a.PromotionType == "buy_x_pay_y" && a.DiscountAmount > 0m && a.TriggerProductIds?.Count > 0)
        {
            var share = a.DiscountAmount / a.TriggerProductIds.Count;
            foreach (var pid in a.TriggerProductIds)
                map[pid] = share;
        }
        return map;
    }

    private static bool LineMatchesCategory(EvaluateCartLine line, int categoryId)
    {
        if (TryGetIntId(line.CategoryId, out var c) && c == categoryId) return true;
        if (line.CategoryIds == null) return false;
        foreach (var id in line.CategoryIds)
            if (TryGetIntId(id, out var cc) && cc == categoryId) return true;
        return false;
    }

    private static bool LineMatchesAnyCategory(EvaluateCartLine line, HashSet<int> categoryIds)
    {
        if (TryGetIntId(line.CategoryId, out var c) && categoryIds.Contains(c)) return true;
        if (line.CategoryIds == null) return false;
        foreach (var id in line.CategoryIds)
            if (TryGetIntId(id, out var cc) && categoryIds.Contains(cc)) return true;
        return false;
    }

    // ─── JSON helpers ────────────────────────────────────────────────────────────

    private static (int? PerOrder, int? PerCustomer) ReadLimits(JsonElement payload)
    {
        int? perOrder = null;
        int? perCustomer = null;
        if (payload.TryGetProperty("limits", out var lim) && lim.ValueKind == JsonValueKind.Object)
        {
            perOrder = ReadInt(lim, "perOrder");
            perCustomer = ReadInt(lim, "perCustomer");
        }
        perCustomer ??= ReadInt(payload, "limitPerCustomer");
        return (perOrder, perCustomer);
    }

    private static bool IsCustomerLimitReached(
        int promotionId,
        int? perCustomer,
        IReadOnlyDictionary<int, int>? priorCustomerRedemptions)
    {
        if (perCustomer is not > 0 || priorCustomerRedemptions is null) return false;
        return priorCustomerRedemptions.TryGetValue(promotionId, out var count) && count >= perCustomer.Value;
    }

    private static bool TryGetIntId(string? raw, out int value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out value);
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal ReadQuantity(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number) return 0m;
        return v.TryGetDecimal(out var d) ? d : 0m;
    }

    private static int? ReadInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static decimal? ReadDecimal(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;

    private static bool? ReadBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : null;

    private static HashSet<int> ReadIntSet(JsonElement obj, string name)
    {
        var set = new HashSet<int>();
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return set;
        foreach (var el in arr.EnumerateArray())
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) set.Add(n);
        return set;
    }
}
