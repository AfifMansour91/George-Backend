using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;

namespace George.Services
{
	public class GeneralService : ServiceBase
	{
		//*********************  Data members/Constants  *********************//
		private readonly GeneralStorage _generalStorage;
		private readonly FileStorageManager _fileStorage;

		//**************************    Construction    **************************//
		public GeneralService(ILogger<GeneralService> logger, IMapper mapper, CacheManager cache,/* AuthorizationManager authManager,*/ 
			GeneralStorage generalStorage, FileStorageManager fileStorage) : base(logger, mapper, cache)
		{
			_generalStorage = generalStorage;
			_fileStorage = fileStorage;
		}


		//*************************    Properties    *************************//



		//*************************    Public Methods    *************************//

		public async Task<IApiResponse<ConfigurationRes>> GetConfigurationAsync(CancellationToken cancelToken = default)
		{
			IApiResponse<ConfigurationRes> response = new ApiResponse<ConfigurationRes>();

			// Try to get the data from the cache.
			response.Data = _cache.GetFromCache<ConfigurationRes>(CacheKey.Configuration);
			if (response.Data == null)
			{
				// Get the data from the DB.
				response.Data = await GetConfigurationInnerAsync(cancelToken).ConfigureAwait(false);

				// Save data to the cache.
				if (response.Data != null)
					_cache.AddToCache(CacheKey.Configuration, response.Data, SysConfig.Data.CacheIntervalInSec);
			}

			return response;
		}

		public async Task<IApiResponse<MetadataRes>> GetMetadataAsync(CancellationToken cancelToken = default)
		{
			IApiResponse<MetadataRes> response = new ApiResponse<MetadataRes>();
			response.Data = new MetadataRes();

			response.Data.OwnershipTypes = EnumToIdNamePair(typeof(OwnershipType));

			response.Data.RegistrationMethods = EnumToIdNamePair(typeof(RegistrationMethod));

			response.Data.Roles = EnumToIdNamePair(typeof(UserRole));

			response.Data.SortFields = EnumToIdNamePair(typeof(SortField));

			response.Data.SortOrders = EnumToIdNamePair(typeof(SortOrder));

			response.Data.StatusCodes = EnumToIdNamePair(typeof(StatusCode));

			response.Data.UserStatuses = EnumToIdNamePair(typeof(UserStatus));


			return response;
		}

		public async Task<IApiResponse<UploadRes>> UploadFileAsync(IFormFile file, CancellationToken cancelToken = default)
		{
			IApiResponse<UploadRes> response = new ApiResponse<UploadRes>();

			// TODO: encrypt the result as key.
			string path = FileHelper.GetTempFolderPath();
			var res = await _fileStorage.UploadFileAsync(file, path, cancelToken);
			if (res != null)
				response.Data = _mapper.Map<UploadRes>(res);

			return response;
		}

		public async Task<IApiResponse<List<UploadRes>>> UploadFilesAsync(FileListReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<List<UploadRes>> response = new ApiResponse<List<UploadRes>>();
			response.Data = new();

			// TODO: encrypt the result as key.
			string path = FileHelper.GetTempFolderPath();
			foreach(var file in request.Files)
			{			
				var res = await _fileStorage.UploadFileAsync(file, path, cancelToken);
				if (res != null)
					response.Data.Add(_mapper.Map<UploadRes>(res));
			}

			return response;
		}


		//*************************    Private/Protected Methods    *************************//

		public async Task<ConfigurationRes> GetConfigurationInnerAsync(CancellationToken cancelToken = default)
		{
			ConfigurationRes res = new();

			// Set parameters.
			res.Parameters.Environment = SysConfig.Data.EnvironmentName;

			// Get the data from the DB.

			//var colors = await _generalStorage.GetColorsAsync(cancelToken).ConfigureAwait(false);
			//if (colors != null)
			//	res.Colors = colors.ConvertAll(a => _mapper.Map<ColorRes>(a));

			//res.Roles = RoleManager.GetRoles();
			res.Roles = EnumToIdNamePair(typeof(UserRole));

			return res;
		}
		
	}
}
