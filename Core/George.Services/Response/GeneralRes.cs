using George.Data;

namespace George.Services
{
	public class ConfigurationRes
	{
		public class ConfigurationParameters
		{
            public string Environment { get; set; } = null!;
        }

		public ConfigurationParameters Parameters { get; set; } = new();
		public List<ColorRes>? Colors { get; set; }
		public List<IdNamePair>? Roles { get; set; }
	}

	public class ColorRes
	{
		public int Id { get; set; }
		public string Name { get; set; } = null!;
		public string HexCode { get; set; } = null!;
	}
	public class MetadataRes
	{
		public List<IdNamePair>? OwnershipTypes { get; set; }
		public List<IdNamePair>? RegistrationMethods { get; set; }
		public List<IdNamePair>? Roles { get; set; }
		public List<IdNamePair>? SortFields { get; set; }
		public List<IdNamePair>? SortOrders { get; set; }
		public List<IdNamePair>? StatusCodes { get; set; }
		public List<IdNamePair>? UserStatuses { get; set; }
	}

	public class UploadRes
	{
		public bool IsSuccessful { get; set; }
		public string? FileKey { get; set; }
		public string? OriginalFileName { get; set; }
		public string? FileUrl { get; set; }
	}

}
