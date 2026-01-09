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
        }

        internal class SettingsBlock
        {
            [JsonProperty("Sender")]
            public string Sender { get; set; }
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
    }
}
