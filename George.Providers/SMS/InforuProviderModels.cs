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

    /// <summary>cloud.inforu.co.il/api/Customer/Create - opens a sub-account under the parent (Inforu support, 11.10.2026).</summary>
    internal class InforuCloudCustomerCreateRequest
    {
        [JsonProperty("User")] public InforuCloudUser User { get; set; } = new();
        [JsonProperty("Data")] public DataBlock Data { get; set; } = new();

        internal class DataBlock
        {
            [JsonProperty("CustomerName")] public string CustomerName { get; set; } = string.Empty;
            [JsonProperty("Username")] public string Username { get; set; } = string.Empty;
            [JsonProperty("Password")] public string Password { get; set; } = string.Empty;
            [JsonProperty("FirstName")] public string? FirstName { get; set; }
            [JsonProperty("LastName")] public string? LastName { get; set; }
            [JsonProperty("ApiToken")] public string ApiToken { get; set; } = string.Empty;
            [JsonProperty("Culture")] public string Culture { get; set; } = "HE";
            [JsonProperty("Email")] public string? Email { get; set; }
            [JsonProperty("PhoneNumber")] public string? PhoneNumber { get; set; }
            /// <summary>1 = SMS.</summary>
            [JsonProperty("QuotaUsageTypeId")] public string QuotaUsageTypeId { get; set; } = "1";
            /// <summary>1 = Unrestricted, 2 = Monthly Renewal, 3 = Packages.</summary>
            [JsonProperty("QuotaTypeId")] public string QuotaTypeId { get; set; } = "3";
            [JsonProperty("QuotaAmount")] public string QuotaAmount { get; set; } = "0";
            [JsonProperty("WarningLevel")] public string WarningLevel { get; set; } = "0";
        }
    }

    /// <summary>cloud.inforu.co.il/api/Customer/Update - sets the quota type/amount of an existing sub-account.</summary>
    internal class InforuCloudCustomerUpdateRequest
    {
        [JsonProperty("User")] public InforuCloudUser User { get; set; } = new();
        [JsonProperty("Data")] public DataBlock Data { get; set; } = new();

        internal class DataBlock
        {
            [JsonProperty("CustomerId")] public string CustomerId { get; set; } = string.Empty;
            [JsonProperty("QuotaUsageTypeId")] public string QuotaUsageTypeId { get; set; } = "1";
            [JsonProperty("QuotaTypeId")] public string QuotaTypeId { get; set; } = "3";
            [JsonProperty("QuotaAmount")] public string QuotaAmount { get; set; } = "0";
            [JsonProperty("WarningLevel", NullValueHandling = NullValueHandling.Ignore)] public string? WarningLevel { get; set; }
        }
    }

    internal class InforuCloudUser
    {
        [JsonProperty("Username")] public string Username { get; set; } = string.Empty;
        [JsonProperty("Token")] public string Token { get; set; } = string.Empty;
    }

    /// <summary>Loose shape: the v1 API answers with StatusId/StatusDescription and a Data object whose keys differ per call.</summary>
    internal class InforuCloudResponse
    {
        [JsonProperty("StatusId")] public int? StatusId { get; set; }
        [JsonProperty("StatusDescription")] public string? StatusDescription { get; set; }
        [JsonProperty("DetailedDescription")] public string? DetailedDescription { get; set; }
        /// <summary>The field InforU actually sends (live answer 21.10.2026).</summary>
        [JsonProperty("DetailDescription")] public string? DetailDescription { get; set; }
        [JsonProperty("Data")] public Newtonsoft.Json.Linq.JToken? Data { get; set; }
        /// <summary>The field InforU actually sends for the payload (live answer 21.10.2026).</summary>
        [JsonProperty("ReturnData")] public Newtonsoft.Json.Linq.JToken? ReturnData { get; set; }
        /// <summary>Customer/Create success: the new customer's id sits at the top level (live answer 21.10.2026).</summary>
        [JsonProperty("CustomerId")] public long? CustomerId { get; set; }
    }
}
