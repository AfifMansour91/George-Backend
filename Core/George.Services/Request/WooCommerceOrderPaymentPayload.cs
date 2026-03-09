using System.ComponentModel.DataAnnotations;

namespace George.Services.Request;

/// <summary>Payload from WooCommerce when order is paid – invoice, clearance, etc.</summary>
public class WooCommerceOrderPaymentPayload
{
    [Required]
    public string OrderNumber { get; set; } = null!;
    /// <summary>Optional; API key already identifies site.</summary>
    public string? SiteId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? PaymentReference { get; set; }
    public string? ClearanceNumber { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? PaidAt { get; set; }
}
