namespace George.Data.Dto
{
    public class AccountCategoryDto
    {
        public long AccountCategoryId { get; set; }
        public long? ParentAccountCategoryId { get; set; }
        public string DisplayName { get; set; } = default!;
        public string? CustomName { get; set; }
        public int SortOrder { get; set; }
        public bool IsEnabled { get; set; }
        public int MasterCategoryId { get; set; }
    }
}
