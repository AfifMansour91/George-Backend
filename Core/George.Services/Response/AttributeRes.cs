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
        /// <summary>Values in their manual display order (the order sent on create/update is the order saved).</summary>
        public List<string> Values { get; set; } = new();
    }

    public class AttributeValueProductCountRes
    {
        public int AttributeId { get; set; }
        public string Value { get; set; } = null!;
        public int ProductCount { get; set; }
    }
}
