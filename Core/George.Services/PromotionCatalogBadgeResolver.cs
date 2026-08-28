using System.Text.Json;
using George.DB;

namespace George.Services;

/// <summary>
/// Derives which products/categories should show a catalog "במבצע" badge from active
/// promotions with <see cref="Promotion.ShowBadge"/> = true.
/// </summary>
public static class PromotionCatalogBadgeResolver
{
    public sealed class BadgeRule
    {
        public int PromotionId { get; set; }
        /// <summary>Promotion display name - shown on the catalog banner (plugin uses <c>promotion-&gt;name</c>).</summary>
        public string Label { get; set; } = string.Empty;
        public string PromotionType { get; set; } = string.Empty;
        /// <summary>For discount promotions: "percent" | "amount".</summary>
        public string? DiscountKind { get; set; }
        public decimal? DiscountValue { get; set; }
        public bool WholeCart { get; set; }
        public bool AllProducts { get; set; }
        public HashSet<int> ProductIds { get; } = new();
        public HashSet<int> CategoryIds { get; } = new();
        public HashSet<int> ExcludedProductIds { get; } = new();
    }

    /// <summary>Legacy merged scope - kept for backward compatibility only.</summary>
    public sealed class BadgeScope
    {
        public HashSet<int> ProductIds { get; } = new();
        public HashSet<int> CategoryIds { get; } = new();
        public bool AllProducts { get; set; }
    }

    public static List<BadgeRule> ResolveRules(
        IEnumerable<Promotion> promotions,
        string? channel,
        DateTime utcNow)
    {
        var rules = new List<BadgeRule>();
        var ch = (channel ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var p in promotions.OrderBy(x => x.Id))
        {
            if (!IsEligible(p, utcNow)) continue;
            if (!ChannelAllows(p, ch)) continue;
            if (!PromotionWeekdaySchedule.PayloadAllowsToday(p.PayloadJson, utcNow)) continue;
            var rule = ExtractRule(p);
            if (rule != null && RuleHasScope(rule))
                rules.Add(rule);
        }
        return rules;
    }

    public static BadgeScope Resolve(
        IEnumerable<Promotion> promotions,
        string? channel,
        DateTime utcNow)
    {
        var scope = new BadgeScope();
        foreach (var rule in ResolveRules(promotions, channel, utcNow))
        {
            if (rule.AllProducts) scope.AllProducts = true;
            foreach (var id in rule.ProductIds) scope.ProductIds.Add(id);
            foreach (var id in rule.CategoryIds) scope.CategoryIds.Add(id);
        }
        return scope;
    }

    private static bool RuleHasScope(BadgeRule rule) =>
        rule.AllProducts || rule.ProductIds.Count > 0 || rule.CategoryIds.Count > 0;

    private static bool IsEligible(Promotion p, DateTime utcNow)
    {
        if (p.IsDeleted || !p.IsActive || !p.ShowBadge) return false;
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
            if (channel == "phone" && found.Contains("web") && found.Contains("mobile") && found.Contains("store"))
                return true;
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static BadgeRule? ExtractRule(Promotion p)
    {
        if (string.IsNullOrWhiteSpace(p.PayloadJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(p.PayloadJson);
            var root = doc.RootElement;
            var rule = new BadgeRule
            {
                PromotionId = p.Id,
                Label = string.IsNullOrWhiteSpace(p.Name) ? "במבצע" : p.Name.Trim(),
            };
            var type = (p.PromotionType ?? "").Trim().ToLowerInvariant();
            switch (type)
            {
                case "discount":
                    ExtractDiscountScope(root, rule, p);
                    break;
                case "buy_x_pay_y":
                    ExtractBxpyScope(root, rule);
                    rule.PromotionType = "buy_x_pay_y";
                    break;
                case "buy_x_get_y":
                    ExtractBxgyScope(root, rule);
                    rule.PromotionType = "buy_x_get_y";
                    break;
                default:
                    return null;
            }
            return rule;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ExtractDiscountScope(JsonElement payload, BadgeRule rule, Promotion p)
    {
        rule.PromotionType = "discount";
        var kind = (p.ListDiscountKind ?? "percent").Trim().ToLowerInvariant();
        if (kind is "percent" or "amount")
        {
            rule.DiscountKind = kind;
            rule.DiscountValue = ReadDecimal(payload, "value");
        }

        AddExcluded(rule, payload);
        var applyScope = ReadString(payload, "applyScope") ?? "all";
        rule.WholeCart = applyScope == "whole_cart" || ReadBool(payload, "appliesToWholeCart") == true;
        if (rule.WholeCart || applyScope == "all")
        {
            rule.AllProducts = true;
            return;
        }
        if (applyScope == "products")
            AddIntSet(rule.ProductIds, payload, "productIds");
        else if (applyScope == "categories")
            AddIntSet(rule.CategoryIds, payload, "categoryIds");
    }

    private static void ExtractBxpyScope(JsonElement payload, BadgeRule rule)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return;
        AddExcluded(rule, cond);
        var productScope = (ReadString(cond, "scope") ?? "product").ToLowerInvariant();
        if (productScope == "product")
        {
            var pid = ReadInt(cond, "productId");
            if (pid is > 0) rule.ProductIds.Add(pid.Value);
            AddIntSet(rule.ProductIds, cond, "productIds");
        }
        else if (productScope == "category")
        {
            var cid = ReadInt(cond, "categoryId");
            if (cid is > 0) rule.CategoryIds.Add(cid.Value);
        }
    }

    private static void ExtractBxgyScope(JsonElement payload, BadgeRule rule)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return;
        AddExcluded(rule, cond);
        // productScope is the BUY condition. "all" = "buy anything" - that must NOT badge the whole
        // catalog; only the reward (gift) products - and specific trigger products - get the badge.
        var productScope = (ReadString(cond, "productScope") ?? "all").ToLowerInvariant();
        if (productScope == "specific_products")
            AddIntSet(rule.ProductIds, cond, "productIds");
        else if (productScope == "specific_categories")
            AddIntSet(rule.CategoryIds, cond, "categoryIds");

        if (payload.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object)
        {
            var benefitApplies = (ReadString(reward, "benefitAppliesTo") ?? "products").ToLowerInvariant();
            if (benefitApplies == "products")
                AddIntSet(rule.ProductIds, reward, "productIds");
            else if (benefitApplies == "same")
            {
                if (productScope == "specific_products")
                    AddIntSet(rule.ProductIds, cond, "productIds");
                else if (productScope == "specific_categories")
                    AddIntSet(rule.CategoryIds, cond, "categoryIds");
            }
            else if (benefitApplies == "categories")
                AddIntSet(rule.CategoryIds, reward, "categoryIds");
            else if (benefitApplies == "all")
                rule.AllProducts = true;
        }
    }

    private static void AddExcluded(BadgeRule rule, JsonElement parent) =>
        AddIntSet(rule.ExcludedProductIds, parent, "excludedProductIds");

    private static void AddIntSet(HashSet<int> target, JsonElement parent, string prop)
    {
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) && n > 0)
                target.Add(n);
        }
    }

    private static string? ReadString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool? ReadBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static int? ReadInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static decimal? ReadDecimal(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;
}
