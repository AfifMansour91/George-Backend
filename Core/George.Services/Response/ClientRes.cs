namespace George.Services.Response
{
    public class ClientRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? ClientRole { get; set; } // "super_admin" | "account_admin" | "site_admin"
        public int? UserId { get; set; }
        public int? AccountId { get; set; }
        public List<int> SiteIds { get; set; } = new();
        public string? Status { get; set; } // "active" | "inactive" | "suspended"
        public string? AvatarUrl { get; set; }
        public string? Department { get; set; }
        public DateTime? LastLogin { get; set; }
        public string? Notes { get; set; }
    }
}

