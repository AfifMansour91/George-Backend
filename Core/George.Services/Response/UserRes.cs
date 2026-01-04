using George.Common;

namespace George.Services
{
	public class UserRes
	{
		public int Id { get; set; }
		public string FirstName { get; set; } = null!;
		public string? LastName { get; set; }
		public string? IdentificationNumber { get; set; } // Optional - not in User model
		public string? Email { get; set; }
		public string? Phone { get; set; } // Made optional to match User model
		//public bool IsActive { get; set; }
		public int LanguageId { get; set; }
		public UserStatus? StatusId { get; set; }
		public int? RoleId { get; set; } // Added: User role ID
		public int? AccountId { get; set; } // Added: Account ID
		public string? AvatarUrl { get; set; } // Added: Avatar URL
		public DateTime? LastLoginDate { get; set; } // Added: Last login date
		public DateTime CreationTime { get; set; }
		public DateTime UpdateTime { get; set; }

		//public bool? CanBlock { get; set; }
		//public bool? CanUnblock { get; set; }
		//public bool? CanEdit { get; set; }
		//public bool? CanDelete { get; set; }

	}

	public class InnerUserRes
	{
		//public int Id { get; set; }
		//public UserRole RoleId { get; set; }
		//public string? FullName { get; set; }

		public string FirstName { get; set; } = null!;

		public string? LastName { get; set; }

		public string? Email { get; set; }


		//public bool IsActive { get; set; }

	}

}
