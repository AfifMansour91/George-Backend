using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.DB
{
	public class AlertUserLocation
	{
		public int UserId { get; set; }
		public int SiteUserId { get; set; }
		public string UserName { get; set; } = null!;

		public string? FirstName { get; set; }
		public string? LastName { get; set; }

		public int? TeamId { get; set; }
		public string? TeamName { get; set; }

		public double? Distance { get; set; }
		public bool? HasApproved { get; set; }
	}

	public class OpenAlertsStats
	{
		public int TechPriority { get; set; }
		public int TechTotal { get; set; }
		public int HumanPriority { get; set; }
		public int HumanTotal { get; set; }
		public int PinnedPriority { get; set; }
		public int PinnedTotal { get; set; }
		public int AllPriority { get; set; }
		public int AllTotal { get; set; }
	}
}
