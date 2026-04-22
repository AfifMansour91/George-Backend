namespace George.Data.Models;

/// <summary>
/// Canonical query/body string values for promotions API.
/// Must stay aligned with <c>shop-manager/src/api/promotionConstants.ts</c> (<c>PROMOTION_FORM_TYPE_QUERY</c>).
/// </summary>
public static class PromotionWire
{
    /// <summary>Values stored in <c>Promotion.PromotionType</c> and sent on create/update.</summary>
    public static class PromotionType
    {
        public const string Discount = "discount";
        public const string BuyXPayY = "buy_x_pay_y";
        public const string BuyXGetY = "buy_x_get_y";

        public static bool IsKnown(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim().ToLowerInvariant();
            return v is Discount or BuyXPayY or BuyXGetY;
        }

        public static string? NormalizeOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim().ToLowerInvariant();
            if (IsKnown(v)) return v;
            // Legacy / UI names from older samples — map to wire values.
            return v switch
            {
                "percentdiscount" or "amountdiscount" => Discount,
                "buyxpayy" => BuyXPayY,
                "buyxgety" => BuyXGetY,
                _ => null,
            };
        }
    }

    public static class ListTab
    {
        public const string All = "all";
        public const string Active = "active";
        public const string Scheduled = "scheduled";
        public const string Drafts = "drafts";
        public const string Ended = "ended";
    }

    public static class Channel
    {
        public const string All = "all";
        public const string Web = "web";
        public const string Mobile = "mobile";
        public const string Store = "store";
    }

    public static class DiscountKind
    {
        public const string All = "all";
        public const string Percent = "percent";
        public const string Amount = "amount";
        public const string Gift = "gift";
        public const string Bxgy = "bxgy";
        public const string Bxpy = "bxpy";
    }

    public static class SortBy
    {
        public const string Redemptions = "redemptions";
        public const string Revenue = "revenue";
        public const string Discount = "discount";
        public const string Updated = "updated";
    }

    public static class SortDir
    {
        public const string Asc = "asc";
        public const string Desc = "desc";
    }

    /// <summary>Default <c>ChannelsJson</c> when null (single web channel).</summary>
    public const string DefaultChannelsJson = "[\"web\"]";
}
