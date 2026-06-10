using System.Data;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;

namespace George.Services
{
	public class ConfigurationService : ServiceBase
	{
		//*********************  Data members/Constants  *********************//
		private readonly GeneralStorage _generalStorage;
		private readonly IConfiguration _configuration;


		//**************************    Construction    **************************//
		public ConfigurationService(ILogger<ConfigurationService> logger, IMapper mapper, CacheManager cache, 
									GeneralStorage generalStorage, IConfiguration configuration) : base(logger, mapper, cache)
		{
			_generalStorage = generalStorage;
			_configuration = configuration;
		}


		//*************************    Properties    *************************//



		//*************************    Public Methods    *************************//

		public void LoadConfiguration()
		{
			try
			{
				//// Create new configuration data.
				//SysConfigData data = new SysConfigData();

				//// Load mandatory fields from config file.
				//SysConfig.Data = data;

				// Load from the DB.
				bool dbLoaded = LoadConfigurationAsync();

				//// Load from DB ErrorMessage
				//List<ErrorMessage> errorMessages = _dbStorage.General.GetErrorMessages();
				//if (errorMessages.HasValue())
				//{
				//	var list = errorMessages.ConvertAll(a => _mapper.Map<DisplayMessageModel>(a));
				//	StatusHandler.Init(list);
				//}


				//SysConfig.Data = data;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Failed - ex: {ex.ToString()}");
			}
		}

		public bool LoadConfigurationAsync(CancellationToken cancelToken = default)
		{
			bool res = false;

			List<SystemConfiguration> configs = _generalStorage.GetSystemConfiguration();
			if (configs.HasValue())
				res = SetSystemConfiguration(configs);

			return res;
		}


		//*************************    Private/Protected Methods    *************************//
		private bool SetSystemConfiguration(List<SystemConfiguration> configs)
		{
			bool res = false;

			try
			{
				// Create new configuration data.
				SysConfigData data = new SysConfigData();

				// Set new configuration data values from database.
				SetConfigs(configs, data);

				// Override with appsettings.json values if present (appsettings takes precedence)
				LoadFromAppSettings(data);

				// Replace the existing configuration data with the new one.
				SysConfig.Data = data;

				CacheManager.SetCacheInterval(data.CacheIntervalInSec);

				res = true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Failed - ex: {ex.ToString()}");
			}

			return res;
		}

		private void LoadFromAppSettings(SysConfigData data)
		{
			// Load file storage settings from appsettings.json
			var fileStorageSection = _configuration.GetSection("FileStorage");
			
			// UseLocalStorage flag - if set to true, forces local storage regardless of AWS config
			var useLocalStorage = fileStorageSection.GetValue<bool?>("UseLocalStorage");
			if (useLocalStorage.HasValue)
			{
				data.UseLocalStorage = useLocalStorage.Value;
			}

			// StorageLocalInternalBasePath - physical path on server
			var storageLocalInternal = fileStorageSection.GetValue<string>("StorageLocalInternalBasePath");
			if (!string.IsNullOrEmpty(storageLocalInternal))
			{
				data.StorageLocalInternalBasePath = storageLocalInternal.Trim();
			}

			// StorageLocalExternalBasePath - public URL for accessing files
			var storageLocalExternal = fileStorageSection.GetValue<string>("StorageLocalExternalBasePath");
			if (!string.IsNullOrEmpty(storageLocalExternal))
			{
				data.StorageLocalExternalBasePath = storageLocalExternal.Trim();
			}
		}


		private bool SetConfigs(List<SystemConfiguration> configs, SysConfigData data)
		{
			bool res = false;
			string? value = string.Empty;

			
			// Set all configuration parameters
			data.Configs = configs.ConvertAll(a => new SysConfig.ParameterValue() { Key = a.Key, Value = a.Value });

			// Create a dictionary of parameters.
			Dictionary<string, string?> dicParameters = configs.Select(t => new { t.Key, t.Value }).ToDictionary(t => t.Key, t => t.Value);

			// Set configuration data of specific parameters.
			dicParameters.TryGetValue(SysConfig.Parameter.EnvironmentName, out value);
			data.EnvironmentName = value.HasValue() ? value!.Trim() : string.Empty;

			dicParameters.TryGetValue(SysConfig.Parameter.DefaultPageSize, out value);
			data.DefaultPageSize = value!.ToInt(10);

			dicParameters.TryGetValue(SysConfig.Parameter.RefreshDataPageSize, out value);
			data.RefreshDataPageSize = value!.ToInt(10);

			dicParameters.TryGetValue(SysConfig.Parameter.RefreshDataWaitTimeInMillisec, out value);
			data.RefreshDataWaitTimeInMillisec = value!.ToInt(10);

			dicParameters.TryGetValue(SysConfig.Parameter.RefreshDataWaitTimeLongInMillisec, out value);
			data.RefreshDataWaitTimeLongInMillisec = value!.ToInt(1000);

			dicParameters.TryGetValue(SysConfig.Parameter.StorageExternalBasePath, out value);
			data.StorageExternalBasePath = value.HasValue() ? value!.Trim() : string.Empty;

			dicParameters.TryGetValue(SysConfig.Parameter.StorageInternalBasePath, out value);
			data.StorageInternalBasePath = value.HasValue() ? value!.Trim() : string.Empty;
			
			dicParameters.TryGetValue(SysConfig.Parameter.StorageLocalExternalBasePath, out value);
			data.StorageLocalExternalBasePath = value.HasValue() ? value!.Trim() : string.Empty;

			dicParameters.TryGetValue(SysConfig.Parameter.StorageLocalInternalBasePath, out value);
			data.StorageLocalInternalBasePath = value.HasValue() ? value!.Trim() : string.Empty;

			dicParameters.TryGetValue(SysConfig.Parameter.TempFolder, out value);
			data.TempFolder = value.HasValue() ? value!.Trim() : string.Empty;

			//dicParameters.TryGetValue(SysConfig.Parameter.WebAppUrl, out value);
			//data.WebAppUrl = value.HasValue() ? value!.Trim() : string.Empty;


			dicParameters.TryGetValue(SysConfig.Parameter.LockoutExpirationInMin, out value);
			data.DefaultPageSize = value!.ToInt(10);

			dicParameters.TryGetValue(SysConfig.Parameter.MaxFailCountBeforeLockout, out value);
			data.MaxFailCountBeforeLockout = value!.ToInt(2);


			dicParameters.TryGetValue(SysConfig.Parameter.AWSBucket, out value);
			data.AWSBucket = value.HasValue() ? value!.Trim() : null;

			dicParameters.TryGetValue(SysConfig.Parameter.AWSAccessKey, out value);
			data.AWSAccessKey = value.HasValue() ? value!.Trim() : null;

			dicParameters.TryGetValue(SysConfig.Parameter.AWSKeySecret, out value);
			data.AWSKeySecret = value.HasValue() ? value!.Trim() : null;

			dicParameters.TryGetValue(SysConfig.Parameter.OrdersRealtimeEnabled, out value);
			data.OrdersRealtimeEnabled = value.HasValue() &&
				(value!.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
				 value.Trim() == "1");

			res = true;
			return res;
		}

	}
}
