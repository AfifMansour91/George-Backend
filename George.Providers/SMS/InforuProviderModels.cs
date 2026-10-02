using Newtonsoft.Json;

namespace George.Providers
{
    internal class InforuSmsRequest
    {
        [JsonProperty("Data")]
        public DataBlock Data { get; set; } = new();

        internal class DataBlock
        {
            [JsonProperty("Message")]
            public string Message { get; set; }

            [JsonProperty("Recipients")]
            public List<Recipient> Recipients { get; set; } = new();

            [JsonProperty("Settings")]
            public SettingsBlock Settings { get; set; } = new();
        }

        internal class Recipient
        {
            [JsonProperty("Phone")]
            public string Phone { get; set; }

            /// <summary>Echoed back in delivery reports as CustomerMessageId.</summary>
            [JsonProperty("CustomerMessageID", NullValueHandling = NullValueHandling.Ignore)]
            public string? CustomerMessageId { get; set; }
        }

        internal class SettingsBlock
        {
            [JsonProperty("Sender")]
            public string Sender { get; set; }

            /// <summary>Inforu POSTs a delivery report (JSON) for every message to this URL.</summary>
            [JsonProperty("DeliveryNotificationUrl", NullValueHandling = NullValueHandling.Ignore)]
            public string? DeliveryNotificationUrl { get; set; }
        }
    }

    /// <summary>General → Get Quota. Empty Level = the authenticated user's own customer.</summary>
    internal class InforuQuotaRequest
    {
        [JsonProperty("Data")]
        public DataBlock Data { get; set; } = new();

        internal class DataBlock
        {
            [JsonProperty("QuotaUsageType")]
            public string QuotaUsageType { get; set; } = "SMS";

            [JsonProperty("Level")]
            public string Level { get; set; } = string.Empty;

            [JsonProperty("LevelValue")]
            public string LevelValue { get; set; } = string.Empty;
        }
    }

    internal class InforuQuotaResponse
    {
        [JsonProperty("StatusId")]
        public int? StatusId { get; set; }

        [JsonProperty("StatusDescription")]
        public string? StatusDescription { get; set; }

        [JsonProperty("DetailedDescription")]
        public string? DetailedDescription { get; set; }

        [JsonProperty("Data")]
        public DataBlock? Data { get; set; }

        internal class DataBlock
        {
            [JsonProperty("Level")]
            public string? Level { get; set; }

            /// <summary>Numeric id in the docs, but tolerate a string.</summary>
            [JsonProperty("LevelValue")]
            public object? LevelValue { get; set; }

            [JsonProperty("List")]
            public List<QuotaItem>? List { get; set; }
        }

        internal class QuotaItem
        {
            [JsonProperty("QuotaUsageType")]
            public string? QuotaUsageType { get; set; }

            /// <summary>Packages | Monthly | Unlimited.</summary>
            [JsonProperty("QuotaType")]
            public string? QuotaType { get; set; }

            [JsonProperty("RemainingQuota")]
            public long? RemainingQuota { get; set; }

            [JsonProperty("WarningLevel")]
            public long? WarningLevel { get; set; }
        }
    }

    /// <summary>General → Create Or Add Quota. Packages: the amount is ADDED; Monthly would overwrite, so we never send that.</summary>
    internal class InforuAddQuotaRequest
    {
        [JsonProperty("Data")]
        public DataBlock Data { get; set; } = new();

        internal class DataBlock
        {
            [JsonProperty("Level")]
            public string Level { get; set; } = "Customer";

            [JsonProperty("LevelValue")]
            public string LevelValue { get; set; } = string.Empty;

            [JsonProperty("QuotaUsageType")]
            public string QuotaUsageType { get; set; } = "SMS";

            [JsonProperty("QuotaType")]
            public string QuotaType { get; set; } = "Packages";

            [JsonProperty("QuotaAmount")]
            public string QuotaAmount { get; set; } = "0";

            [JsonProperty("AllowChangeQuotaType")]
            public string AllowChangeQuotaType { get; set; } = "false";
        }
    }

    internal class InforuAddQuotaResponse
    {
        [JsonProperty("StatusId")]
        public int? StatusId { get; set; }

        [JsonProperty("StatusDescription")]
        public string? StatusDescription { get; set; }

        [JsonProperty("DetailedDescription")]
        public string? DetailedDescription { get; set; }

        [JsonProperty("Data")]
        public DataBlock? Data { get; set; }

        internal class DataBlock
        {
            [JsonProperty("Before")]
            public Snapshot? Before { get; set; }

            [JsonProperty("After")]
            public Snapshot? After { get; set; }
        }

        internal class Snapshot
        {
            [JsonProperty("QuotaType")]
            public string? QuotaType { get; set; }

            [JsonProperty("RemainingQuota")]
            public long? RemainingQuota { get; set; }
        }
    }

    internal class InforuSmsResponse
    {
        [JsonProperty("Status")]
        public string Status { get; set; }

        [JsonProperty("Message")]
        public string Message { get; set; }

        [JsonProperty("BatchId")]
        public string BatchId { get; set; }

        /// <summary>Inforu API v2: 1 = success.</summary>
        [JsonProperty("StatusId")]
        public int? StatusId { get; set; }

        [JsonProperty("StatusDescription")]
        public string? StatusDescription { get; set; }

        [JsonProperty("DetailedDescription")]
        public string? DetailedDescription { get; set; }
    }
}
