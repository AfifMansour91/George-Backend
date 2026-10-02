namespace George.Services.Response
{
    /// <summary>Per-account SMS settings. The API token is never returned - only a masked hint.</summary>
    public class AccountSmsSettingsRes
    {
        public int AccountId { get; set; }

        /// <summary>True when a settings row exists for the account (even if disabled).</summary>
        public bool IsConfigured { get; set; }

        public bool IsEnabled { get; set; }

        public string Provider { get; set; } = "ActiveTrail";

        public string? ApiBaseUrl { get; set; }

        /// <summary>Inforu API username; null for ActiveTrail.</summary>
        public string? Username { get; set; }

        public bool HasApiToken { get; set; }

        /// <summary>Masked token hint (last characters only), e.g. "••••4C3A".</summary>
        public string? ApiTokenMasked { get; set; }

        public string? FromName { get; set; }

        public string? SourcePhone { get; set; }

        /// <summary>Sub-account opened by the platform under its own provider account - the shop is still metered by the marketing bank.</summary>
        public bool BilledByPlatform { get; set; }

        /// <summary>Inforu customer id of the sub-account, once known.</summary>
        public string? InforuCustomerId { get; set; }

        /// <summary>True when sends for this account effectively go through the system-wide SMS account.</summary>
        public bool UsingSystemDefault { get; set; }

        /// <summary>"ActiveTrail" | "Inforu" | null - which saved row is active (the flat fields above describe it).</summary>
        public string? ActiveProvider { get; set; }

        /// <summary>Provider behind the SYSTEM account right now ("ActiveTrail" | "Inforu", config Sms:Provider) - what "system account" means for this server.</summary>
        public string SystemProvider { get; set; } = "ActiveTrail";

        /// <summary>The Inforu sub-account Giorgio opened for the business, if any (read-only for the shop).</summary>
        public AccountSmsProviderRes? Inforu { get; set; }

        /// <summary>The business's own ActiveTrail account, if saved.</summary>
        public AccountSmsProviderRes? ActiveTrail { get; set; }

        /// <summary>Live remaining SMS of the Inforu sub-account; null when there is none or Inforu did not answer.</summary>
        public AccountSmsQuotaRes? InforuQuota { get; set; }
    }

    public class AccountSmsProviderRes
    {
        public string Provider { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string? Username { get; set; }
        public string? FromName { get; set; }
        public bool HasApiToken { get; set; }
        public string? ApiTokenMasked { get; set; }
        public bool BilledByPlatform { get; set; }
        public string? InforuCustomerId { get; set; }
        /// <summary>The credentials are complete enough to send with.</summary>
        public bool IsValid { get; set; }
    }

    public class AccountSmsQuotaRes
    {
        public bool Available { get; set; }
        public int? Remaining { get; set; }
        public string? QuotaType { get; set; }
        public string? Error { get; set; }
    }

    public class AccountSmsTestRes
    {
        public bool Sent { get; set; }

        /// <summary>True when the test went out with the account's own credentials (false = system default).</summary>
        public bool UsedAccountConfig { get; set; }
    }
}
