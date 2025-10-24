using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using George.Common;

namespace George.Services
{
	public class UserPermissions
	{

		public bool IsEmpty { get; }

		public bool IsMaster = false;
		public int UserId { get; set; }
	}
}
