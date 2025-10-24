using George.Services;

namespace George.Api.Core
{
	// This class is used in order to prevent continuous reads from the config file.
	public static class Globals
	{
		public static bool OverrideAuthentication { get; set; } = false;
		public static int OverrideUserId { get; set; } = AuthHelper.INVALID_ID;
		public static bool OverrideIsMaster { get; set; } = false;

		public static string? MachineName { get; set; }

		
	}
}
