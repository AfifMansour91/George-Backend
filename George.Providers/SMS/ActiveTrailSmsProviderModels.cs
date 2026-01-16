using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace George.Providers.ActiveTrail
{
	public class ActiveTrailSmsReq
	{
		public class DetailsReq
		{
			//[JsonProperty("unsubscribe_text")]
			//public string? UnsubscribeText { get; set; }

			[JsonProperty("can_unsubscribe")]
			public bool? CanUnsubscribe { get; set; }

			//[JsonProperty("name")]
			//public string CampaignName { get; set; } = null!;

			[JsonProperty("from_name")]
			public string FromName { get; set; } = null!;

			//[JsonProperty("sms_sending_profile_id")]
			//public int? SmsSendingProfileId { get; set; }

			[JsonProperty("content")]
			public string Content { get; set; } = null!;

            [JsonProperty("name")]
            public string Name { get; set; } = null!;
        }

		public class SchedulingReq
		{
			[JsonProperty("send_now")]
			public bool SendNow { get; set; }

			[JsonProperty("scheduled_date_utc")]
			public DateTime? ScheduledDateUtc { get; set; }
		}

		public class MobileReq
		{
			[JsonProperty("phone_number")]
			public string PhoneNumber { get; set; } = null!;
		}

		[JsonProperty("details")]
		public DetailsReq Details { get; set; } = null!;

		[JsonProperty("scheduling")]
		public SchedulingReq Scheduling { get; set; } = null!;

		[JsonProperty("mobiles")]
		public List<MobileReq> Mobiles { get; set; } = null!;
	}

	public class ActiveTrailRes
	{
		// SMS campaign id
		[JsonProperty("id")]
		public int CampaignId { get; set; }

		// SMS campaign name
		[JsonProperty("name")]
		public string Name { get; set; } = null!;

		// From name
		[JsonProperty("from_name")]
		public string FromName { get; set; } = null!;

		// Using a specific sending profile
		[JsonProperty("sms_sending_profile_id")]
		public int SmsSendingProfileId { get; set; }

		// The text of the SMS campaign
		[JsonProperty("content")]
		public string Content { get; set; } = null!;
	}


}