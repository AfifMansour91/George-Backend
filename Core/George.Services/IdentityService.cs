using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Common.Utils;
using George.Data;
using George.DB;
using Task = System.Threading.Tasks.Task;
using UserStatus = George.Common.UserStatus;
using George.Providers;

namespace George.Services
{
	public class IdentityService : ServiceBase
	{
		//*********************  Data members/Constants  *********************//
		private readonly AuthHelper _authHelper;
		private readonly UserStorage _userStorage;
		private readonly IConfiguration _configuration;
		private readonly FileStorageManager _fileStorage;
		private readonly SmsProvider _smsProvider;


		//**************************    Construction    **************************//
		public IdentityService(ILogger<IdentityService> logger, IMapper mapper, CacheManager cache, IConfiguration configuration,
				UserStorage userStorage, AuthHelper authHelper, /*AuthorizationManager authManager, */
				FileStorageManager fileStorage, SmsProvider smsProvider) : base(logger, mapper, cache)
		{
			_authHelper = authHelper;
			_userStorage = userStorage;
			_configuration = configuration;
			_fileStorage = fileStorage;
			_smsProvider = smsProvider;
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
				//user.LockoutFailCount++;

				// Update the lockout fail count.
				await UpdateUserLockoutFailCountAsync(user.Id, user.LockoutFailCount, cancelToken);

				return CreateResponse(response, StatusCode.InvalidOtp);
			}

			// Create the token.
			response.Data = _authHelper.CreateAuthenticationToken(user.Id, (UserRole)user.RoleId);

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
            response.Data = _authHelper.CreateAuthenticationToken(user.Id, (UserRole)user.RoleId);

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

        /// <param name="requestOriginHost">Host of the login page that asked for the code - used for the SMS domain-bound autofill line.</param>
        public async Task<IApiResponse<bool>> SendLoginOtpAsync(SendLoginOtpReq request, CancellationToken cancelToken = default, string? requestOriginHost = null)
        {
            IApiResponse<bool> response = new ApiResponse<bool>();

            // Normalize the phone number: strip all non-digit characters so that
            // formats like 0544-123456, 054-4123456, 054-412-3456 all work.
            request.Phone = new string(request.Phone.Where(char.IsDigit).ToArray());

            // Get the data from the DB.
            User? model = await _userStorage.GetUserByPhoneAsync(request.Phone, cancelToken).ConfigureAwait(false);
            if (model == null)
                return CreateResponse(response, StatusCode.InvalidRequest);

            // Check if the user is active/pending.
            if (model.StatusId != (int)UserStatus.Active && model.StatusId != (int)UserStatus.Pending)
                return CreateResponse(response, StatusCode.BlockedUser);

            // Verify user lockout.
            if (model.LockoutExpiration.HasValue && DateTime.UtcNow <= model.LockoutExpiration)
                return CreateResponse(response, StatusCode.UserLockedOut);

            // Check if the lock out expiration time has passed.
            if (model.LockoutExpiration.HasValue && DateTime.UtcNow > model.LockoutExpiration)
                // Reset the lockout fail count.
                model.LockoutFailCount = 0;
            //else
            //    // Increment the lockout fail count.
            //    model.LockoutFailCount++;

            // Update the lockout fail count.
            await UpdateUserLockoutFailCountAsync(model.Id, model.LockoutFailCount, cancelToken);

            // Check again if the user is locked out. (After LockoutFailCount changed)
            if (model.LockoutFailCount > SysConfig.Data.MaxFailCountBeforeLockout)
                return CreateResponse(response, StatusCode.UserLockedOut);

            // Should override?
            string? overrideOtp = null;
            if (model.Id == SysConfig.Data.StaticUserId)
                overrideOtp = SysConfig.Data.StaticUserOtp;

            overrideOtp = _configuration["Auth:OverrideOtp"];
            if (overrideOtp.HasValue())
            {
                // Set the override otp directly in the user model
                model.Otp = overrideOtp;
				model.OtpExpiration = DateTime.UtcNow.AddMinutes(SysConfig.Data.LockoutExpirationInMin);
                await _userStorage.UpdateUserAsync(model, cancelToken).ConfigureAwait(false);
                response.Data = true;
            }
            else
            {
                // Set user otp.
                string? otp = await _userStorage.SetLoginUserOtpAsync(model.Id, cancelToken).ConfigureAwait(false);
                if (otp.HasValue() && model.Phone.HasValue())
                {
                    // Send the otp via SMS
                    // Using default language ID (1 for English, can be adjusted based on user preference)
                    int languageId = 1; // Default to English, can be retrieved from user model if available
                    bool smsSent = await _smsProvider.SendOtpMessageAsync(model.Phone, languageId, otp!, cancelToken, requestOriginHost).ConfigureAwait(false);
                    
                    if (smsSent)
                    {
                        response.Data = true;
                    }
                    else
                    {
                        _logger.LogError($"Failed to send OTP SMS to phone: {model.Phone}");
                        return CreateResponse(response, StatusCode.GeneralError, "Failed to send OTP SMS");
                    }
                }
                else
                {
                    _logger.LogError($"Failed to generate OTP or phone number is missing for user ID: {model.Id}");
                    return CreateResponse(response, StatusCode.GeneralError, "Failed to generate OTP");
                }
            }

            return response;
        }
        public async Task<IApiResponse<AuthRes>> LoginWithOtpAsync(LoginWithOtpReq request, CancellationToken cancelToken = default)
        {
            IApiResponse<AuthRes> response = new ApiResponse<AuthRes>();

            // Normalize the phone number: strip all non-digit characters so that
            // formats like 0544-123456, 054-4123456, 054-412-3456 all work.
            request.Phone = new string(request.Phone.Where(char.IsDigit).ToArray());

            // Get the data from the DB.
            User? user = await _userStorage.GetUserByPhoneAsync(request.Phone, cancelToken).ConfigureAwait(false);
            if (user == null)
                return CreateResponse(response, StatusCode.InvalidCredentials);

            // Check if the user's status can be changed from Pending to Active.
            if (user.StatusId == (int)UserStatus.Pending)
                user.StatusId = (int)UserStatus.Active;

            if (user.StatusId == (int)UserStatus.Blocked)
                return CreateResponse(response, StatusCode.BlockedUser);

            // Verify user lockout.
            if (user.LockoutExpiration.HasValue && DateTime.UtcNow <= user.LockoutExpiration)
                return CreateResponse(response, StatusCode.UserLockedOut);

			// Verify the otp.
			bool isValid = IsValidOtp(user.Otp, request.Otp, user.OtpExpiration);
			if (!isValid)
			{
				// Increment the lockout fail count.
				//user.LockoutFailCount++;

				// Update the lockout fail count.
				await UpdateUserLockoutFailCountAsync(user.Id, user.LockoutFailCount, cancelToken);

				return CreateResponse(response, StatusCode.InvalidOtp);
			}

			// Create the token.
			response.Data = _authHelper.CreateAuthenticationToken(user.Id, (UserRole)user.RoleId);

			// Set the user's status.
			response.Data.StatusId = (UserStatus)user.StatusId;

            // Update user's login.
            var res = await _userStorage.UpdateUserLoginAsync(user.Id, response.Data.RefreshToken!, response.Data.RefreshTokenExpiration,
                false, (UserStatus)user.StatusId, cancelToken).ConfigureAwait(false);

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

		public async Task<IApiResponse<bool>> ChangePasswordAsync(ChangePasswordReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<bool> response = new ApiResponse<bool>();

			// Get current user
			User? user = await _userStorage.GetUserAsync(_authUser.Id, cancelToken).ConfigureAwait(false);
			if (user == null)
				return CreateResponse(response, StatusCode.UserNotFound, "User not found.");

			// Verify current password
			bool isCurrentPasswordValid = false;
			if (user.Password != null && user.Password.Contains(':'))
			{
				// Password is hashed
				isCurrentPasswordValid = Cryptography.VerifyPasswordHash(request.CurrentPassword, user.Password);
			}
			else
			{
				// Plain text password (legacy)
				isCurrentPasswordValid = user.Password == request.CurrentPassword;
			}

			if (!isCurrentPasswordValid)
				return CreateResponse(response, StatusCode.InvalidCredentials, "Current password is incorrect.");

			// Hash new password
			string newPasswordHash = Cryptography.GeneratePasswordHash(request.NewPassword);

			// Update password
			user.Password = request.NewPassword; // newPasswordHash;
			await _userStorage.UpdateUserPasswordAsync(user.Id, newPasswordHash, cancelToken).ConfigureAwait(false);

			response.Data = true;
			return response;
		}

		/// <summary>Request password reset. Generates a token and stores it in cache. Always returns success to avoid revealing if email exists.</summary>
		/// <param name="requestOrigin">Origin of the login page (https://host) - the reset link is built on the same host.</param>
		public async Task<IApiResponse<RequestPasswordResetRes>> RequestPasswordResetAsync(RequestPasswordResetReq request, CancellationToken cancelToken = default, string? requestOrigin = null)
		{
			IApiResponse<RequestPasswordResetRes> response = new ApiResponse<RequestPasswordResetRes>();
			response.Data = new RequestPasswordResetRes();

			User? user = await _userStorage.GetUserByEmailAsync(request.Email, cancelToken).ConfigureAwait(false);
			if (user == null)
			{
				// Always return success - don't reveal if email exists
				return response;
			}

			if (user.StatusId == (int)UserStatus.Blocked)
			{
				// Don't reveal blocked status either
				return response;
			}

			string token = Guid.NewGuid().ToString("N");
			const int expirationMinutes = 60;
			string cacheKey = "PasswordReset_" + token;
			_cache.SetCacheItem(cacheKey, user.Id, expirationMinutes * 60);

			// SECURITY (2026-10-26): the token is NEVER returned to the caller. The earlier "dev mode" switch
			// (Identity:ReturnResetTokenInDev) was on in production, so anyone who typed an account's email was sent
			// straight to the "set new password" form. The only way to get the token is the link we send to the
			// account's phone; the one development exception below requires BOTH the flag and a Development host.
			var isDevelopmentHost = string.Equals(
				Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
			bool returnTokenInDev = isDevelopmentHost
				&& string.Equals(_configuration["Identity:ReturnResetTokenInDev"], "true", StringComparison.OrdinalIgnoreCase);

			var resetUrl = BuildPasswordResetUrl(token, requestOrigin);
			if (!string.IsNullOrWhiteSpace(user.Phone) && !string.IsNullOrWhiteSpace(resetUrl))
			{
				try
				{
					var text = $"קישור לאיפוס הסיסמה שלך ב-Giorgio (תקף לשעה):{Environment.NewLine}{resetUrl}";
					var sent = await _smsProvider.SendTextAsync(user.Phone, text, cancelToken).ConfigureAwait(false);
					_logger.LogInformation("Password reset link SMS for user {UserId}: sent={Sent}", user.Id, sent);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Password reset link SMS failed for user {UserId}", user.Id);
				}
			}
			else
			{
				_logger.LogWarning("Password reset requested for user {UserId} but no phone / public URL to deliver the link to", user.Id);
			}

			if (returnTokenInDev)
			{
				response.Data.ResetToken = token;
				_logger.LogInformation("Password reset token returned in the response for user {UserId} (Development host only)", user.Id);
			}

			return response;
		}

		/// <summary>/reset-password?token=... on the host the request came from (fallback: App:PublicBaseUrl).</summary>
		private string? BuildPasswordResetUrl(string token, string? requestOrigin)
		{
			string? baseUrl = null;
			if (!string.IsNullOrWhiteSpace(requestOrigin)
				&& Uri.TryCreate(requestOrigin.Trim(), UriKind.Absolute, out var originUri)
				&& (originUri.Scheme == Uri.UriSchemeHttps || originUri.Scheme == Uri.UriSchemeHttp)
				&& !string.Equals(originUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
				baseUrl = originUri.GetLeftPart(UriPartial.Authority);
			baseUrl ??= (_configuration["App:PublicBaseUrl"] ?? _configuration["PublicAppBaseUrl"] ?? _configuration["Client:BaseUrl"])?.TrimEnd('/');
			return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl}/reset-password?token={token}";
		}

		/// <summary>Set new password using a valid reset token.</summary>
		public async Task<IApiResponse<bool>> ResetPasswordAsync(ResetPasswordReq request, CancellationToken cancelToken = default)
		{
			IApiResponse<bool> response = new ApiResponse<bool>();

			string cacheKey = "PasswordReset_" + request.Token;
			int userId = _cache.GetFromCache<int>(cacheKey);
			if (userId <= 0)
			{
				return CreateResponse(response, StatusCode.InvalidToken, "Invalid or expired reset token.");
			}

			User? user = await _userStorage.GetUserAsync(userId, cancelToken).ConfigureAwait(false);
			if (user == null)
			{
				_cache.ClearCache(cacheKey);
				return CreateResponse(response, StatusCode.UserNotFound, "User not found.");
			}

			string newPasswordHash = Cryptography.GeneratePasswordHash(request.NewPassword);
			user.Password = request.NewPassword;
			await _userStorage.UpdateUserPasswordAsync(user.Id, newPasswordHash, cancelToken).ConfigureAwait(false);

			// Invalidate the token
			_cache.ClearCache(cacheKey);

			response.Data = true;
			return response;
		}



		//*************************    Private/Protected Methods    *************************//
		private async Task UpdateUserLockoutFailCountAsync(int userId, int lockoutFailCount, CancellationToken cancelToken = default)
		{
			DateTime? lockoutExpiration = null;

			//// Check if the user should be locked.
			//if (lockoutFailCount > SysConfig.Data.MaxFailCountBeforeLockout)
			//	lockoutExpiration = DateTime.UtcNow.AddMinutes(SysConfig.Data.LockoutExpirationInMin);				

			// Update user lockout fail count.
			await _userStorage.UpdateUserLockoutFailCountAsync(userId, lockoutFailCount, lockoutExpiration, cancelToken);
		}

	}
}
