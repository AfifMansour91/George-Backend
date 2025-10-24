using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace George.Common
{
	public class HttpHelper
	{
		public class HttpHelperResult<T>
		{
			public bool IsSuccessful { get { return (HttpResponse != null) ? HttpResponse.IsSuccessStatusCode : false; } }

			public HttpResponseMessage? HttpResponse { get; set; }
			public string? HttpContent { get; set; }
			public T? Data { get; set; }
		}

		//*********************  Data members/Constants  *********************//
		//private HttpClientHandler _handler;
		protected HttpClient _httpClient;
		protected JsonSerializerSettings _settings;
		protected bool _disposed;

		//*************************    Construction    *************************//
		public HttpHelper(HttpClient httpClient)
		{
			_httpClient = httpClient;
			_settings = CreateJsonSettings();
		}

		//*************************    Properties    *************************//
		public string? BaseAddress {
			get => _httpClient?.BaseAddress?.AbsolutePath;

			set {
				if (_httpClient == null)
				{
					throw new Exception("HttpClient is null.");
				}

				if (value != null)
					_httpClient.BaseAddress = new Uri(value);
			}
		}

		public JsonSerializerSettings JsonSerializerSettings { get => this._settings; set => this._settings = value; }

		//*************************    Public Methods    *************************//
		public void SetHttpHeaderKey(string fieldName, string fieldValue)
		{
			// Remove the field if exists.
			_httpClient.DefaultRequestHeaders.Remove(fieldName);

			// Add the new one.
			_httpClient.DefaultRequestHeaders.Add(fieldName, fieldValue);
		}

		public void SetBasicHttpAuthentication(string username, string password)
		{
			byte[] byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
		}

		public void ResetHttpClient(HttpClientHandler handler)
		{
			if (_httpClient != null)
				_httpClient.Dispose();

			_httpClient = new HttpClient(handler);
		}

		public async Task<HttpHelperResult<TResponse>> HttpGetAsync<TResponse>(string url, CancellationToken cancelToken = default) where TResponse : class //new()
		{
			var response = new HttpHelperResult<TResponse> {
				HttpResponse = await _httpClient.GetAsync(url, cancelToken).ConfigureAwait(false)
			};

			if (!response.HttpResponse.IsSuccessStatusCode)
			{
				return response;
			}

			response.HttpContent = await response.HttpResponse.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
			response.Data = JsonConvert.DeserializeObject<TResponse>(response.HttpContent, _settings);

			return response;
		}

		public async Task<HttpHelperResult<string>> HttpGetAsync(string url, CancellationToken cancelToken = default)
		{
			var response = new HttpHelperResult<string> {
				HttpResponse = await _httpClient.GetAsync(url, cancelToken).ConfigureAwait(false)
			};

			if (response.HttpResponse.IsSuccessStatusCode)
			{
				response.HttpContent = response.Data = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			return response;
		}

		public async Task<HttpHelperResult<string>> HttpGetAsync(string url, Dictionary<string, string> parameters, CancellationToken cancelToken = default)
		{
			var content = new FormUrlEncodedContent(parameters);
			//var response1 = await _httpClient.GetAsync($"{url}?{await content.ReadAsStringAsync()}", cancelToken);
			//var result = await response1.Content.ReadAsStringAsync();

			var response = new HttpHelperResult<string> {
				//HttpResponse = await _httpClient.GetAsync(url, cancelToken).ConfigureAwait(false)
				HttpResponse = await _httpClient.GetAsync($"{url}?{await content.ReadAsStringAsync()}", cancelToken)
			};

			if (response.HttpResponse.IsSuccessStatusCode)
			{
				response.HttpContent = response.Data = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			return response;
		}

		public async Task<HttpHelperResult<TResponse>> HttpPostAsync<TRequest, TResponse>(TRequest request, string url, CancellationToken cancelToken = default) where TResponse : class
		{
			var response = new HttpHelperResult<TResponse>();

			// This is an alternative to PostAsJsonAsync() since it doesn't set the content length in the HTTP header.
			var json = JsonConvert.SerializeObject(request, _settings);
			var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
			httpContent.Headers.ContentLength = json.Length;

			response.HttpResponse = await _httpClient.PostAsync(url, httpContent, cancelToken).ConfigureAwait(false);
			response.HttpContent = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (response.HttpResponse.IsSuccessStatusCode)
			{
				//response.HttpContent = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				response.Data = JsonConvert.DeserializeObject<TResponse>(response.HttpContent, _settings);
			}

			return response;
		}

		public async Task<HttpHelperResult<string>> HttpPostAsync<TRequest>(TRequest request, string url, CancellationToken cancelToken = default)
		{
			var response = new HttpHelperResult<string>();

			// This is an alternative to PostAsJsonAsync() since it doesn't set the content length in the HTTP header.
			var json = JsonConvert.SerializeObject(request, _settings);
			var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
			httpContent.Headers.ContentLength = json.Length;

			response.HttpResponse = await _httpClient.PostAsync(url, httpContent, cancelToken).ConfigureAwait(false);
			response.HttpContent = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (response.HttpResponse.IsSuccessStatusCode)
			{
				response.HttpContent = response.Data = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			return response;
		}

		public async Task<HttpHelperResult<TResponse>> HttpPatchAsync<TRequest, TResponse>(TRequest request, string url, CancellationToken cancelToken = default) where TResponse : class
		{
			var response = new HttpHelperResult<TResponse>();

			// This is an alternative to PostAsJsonAsync() since it doesn't set the content length in the HTTP header.
			var json = JsonConvert.SerializeObject(request, _settings);
			var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
			httpContent.Headers.ContentLength = json.Length;

			response.HttpResponse = await _httpClient.PatchAsync(url, httpContent, cancelToken).ConfigureAwait(false);
			if (response.HttpResponse.IsSuccessStatusCode)
			{
				response.HttpContent = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				response.Data = JsonConvert.DeserializeObject<TResponse>(response.HttpContent, _settings);
			}

			return response;
		}

		public async Task<HttpHelperResult<string>> HttpPutAsync<TRequest>(TRequest request, string url, CancellationToken cancelToken = default)
		{
			// This is an alternative to PostAsJsonAsync() since it doesn't set the content length in the HTTP header.
			var json = JsonConvert.SerializeObject(request, _settings);
			var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
			httpContent.Headers.ContentLength = json.Length;

			var response = new HttpHelperResult<string> { HttpResponse = await _httpClient.PutAsync(url, httpContent, cancelToken).ConfigureAwait(false) };
			if (response.HttpResponse.IsSuccessStatusCode)
			{
				response.HttpContent = response.Data = await response.HttpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			return response;
		}

		//*************************    Private Methods    *************************//
		private JsonSerializerSettings CreateJsonSettings()
		{
			return new JsonSerializerSettings {
				Error = HandleDeserializationError,
				MissingMemberHandling = MissingMemberHandling.Ignore,
				StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore
			};
		}

		private void HandleDeserializationError(object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs errorArgs)
		{
			var currentError = errorArgs.ErrorContext.Error.Message;
			errorArgs.ErrorContext.Handled = true;
		}
	}
}
