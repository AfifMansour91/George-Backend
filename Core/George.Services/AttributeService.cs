//using AutoMapper;
//using George.Common;
//using George.Common.Request;
//using George.Data;
//using George.Data.Models;
//using George.DB;
//using George.Services.Request;
//using George.Services.Response;
//using Microsoft.Extensions.Logging;

//namespace George.Services
//{
//    public class AttributeService : ServiceBase
//    {
//        //*********************  Data members/Constants  *********************//
//        //private readonly FileStorageManager _fileStorage;
//        private readonly AttributeStorage _attributeStorage;


//        //**************************    Construction    **************************//
//        public AttributeService(ILogger<AttributeService> logger, IMapper mapper, CacheManager cache,
//            AttributeStorage attributeStorage) : base(logger, mapper, cache)
//        {
//            _attributeStorage = attributeStorage;
//        }


//        //*************************    Public Methods    *************************//
//        public async Task<IApiResponse<ApiListResponse<AttributeRes>>> GetAttributesAsync(ApiListReq<AttributeFilter> request, CancellationToken cancelToken)
//        {
//            IApiResponse<ApiListResponse<AttributeRes>> response = new ApiResponse<ApiListResponse<AttributeRes>>();
//            response.Data = new();

//            // Get the data from the DB.
//            DataListResult<Attribute> res = await _attributeStorage.GetAttributesAsync(request.Filter, request, cancelToken).ConfigureAwait(false);
//            if (res != null && res.Items.HasValue())
//            {
//                // Convert to response.
//                response.Data!.Items = res.Items.ConvertAll(a => _mapper.Map<AttributeRes>(a));
//            }

//            // Set the paging.
//            response.Data!.Skip = request.Skip;
//            response.Data.Limit = request.Take;
//            response.Data.Total = res!.Total;

//            return response;
//        }

//        public async Task<IApiResponse<AttributeRes?>> GetAttributeAsync(int id, CancellationToken cancelToken = default)
//        {
//            IApiResponse<AttributeRes?> response = new ApiResponse<AttributeRes?>();

//            // Get the data from the DB.
//            Attribute? model = await _attributeStorage.GetAttributeAsync(id, cancelToken).ConfigureAwait(false);
//            if (model != null)
//            {
//                // Convert to response.
//                response.Data = _mapper.Map<AttributeRes>(model);
//            }

//            return response;
//        }

//        public async Task<IApiResponse<AttributeRes>> CreateAttributeAsync(CreateAttributeReq request, CancellationToken cancelToken = default)
//        {
//            IApiResponse<AttributeRes> response = new ApiResponse<AttributeRes>();

//            //// Verify that the user is authorized to access the item.
//            //if (await CanUser....(Id) == false)
//            //	return CreateResponse(response, StatusCode.ItemNotAuthorized);

//            // Convert to EF model
//            Attribute? model = _mapper.Map<Attribute>(request);

//            // Create the data in the DB.
//            model = await _attributeStorage.CreateAttributeAsync(model, cancelToken).ConfigureAwait(false);
//            if (model != null)
//            {
//                // Convert to response.
//                response.Data = _mapper.Map<AttributeRes>(model);
//            }

//            return response;
//        }

//        public async Task<IApiResponse<AttributeRes?>> UpdateAttributeAsync(UpdateAttributeReq request, CancellationToken cancelToken = default)
//        {
//            IApiResponse<AttributeRes> response = new ApiResponse<AttributeRes>();

//            //// Verify that the user is authorized to access the item.
//            //if (await CanUser....(Id) == false)
//            //	return CreateResponse(response, StatusCode.ItemNotAuthorized);

//            // Convert to EF model
//            Attribute? model = _mapper.Map<Attribute>(request);

//            // Create the data in the DB.
//            model = await _attributeStorage.UpdateAttributeAsync(model, cancelToken).ConfigureAwait(false);
//            if (model != null)
//            {
//                // Convert to response.
//                response.Data = _mapper.Map<AttributeRes>(model);
//            }

//            return response;
//        }

//        public async Task<IApiResponse<AttributeRes?>> DeleteAttributeAsync(int id, CancellationToken cancelToken = default)
//        {
//            IApiResponse<AttributeRes?> response = new ApiResponse<AttributeRes?>();

//            //// Verify authorization.

//            //// Check for dependencies.
//            //if (await _taskStorage.TaskHasDependenciesAsync(id))
//            //	return CreateResponse(response, StatusCode.ItemHasDependencies);

//            // Delete from the DB.
//            Attribute? model = await _attributeStorage.DeleteAttributeAsync(id, cancelToken).ConfigureAwait(false);
//            if (model != null)
//            {
//                // Convert to response.
//                response.Data = _mapper.Map<AttributeRes>(model);
//            }

//            return response;
//        }

//        //*************************    Private/Protected Methods    *************************//


//    }

//}
