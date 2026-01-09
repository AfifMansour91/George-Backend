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
		
		public async Task<HttpHelperResult<string>?> SendSmsAsync(string phone, string campaignName, string text, CancellationToken cancelToken = default)
		{
			VerifyInit();

			// Build the request.
			var body = new ActiveTrailSmsReq {
				Details = new DetailsReq {
					Content = text,
					CampaignName = campaignName,
					FromName = _displayName
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
			return await ExecuteAsync(_apiBaseUrl + _campaignUrl, body);
		}


		//*************************    Private Methods    ************************//
		
		private void VerifyInit()
		{
			if(!IsInitialized) 
				throw new GeorgeNotInitializedException("SMS provider is not initialized");
		}
		
		private async Task<HttpHelperResult<string>?> ExecuteAsync(string url, ActiveTrailSmsReq? req = null, CancellationToken cancelToken = default)
		{
			// Set authentication.
			_httpHelper.SetHttpHeaderKey("Authorization", _authToken);

			// Sens the request to active trail API.
			return await _httpHelper.HttpPostAsync<ActiveTrailSmsReq>(req, url, cancelToken);
		}
	}
}
