using System.Text.Json;
using George.Data.Models;

namespace George.Services;

/// <summary>
/// Validates <see cref="George.DB.Promotion.PayloadJson"/> by <see cref="George.DB.Promotion.PromotionType"/>.
/// Contract is minimal v1 so shop-manager forms can evolve; drafts only require a JSON object.
/// </summary>
public static class PromotionPayloadValidator
{
    public static bool TryValidate(
        string promotionType,
        string? payloadJson,
        bool isDraft,
        string? listDiscountKind,
        out string? errorMessage)
    {
        errorMessage = null;
        var raw = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            errorMessage = "PayloadJson must be valid JSON.";
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "PayloadJson must be a JSON object.";
                return false;
            }

            if (isDraft)
                return true;

            return promotionType switch
            {
                PromotionWire.PromotionType.Discount => ValidateDiscount(doc.RootElement, listDiscountKind, out errorMessage),
                PromotionWire.PromotionType.BuyXPayY => ValidateBuyXPayY(doc.RootElement, out errorMessage),
                PromotionWire.PromotionType.BuyXGetY => ValidateBuyXGetY(doc.RootElement, out errorMessage),
                _ => Fail($"Unsupported promotion type: {promotionType}", out errorMessage),
            };
        }
    }

    private static bool Fail(string msg, out string? errorMessage)
    {
        errorMessage = msg;
        return false;
    }

    private static bool ValidateDiscount(JsonElement root, string? listDiscountKind, out string? errorMessage)
    {
        var kind = (listDiscountKind ?? PromotionWire.DiscountKind.Percent).Trim().ToLowerInvariant();
        if (kind is not (PromotionWire.DiscountKind.Percent or PromotionWire.DiscountKind.Amount))
            return Fail("For discount promotions ListDiscountKind must be percent or amount when not a draft.", out errorMessage);

        if (!root.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Number)
            return Fail("Payload for discount must include numeric property \"value\".", out errorMessage);

        if (kind == PromotionWire.DiscountKind.Percent)
        {
            if (!valueEl.TryGetDecimal(out var p) || p <= 0m || p > 100m)
                return Fail("Discount percent \"value\" must be between 0 and 100.", out errorMessage);
        }
        else
        {
            if (!valueEl.TryGetDecimal(out var a) || a <= 0m)
                return Fail("Discount amount \"value\" must be greater than 0.", out errorMessage);
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Spec-aligned shape (camelCase wire format from `Sprint4/מבצעים.md`):
    /// {
    ///   "version": 1,
    ///   "condition": { "scope": "product"|"category", "productId"?, "categoryId"?, "quantity": int>0, "excludedProductIds": [int] },
    ///   "pricing":   { "fixedPrice": number>0 },
    ///   "overagePolicy"?: "same_price"|"full_price",
    ///   "limits"?:    { "perOrder"?, "perCustomer"? },
    ///   "daysOfWeek"?: [string]
    /// }
    /// </summary>
    private static bool ValidateBuyXPayY(JsonElement root, out string? errorMessage)
    {
        if (!root.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object)
            return Fail("Payload for buy_x_pay_y must include a \"condition\" object.", out errorMessage);

        var scope = cond.TryGetProperty("scope", out var scopeEl) && scopeEl.ValueKind == JsonValueKind.String
            ? scopeEl.GetString()?.Trim().ToLowerInvariant()
            : null;
        if (scope is not ("product" or "category"))
            return Fail("buy_x_pay_y condition.scope must be \"product\" or \"category\".", out errorMessage);

        if (scope == "product")
        {
            if (!cond.TryGetProperty("productId", out var pid) || pid.ValueKind != JsonValueKind.Number
                || !pid.TryGetInt32(out var pidInt) || pidInt <= 0)
                return Fail("buy_x_pay_y: condition.productId is required when scope=\"product\".", out errorMessage);
        }
        else
        {
            if (!cond.TryGetProperty("categoryId", out var cid) || cid.ValueKind != JsonValueKind.Number
                || !cid.TryGetInt32(out var cidInt) || cidInt <= 0)
                return Fail("buy_x_pay_y: condition.categoryId is required when scope=\"category\".", out errorMessage);
        }

        if (!cond.TryGetProperty("quantity", out var qEl) || qEl.ValueKind != JsonValueKind.Number
            || !qEl.TryGetDecimal(out var qty) || qty <= 0m)
            return Fail("buy_x_pay_y: condition.quantity must be a number > 0.", out errorMessage);

        if (!root.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object)
            return Fail("buy_x_pay_y must include a \"pricing\" object.", out errorMessage);
        if (!pricing.TryGetProperty("fixedPrice", out var priceEl) || priceEl.ValueKind != JsonValueKind.Number
            || !priceEl.TryGetDecimal(out var price) || price <= 0m)
            return Fail("buy_x_pay_y: pricing.fixedPrice must be a number > 0.", out errorMessage);

        if (root.TryGetProperty("overagePolicy", out var op) && op.ValueKind == JsonValueKind.String)
        {
            var ops = op.GetString()?.Trim().ToLowerInvariant();
            if (ops is not (null or "same_price" or "full_price"))
                return Fail("buy_x_pay_y overagePolicy must be \"same_price\" or \"full_price\".", out errorMessage);
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Spec-aligned shape (camelCase) — see Sprint4/מבצעים.md.
    /// </summary>
    private static bool ValidateBuyXGetY(JsonElement root, out string? errorMessage)
    {
        if (!root.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object)
            return Fail("Payload for buy_x_get_y must include a \"condition\" object.", out errorMessage);

        var scope = cond.TryGetProperty("productScope", out var scopeEl) && scopeEl.ValueKind == JsonValueKind.String
            ? scopeEl.GetString()?.Trim().ToLowerInvariant()
            : "all";
        if (scope is not ("all" or "specific_products" or "specific_categories"))
            return Fail("buy_x_get_y condition.productScope must be \"all\", \"specific_products\", or \"specific_categories\".", out errorMessage);

        if (scope == "specific_products")
        {
            if (!cond.TryGetProperty("productIds", out var pids) || pids.ValueKind != JsonValueKind.Array || pids.GetArrayLength() == 0)
                return Fail("buy_x_get_y: condition.productIds must be a non-empty array when productScope=\"specific_products\".", out errorMessage);
        }
        else if (scope == "specific_categories")
        {
            if (!cond.TryGetProperty("categoryIds", out var cids) || cids.ValueKind != JsonValueKind.Array || cids.GetArrayLength() == 0)
                return Fail("buy_x_get_y: condition.categoryIds must be a non-empty array when productScope=\"specific_categories\".", out errorMessage);
        }

        if (!root.TryGetProperty("reward", out var reward) || reward.ValueKind != JsonValueKind.Object)
            return Fail("Payload for buy_x_get_y must include a \"reward\" object.", out errorMessage);

        var benefitAppliesTo = reward.TryGetProperty("benefitAppliesTo", out var baEl) && baEl.ValueKind == JsonValueKind.String
            ? baEl.GetString()?.Trim().ToLowerInvariant()
            : "products";
        if (benefitAppliesTo is "product") benefitAppliesTo = "products";
        if (benefitAppliesTo is "category") benefitAppliesTo = "categories";
        if (benefitAppliesTo is not ("products" or "categories" or "same" or "all"))
            return Fail("buy_x_get_y: reward.benefitAppliesTo must be \"products\", \"categories\", \"same\", or \"all\".", out errorMessage);

        if (benefitAppliesTo == "products")
        {
            if (!reward.TryGetProperty("productIds", out var rids) || rids.ValueKind != JsonValueKind.Array || rids.GetArrayLength() == 0)
                return Fail("buy_x_get_y: reward.productIds must be a non-empty array when benefitAppliesTo=\"products\".", out errorMessage);
        }
        else if (benefitAppliesTo == "categories")
        {
            if (!reward.TryGetProperty("categoryIds", out var bcids) || bcids.ValueKind != JsonValueKind.Array || bcids.GetArrayLength() == 0)
                return Fail("buy_x_get_y: reward.categoryIds must be a non-empty array when benefitAppliesTo=\"categories\".", out errorMessage);
        }

        if (!reward.TryGetProperty("discountType", out var dt) || dt.ValueKind != JsonValueKind.String)
            return Fail("buy_x_get_y: reward.discountType must be \"free\", \"percentage\", or \"fixed_price\".", out errorMessage);
        var dts = dt.GetString()?.Trim().ToLowerInvariant();
        if (dts is not ("free" or "percentage" or "fixed_price"))
            return Fail("buy_x_get_y: reward.discountType must be \"free\", \"percentage\", or \"fixed_price\".", out errorMessage);

        if (dts == "percentage")
        {
            if (!reward.TryGetProperty("discountValue", out var pEl) || pEl.ValueKind != JsonValueKind.Number
                || !pEl.TryGetDecimal(out var p) || p <= 0m || p > 100m)
                return Fail("buy_x_get_y: when discountType=\"percentage\", reward.discountValue must be in (0, 100].", out errorMessage);
        }
        else if (dts == "fixed_price")
        {
            if (!reward.TryGetProperty("discountValue", out var fEl) || fEl.ValueKind != JsonValueKind.Number
                || !fEl.TryGetDecimal(out var f) || f < 0m)
                return Fail("buy_x_get_y: when discountType=\"fixed_price\", reward.discountValue must be a number >= 0.", out errorMessage);
        }

        errorMessage = null;
        return true;
    }
}
