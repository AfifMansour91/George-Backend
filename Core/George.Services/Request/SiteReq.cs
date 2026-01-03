using George.Common;
using George.Services.Response;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Services.Request
{
    public class SiteReq
    {
        public bool IsActive { get; set; } = true;
        public int? AccountId { get; set; }
        public string SiteName { get; set; } = null!;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
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
