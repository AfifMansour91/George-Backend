namespace George.Services.Response;

/// <summary>
/// Response shape for <c>POST /Promotion/evaluate</c> - see <c>Sprint4/מבצעים.md</c>.
/// Storefront/kiosk reads this to render in-cart messaging and final totals.
/// </summary>
public class EvaluatePromotionsRes
{
    public List<AppliedPromotion> PromotionsApplied { get; set; } = new();

    /// <summary>Sum of all discount amounts across applied promotions (NIS).</summary>
    public decimal TotalDiscount { get; set; }

    /// <summary>
    /// Promotions whose scope matches the cart but whose threshold isn't met yet - used by the
    /// storefront to render the spec's "הוסף עוד X לקבלת …" encouragement messages
    /// (`Sprint4/מבצעים.md` → "הודעות עידוד וחיסכון בסל הקניות").
    /// </summary>
    public List<NearbyPromotion> PromotionsNearby { get; set; } = new();
}

public class AppliedPromotion
{
    public int PromotionId { get; set; }
    public string PromotionType { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;

    /// <summary>"percentage" | "amount" | "free" | "fixed_price" - depends on PromotionType.</summary>
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>For BxGY: the gift product chosen (or only option) - empty when storefront must prompt.</summary>
    public int? RewardProductId { get; set; }
    public string? RewardProductName { get; set; }

    /// <summary>For BxGY with multiple gift options - storefront pops the picker. Empty otherwise.</summary>
    public List<RewardOption>? RewardOptions { get; set; }

    /// <summary>For BxPY: helps the storefront render the per-unit "promotion price" with a strike-through.</summary>
    public BxpyPriceBreakdown? PriceBreakdown { get; set; }

    /// <summary>For % / ₪ discount: per-line breakdown so the storefront can show before/after per item.</summary>
    public List<EligibleItem>? EligibleItems { get; set; }

    /// <summary>When true, savings/near-miss messaging belongs in the cart header banner (whole-cart discount).</summary>
    public bool WholeCart { get; set; }

    /// <summary>Buy-side product ids (string) - storefront renders per-line hints under these cart rows.</summary>
    public List<string>? TriggerProductIds { get; set; }

    /// <summary>
    /// When true, storefront may auto-add a free specific-product BxGY gift (WP <c>auto_add</c> guards).
    /// </summary>
    public bool AutoAddEligible { get; set; }

    /// <summary>How many gift units to add/sync when <see cref="AutoAddEligible"/> is true.</summary>
    public int? AutoAddQuantity { get; set; }
}

public class RewardOption
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
}

public class BxpyPriceBreakdown
{
    public decimal BaseQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal OverageQuantity { get; set; }
    public string OveragePolicy { get; set; } = "full_price";
    public decimal PromotionPrice { get; set; }
    public decimal OveragePrice { get; set; }
    public decimal Total { get; set; }
    public decimal DisplayPricePerUnit { get; set; }
}

public class EligibleItem
{
    public string ProductId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
}


public class NearbyPromotion
{
    public int PromotionId { get; set; }
    public string PromotionType { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;

    /// <summary>What's missing for the offer to apply.</summary>
    public MissingThreshold Missing { get; set; } = new();

    /// <summary>Estimated discount amount (NIS) if the customer crosses the threshold.</summary>
    public decimal? PotentialSaving { get; set; }

    /// <summary>For BxPY near-miss: bundle quantity (e.g. 3 kg).</summary>
    public decimal? DealQuantity { get; set; }

    /// <summary>For BxPY near-miss: bundle price (NIS).</summary>
    public decimal? DealPrice { get; set; }

    /// <summary>For BxGY: the gift product the customer would unlock (when single-option).</summary>
    public int? RewardProductId { get; set; }
    public string? RewardProductName { get; set; }
    /// <summary>"free" | "percentage" | "fixed_price" - same enum as AppliedPromotion.</summary>
    public string? RewardDiscountType { get; set; }
    public decimal? RewardDiscountValue { get; set; }

    /// <summary>When true, encouragement message belongs in the cart header banner.</summary>
    public bool WholeCart { get; set; }

    /// <summary>Buy-side product ids - storefront renders per-line hints under these cart rows.</summary>
    public List<string>? TriggerProductIds { get; set; }
}

public class MissingThreshold
{
    /// <summary>"quantity" (units short) | "amount" (₪ short).</summary>
    public string Kind { get; set; } = "quantity";
    public decimal Current { get; set; }
    public decimal Required { get; set; }
}
