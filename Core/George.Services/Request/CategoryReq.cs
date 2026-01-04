using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class CategoryReq
    {
        [Required]
        public string Name { get; set; } = null!;
        
        public int? ParentCategoryId { get; set; }
        
        public string? Description { get; set; }
        
        public string? CustomName { get; set; }
        
        public bool? IsEnabled { get; set; }
        
        public int? SortOrder { get; set; }
        
        public bool? DisplayAsMain { get; set; }
        
        public int? AccountId { get; set; }
        
        public List<int>? SiteIds { get; set; }
    }

    public class CreateCategoryReq : CategoryReq
    {
    }

    public class UpdateCategoryReq : CategoryReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}
