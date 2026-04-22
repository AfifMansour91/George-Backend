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

    private static bool ValidateBuyXPayY(JsonElement root, out string? errorMessage)
    {
        if (!root.TryGetProperty("buyUnits", out var buyEl) || buyEl.ValueKind != JsonValueKind.Number
            || !buyEl.TryGetInt32(out var buy) || buy < 1)
            return Fail("Payload for buy_x_pay_y must include integer \"buyUnits\" >= 1.", out errorMessage);

        if (!root.TryGetProperty("payUnits", out var payEl) || payEl.ValueKind != JsonValueKind.Number
            || !payEl.TryGetInt32(out var pay) || pay < 1)
            return Fail("Payload for buy_x_pay_y must include integer \"payUnits\" >= 1.", out errorMessage);

        if (pay > buy)
            return Fail("buy_x_pay_y: payUnits must be less than or equal to buyUnits.", out errorMessage);

        errorMessage = null;
        return true;
    }

    private static bool ValidateBuyXGetY(JsonElement root, out string? errorMessage)
    {
        if (!root.TryGetProperty("benefitType", out var bt) || bt.ValueKind != JsonValueKind.String)
            return Fail("Payload for buy_x_get_y must include string \"benefitType\" (free | percent_discount | fixed_price).", out errorMessage);

        var bts = bt.GetString()?.Trim().ToLowerInvariant();
        if (bts is not ("free" or "percent_discount" or "fixed_price"))
            return Fail("buy_x_get_y benefitType must be free, percent_discount, or fixed_price.", out errorMessage);

        if (bts == "percent_discount")
        {
            if (!root.TryGetProperty("benefitPercent", out var pEl) || pEl.ValueKind != JsonValueKind.Number
                || !pEl.TryGetDecimal(out var p) || p <= 0m || p > 100m)
                return Fail("When benefitType is percent_discount, \"benefitPercent\" must be between 0 and 100.", out errorMessage);
        }

        if (bts == "fixed_price")
        {
            if (!root.TryGetProperty("benefitFixedPriceNis", out var fEl) || fEl.ValueKind != JsonValueKind.Number
                || !fEl.TryGetDecimal(out var f) || f < 0m)
                return Fail("When benefitType is fixed_price, \"benefitFixedPriceNis\" must be a number >= 0.", out errorMessage);
        }

        errorMessage = null;
        return true;
    }
}
