using Newtonsoft.Json;

namespace George.Providers.Sms019
{
    public class SmsRequest
    {
        [JsonProperty("sms")]
        public Sms Sms { get; set; }
    }

    public class Sms
    {
        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("destinations")]
        public Destinations Destinations { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class User
    {
        [JsonProperty("username")]
        public string Username { get; set; }
    }

    public class Destinations
    {
        [JsonProperty("phone")]
        public List<string> Phone { get; set; }
    }


	public class SmsResponse
	{
        [JsonProperty("status")]
		public string Status { get; set; }

        [JsonProperty("message")]
		public string Message { get; set; }

        [JsonProperty("shipment_id")]
		public string ShipmentId { get; set; }
    }
}
