using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using George.Common;
using George.Data;

namespace George.Services
{
	public abstract class RegistryUnitReq
	{
		[RequiredEnumField]
		public OwnershipType OwnershipTypeId { get; set; }

		[RequiredEnumField(allowZero:true)]
		public RegistrationMethod? RegistrationMethodId { get; set; }

		public int? LandUseId { get; set; }

		[PositiveNumber]
		public int Block { get; set; }

		[PositiveNumber(includeZero:true)]
		public int Parcel { get; set; }

		public string? Address { get; set; }

		public decimal? Area { get; set; }

		public string? Data { get; set; }

		public List<int> SubParcels { get; set; } = new();
	}

	public class CreateRegistryUnitReq : RegistryUnitReq
	{
		
	}

	public class UpdateRegistryUnitReq : RegistryUnitReq
	{
		[Required]
		[ValidId]
		public int Id { get; set; }
	}

	public abstract class MediumReq
	{
		[RequiredNotEmpty]
		public string Name { get; set; } = null!;

		public string? Url { get; set; }

		//public string? FileKey { get; set; }

		public IFormFile? File { get; set; }

		[StringLength(1000)]
		public string? Description { get; set; }
	}

	public class CreateMediumReq : MediumReq
	{
		[Required]
		[ValidId]
		public int RegistryUnitId { get; set; }
	}

	public class UpdateMediumReq : MediumReq
	{
		[Required]
		[ValidId]
		public int Id { get; set; }
	}
}
