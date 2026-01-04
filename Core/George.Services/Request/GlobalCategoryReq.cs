using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class GlobalCategoryReq
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int? ParentGlobalCategoryId { get; set; }

        public List<int>? BusinessTypeIds { get; set; }

        public int? SortOrder { get; set; }

        public int? ProductCount { get; set; }
    }

    public class CreateGlobalCategoryReq : GlobalCategoryReq
    {
    }

    public class UpdateGlobalCategoryReq : GlobalCategoryReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}

