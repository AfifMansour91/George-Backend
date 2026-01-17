using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class SiteReq
    {
        [Required]
        public int AccountId { get; set; }
        public string? SiteName { get; set; } = null!;
        
        public string? Location { get; set; }
        public string? Description { get; set; }
        public List<int>? BusinessTypeIds { get; set; }
        public string? Status { get; set; } // "active" | "inactive"
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
        public string Currency { get; set; } = "ILS";
        public string? WooCommerceUrl { get; set; }
        public string? WooCommerceKey { get; set; }
        public string? WooCommerceSecret { get; set; }
        public bool? WooCommerceEnabled { get; set; }
    }

    public class CreateSiteReq : SiteReq
    {
    }

    public class UpdateSiteReq : SiteReq
    {
        // Note: Id is not included here because it comes from the route parameter, not the request body
        // This ensures updates are always by ID from the route, not from the request body
    }
}
