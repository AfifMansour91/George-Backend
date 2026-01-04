namespace George.Services.Response
{
    public class MediaRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string Url { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Type { get; set; } // "image" | "video" | "document"
        public int? BusinessTypeId { get; set; }
        public List<int> CategoryIds { get; set; } = new();
        public List<int> SubcategoryIds { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public long? FileSize { get; set; }
        public int? UsageCount { get; set; }
        public int? AccountId { get; set; }
    }
}

