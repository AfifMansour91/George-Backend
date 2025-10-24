using AutoMapper;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;

namespace George.Services
{
	public class RegistryUnitService : ServiceBase
	{
		//*********************  Data members/Constants  *********************//
		private readonly FileStorageManager _fileStorage;
		private readonly RegistryUnitStorage _registryUnitStorage;


		//**************************    Construction    **************************//
		public RegistryUnitService(ILogger<RegistryUnitService> logger, IMapper mapper, CacheManager cache, 
			FileStorageManager fileStorage, RegistryUnitStorage registryUnitStorage) : base(logger, mapper, cache)
		{
			_fileStorage = fileStorage;
			_registryUnitStorage = registryUnitStorage;
		}


		//*************************    Public Methods    *************************//
		public async Task<IApiResponse<ApiListResponse<RegistryUnitRes>>> GetRegistryUnitsAsync(ApiListReq<RegistryUnitFilter> request, CancellationToken cancelToken)
		{
			IApiResponse<ApiListResponse<RegistryUnitRes>> response = new ApiResponse<ApiListResponse<RegistryUnitRes>>();
			response.Data = new();

			// Get the data from the DB.
			DataListResult<RegistryUnit> res = await _registryUnitStorage.GetRegistryUnitsAsync(request.Filter, request, cancelToken).ConfigureAwait(false);
			if (res != null && res.Items.HasValue())
			{
				// Convert to response.
				response.Data!.Items = res.Items.ConvertAll(a => _mapper.Map<RegistryUnitRes>(a));
			}

			// Set the paging.
			response.Data!.Skip = request.Skip;
			response.Data.Limit = request.Take;
			response.Data.Total = res!.Total;

			return response;
		}

		public async Task<IApiResponse<RegistryUnitRes?>> GetRegistryUnitAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<RegistryUnitRes?> response = new ApiResponse<RegistryUnitRes?>();

			// Get the data from the DB.
			RegistryUnit? model = await _registryUnitStorage.GetRegistryUnitAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<RegistryUnitRes>(model);
			}

			return response;
		}

		public async Task<IApiResponse<RegistryUnitRes>> CreateRegistryUnitAsync(CreateRegistryUnitReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<RegistryUnitRes> response = new ApiResponse<RegistryUnitRes>();

			//// Verify that the user is authorized to access the item.
			//if (await CanUser....(Id) == false)
			//	return CreateResponse(response, StatusCode.ItemNotAuthorized);

			// Convert to EF model
			RegistryUnit? model = _mapper.Map<RegistryUnit>(request);

			// Create the data in the DB.
			model = await _registryUnitStorage.CreateRegistryUnitAsync(model, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<RegistryUnitRes>(model);
			}

			return response;
		}

		public async Task<IApiResponse<RegistryUnitRes?>> UpdateRegistryUnitAsync(UpdateRegistryUnitReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<RegistryUnitRes> response = new ApiResponse<RegistryUnitRes>();

			//// Verify that the user is authorized to access the item.
			//if (await CanUser....(Id) == false)
			//	return CreateResponse(response, StatusCode.ItemNotAuthorized);

			// Convert to EF model
			RegistryUnit? model = _mapper.Map<RegistryUnit>(request);

			// Create the data in the DB.
			model = await _registryUnitStorage.UpdateRegistryUnitAsync(model, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<RegistryUnitRes>(model);
			}

			return response;
		}

		public async Task<IApiResponse<RegistryUnitRes?>> DeleteRegistryUnitAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<RegistryUnitRes?> response = new ApiResponse<RegistryUnitRes?>();

			//// Verify authorization.

			//// Check for dependencies.
			//if (await _taskStorage.TaskHasDependenciesAsync(id))
			//	return CreateResponse(response, StatusCode.ItemHasDependencies);

			// Delete from the DB.
			RegistryUnit? model = await _registryUnitStorage.DeleteRegistryUnitAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<RegistryUnitRes>(model);
			}

			return response;
		}




		public async Task<IApiResponse<ApiListResponse<MediumRes>>> GetMediaAsync(int registryUnitId, ApiListReq request, CancellationToken cancelToken)
		{
			IApiResponse<ApiListResponse<MediumRes>> response = new ApiResponse<ApiListResponse<MediumRes>>();
			response.Data = new();

			// Get the data from the DB.
			DataListResult<Medium> res = await _registryUnitStorage.GetMediaAsync(registryUnitId, request, cancelToken).ConfigureAwait(false);
			if (res != null && res.Items.HasValue())
			{
				// Convert to response.
				response.Data!.Items = res.Items.ConvertAll(a => _mapper.Map<MediumRes>(a));
			}

			// Set the paging.
			response.Data!.Skip = request.Skip;
			response.Data.Limit = request.Take;
			response.Data.Total = res!.Total;

			return response;
		}

		public async Task<IApiResponse<MediumRes?>> GetMediumAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<MediumRes?> response = new ApiResponse<MediumRes?>();

			// Get the data from the DB.
			Medium? model = await _registryUnitStorage.GetMediumAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<MediumRes>(model);
			}

			return response;
		}


		public async Task<IApiResponse<MediumRes?>> CreateMediumAsync(CreateMediumReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<MediumRes?> response = new ApiResponse<MediumRes?>();

			//// Verify that the user is authorized to access the item.
			//if (await CanUser....(Id) == false)
			//	return CreateResponse(response, StatusCode.ItemNotAuthorized);

			// Convert to EF model
			Medium? model = _mapper.Map<Medium>(request);


			// Upload file.
			if(request.File != null)
			{
				string path = FileHelper.GetRegistryUnitFolderPath(request.RegistryUnitId);
				var res = await _fileStorage.UploadFileAsync(request.File, path, cancelToken);
				if (res == null)
					return CreateResponse(response, StatusCode.FailedToLoadFile);

				model.FileUrl = res.FilePath;
			}

			// Create the data in the DB.
			model = await _registryUnitStorage.CreateMediumAsync(model, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<MediumRes>(model);
			}

			return response;
		}

		public async Task<IApiResponse<MediumRes?>> UpdateMediumAsync(UpdateMediumReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<MediumRes?> response = new ApiResponse<MediumRes?>();

			//// Verify that the user is authorized to access the item.
			//if (await CanUser....(Id) == false)
			//	return CreateResponse(response, StatusCode.ItemNotAuthorized);

			// Convert to EF model
			Medium? model = _mapper.Map<Medium>(request);

			// Upload file.
			if(request.File != null)
			{
				var medium = await _registryUnitStorage.GetMediumAsync(request.Id, cancelToken);
				if (medium == null)
					return CreateResponse(response, StatusCode.ItemNotFound);

				string path = FileHelper.GetRegistryUnitFolderPath(medium.RegistryUnitId);
				var res = await _fileStorage.UploadFileAsync(request.File, path, cancelToken);
				if (res == null)
					return CreateResponse(response, StatusCode.FailedToLoadFile);

				model.FileUrl = res.FilePath;
			}

			// Create the data in the DB.
			model = await _registryUnitStorage.UpdateMediumAsync(model, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<MediumRes>(model);
			}

			return response;
		}

		public async Task<IApiResponse<MediumRes?>> DeleteMediumAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<MediumRes?> response = new ApiResponse<MediumRes?>();

			//// Verify authorization.

			//// Check for dependencies.
			//if (await _taskStorage.TaskHasDependenciesAsync(id))
			//	return CreateResponse(response, StatusCode.ItemHasDependencies);

			// Delete from the DB.
			Medium? model = await _registryUnitStorage.DeleteMediumAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Convert to response.
				response.Data = _mapper.Map<MediumRes>(model);
			}

			return response;
		}



		//*************************    Private/Protected Methods    *************************//


	}
}
