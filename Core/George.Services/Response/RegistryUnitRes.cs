using George.Common;
using George.Data;

namespace George.Services
{
	public class RegistryUnitRes
	{
		public int Id { get; set; }

		public OwnershipType OwnershipTypeId { get; set; }

		public RegistrationMethod? RegistrationMethodId { get; set; }

		public int? LandUseId { get; set; }

		public int Block { get; set; }

		public int Parcel { get; set; }

		public string? Address { get; set; }

		public decimal? Area { get; set; }

		public string? Data { get; set; }

		public int IsFilled { get; set; }

		public List<int>? SubParcels { get; set; }

		public List<MediumRes>? Media { get; set; }
	}

	public class MediumRes
	{
		public int Id { get; set; }

		public string Name { get; set; } = null!;

		public string? Url { get; set; }

		public string? FileUrl { get; set; }

		public string? Description { get; set; }
	}


}
