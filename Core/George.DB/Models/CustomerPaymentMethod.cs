using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index(nameof(CustomerId), nameof(SiteId))]
public partial class CustomerPaymentMethod
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int SiteId { get; set; }

    [StringLength(1000)]
    public string EncryptedToken { get; set; } = null!;

    [StringLength(16)]
    public string? TokenExDate { get; set; }

    [StringLength(8)]
    public string? CardExpirationMMYY { get; set; }

    [StringLength(8)]
    public string? Last4Digits { get; set; }

    [StringLength(32)]
    public string? CardBrand { get; set; }

    [StringLength(500)]
    public string? EncryptedApprovalNumber { get; set; }

    public bool IsDefault { get; set; }

    public bool IsRetired { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey(nameof(SiteId))]
    public virtual Site Site { get; set; } = null!;

    [InverseProperty("CustomerPaymentMethod")]
    public virtual ICollection<Order> Order { get; set; } = new List<Order>();
}
