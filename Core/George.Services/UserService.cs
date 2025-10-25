using AutoMapper;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;
using UserStatus = George.Common.UserStatus;

namespace George.Services
{
	public class UserService : ServiceBase
	{
		//*********************  Data members/Constants  *********************//
		private readonly AuthHelper _authHelper;
		private readonly UserStorage _userStorage;
		private readonly FileStorageManager _fileStorage;


		//**************************    Construction    **************************//
		public UserService(ILogger<UserService> logger, IMapper mapper, CacheManager cache, AuthHelper authHelper, 
			/*AuthorizationManager authManager,*/ GeneralStorage generalStorage, UserStorage userStorage, FileStorageManager fileStorage) : base(logger, mapper, cache)
		{
			_authHelper = authHelper;
			_userStorage = userStorage;
			_fileStorage = fileStorage;
		}


		//*************************    Public Methods    *************************//

		public async Task<IApiResponse<ApiListResponse<UserRes>?>> GetUsersAsync(ApiListReq<UserFilter> request, CancellationToken cancelToken = default)
		{
			IApiResponse<ApiListResponse<UserRes>?> response = new ApiResponse<ApiListResponse<UserRes>?>(){ Data = new() };

			//// Verify that the user is authorized to access the item.
			//if (!await ValidatePermissionAsync(EntityType.User, 0, AuthAction.View, cancelToken))
			//	return CreateResponse(response, StatusCode.UnauthorizedData);

			// Get the data from the DB.
			DataListResult<User> res = await _userStorage.GetUsersAsync(request.Filter, request, cancelToken).ConfigureAwait(false);
			if (res.Items.HasValue())
			{
				// Convert to response.
				response.Data!.Items = res.Items.ConvertAll(a => _mapper.Map<UserRes>(a));
				foreach (var item in response.Data.Items)
				{
					//if (await CanPerformUserActionsAsync(item.Id, cancelToken))
					//{
					//	item.CanBlock = CanSetBlockStatus(true, item.StatusId);
					//	item.CanUnblock = CanSetBlockStatus(false, item.StatusId);
					//	item.CanEdit = true;
					//	item.CanDelete = (item.HasOpenAlert == false);
					//}
					//else
					//{
					//	item.CanBlock = false;
					//	item.CanUnblock = false;
					//	item.CanEdit = false;
					//	item.CanDelete = false;
					//}

					//// Im any case, only system and control center users can be deleted in this API.
					//if(item.SystemRoleId == null && item.ControlCenterRoleId == null)
					//{
					//	item.CanEdit = false;
					//	item.CanDelete = false;
					//}
				}
			}

			// Set the paging.
			response.Data.Skip = request.Skip;
			response.Data.Limit = request.Take;
			response.Data.Total = res.Total;

			return response;
		}

		public async Task<IApiResponse<UserRes>> GetUserAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<UserRes> response = new ApiResponse<UserRes>();

			// Get the data from the DB.
			User? model = await _userStorage.GetUserAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Set the response.
				response.Data = _mapper.Map<UserRes>(model);

				//if (await CanPerformUserActionsAsync(model.Id, cancelToken))
				//{
				//	response.Data.CanBlock = CanSetBlockStatus(true, (UserStatus)model.StatusId);
				//	response.Data.CanUnblock = CanSetBlockStatus(false, (UserStatus)model.StatusId);
				//	response.Data.CanDelete = (model.HasOpenAlert == false);
				//}
				//else
				//{
				//	response.Data.CanBlock = false;
				//	response.Data.CanUnblock = false;
				//	response.Data.CanDelete = false;
				//}

            }

			return response;
		}

		public async Task<IApiResponse<UserRes>> DeleteUserAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<UserRes> response = new ApiResponse<UserRes>();

			//// Verify that the user is authorized to access the item.
			//if (!await ValidatePermissionAsync(EntityType.User, 0, AuthAction.Edit, cancelToken))
			//	return CreateResponse(response, StatusCode.UnauthorizedData);

			// Check for dependencies.
			if (await _userStorage.UserHasDependenciesAsync(id))
				return CreateResponse(response, StatusCode.ItemHasDependencies, "User cannot be deleted since he has dependencies (part of an active alert).");


			// Delete from the DB.
			User? model = await _userStorage.DeleteUserAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
				response.Data = _mapper.Map<UserRes>(model);

			return response;
		}

		//public async Task<IApiResponse<UserRes>> BlockUserAsync(int id, BoolReq request, CancellationToken cancelToken = default)
		//{
		//	IApiResponse<UserRes> response = new ApiResponse<UserRes>();

		//	// Verify that the user is authorized to access the item.
		//	if (!await ValidatePermissionAsync(EntityType.User, 0, AuthAction.Edit, cancelToken))
		//		return CreateResponse(response, StatusCode.UnauthorizedData);

		//	// When the user that updates the account is system user, send notifications to the account admins.
		//	var currentUserPermissions = await GetPermissionAsync(AuthUser.Id, cancelToken);
		//	var effectedUserPermissions = await GetPermissionAsync(id, cancelToken);

		//	// Check for dependencies.
		//	if (await _userStorage.UserHasDependenciesAsync(id))
		//		return CreateResponse(response, StatusCode.ItemHasDependencies, "User cannot be deleted since he has dependencies (part of an active alert).");

		//	if(await CanSetBlockStatusAsync(id, request.Value, cancelToken) == false)
		//		return CreateResponse(response, StatusCode.UserBlockStateCannotBeChanged);

		//	// Block in the DB.
		//	User? model = await _userStorage.BlockUserAsync(id, request.Value, cancelToken).ConfigureAwait(false);
		//	if (model != null)
		//	{
		//		response.Data = _mapper.Map<UserRes>(model);
		//	}

		//	return response;
		//}

		public async Task<IApiResponse<ProfileRes>> GetProfileAsync(int id, CancellationToken cancelToken = default)
		{
			IApiResponse<ProfileRes> response = new ApiResponse<ProfileRes>();

			//// Verify that the user is authorized to access the item.
			//if (!await ValidatePermissionAsync(EntityType.User, 0, AuthAction.View, cancelToken))
			//	return CreateResponse(response, StatusCode.UnauthorizedData);

			// Get the data from the DB.
			User? model = await _userStorage.GetUserAsync(id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Set the response.
				response.Data = _mapper.Map<ProfileRes>(model);


				//// Set permissions.
				//response.Data.Permissions = await _authManager!.GetUserPermissionsAsync(_authUser.Id, _authUser.IsMaster, cancelToken);
			}

			return response;
		}

		public async Task<IApiResponse<bool>> IsEmailAvailableAsync(EmailReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<bool> response = new ApiResponse<bool>();

			// Get the data from the DB.
			response.Data = await _userStorage.IsEmailAvailableAsync(request.Email, cancelToken).ConfigureAwait(false);

			return response;
		}

		

		//*************************    Private/Protected Methods    *************************//

		public async Task<bool> CanSetBlockStatusAsync(int userId, bool shouldBlock, CancellationToken cancelToken = default)
		{
			UserStatus? statusId = await _userStorage.GetUserStatusAsync(userId, cancelToken);

			return CanSetBlockStatus(shouldBlock, statusId);
		}

		public bool CanSetBlockStatus(bool shouldBlock, UserStatus? statusId)
		{
			if (statusId == null)
				return false;
			else if (shouldBlock == true && statusId != UserStatus.Active)
				return false;
			else if (shouldBlock == false && statusId != UserStatus.Blocked)
				return false;

			return true;
		}

	}
}
