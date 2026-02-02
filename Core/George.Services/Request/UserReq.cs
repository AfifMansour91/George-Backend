using System.ComponentModel.DataAnnotations;
using George.Common;
using Newtonsoft.Json;

namespace George.Services
{

	public class EmailReq
	{
		[RequiredNotEmpty]
		[EmailAddress]
		public string Email { get; set; } = null!;
	}

	public class CreateUserReq
	{
		[RequiredNotEmpty]
		[StringLength(50)]
		public string FirstName { get; set; } = null!;

		[StringLength(50)]
		public string? LastName { get; set; }

		[RequiredNotEmpty]
		[EmailAddress]
		public string Email { get; set; } = null!;

		[StringLength(50)]
		public string? Phone { get; set; }

		public string? Password { get; set; }

		public int? AccountId { get; set; }

		public int? RoleId { get; set; } // UserRole: 1=Admin, 2=AccountAdmin, 3=SiteAdmin

		public int? StatusId { get; set; } // UserStatus: 1=Active, 2=Blocked, 3=PendingInvite, 4=Pending

		[StringLength(500)]
		public string? AvatarUrl { get; set; }

		/// <summary>Site IDs for site_admin role. Empty/null = no sites assigned.</summary>
		public List<int>? SiteIds { get; set; }
	}

	public class UpdateUserReq
	{
		[Required]
		[ValidId]
		public int Id { get; set; }

		[StringLength(50)]
		public string? FirstName { get; set; }

		[StringLength(50)]
		public string? LastName { get; set; }

		[EmailAddress]
		public string? Email { get; set; }

		[StringLength(50)]
		public string? Phone { get; set; }

		public string? Password { get; set; }

		public int? AccountId { get; set; }

		public int? RoleId { get; set; } // UserRole: 1=Admin, 2=AccountAdmin, 3=SiteAdmin

		public int? StatusId { get; set; } // UserStatus: 1=Active, 2=Blocked, 3=PendingInvite, 4=Pending

		[StringLength(500)]
		public string? AvatarUrl { get; set; }

		/// <summary>Site IDs for site_admin role. Empty/null = no sites assigned.</summary>
		public List<int>? SiteIds { get; set; }
	}

}
