using System;
using System.Collections.Generic;
using System.Text;
using George.Common;

namespace George.Services
{
	public class AuthenticatedUser
	{
		public int Id { get; set; } = AuthHelper.INVALID_ID;

		//public string? UserIP { get; set; }

		//public string? UserAgent { get; set; }

		//public UserRole Role { get; set; }

		public bool IsMaster { get; set; } = false;

		public bool IsAuthenticated()
		{
			return (this.Id.IsValidID() /*&& this.Role != UserRole.None*/);
		}
	}
}
