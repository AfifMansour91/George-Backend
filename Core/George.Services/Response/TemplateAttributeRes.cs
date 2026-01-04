namespace George.Services.Response
{
    public class TemplateAttributeRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string Name { get; set; } = null!;
        public List<string> Values { get; set; } = new();
        public List<int> SiteIds { get; set; } = new();
    }
}

