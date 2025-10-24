using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;
using Task = System.Threading.Tasks.Task;

namespace George.Services
{
	public class IdentityService : ServiceBase
	{
		//*********************  Data members/Constants  *********************//
		private readonly AuthHelper _authHelper;
		private readonly UserStorage _userStorage;
		private readonly IConfiguration _configuration;
		private readonly FileStorageManager _fileStorage;


		//**************************    Construction    **************************//
		public IdentityService(ILogger<IdentityService> logger, IMapper mapper, CacheManager cache, IConfiguration configuration,
				UserStorage userStorage, AuthHelper authHelper, /*AuthorizationManager authManager, */
				FileStorageManager fileStorage) : base(logger, mapper, cache)
		{
			_authHelper = authHelper;
			_userStorage = userStorage;
			_configuration = configuration;
			_fileStorage = fileStorage;
		}


		//*************************    Properties    *************************//


		//*************************    Public Methods    *************************//
		public async Task<IApiResponse<AuthRes>> LoginAsync(LoginReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<AuthRes> response = new ApiResponse<AuthRes>();

			// Get the data from the DB.
			User? user = await _userStorage.GetUserByCredentialsAsync(request.Email, request.Password, cancelToken).ConfigureAwait(false);
			if (user == null)
				return CreateResponse(response, StatusCode.InvalidCredentials);

			if (user.IsEmailVerified == false)
				return CreateResponse(response, StatusCode.UserEmailNotVerified);

			// Verify the user status.
			if (user.StatusId != (int)UserStatus.Active && user.StatusId != (int)UserStatus.Pending)
				return CreateResponse(response, user.StatusId == (int)UserStatus.Blocked ? StatusCode.BlockedUser: StatusCode.InactiveUser);

			// Verify the otp.
			bool isValid = true;// IsValidPassword(user.Otp, request.Otp, user.OtpExpiration);
			if (!isValid)
			{
				// Increment the lockout fail count.
				user.LockoutFailCount++;

				// Update the lockout fail count.
				await UpdateUserLockoutFailCountAsync(user.Id, user.LockoutFailCount, cancelToken);

				return CreateResponse(response, StatusCode.InvalidOtp);
			}

			// Create the token.
			response.Data = _authHelper.CreateAuthenticationToken(user.Id, user.IsMaster);

			//// Set permissions.
			//response.Data.Permissions = await _authManager!.GetUserPermissionsAsync(user.Id, user.IsMaster, cancelToken);

			// Set the user's status.
			response.Data.StatusId = (UserStatus)user.StatusId;

			// Update user's login.
			var res = await _userStorage.UpdateUserLoginAsync(user.Id, response.Data.RefreshToken!, response.Data.RefreshTokenExpiration, 
									false, (UserStatus)user.StatusId, cancelToken).ConfigureAwait(false);
			if(res != null)
			{
			}

			return response;
		}

  		public async Task<IApiResponse<AuthRes>> RefreshLoginAsync(RefreshLoginReq request, CancellationToken cancelToken = default)
        {
            IApiResponse<AuthRes> response = new ApiResponse<AuthRes>();

            // Get the user ID from the token.
            int userId = _authHelper.GetUserIdFromExpiredToken(request.AccessToken);
            if (!userId.IsValidID())
                return CreateResponse(response, StatusCode.InvalidCredentials);

            // Get the user from the DB.
            User? user = await _userStorage.GetThinUserAsync(userId, cancelToken).ConfigureAwait(false);
            if (user == null)
                return CreateResponse(response, StatusCode.InvalidCredentials);

            // Check that the refresh token is correct.
            if (!user.RefreshToken!.Equals(request.RefreshToken, StringComparison.OrdinalIgnoreCase))
				return CreateResponse(response, StatusCode.InvalidToken);

            // Check that the refresh token has not expired.
            if (!user.RefreshTokenExpiration.HasValue || DateTime.UtcNow > user.RefreshTokenExpiration)
                return CreateResponse(response, StatusCode.InvalidToken);


            // Create a new token.
            response.Data = _authHelper.CreateAuthenticationToken(user.Id, user.IsMaster);

			//// Set permissions.
			//response.Data.Permissions = await _authManager!.GetUserPermissionsAsync(user.Id, user.IsMaster, cancelToken);

            // Update user's login.
            var res = await _userStorage.UpdateUserLoginAsync(user.Id, response.Data.RefreshToken!, response.Data.RefreshTokenExpiration, 
															false, null, cancelToken).ConfigureAwait(false);
			if(res == null)
				response.Data = null;

			return response;
        }

		public async Task<IApiResponse<bool>> LogoutAsync(CancellationToken cancelToken = default)
		{
			IApiResponse<bool> response = new ApiResponse<bool>();

			// Remove the refresh token.
			var res = await _userStorage.RemoveRefreshTokenAsync(_authUser.Id, cancelToken).ConfigureAwait(false);
			if (res != null)
			{
				// Set the response.
				response.Data = true;
			}

			return response;
		}



		public async Task<IApiResponse<ProfileRes>> GetProfileAsync(CancellationToken cancelToken = default)
		{
			IApiResponse<ProfileRes> response = new ApiResponse<ProfileRes>();

			// Get the data from the DB.
			User? model = await _userStorage.GetUserAsync(_authUser.Id, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				// Set the response.
				response.Data = _mapper.Map<ProfileRes>(model);


				// Set permissions.
				//response.Data.Permissions = await _authManager!.GetUserPermissionsAsync(_authUser.Id, _authUser.IsMaster, cancelToken);
			}

			return response;
		}

		public async Task<IApiResponse<ProfileRes>> UpdateProfileAsync(ProfileReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<ProfileRes> response = new ApiResponse<ProfileRes>();

			User? model = await _userStorage.GetUserAsync(_authUser.Id, cancelToken).ConfigureAwait(false);
			if(model == null)
				return CreateResponse(response, StatusCode.ItemNotFound);

			// Update fields.
			model.FirstName = request.FirstName;
			model.LastName = request.LastName ?? "";


			//// If needed - replace email replacement.
			//if(request.Email != null && !request.Email.EqualsCI(model.Email))
			//{
			//	model.EmailVerificationToken = null;
			//	model.EmailVerificationTokenExpiration = DateTime.UtcNow.AddMinutes(SysConfig.Data.OtpExpirationInMin);
			//}

			//// Check if the user's status can be changed from Pending to Active.
			//if (model.StatusId == (int)UserStatus.Pending)
			//	model.StatusId = (int)UserStatus.Active;

			// Get the data from the DB.
			model = await _userStorage.UpdateUserProfileAsync(model, cancelToken).ConfigureAwait(false);
			if (model != null)
			{
				response.Data = _mapper.Map<ProfileRes>(model);

				//// If needed - send the phone otp.
				//if (model.PhoneReplacement.HasValue())
				//{
				//	// Send the otp.
				//	await SendPhoneOtpAsync(new SendPhoneOtpReq { Phone = model.PhoneReplacement!, LanguageId = (int)model.LanguageId! }, cancelToken).ConfigureAwait(false);
				//}

				//// If needed - send the email otp.
				//if (model.EmailReplacement.HasValue())
				//{
				//	// Send the otp.
				//	await SendEmailOtpAsync(new SendEmailOtpReq { Email = model.EmailReplacement!, LanguageId = (int)model.LanguageId! }, cancelToken).ConfigureAwait(false);
				//}

			}

			return response;
		}



		//*************************    Private/Protected Methods    *************************//
		private async Task UpdateUserLockoutFailCountAsync(int userId, int lockoutFailCount, CancellationToken cancelToken = default)
		{
			DateTime? lockoutExpiration = null;

			// Check if the user should be locked.
			if (lockoutFailCount > SysConfig.Data.MaxFailCountBeforeLockout)
				lockoutExpiration = DateTime.UtcNow.AddMinutes(SysConfig.Data.LockoutExpirationInMin);				

			// Update user lockout fail count.
			await _userStorage.UpdateUserLockoutFailCountAsync(userId, lockoutFailCount, lockoutExpiration, cancelToken);
		}

	}
}
