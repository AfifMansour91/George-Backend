using System;
using System.Collections.Generic;
using System.Text;
using George.Common;
using Newtonsoft.Json;
using George.Data;

namespace George.Services
{
	public class AuthRes
	{
		public bool ShouldSerializeUserId()
		{
			return UserId.IsValidID();
		}

		public bool ShouldSerializeRole()
		{
			//return RoleId != UserRole.None;
			return false;
		}

		public int UserId { get; set; }
		public UserStatus? StatusId { get; set; }
		//public UserRole RoleId { get; set; }
		public string? AccessToken { get; set; }
		public DateTime AccessTokenExpiration { get; set; }

		public string? RefreshToken { get; set; }
		[JsonIgnore]
		public DateTime RefreshTokenExpiration { get; set; }

		//public UserPermissions? Permissions { get; set; }
	}

	public class EmergencyContactRes
	{
		public int Id { get; set; }
		public string Name { get; set; } = null!;
		public string? Email { get; set; }
		public string Phone { get; set; } = null!;
		public bool IsDefault { get; set; }
		public DateTime CreationTime { get; set; }
		public DateTime UpdateTime { get; set; }
	}

	public class ProfileRes
	{
		public int Id { get; set; }
		public int StatusId { get; set; }
		public string FirstName { get; set; } = null!;
		public string? LastName { get; set; }
		public string? Email { get; set; }
		public bool IsEmailVerified { get; set; }
		//public UserPermissions? Permissions { get; set; }
	}

	//public class RoleRes
	//{
	//	public class PermissionRes
	//	{
	//		public int Id { get; set; }
	//		public string Name { get; set; }
	//	}

	//	public int Id { get; set; }
	//	public string Name { get; set; }

	//	public List<PermissionRes> Permissions { get; set; } = new();
 //   }
}