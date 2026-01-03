using George.Data;
using George.DB;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Services.Response
{
    public class SiteRes
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid GuidId { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public int? UpdateUserId { get; set; }
        public bool IsActive { get; set; }
        public int AccountId { get; set; }
        public string SiteName { get; set; } = null!;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
        public AccountRes Account { get; set; } = null!;
    }
}
