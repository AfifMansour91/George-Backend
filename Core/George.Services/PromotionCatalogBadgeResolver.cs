using System.Text.Json;
using George.DB;

namespace George.Services;

/// <summary>
/// Derives which products/categories should show a catalog "במבצע" badge from active
/// promotions with <see cref="Promotion.ShowBadge"/> = true.
/// </summary>
public static class PromotionCatalogBadgeResolver
{
    public sealed class BadgeScope
    {
        public HashSet<int> ProductIds { get; } = new();
        public HashSet<int> CategoryIds { get; } = new();
        public bool AllProducts { get; set; }
    }

    public static BadgeScope Resolve(
        IEnumerable<Promotion> promotions,
        string? channel,
        DateTime utcNow)
    {
        var scope = new BadgeScope();
        var ch = (channel ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var p in promotions)
        {
            if (!IsEligible(p, utcNow)) continue;
            if (!ChannelAllows(p, ch)) continue;
            ExtractScope(p, scope);
        }
        return scope;
    }

    private static bool IsEligible(Promotion p, DateTime utcNow)
    {
        if (p.IsDeleted || p.IsDraft || !p.IsActive || !p.ShowBadge) return false;
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
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String) continue;
                var v = (el.GetString() ?? "").Trim().ToLowerInvariant();
                if (v == "all" || v == channel) return true;
            }
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static void ExtractScope(Promotion p, BadgeScope scope)
    {
        if (string.IsNullOrWhiteSpace(p.PayloadJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(p.PayloadJson);
            var root = doc.RootElement;
            var type = (p.PromotionType ?? "").Trim().ToLowerInvariant();
            switch (type)
            {
                case "discount":
                    ExtractDiscountScope(root, scope);
                    break;
                case "buy_x_pay_y":
                    ExtractBxpyScope(root, scope);
                    break;
                case "buy_x_get_y":
                    ExtractBxgyScope(root, scope);
                    break;
            }
        }
        catch (JsonException)
        {
            // ignore malformed payload
        }
    }

    private static void ExtractDiscountScope(JsonElement payload, BadgeScope scope)
    {
        var applyScope = ReadString(payload, "applyScope") ?? "all";
        if (applyScope == "whole_cart" || ReadBool(payload, "appliesToWholeCart") == true || applyScope == "all")
        {
            scope.AllProducts = true;
            return;
        }
        if (applyScope == "products")
            AddIntSet(scope.ProductIds, payload, "productIds");
        else if (applyScope == "categories")
            AddIntSet(scope.CategoryIds, payload, "categoryIds");
    }

    private static void ExtractBxpyScope(JsonElement payload, BadgeScope scope)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return;
        var productScope = (ReadString(cond, "scope") ?? "product").ToLowerInvariant();
        if (productScope == "product")
        {
            var pid = ReadInt(cond, "productId");
            if (pid is > 0) scope.ProductIds.Add(pid.Value);
        }
        else if (productScope == "category")
        {
            var cid = ReadInt(cond, "categoryId");
            if (cid is > 0) scope.CategoryIds.Add(cid.Value);
        }
    }

    private static void ExtractBxgyScope(JsonElement payload, BadgeScope scope)
    {
        if (!payload.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object) return;
        var productScope = (ReadString(cond, "productScope") ?? "all").ToLowerInvariant();
        if (productScope == "all")
        {
            scope.AllProducts = true;
            return;
        }
        if (productScope == "specific_products")
            AddIntSet(scope.ProductIds, cond, "productIds");
        else if (productScope == "specific_categories")
            AddIntSet(scope.CategoryIds, cond, "categoryIds");
        if (payload.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object)
            AddIntSet(scope.ProductIds, reward, "productIds");
    }

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
}
