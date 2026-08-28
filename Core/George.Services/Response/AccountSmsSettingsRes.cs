namespace George.Services.Response
{
    /// <summary>Per-account SMS settings. The API token is never returned — only a masked hint.</summary>
    public class AccountSmsSettingsRes
    {
        public int AccountId { get; set; }

        /// <summary>True when a settings row exists for the account (even if disabled).</summary>
        public bool IsConfigured { get; set; }

        public bool IsEnabled { get; set; }

        public string Provider { get; set; } = "ActiveTrail";

        public string? ApiBaseUrl { get; set; }

        public bool HasApiToken { get; set; }

        /// <summary>Masked token hint (last characters only), e.g. "••••4C3A".</summary>
        public string? ApiTokenMasked { get; set; }

        public string? FromName { get; set; }

        public string? SourcePhone { get; set; }

        /// <summary>True when sends for this account effectively go through the system-wide SMS account.</summary>
        public bool UsingSystemDefault { get; set; }
    }

    public class AccountSmsTestRes
    {
        public bool Sent { get; set; }

        /// <summary>True when the test went out with the account's own credentials (false = system default).</summary>
        public bool UsedAccountConfig { get; set; }
    }
}
