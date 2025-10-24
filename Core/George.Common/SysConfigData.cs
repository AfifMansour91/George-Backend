
namespace George.Common
{
	public class SysConfigData
	{
		//*********************  Data members/Constants  *********************//
		public const decimal APP_VERSION = 1.0m;
		public const decimal API_VERSION = 1.0m;
		public const int DEFAULT_TOKEN_EXPIRATION_IN_MIN = 15;
		public const int DEFAULT_OTP_EXPIRATION_IN_MIN = 10;
		public const int DELAY_WORKER_EXECUTION_IN_SEC = 1;
		public const int DEFAULT_MIN_ALERT_VIDEO_TIME_IN_SEC = 30;
		public const int DEFAULT_LOCATION_EXPIRATION_IN_MIN = 60*24;
		public const string PRODUCTION_ENVIRONMENT = "PROD";
		public const string DEFAULT_DATE_FORMAT = "dd.MM.yyyy HH:mm";


		//**************************    Construction    **************************//
		public SysConfigData()
		{
			CacheIntervalInSec = 10 * 60; // 10 minutes default.
			ApiVersion = API_VERSION;
			AppVersion = APP_VERSION;
			//StorageInternalBasePath = @"./FileStorage";
			//StorageExternalBasePath = "";
		}


		//*************************    Properties    *************************//
		public bool IsTestEnvironment {
			get {
				return EnvironmentName.HasValue() == false ||
						EnvironmentName.Equals(PRODUCTION_ENVIRONMENT, StringComparison.OrdinalIgnoreCase) == false;
			}
		}

		public List<SysConfig.ParameterValue> Configs { get; set; }
		
		public string DateFormat { get; set; } = DEFAULT_DATE_FORMAT;

		public double CacheIntervalInSec { get; set; } = 10 * 60; // 10 minutes default.
		public string EnvironmentName { get; set; }
		public int DefaultPageSize { get; set; }

		public int RefreshDataPageSize { get; set; } = 10;
		//public int RefreshDataFirstId { get; set; } = 1;
		public int RefreshDataWaitTimeInMillisec { get; set; } = 10;
		public int RefreshDataWaitTimeLongInMillisec { get; set; } = 1000 * 10;

		public string StorageInternalBasePath { get; set; }
		public string StorageExternalBasePath { get; set; }
		public string StorageLocalInternalBasePath { get; set; }
		public string StorageLocalExternalBasePath { get; set; }
		public string TempFolder { get; set; }
		public decimal ApiVersion { get; set; }
		public decimal AppVersion { get; set; }

		public string WebAppUrl { get; set; }
		public int LockoutExpirationInMin { get; set; } = DEFAULT_TOKEN_EXPIRATION_IN_MIN;
		public int MaxFailCountBeforeLockout { get; set; } = 3;

		public string? AWSBucket { get; set; }
		public string? AWSAccessKey { get; set; }
		public string? AWSKeySecret { get; set; }




		//*************************    Public Methods    *************************//
		public string? GetParameter(string parameterName, string? defaultValue = default)
		{
			var config = this.Configs.Where(a => a.Key.Equals(parameterName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
			if (config == null)
				return defaultValue;

			return config.Value;
		}

	}


}
