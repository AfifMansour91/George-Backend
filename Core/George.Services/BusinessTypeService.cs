using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Data.Models;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class BusinessTypeService : ServiceBase
    {
        //*********************  Data members/Constants  *********************//
        //private readonly FileStorageManager _fileStorage;
        private readonly BusinessTypeStorage _businessTypeStorage;


        //**************************    Construction    **************************//
        public BusinessTypeService(ILogger<BusinessTypeService> logger, IMapper mapper, CacheManager cache,
            BusinessTypeStorage businessTypeStorage) : base(logger, mapper, cache)
        {
            _businessTypeStorage = businessTypeStorage;
        }


        //*************************    Public Methods    *************************//
        public async Task<IApiResponse<ApiListResponse<BusinessTypeRes>>> GetBusinessTypesAsync(ApiListReq<BusinessTypeFilter> request, CancellationToken cancelToken)
        {
            IApiResponse<ApiListResponse<BusinessTypeRes>> response = new ApiResponse<ApiListResponse<BusinessTypeRes>>();
            response.Data = new();

            // Get the data from the DB.
            DataListResult<BusinessType> res = await _businessTypeStorage.GetBusinessTypesAsync(request.Filter, request, cancelToken).ConfigureAwait(false);
            if (res != null && res.Items.HasValue())
            {
                // Convert to response.
                response.Data!.Items = res.Items.ConvertAll(a => _mapper.Map<BusinessTypeRes>(a));
            }

            // Set the paging.
            response.Data!.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res!.Total;

            return response;
        }

        public async Task<IApiResponse<BusinessTypeRes?>> GetBusinessTypeAsync(int id, CancellationToken cancelToken = default)
        {
            IApiResponse<BusinessTypeRes?> response = new ApiResponse<BusinessTypeRes?>();

            // Get the data from the DB.
            BusinessType? model = await _businessTypeStorage.GetBusinessTypeAsync(id, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Convert to response.
                response.Data = _mapper.Map<BusinessTypeRes>(model);
            }

            return response;
        }

        public async Task<IApiResponse<BusinessTypeRes>> CreateBusinessTypeAsync(CreateBusinessTypeReq request, CancellationToken cancelToken = default)
        {
            IApiResponse<BusinessTypeRes> response = new ApiResponse<BusinessTypeRes>();

            //// Verify that the user is authorized to access the item.
            //if (await CanUser....(Id) == false)
            //	return CreateResponse(response, StatusCode.ItemNotAuthorized);

            // Convert to EF model
            BusinessType? model = _mapper.Map<BusinessType>(request);

            // Create the data in the DB.
            model = await _businessTypeStorage.CreateBusinessTypeAsync(model, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Convert to response.
                response.Data = _mapper.Map<BusinessTypeRes>(model);
            }

            return response;
        }

        public async Task<IApiResponse<BusinessTypeRes?>> UpdateBusinessTypeAsync(UpdateBusinessTypeReq request, CancellationToken cancelToken = default)
        {
            IApiResponse<BusinessTypeRes> response = new ApiResponse<BusinessTypeRes>();

            //// Verify that the user is authorized to access the item.
            //if (await CanUser....(Id) == false)
            //	return CreateResponse(response, StatusCode.ItemNotAuthorized);

            // Convert to EF model
            BusinessType? model = _mapper.Map<BusinessType>(request);

            // Create the data in the DB.
            model = await _businessTypeStorage.UpdateBusinessTypeAsync(model, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Convert to response.
                response.Data = _mapper.Map<BusinessTypeRes>(model);
            }

            return response;
        }

        public async Task<IApiResponse<BusinessTypeRes?>> DeleteBusinessTypeAsync(int id, CancellationToken cancelToken = default)
        {
            IApiResponse<BusinessTypeRes?> response = new ApiResponse<BusinessTypeRes?>();

            //// Verify authorization.

            //// Check for dependencies.
            //if (await _taskStorage.TaskHasDependenciesAsync(id))
            //	return CreateResponse(response, StatusCode.ItemHasDependencies);

            // Delete from the DB.
            BusinessType? model = await _businessTypeStorage.DeleteBusinessTypeAsync(id, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Convert to response.
                response.Data = _mapper.Map<BusinessTypeRes>(model);
            }

            return response;
        }

        //*************************    Private/Protected Methods    *************************//


    }

}
