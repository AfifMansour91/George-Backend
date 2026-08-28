using George.Common;
using static George.Common.HttpHelper;
using static George.Providers.ActiveTrail.ActiveTrailSmsReq;

namespace George.Providers.ActiveTrail
{
	internal class ActiveTrailSmsProvider
	{
		//***********************  Data members/Constants  ***********************//
		private static string _apiBaseUrl = string.Empty;
		private static string _campaignUrl = string.Empty;
		private static string _authToken = string.Empty;
		private static string _displayName = string.Empty;
		protected readonly HttpHelper _httpHelper;


		//**************************    Construction    **************************//
		public ActiveTrailSmsProvider(HttpHelper httpHelper)
		{
			_httpHelper = httpHelper;
		}


		//*************************    Properties    *************************//
        public static bool IsInitialized { get; private set; }



        //*************************    Public Methods    *************************//

        public static void Init(string apiBaseUrl, string campaignUrl, string authToken, string? displayName = null)
		{
			_apiBaseUrl = apiBaseUrl;
			_campaignUrl = campaignUrl;
			_authToken = authToken;
			if(displayName != null)
				_displayName = displayName;

			if (_apiBaseUrl.HasValue() && _campaignUrl.HasValue() && _authToken.HasValue() && _displayName.HasValue())
				IsInitialized = true;
			else 
				IsInitialized = false;
		}
		
		public async Task<HttpHelperResult<string>?> SendSmsAsync(string phone, string campaignName, string text, SmsAccountConfig? accountConfig = null, CancellationToken cancelToken = default)
		{
			// A valid per-account config carries its own credentials, so the static (system) init is not required for it.
			bool useAccountConfig = accountConfig?.IsValid == true;
			if (!useAccountConfig)
				VerifyInit();

			string displayName = useAccountConfig ? accountConfig!.FromName : _displayName;
			string apiBaseUrl = useAccountConfig && accountConfig!.ApiBaseUrl.HasValue() ? accountConfig.ApiBaseUrl! : _apiBaseUrl;
			string authToken = useAccountConfig ? accountConfig!.ApiToken : _authToken;

			// Build the request.
			var body = new ActiveTrailSmsReq {
				Details = new DetailsReq {
					Content = text,
					Name = displayName,
					//CampaignName = campaignName,
					FromName = displayName,
					CanUnsubscribe = false,
				},
				Mobiles = new List<MobileReq>
				{
					  new MobileReq
					  {
						  PhoneNumber = phone
					  }
				},
				Scheduling = new SchedulingReq {
					SendNow = true
				}
			};

			// Send it.
			return await ExecuteAsync(apiBaseUrl, authToken, body);
		}


		//*************************    Private Methods    ************************//

		private void VerifyInit()
		{
			if(!IsInitialized)
				throw new GeorgeNotInitializedException("SMS provider is not initialized");
		}

		private async Task<HttpHelperResult<string>?> ExecuteAsync(string url, string authToken, ActiveTrailSmsReq? req = null, CancellationToken cancelToken = default)
		{
			// Set authentication.
			_httpHelper.SetHttpHeaderKey("Authorization", authToken);

			// Sens the request to active trail API.
			return await _httpHelper.HttpPostAsync<ActiveTrailSmsReq>(req, url, cancelToken);
		}
	}
}
