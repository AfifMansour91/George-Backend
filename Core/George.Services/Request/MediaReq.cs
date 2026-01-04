using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class MediaReq
    {
        [Required]
        public string Url { get; set; } = null!;

        [Required]
        public string Name { get; set; } = null!;

        public string? Type { get; set; } // "image" | "video" | "document"

        public int? BusinessTypeId { get; set; }

        public List<int>? CategoryIds { get; set; }

        public List<int>? SubcategoryIds { get; set; }

        public List<string>? Tags { get; set; }

        public long? FileSize { get; set; }

        public int? UsageCount { get; set; }

        public int? AccountId { get; set; }
    }

    public class CreateMediaReq : MediaReq
    {
    }

    public class UpdateMediaReq : MediaReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}

