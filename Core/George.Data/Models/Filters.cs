using Microsoft.EntityFrameworkCore;
using George.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Common
{
	public class SearchFilter
	{
		public string? SearchTerm { get; set; }
	}

	public class RegistryUnitFilter// : SearchFilter
	{
		public bool? HasRegistrationMethod { get; set; }
		public bool? HasLandUse { get; set; }
		public bool? HasAddress { get; set; }
		public bool? HasArea { get; set; }
		public bool? IsFilled { get; set; }

		public List<OwnershipType>? OwnershipTypeIds { get; set; }
		public List<RegistrationMethod>? RegistrationMethodIds { get; set; }
		public List<int>? LandUseIds { get; set; }
		
		public int? Block { get; set; }
		public int? Parcel { get; set; }
		public decimal? MinArea { get; set; }
		public decimal? MaxArea { get; set; }
		public string? Address { get; set; }
	}

	public class UserFilter : SearchFilter
	{
		public string? Name { get; set; }
		public UserStatus? StatusId { get; set; }
	}


}
