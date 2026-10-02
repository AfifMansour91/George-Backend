namespace George.Common.Request
{
    /// <summary>Save per-account SMS credentials. ApiToken null/empty keeps the token already stored (the client only ever sees a masked token).</summary>
    public class AccountSmsSettingsReq
    {
        public bool IsEnabled { get; set; }

        /// <summary>"ActiveTrail" (default) or "Inforu".</summary>
        public string? Provider { get; set; }

        /// <summary>Optional provider API URL override; empty = provider default URL.</summary>
        public string? ApiBaseUrl { get; set; }

        /// <summary>Inforu API username (Basic auth = username:token). Not used by ActiveTrail. Empty keeps the stored value.</summary>
        public string? Username { get; set; }

        /// <summary>New API token; null/empty = keep the existing stored token.</summary>
        public string? ApiToken { get; set; }

        /// <summary>Sender/display name shown to the SMS recipient.</summary>
        public string? FromName { get; set; }

        public string? SourcePhone { get; set; }

        /// <summary>Sub-account opened by the platform (billed through George). Super-admin only; null keeps the stored value.</summary>
        public bool? BilledByPlatform { get; set; }

        /// <summary>Inforu customer id of the sub-account (super-admin only; null keeps the stored / auto-discovered value, empty string clears it).</summary>
        public string? InforuCustomerId { get; set; }
    }

    /// <summary>Choose the account's active SMS provider: "ActiveTrail" | "Inforu" | null (system default).</summary>
    public class AccountSmsActiveProviderReq
    {
        public string? Provider { get; set; }
    }

    /// <summary>Send a test SMS to verify the account's SMS credentials.</summary>
    public class AccountSmsTestReq
    {
        public string Phone { get; set; } = string.Empty;

        /// <summary>Optional message override; empty = default test text.</summary>
        public string? Message { get; set; }
    }
}
