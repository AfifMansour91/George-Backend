using George.Common;

namespace George.Services
{
	public class UserRes
	{
		public int Id { get; set; }
		public string FirstName { get; set; } = null!;
		public string? LastName { get; set; }
		public string IdentificationNumber { get; set; } = null!;
		public string? Email { get; set; }
		public string Phone { get; set; } = null!;
		//public bool IsActive { get; set; }
		public int LanguageId { get; set; }
		public UserStatus? StatusId { get; set; }
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
