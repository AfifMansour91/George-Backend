using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Providers.ActiveTrail;
using System;
using System.Text;
using System.Text.Json;
using static George.Common.HttpHelper;

namespace George.Providers.Sms019
{
    public class Sms019Provider
    {
        //***********************  Data members/Constants  ***********************//
        private readonly ILogger<Sms019Provider> _logger;
        private static string _apiUrl = string.Empty;
        private static string _apiToken = string.Empty;
        private static string _sourcePhone = string.Empty;
        private static string _username = string.Empty;
        protected readonly HttpHelper _httpHelper;


        //**************************    Construction    **************************//
        public Sms019Provider(ILogger<Sms019Provider> logger, HttpHelper httpHelper)
        {
            _logger = logger;
            _httpHelper = httpHelper;
        }


        //*************************    Properties    *************************//
        public static bool IsInitialized { get; private set; }


		//*************************    Public Methods    *************************//

		public static void Init(string apiBaseUrl, string authToken, string username, string sourcePhone)
		{
            _apiUrl = apiBaseUrl;
            _apiToken = authToken;
            _username = username;
            _sourcePhone = sourcePhone;

            if (_apiUrl.HasValue() && _apiToken.HasValue() && _username.HasValue() && _sourcePhone.HasValue())
                IsInitialized = true;
            else
                IsInitialized = false;
        }

		public async Task<bool> SendSmsAsync(string message, List<string> phoneNumbers, CancellationToken cancelToken = default)
        {
            bool response = false;

            var smsRequest = new SmsRequest
            {
                Sms = new Sms
                {
                    User = new User { Username = _username },
                    Source = _sourcePhone,
                    Destinations = new Destinations
                    {
                        Phone = phoneNumbers // Directly assign the list of strings
                    },
                    Message = message
                }
            };

            var httpRes = await ExecuteAsync(smsRequest, cancelToken);
            if (!httpRes.IsSuccessful || httpRes.Data == null || (httpRes.Data != null && httpRes.Data.Status != "0"))
                _logger.LogError($"SMS send failed.  - HTTP response: {httpRes.HttpResponse}, HTTP content: {httpRes.HttpContent}");
            else
                response = true;

            return response;
        }

        //public async Task<string> VerifyPhoneAsync(List<string> phoneNumbers, CancellationToken cancelToken = default)
        //{
        //    var verifyPhoneRequest = new
        //    {
        //        verify_phone = new
        //        {
        //            user = new { username = _username },
        //            phone = phoneNumbers
        //        }
        //    };

        //    try
        //    {
        //        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        //        var jsonContent = JsonSerializer.Serialize(verifyPhoneRequest, jsonOptions);

        //        _logger.LogInformation("Sending phone verification JSON: {JsonContent}", jsonContent);
        //        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        //        // Add Authorization header with Bearer token
        //        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);
                

        //        var response = await _httpClient.PostAsync(_apiUrl, content, cancelToken);

        //        if (response.IsSuccessStatusCode)
        //        {
        //            return await response.Content.ReadAsStringAsync();
        //        }

        //        _logger.LogError("Phone verification failed with status code: {StatusCode}", response.StatusCode);
        //        return "Failed";
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Exception occurred during phone verification.");
        //        return "Exception";
        //    }
        //}

        public SmsUserResponse? ParseUserResponse(IFormCollection response)
		{
			VerifyInit();

            // Get all form parameters
			var responseParams = response.ToDictionary(
				f => f.Key,
				f => f.Value.ToString()
			);

            string? value;
            SmsUserResponse res = new();
            if (responseParams.TryGetValue("message", out value))
                res.Value = value;
            if (responseParams.TryGetValue("phone", out value))
                res.Phone = value;
            if (responseParams.TryGetValue("date", out value))
                res.Date = value;

            if(res.Value.HasValue() && res.Phone.HasValue())
                return res;

            return null;
		}

        //*************************    Private Methods    ************************//
        private void VerifyInit()
		{
			if(!IsInitialized) 
				throw new GeorgeNotInitializedException("SMS provider is not initialized");
		}

        private async Task<HttpHelperResult<SmsResponse>?> ExecuteAsync(SmsRequest smsRequest, CancellationToken cancelToken)
        {
            try
            {
                // Add Authorization header with Bearer token
                _httpHelper.SetHttpHeaderKey("Authorization", "Bearer " + _apiToken);

                // Sends the request to the service's API.
                return await _httpHelper.HttpPostAsync<SmsRequest, SmsResponse>(smsRequest, _apiUrl, cancelToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending SMS.");
                throw;
            }
        }
    }
}
