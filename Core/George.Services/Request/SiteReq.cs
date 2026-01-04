using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class SiteReq
    {
        [Required]
        public int AccountId { get; set; }
        
        [Required]
        public string SiteName { get; set; } = null!;
        
        public string? Location { get; set; }
        public string? Description { get; set; }
        public List<int>? BusinessTypeIds { get; set; }
        public string? Status { get; set; } // "active" | "inactive"
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
        public string Currency { get; set; } = "ILS";
    }

    public class CreateSiteReq : SiteReq
    {
    }

    public class UpdateSiteReq : SiteReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}
