namespace George.Services.Response
{
    public class GlobalCategoryRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int? ParentGlobalCategoryId { get; set; }
        public List<int> BusinessTypeIds { get; set; } = new();
        public int? SortOrder { get; set; }
        public int? ProductCount { get; set; }
        public string? ImageUrl { get; set; }
        public string? IconUrl { get; set; }
    }
}

