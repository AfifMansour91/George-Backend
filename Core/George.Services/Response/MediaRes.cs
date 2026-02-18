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
    }

    /// <summary>Result of downloading external media and saving to our storage.</summary>
    public class DownloadAndSaveMediaRes
    {
        public int Processed { get; set; }
        public int Saved { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<DownloadAndSaveError> Errors { get; set; } = new();
    }

    public class DownloadAndSaveError
    {
        public int MediaId { get; set; }
        public string Message { get; set; } = null!;
    }
}

