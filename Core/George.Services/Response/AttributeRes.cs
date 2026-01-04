namespace George.Services.Response
{
    public class AttributeRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string Name { get; set; } = null!;
        public int SiteId { get; set; }
        public List<string> Values { get; set; } = new();
    }
}
