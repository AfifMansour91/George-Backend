using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using George.Common;

namespace George.Providers
{
	public class SmsUserResponse
	{
		public string Phone { get; set; } = null!;
		public string Value { get; set; } = null!;
		public string Date { get; set; } = null!;
    }

	/// <summary>Per-account SMS credentials override. When null (or invalid) the system-wide static config is used.</summary>
	public class SmsAccountConfig
	{
		/// <summary>Optional API URL override; null = system default URL.</summary>
		public string? ApiBaseUrl { get; set; }

		public string ApiToken { get; set; } = string.Empty;

		/// <summary>Sender/display name shown to the SMS recipient.</summary>
		public string FromName { get; set; } = string.Empty;

		public bool IsValid =>
			!string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(FromName);
	}
}