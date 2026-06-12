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
    private readonly record struct EvalOutcome(AppliedPromotion? Applied, NearbyPromotion? Nearby)
    {
        public static EvalOutcome None => new(null, null);
        public static EvalOutcome From(AppliedPromotion a) => new(a, null);
        public static EvalOutcome NearMiss(NearbyPromotion n) => new(null, n);
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
            decimal cartTotal = req.CartTotal ?? eligible.Sum(e => e.OriginalPrice);
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
        var qty = ReadInt(cond, "quantity") ?? 0;
        var fixedPrice = ReadDecimal(pricing, "fixedPrice") ?? 0m;
        if (qty <= 0 || fixedPrice <= 0m) return EvalOutcome.None;

        var excludedIds = ReadIntSet(cond, "excludedProductIds");
        var eligibleLines = new List<EvaluateCartLine>();
        if (scope == "product")
        {
            var pid = ReadInt(cond, "productId");
            if (pid is null) return EvalOutcome.None;
            foreach (var line in req.Cart)
                if (TryGetIntId(line.ProductId, out var lid) && lid == pid) eligibleLines.Add(line);
        }
        else if (scope == "category")
        {
            var cid = ReadInt(cond, "categoryId");
            if (cid is null) return EvalOutcome.None;
            foreach (var line in req.Cart)
            {
                if (!LineMatchesCategory(line, cid.Value)) continue;
                if (TryGetIntId(line.ProductId, out var pid2) && excludedIds.Contains(pid2)) continue;
                eligibleLines.Add(line);
            }
        }
        if (eligibleLines.Count == 0) return EvalOutcome.None;

        decimal totalQty = eligibleLines.Sum(l => l.Quantity);
        if (totalQty < qty)
        {
            // Near-miss: tell the storefront how much more to add and the saving they'd unlock.
            decimal listSum = eligibleLines
                .Where(l => l.PricePerUnit.HasValue)
                .Sum(l => l.PricePerUnit!.Value * l.Quantity);
            decimal? potential = null;
            decimal perUnit = eligibleLines
                .Where(l => l.PricePerUnit.HasValue && l.Quantity > 0)
                .Select(l => l.PricePerUnit!.Value)
                .DefaultIfEmpty(0m)
                .Max();
            if (perUnit > 0m) potential = Math.Max(0m, perUnit * qty - fixedPrice);
            return EvalOutcome.NearMiss(new NearbyPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_pay_y",
                PromotionName = p.Name,
                Missing = new MissingThreshold { Kind = "quantity", Current = totalQty, Required = qty },
                PotentialSaving = potential,
                TriggerProductIds = ProductIdsFromLines(eligibleLines),
            });
        }

        var policy = (ReadString(payload, "overagePolicy") ?? defaults.OveragePolicyDefault).ToLowerInvariant();
        if (policy != "same_price" && policy != "full_price") policy = "full_price";

        decimal originalEligibleCost = eligibleLines
            .Where(l => l.PricePerUnit.HasValue)
            .Sum(l => l.PricePerUnit!.Value * l.Quantity);

        var (perOrderLimit, _) = ReadLimits(payload);
        decimal total;
        decimal baseQty = qty;
        decimal actualQty = totalQty;
        decimal overage;
        decimal promoPrice;
        decimal overagePrice;

        if (perOrderLimit is > 0)
        {
            int bundlesApplied = Math.Min((int)Math.Floor(totalQty / qty), perOrderLimit.Value);
            if (bundlesApplied < 1) return EvalOutcome.None;

            decimal promoUnits = bundlesApplied * qty;
            decimal outsideUnits = totalQty - promoUnits;
            promoPrice = bundlesApplied * fixedPrice;
            if (policy == "same_price")
            {
                var perUnit = qty == 0 ? 0m : fixedPrice / qty;
                overagePrice = Math.Round(perUnit * outsideUnits, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                decimal maxUnitPrice = eligibleLines
                    .Where(l => l.PricePerUnit.HasValue && l.Quantity > 0)
                    .Select(l => l.PricePerUnit!.Value)
                    .DefaultIfEmpty(0m)
                    .Max();
                overagePrice = Math.Round(maxUnitPrice * outsideUnits, 2, MidpointRounding.AwayFromZero);
            }
            overage = outsideUnits;
            total = promoPrice + overagePrice;
            actualQty = promoUnits;
        }
        else
        {
            overage = Math.Max(0m, actualQty - baseQty);
            promoPrice = fixedPrice;
            if (policy == "same_price")
            {
                var perUnit = baseQty == 0 ? 0m : fixedPrice / baseQty;
                overagePrice = Math.Round(perUnit * overage, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                decimal perUnit = eligibleLines
                    .Where(l => l.PricePerUnit.HasValue && l.Quantity > 0)
                    .Select(l => l.PricePerUnit!.Value)
                    .DefaultIfEmpty(0m)
                    .Max();
                overagePrice = Math.Round(perUnit * overage, 2, MidpointRounding.AwayFromZero);
            }
            total = promoPrice + overagePrice;
        }

        decimal discountAmount = Math.Max(0m, originalEligibleCost - total);
        if (discountAmount <= 0m) return EvalOutcome.None;

        return EvalOutcome.From(new AppliedPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_pay_y",
            PromotionName = p.Name,
            DiscountType = "fixed_price",
            DiscountAmount = discountAmount,
            PriceBreakdown = new BxpyPriceBreakdown
            {
                BaseQuantity = baseQty,
                ActualQuantity = actualQty,
                OverageQuantity = overage,
                OveragePolicy = policy,
                PromotionPrice = promoPrice,
                OveragePrice = overagePrice,
                Total = total,
                DisplayPricePerUnit = baseQty == 0 ? 0m : Math.Round(promoPrice / baseQty, 2, MidpointRounding.AwayFromZero),
            },
            TriggerProductIds = ProductIdsFromLines(eligibleLines),
        });
    }

    // ─── BUY X GET Y ─────────────────────────────────────────────────────────────

    private static EvalOutcome EvaluateBuyXGetY(
        Promotion p, JsonElement payload, EvaluatePromotionsReq req, SiteEvaluationDefaults defaults)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return EvalOutcome.None;
        if (!payload.TryGetProperty("reward", out var reward) || reward.ValueKind != JsonValueKind.Object) return EvalOutcome.None;

        var productScope = (ReadString(cond, "productScope") ?? "all").ToLowerInvariant();
        var productIds = ReadIntSet(cond, "productIds");
        var categoryIds = ReadIntSet(cond, "categoryIds");
        var excludedIds = ReadIntSet(cond, "excludedProductIds");

        var buy = new List<EvaluateCartLine>();
        foreach (var line in req.Cart)
        {
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
        if (buy.Count == 0) return EvalOutcome.None;

        var rewardIds = ReadIntSet(reward, "productIds").ToList();
        if (rewardIds.Count == 0) return EvalOutcome.None;
        var discountType = (ReadString(reward, "discountType") ?? "free").ToLowerInvariant();
        var discountValue = ReadDecimal(reward, "discountValue");

        // Threshold check + near-miss capture.
        var minQty = ReadInt(cond, "minQuantity");
        var minAmt = ReadDecimal(cond, "minAmount");
        decimal currentQty = buy.Sum(l => l.Quantity);
        decimal currentAmt = buy.Where(l => l.PricePerUnit.HasValue).Sum(l => l.PricePerUnit!.Value * l.Quantity);

        // Single-reward near-miss carries the gift product info so the storefront can render
        // "הוסף עוד X כדי לקבל [שם המוצר] חינם" (spec).
        NearbyPromotion BuildNearby(string kind, decimal current, decimal required) => new NearbyPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_get_y",
            PromotionName = p.Name,
            Missing = new MissingThreshold { Kind = kind, Current = current, Required = required },
            RewardProductId = rewardIds.Count == 1 ? rewardIds[0] : null,
            RewardDiscountType = discountType,
            RewardDiscountValue = discountValue,
            TriggerProductIds = ProductIdsFromLines(buy),
        };

        if (minQty is { } mq && currentQty < mq) return EvalOutcome.NearMiss(BuildNearby("quantity", currentQty, mq));
        if (minAmt is { } ma && currentAmt < ma) return EvalOutcome.NearMiss(BuildNearby("amount", currentAmt, ma));

        var (perOrderLimit, _) = ReadLimits(payload);
        int maxQuantity = ReadInt(reward, "maxQuantity") ?? 1;
        int naturalApplications = 1;
        if (minQty is > 0)
            naturalApplications = (int)Math.Floor(currentQty / minQty.Value);
        else if (minAmt is > 0)
            naturalApplications = (int)Math.Floor(currentAmt / minAmt.Value);
        int applications = perOrderLimit is > 0
            ? Math.Min(naturalApplications, perOrderLimit.Value)
            : naturalApplications;
        if (applications < 1) return EvalOutcome.None;

        // Multi-reward → tell storefront to prompt; no discount yet.
        if (rewardIds.Count > 1)
        {
            return EvalOutcome.From(new AppliedPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_get_y",
                PromotionName = p.Name,
                DiscountType = discountType,
                DiscountValue = discountValue,
                DiscountAmount = 0m,
                RewardOptions = rewardIds.Select(id => new RewardOption { ProductId = id, ProductName = string.Empty }).ToList(),
                TriggerProductIds = ProductIdsFromLines(buy),
            });
        }

        var rewardId = rewardIds[0];
        var rewardLine = req.Cart.FirstOrDefault(l => TryGetIntId(l.ProductId, out var rid) && rid == rewardId);
        if (rewardLine is null || rewardLine.PricePerUnit is null)
        {
            return EvalOutcome.From(new AppliedPromotion
            {
                PromotionId = p.Id,
                PromotionType = "buy_x_get_y",
                PromotionName = p.Name,
                DiscountType = discountType,
                DiscountValue = discountValue,
                DiscountAmount = 0m,
                RewardProductId = rewardId,
                TriggerProductIds = ProductIdsFromLines(buy),
            });
        }

        decimal discountableQty = Math.Min(rewardLine.Quantity, maxQuantity * applications);
        if (discountableQty <= 0m) return EvalOutcome.None;

        decimal originalRewardPortion = rewardLine.PricePerUnit.Value * discountableQty;
        decimal rewardDiscount = discountType switch
        {
            "free" => originalRewardPortion,
            "percentage" => discountValue is { } v && v > 0 && v <= 100
                ? Math.Round(originalRewardPortion * v / 100m, 2, MidpointRounding.AwayFromZero)
                : 0m,
            "fixed_price" => discountValue is { } v && v >= 0
                ? Math.Max(0m, originalRewardPortion - (v * discountableQty))
                : 0m,
            _ => 0m,
        };
        if (rewardDiscount <= 0m) return EvalOutcome.None;

        return EvalOutcome.From(new AppliedPromotion
        {
            PromotionId = p.Id,
            PromotionType = "buy_x_get_y",
            PromotionName = p.Name,
            RewardProductId = rewardId,
            DiscountType = discountType,
            DiscountValue = discountValue,
            DiscountAmount = rewardDiscount,
            TriggerProductIds = ProductIdsFromLines(buy),
        });
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
