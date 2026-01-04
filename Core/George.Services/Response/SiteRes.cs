namespace George.Services.Response
{
    public class SiteRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public int AccountId { get; set; }
        public string SiteName { get; set; } = null!;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public List<int> BusinessTypeIds { get; set; } = new();
        public string? Status { get; set; } // "active" | "inactive"
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
        public string Currency { get; set; } = "ILS";
    }
}
