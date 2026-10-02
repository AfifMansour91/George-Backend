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

	/// <summary>Supported per-account SMS provider names (stored in AccountSmsSettings.Provider).</summary>
	public static class SmsProviderNames
	{
		public const string ActiveTrail = "ActiveTrail";
		public const string Inforu = "Inforu";
	}

	/// <summary>Per-message extras a provider may support (Inforu: message id echoed back in delivery reports + the DLR webhook).</summary>
	public class SmsSendOptions
	{
		public string? CustomerMessageId { get; set; }
		public string? DeliveryNotificationUrl { get; set; }
	}

	/// <summary>Outcome of one send. <see cref="ProviderStatusId"/> is the provider's own code (Inforu: 1 ok, -13/-14/-15 quota exceeded, -21/-94/-291 sender problems).</summary>
	public class SmsSendResult
	{
		public bool Success { get; set; }
		public int? ProviderStatusId { get; set; }
		public string? Error { get; set; }

		/// <summary>The provider account (or the sub-account) has no messages left - retrying every recipient would only fail them all.</summary>
		public bool QuotaExceeded => ProviderStatusId is -13 or -14 or -15;

		public static SmsSendResult Ok() => new() { Success = true };
		public static SmsSendResult Fail(string error, int? statusId = null) => new() { Success = false, Error = error, ProviderStatusId = statusId };
	}

	/// <summary>The platform's own (parent) Inforu account - the only one allowed to add quota to the shops' sub-accounts.</summary>
	public class InforuParentCredentials
	{
		public string? ApiBaseUrl { get; set; }
		public string Username { get; set; } = string.Empty;
		public string ApiToken { get; set; } = string.Empty;
	}

	/// <summary>Inforu Admin/GetQuota, SMS row only.</summary>
	public class InforuQuotaInfo
	{
		public bool Success { get; set; }
		public string? Error { get; set; }
		/// <summary>Customer | Project | User - the level the answer describes.</summary>
		public string? Level { get; set; }
		/// <summary>The id at that level (the sub-account's Inforu customer id when Level = Customer).</summary>
		public string? LevelValue { get; set; }
		public int? RemainingSms { get; set; }
		/// <summary>Packages | Monthly | Unlimited.</summary>
		public string? QuotaType { get; set; }
		public int? WarningLevel { get; set; }
	}

	/// <summary>Inforu Admin/CreateOrAddQuota.</summary>
	public class InforuAddQuotaOutcome
	{
		public bool Success { get; set; }
		public string? Error { get; set; }
		public int? RemainingBefore { get; set; }
		public int? RemainingAfter { get; set; }
	}

	/// <summary>Per-account SMS credentials override. When null (or invalid) the system-wide static config is used.</summary>
	public class SmsAccountConfig
	{
		public string Provider { get; set; } = SmsProviderNames.ActiveTrail;

		/// <summary>Optional API URL override; null = provider default URL.</summary>
		public string? ApiBaseUrl { get; set; }

		/// <summary>Inforu API username (Basic auth = username:token). Not used by ActiveTrail.</summary>
		public string Username { get; set; } = string.Empty;

		public string ApiToken { get; set; } = string.Empty;

		/// <summary>Sender/display name shown to the SMS recipient.</summary>
		public string FromName { get; set; } = string.Empty;

		/// <summary>Sub-account the platform opened for the shop (it pays the provider) - the shop is still metered by George.</summary>
		public bool BilledByPlatform { get; set; }

		/// <summary>The sub-account's numeric customer id in Inforu (LevelValue for quota calls). Learned from GetQuota when not set by hand.</summary>
		public string? InforuCustomerId { get; set; }

		public bool IsInforu =>
			string.Equals(Provider?.Trim(), SmsProviderNames.Inforu, StringComparison.OrdinalIgnoreCase);

		public bool IsValid =>
			!string.IsNullOrWhiteSpace(ApiToken) &&
			!string.IsNullOrWhiteSpace(FromName) &&
			(!IsInforu || !string.IsNullOrWhiteSpace(Username));
	}
}