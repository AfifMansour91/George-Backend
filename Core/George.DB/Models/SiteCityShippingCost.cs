using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Default delivery fee per city, learned on the new-order screen: choosing 15 ₪ for "כפר סבא" once makes 15 the
/// proposed fee for every later order to כפר סבא, whoever the customer is (PEPE 24/9 - a stand-in until a real
/// delivery module with zones exists). One row per (site, city); the latest choice wins.
/// </summary>
[Index("SiteId", "City", Name = "UX_SiteCityShippingCost_Site_City", IsUnique = true)]
public partial class SiteCityShippingCost
{
    [Key]
    public int Id { get; set; }

    public int SiteId { get; set; }

    [StringLength(120)]
    public string City { get; set; } = null!;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Cost { get; set; }

    public DateTime UpdatedDate { get; set; }
}
