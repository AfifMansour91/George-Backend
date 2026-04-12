using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Common.Utils;
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
		private readonly UserPreferenceStorage _userPreferenceStorage;
		private readonly FileStorageManager _fileStorage;


		//**************************    Construction    **************************//
		public UserService(ILogger<UserService> logger, IMapper mapper, CacheManager cache, AuthHelper authHelper, 
			/*AuthorizationManager authManager,*/ GeneralStorage generalStorage, UserStorage userStorage, UserPreferenceStorage userPreferenceStorage, FileStorageManager fileStorage) : base(logger, mapper, cache)
		{
			_authHelper = authHelper;
			_userStorage = userStorage;
			_userPreferenceStorage = userPreferenceStorage;
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

		public async Task<IApiResponse<UserRes>> CreateUserAsync(CreateUserReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<UserRes> response = new ApiResponse<UserRes>();

			User? existingByEmail = await _userStorage.GetUserByEmailAsync(request.Email, cancelToken).ConfigureAwait(false);
			if (existingByEmail != null)
			{
				if (existingByEmail.IsDeleted)
					return CreateResponse(response, StatusCode.UserEmailAlreadyInUse, "The specified email is already in use by another user.");

				if (!request.AccountId.HasValue)
					return CreateResponse(response, StatusCode.InvalidRequest, "AccountId is required when attaching an existing user by email.");

				int targetAccountId = request.AccountId.Value;

				// Block moving a user from one account to another via "create" (use an explicit transfer/update instead).
				if (existingByEmail.AccountId.HasValue && existingByEmail.AccountId.Value != targetAccountId)
					return CreateResponse(response, StatusCode.UserEmailAlreadyInUse, "The specified email is already in use by another user.");

				// Same email, no account (e.g. after remove-from-account) or already on this account: upsert as update.
				var attachReq = new UpdateUserReq
				{
					Id = existingByEmail.Id,
					FirstName = request.FirstName,
					LastName = request.LastName,
					Email = request.Email,
					Phone = request.Phone,
					Password = request.Password,
					AccountId = targetAccountId,
					RoleId = request.RoleId,
					StatusId = request.StatusId,
					AvatarUrl = request.AvatarUrl,
					SiteIds = request.SiteIds,
					RemoveFromAccount = false,
				};

				return await UpdateUserAsync(attachReq, cancelToken).ConfigureAwait(false);
			}

			// Hash password if provided
			string? passwordHash = null;
			if (!string.IsNullOrWhiteSpace(request.Password))
			{
				passwordHash = Cryptography.GeneratePasswordHash(request.Password);
			}

			// Create user model
			var user = new User
			{
				FirstName = request.FirstName,
				LastName = request.LastName ?? "",
				Email = request.Email,
				Phone = request.Phone,
				Password = request.Password, //passwordHash,
                AccountId = request.AccountId,
				RoleId = request.RoleId ?? (int)UserRole.SiteAdmin,
				StatusId = request.StatusId ?? (int)UserStatus.Active,
				AvatarUrl = request.AvatarUrl,
				IsEmailVerified = true,
				LockoutFailCount = 0,
				IsDeleted = false,
				CreationUserId = _authUser?.Id,
			};

			// Create in DB (with site associations for site_admin)
			user = await _userStorage.CreateUserAsync(user, request.SiteIds, cancelToken).ConfigureAwait(false);

			if (user != null)
			{
				response.Data = _mapper.Map<UserRes>(user);
			}

			return response;
		}

		public async Task<IApiResponse<UserRes>> UpdateUserAsync(UpdateUserReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<UserRes> response = new ApiResponse<UserRes>();

			// Get existing user
			User? existingUser = await _userStorage.GetUserAsync(request.Id, cancelToken).ConfigureAwait(false);
			if (existingUser == null)
			{
				return CreateResponse(response, StatusCode.UserNotFound, "User not found.");
			}

			// Check if email is changed and already in use
			if (request.Email != null && request.Email != existingUser.Email)
			{
				bool isEmailAvailable = await _userStorage.IsEmailAvailableAsync(request.Email, cancelToken).ConfigureAwait(false);
				if (!isEmailAvailable)
				{
					return CreateResponse(response, StatusCode.UserEmailAlreadyInUse, "The specified email is already in use by another user.");
				}
			}

		bool removeFromAccount = request.RemoveFromAccount == true;

		// Update user model
		var user = new User
		{
			Id = request.Id,
			FirstName = request.FirstName ?? existingUser.FirstName,
			LastName = request.LastName ?? existingUser.LastName,
			Email = request.Email ?? existingUser.Email,
			Phone = request.Phone ?? existingUser.Phone,
			AccountId = removeFromAccount ? null : (request.AccountId ?? existingUser.AccountId),
			RoleId = request.RoleId ?? existingUser.RoleId,
			StatusId = request.StatusId ?? existingUser.StatusId,
			// Empty string from client means "clear avatar"; null/omit means keep existing
			AvatarUrl = request.AvatarUrl == null ? existingUser.AvatarUrl : (request.AvatarUrl.Length == 0 ? null : request.AvatarUrl),
			UpdateUserId = _authUser?.Id,
		};

		// Update password only if provided (not null or empty)
		if (!string.IsNullOrWhiteSpace(request.Password))
		{
			user.Password = request.Password; // Store as plain text (same as CreateUserAsync)
		}
		else
		{
			user.Password = existingUser.Password; // Keep existing password
		}

		// When detaching from account, always clear site links (same as empty list in UI)
		List<int>? siteIdsForStorage = removeFromAccount ? new List<int>() : request.SiteIds;

		// Update in DB (with site associations for site_admin)
		User? updatedUser = await _userStorage.UpdateUserAsync(user, siteIdsForStorage, cancelToken).ConfigureAwait(false);

			if (updatedUser != null)
			{
				response.Data = _mapper.Map<UserRes>(updatedUser);
			}

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

		/// <summary>
		/// Get current user's UI preferences (e.g. product list view/filters). Returns key-value map; keys like "myProducts_viewPrefs", "globalCatalog_viewPrefs".
		/// </summary>
		public async Task<IApiResponse<Dictionary<string, object?>>> GetPreferencesAsync(int userId, CancellationToken cancelToken = default)
		{
			IApiResponse<Dictionary<string, object?>> response = new ApiResponse<Dictionary<string, object?>> { Data = new Dictionary<string, object?>() };
			try
			{
				var json = await _userPreferenceStorage.GetPreferencesJsonAsync(userId, cancelToken);
				if (!string.IsNullOrWhiteSpace(json))
				{
					var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
					if (dict != null)
						response.Data = dict;
				}
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "User preferences JSON invalid for user {UserId}", userId);
			}
			return response;
		}

		/// <summary>
		/// Save current user's UI preferences. Merges with existing; pass the full key-value map (e.g. myProducts_viewPrefs, globalCatalog_viewPrefs).
		/// </summary>
		public async Task<IApiResponse<bool>> SavePreferencesAsync(int userId, Dictionary<string, object?> preferences, CancellationToken cancelToken = default)
		{
			IApiResponse<bool> response = new ApiResponse<bool> { Data = true };
			try
			{
				var existingJson = await _userPreferenceStorage.GetPreferencesJsonAsync(userId, cancelToken);
				var existing = new Dictionary<string, object?>();
				if (!string.IsNullOrWhiteSpace(existingJson))
				{
					try
					{
						var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(existingJson);
						if (parsed != null)
							existing = parsed;
					}
					catch { /* ignore */ }
				}
				foreach (var kv in preferences)
					existing[kv.Key] = kv.Value;
				var json = JsonSerializer.Serialize(existing);
				await _userPreferenceStorage.SavePreferencesJsonAsync(userId, json, cancelToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to save preferences for user {UserId}", userId);
				return CreateResponse(response, StatusCode.InvalidRequest, ex.Message);
			}
			return response;
		}

	}
}
