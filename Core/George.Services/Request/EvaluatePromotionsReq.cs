using System.ComponentModel.DataAnnotations;

namespace George.Services.Request;

/// <summary>
/// Request payload for <c>POST /Promotion/evaluate</c> — see <c>Sprint4/מבצעים.md</c>.
/// Storefront/kiosk sends the cart and asks which promotions apply.
/// </summary>
public class EvaluatePromotionsReq
{
    [Required]
    public int SiteId { get; set; }

    [Required]
    public List<EvaluateCartLine> Cart { get; set; } = new();

    /// <summary>Optional cart-level total (without shipping) for whole-cart discount rules.</summary>
    public decimal? CartTotal { get; set; }

    /// <summary>Optional coupon code typed at checkout (case-insensitive on the server).</summary>
    public string? CouponCode { get; set; }

    /// <summary>"web" | "mobile" | "store" — must match `PromotionWire.Channel`.</summary>
    public string? Channel { get; set; }

    public string? CustomerId { get; set; }
}

public class EvaluateCartLine
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Allows fractional units (e.g. 3.2 kg from a scale).</summary>
    [Required]
    public decimal Quantity { get; set; }

    /// <summary>Per-unit price as displayed at checkout (defensive: server treats as a hint).</summary>
    public decimal? PricePerUnit { get; set; }

    public string? CategoryId { get; set; }

    /// <summary>"unit" | "kg" | "package" — falls back to product's catalog unit.</summary>
    public string? Unit { get; set; }

    /// <summary>"scale" when the quantity comes from a connected weighing scale (kiosk).</summary>
    public string? Source { get; set; }
}
