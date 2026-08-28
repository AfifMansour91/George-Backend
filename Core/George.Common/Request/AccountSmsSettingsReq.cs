namespace George.Common.Request
{
    /// <summary>Save per-account SMS credentials. ApiToken null/empty keeps the token already stored (the client only ever sees a masked token).</summary>
    public class AccountSmsSettingsReq
    {
        public bool IsEnabled { get; set; }

        /// <summary>Currently only "ActiveTrail"; omit for default.</summary>
        public string? Provider { get; set; }

        /// <summary>Optional provider API URL override; empty = system default URL.</summary>
        public string? ApiBaseUrl { get; set; }

        /// <summary>New API token; null/empty = keep the existing stored token.</summary>
        public string? ApiToken { get; set; }

        /// <summary>Sender/display name shown to the SMS recipient.</summary>
        public string? FromName { get; set; }

        public string? SourcePhone { get; set; }
    }

    /// <summary>Send a test SMS to verify the account's SMS credentials.</summary>
    public class AccountSmsTestReq
    {
        public string Phone { get; set; } = string.Empty;

        /// <summary>Optional message override; empty = default test text.</summary>
        public string? Message { get; set; }
    }
}
