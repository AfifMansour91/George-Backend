using System.Text.Json;
using George.DB;

namespace George.Services;

/// <summary>
/// Maps a George <see cref="Promotion"/> row to the WordPress Promotion Engine
/// (<c>POST /wp-json/promeng/v1/promotions</c>) upsert shape. See
/// <c>Sprint4/promotion-engine/promotion-engine/docs/GIORGIO_API.md</c>.
/// </summary>
public static class PromotionPromengWireMapper
{
    private static readonly Dictionary<string, int> WeekdayToPromeng = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sun"] = 0, ["Mon"] = 1, ["Tue"] = 2, ["Wed"] = 3, ["Thu"] = 4, ["Fri"] = 5, ["Sat"] = 6,
    };

    public static string ExternalId(Promotion p) => $"george-{p.Id}";

    public static bool TryParseExternalId(string? externalId, out int promotionId)
    {
        promotionId = 0;
        if (string.IsNullOrWhiteSpace(externalId)) return false;
        var s = externalId.Trim();
        if (s.StartsWith("george-", StringComparison.OrdinalIgnoreCase))
            s = s[7..];
        return int.TryParse(s, out promotionId) && promotionId > 0;
    }

    public static object ToUpsertBody(
        Promotion p,
        string eventName,
        IReadOnlyDictionary<int, int> productIdToWoo,
        IReadOnlyDictionary<int, int> categoryIdToWoo)
    {
        var active = p.IsActive && !string.Equals(eventName, PromotionWebhookDispatcher.EventEnded, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(p.PayloadJson) ? "{}" : p.PayloadJson);
        var root = doc.RootElement;

        var type = (p.PromotionType ?? "").Trim().ToLowerInvariant();
        var config = type switch
        {
            "discount" => MapDiscountConfig(root, productIdToWoo, categoryIdToWoo, p.ListDiscountKind),
            "buy_x_pay_y" => MapBxpyConfig(root, productIdToWoo, categoryIdToWoo),
            "buy_x_get_y" => MapBxgyConfig(root, productIdToWoo, categoryIdToWoo),
            _ => new Dictionary<string, object>(),
        };

        ReadLimits(root, out var limitPerOrder, out var limitPerCustomer);

        var channels = MapChannels(p.ChannelsJson);
        var syncsToWeb = channels.Length > 0;
        active = active && syncsToWeb;

        return new
        {
            external_id = ExternalId(p),
            name = p.Name,
            type,
            active,
            show_label = p.ShowBadge,
            requires_coupon = !string.IsNullOrWhiteSpace(p.CouponCode),
            coupon_code = string.IsNullOrWhiteSpace(p.CouponCode) ? null : p.CouponCode.Trim(),
            priority = p.Priority > 0 ? p.Priority : 10,
            starts_at = FormatPromengDateTime(p.ScheduleStartDateUtc),
            ends_at = FormatPromengDateTime(p.ScheduleEndDateUtc),
            weekdays = MapWeekdays(root),
            limit_per_order = limitPerOrder,
            limit_per_customer = limitPerCustomer,
            channels,
            config,
        };
    }

    private static Dictionary<string, object> MapDiscountConfig(
        JsonElement root,
        IReadOnlyDictionary<int, int> productIdToWoo,
        IReadOnlyDictionary<int, int> categoryIdToWoo,
        string? listDiscountKind)
    {
        var scope = (ReadString(root, "applyScope") ?? "all").ToLowerInvariant();
        if (ReadBool(root, "appliesToWholeCart") == true) scope = "whole_cart";

        var appliesTo = scope switch
        {
            "whole_cart" => "cart",
            "products" => "products",
            "categories" => "categories",
            _ => "all",
        };

        var kind = (listDiscountKind ?? "percent").Trim().ToLowerInvariant();
        return new Dictionary<string, object>
        {
            ["applies_to"] = appliesTo,
            ["discount_type"] = kind == "amount" ? "fixed" : "percent",
            ["discount_value"] = ReadDecimal(root, "value") ?? 0m,
            ["product_ids"] = MapProductIds(root, "productIds", productIdToWoo),
            ["category_ids"] = MapCategoryIds(root, "categoryIds", categoryIdToWoo),
            ["excluded_product_ids"] = MapProductIds(root, "excludedProductIds", productIdToWoo),
            ["condition_min_items"] = ReadInt(root, "minPurchaseQuantity") ?? 0,
            ["condition_min_amount"] = ReadBool(root, "applyPurchaseCondition") == true
                ? ReadDecimal(root, "minPurchaseAmount") ?? 0m
                : 0m,
            ["limit_discounted_items"] = ReadInt(root, "limitPerItems") ?? 0,
        };
    }

    private static Dictionary<string, object> MapBxpyConfig(
        JsonElement root,
        IReadOnlyDictionary<int, int> productIdToWoo,
        IReadOnlyDictionary<int, int> categoryIdToWoo)
    {
        if (!root.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object>();

        var scope = (ReadString(cond, "scope") ?? "product").ToLowerInvariant();
        // Payloads carry both the legacy single productId and productIds[] - dedupe before mapping.
        var productIds = new List<int>();
        var pid = ReadInt(cond, "productId");
        if (pid is > 0) productIds.Add(pid.Value);
        productIds.AddRange(ReadIntList(cond, "productIds"));
        productIds = productIds.Distinct().ToList();

        var categoryIds = new List<int>();
        var cid = ReadInt(cond, "categoryId");
        if (cid is > 0) categoryIds.Add(cid.Value);
        categoryIds.AddRange(ReadIntList(cond, "categoryIds"));
        categoryIds = categoryIds.Distinct().ToList();

        return new Dictionary<string, object>
        {
            ["scope"] = scope == "category" ? "category" : "product",
            ["product_ids"] = MapGeorgeIds(productIds, productIdToWoo),
            ["category_ids"] = MapGeorgeIds(categoryIds, categoryIdToWoo),
            ["excluded_product_ids"] = MapProductIds(cond, "excludedProductIds", productIdToWoo),
            ["buy_quantity"] = ReadDecimal(cond, "quantity") ?? 1m,
            ["pay_amount"] = root.TryGetProperty("pricing", out var pri) ? ReadDecimal(pri, "fixedPrice") ?? 0m : 0m,
        };
    }

    private static Dictionary<string, object> MapBxgyConfig(
        JsonElement root,
        IReadOnlyDictionary<int, int> productIdToWoo,
        IReadOnlyDictionary<int, int> categoryIdToWoo)
    {
        var config = new Dictionary<string, object>();
        if (!root.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object)
            return config;

        var productScope = (ReadString(cond, "productScope") ?? "all").ToLowerInvariant();
        config["buy_applies_to"] = productScope switch
        {
            "specific_products" => "products",
            "specific_categories" => "categories",
            _ => "all",
        };
        config["buy_product_ids"] = MapProductIds(cond, "productIds", productIdToWoo);
        config["buy_category_ids"] = MapCategoryIds(cond, "categoryIds", categoryIdToWoo);
        config["buy_excluded_product_ids"] = MapProductIds(cond, "excludedProductIds", productIdToWoo);
        config["buy_min_items"] = ReadInt(cond, "minQuantity") ?? 1;
        config["buy_min_amount"] = ReadDecimal(cond, "minAmount") ?? 0m;

        if (root.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object)
        {
            var benefitApplies = (ReadString(reward, "benefitAppliesTo") ?? "products").ToLowerInvariant();
            config["benefit_applies_to"] = benefitApplies;
            config["benefit_product_ids"] = MapProductIds(reward, "productIds", productIdToWoo);
            config["benefit_category_ids"] = MapCategoryIds(reward, "categoryIds", categoryIdToWoo);
            config["benefit_excluded_product_ids"] = MapProductIds(reward, "benefitExcludedProductIds", productIdToWoo);
            config["benefit_quantity"] = ReadInt(reward, "benefitQuantity") ?? ReadInt(reward, "maxQuantity") ?? 1;

            var discountType = (ReadString(reward, "discountType") ?? "free").ToLowerInvariant();
            config["benefit_type"] = discountType switch
            {
                "percentage" => "percent",
                "fixed_price" => "fixed",
                _ => "free",
            };
            config["benefit_value"] = ReadDecimal(reward, "discountValue") ?? (discountType == "fixed_price" ? 1m : 0m);
            config["auto_add"] = ReadBool(reward, "autoAdd") != false;
            config["apply_to_cheapest"] = ReadBool(reward, "applyToCheapest") == true;
        }

        return config;
    }

    public static void CollectGeorgeIdsFromPayload(Promotion p, HashSet<int> productIds, HashSet<int> categoryIds)
    {
        if (string.IsNullOrWhiteSpace(p.PayloadJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(p.PayloadJson);
            CollectIdsFromElement(doc.RootElement, productIds, categoryIds);
        }
        catch (JsonException)
        {
            // ignore malformed payload - mapper will emit empty id arrays
        }
    }

    private static void CollectIdsFromElement(JsonElement el, HashSet<int> productIds, HashSet<int> categoryIds)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var name = prop.Name.ToLowerInvariant();
                if (name is "productid" or "productids" or "excludedproductids" or "benefitexcludedproductids")
                    AddIdsFromValue(prop.Value, productIds);
                else if (name is "categoryid" or "categoryids")
                    AddIdsFromValue(prop.Value, categoryIds);
                else
                    CollectIdsFromElement(prop.Value, productIds, categoryIds);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
                CollectIdsFromElement(item, productIds, categoryIds);
        }
    }

    private static void AddIdsFromValue(JsonElement value, HashSet<int> target)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) && n > 0)
            target.Add(n);
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var el in value.EnumerateArray())
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id) && id > 0)
                    target.Add(id);
    }

    private static List<int> MapProductIds(JsonElement parent, string prop, IReadOnlyDictionary<int, int> map) =>
        MapGeorgeIds(ReadIntList(parent, prop), map);

    private static List<int> MapCategoryIds(JsonElement parent, string prop, IReadOnlyDictionary<int, int> map) =>
        MapGeorgeIds(ReadIntList(parent, prop), map);

    private static List<int> MapGeorgeIds(IEnumerable<int> georgeIds, IReadOnlyDictionary<int, int> map)
    {
        var result = new List<int>();
        foreach (var id in georgeIds.Distinct())
        {
            if (map.TryGetValue(id, out var woo) && woo > 0)
                result.Add(woo);
        }
        return result;
    }

    private static List<int> ReadIntList(JsonElement parent, string prop)
    {
        var list = new List<int>();
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var el in arr.EnumerateArray())
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) && n > 0)
                list.Add(n);
        return list;
    }

    private static void ReadLimits(JsonElement root, out int? limitPerOrder, out int? limitPerCustomer)
    {
        limitPerOrder = null;
        limitPerCustomer = null;
        if (!root.TryGetProperty("limits", out var lim) || lim.ValueKind != JsonValueKind.Object) return;
        limitPerOrder = ReadInt(lim, "perOrder");
        limitPerCustomer = ReadInt(lim, "perCustomer");
    }

    private static int[] MapWeekdays(JsonElement root)
    {
        if (!root.TryGetProperty("daysOfWeek", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [0, 1, 2, 3, 4, 5, 6];

        var days = new List<int>();
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.String) continue;
            var key = (el.GetString() ?? "").Trim();
            if (WeekdayToPromeng.TryGetValue(key, out var d) && !days.Contains(d))
                days.Add(d);
        }
        return days.Count > 0 ? days.OrderBy(x => x).ToArray() : [0, 1, 2, 3, 4, 5, 6];
    }

    private static string[] MapChannels(string? channelsJson)
    {
        if (!SyncsToWebStore(channelsJson)) return Array.Empty<string>();
        return ["web"];
    }

    /// <summary>
    /// WooCommerce Promeng only runs web-channel promotions. Kiosk/phone-only promos are not pushed as active.
    /// </summary>
    internal static bool SyncsToWebStore(string? channelsJson)
    {
        if (string.IsNullOrWhiteSpace(channelsJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(channelsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return true;
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String) continue;
                var v = (el.GetString() ?? "").Trim().ToLowerInvariant();
                if (v == "all") return true;
                if (!string.IsNullOrEmpty(v)) found.Add(v);
            }
            if (found.Count == 0) return true;
            return found.Contains("web") || found.Contains("mobile");
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static string? FormatPromengDateTime(DateTime? utc)
    {
        if (utc is null) return null;
        return utc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string? ReadString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool? ReadBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static int? ReadInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static decimal? ReadDecimal(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return null;
    }
}
