using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class ClientReq
    {
        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public string? Phone { get; set; }

        public string? ClientRole { get; set; } // "super_admin" | "account_admin" | "site_admin"

        public int? UserId { get; set; } // Link to existing User

        public int? AccountId { get; set; }

        public List<int>? SiteIds { get; set; }

        public string? Status { get; set; } // "active" | "inactive" | "suspended"

        public string? AvatarUrl { get; set; }

        public string? Department { get; set; }

        public string? Notes { get; set; }
    }

    public class CreateClientReq : ClientReq
    {
    }

    public class UpdateClientReq : ClientReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}

