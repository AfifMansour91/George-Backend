using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Data
{
	public class SystemUserDTO
	{
		public int RoleId { get; set; }
	}

	public class AccountUserDTO
	{
		public int AccountId { get; set; }
		public int? SiteId { get; set; }
		public int RoleId { get; set; }
		public int? SiteStatusId { get; set; }
	}

	public class SiteUserDTO
	{
		public int SiteId { get; set; }
		public int RoleId { get; set; }
		public int SiteStatusId { get; set; }
	}
}
