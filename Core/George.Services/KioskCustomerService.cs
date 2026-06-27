using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;
using George.Providers;
using George.Providers.Twilio;
using UserStatus = George.Common.UserStatus;
using UserRole = George.Common.UserRole;

namespace George.Services
{
    public class KioskCustomerService : ServiceBase
    {
        private readonly UserStorage _userStorage;
        private readonly ClientStorage _clientStorage;
        private readonly SiteStorage _siteStorage;
        private readonly AuthHelper _authHelper;
        private readonly SmsProvider _smsProvider;
        private readonly TwilioVoiceOtpProvider _twilioVoiceOtpProvider;
        private readonly IConfiguration _configuration;
        private readonly CustomerStorage _customerStorage;
        private readonly IIntegrationLogQueue _integrationLogQueue;

        public KioskCustomerService(
            ILogger<KioskCustomerService> logger,
            IMapper mapper,
            CacheManager cache,
            UserStorage userStorage,
            ClientStorage clientStorage,
            SiteStorage siteStorage,
            AuthHelper authHelper,
            SmsProvider smsProvider,
            TwilioVoiceOtpProvider twilioVoiceOtpProvider,
            CustomerStorage customerStorage,
            IIntegrationLogQueue integrationLogQueue,
            IConfiguration configuration) : base(logger, mapper, cache)
        {
            _userStorage = userStorage;
            _clientStorage = clientStorage;
            _siteStorage = siteStorage;
            _authHelper = authHelper;
            _smsProvider = smsProvider;
            _twilioVoiceOtpProvider = twilioVoiceOtpProvider;
            _customerStorage = customerStorage;
            _integrationLogQueue = integrationLogQueue;
            _configuration = configuration;
        }

        /// <summary>On first kiosk registration, ensure a CRM Customer exists for this phone+site (with SMS marketing consent),
        /// and record a "registered for SMS via kiosk" event on the customer timeline. Best-effort: never breaks the OTP flow.</summary>
        private async Task BridgeKioskSignupToCrmAsync(int siteId, int accountId, string phone, CancellationToken cancelToken)
        {
            try
            {
                var customer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                    siteId, accountId, phone, name: "", email: null, city: null, defaultAddress: null, notes: null,
                    marketingSms: true,
                    onCreated: c => _integrationLogQueue.TryEnqueue(
                        CustomerActivityLog.Build(siteId, c.Id, CustomerActivityLog.OpCreated, "הלקוח נוצר", "נוצר דרך הקיוסק", null)),
                    cancelToken: cancelToken).ConfigureAwait(false);
                _integrationLogQueue.TryEnqueue(CustomerActivityLog.Build(
                    siteId, customer.Id, CustomerActivityLog.OpSmsSignup, "נרשם לקבל דיוור ב-SMS", "דרך הקיוסק", null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BridgeKioskSignupToCrmAsync failed siteId={SiteId}", siteId);
            }
        }

        public async Task<IApiResponse<bool>> SendOtpAsync(SendKioskCustomerOtpReq request, CancellationToken cancelToken = default)
        {
            IApiResponse<bool> response = new ApiResponse<bool>();

            if (request.SiteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

            var site = await _siteStorage.GetSiteAsync(request.SiteId, cancelToken).ConfigureAwait(false);
            if (site == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Site not found.");

            User? user = await _userStorage.GetUserByPhoneAsync(request.Phone, cancelToken).ConfigureAwait(false);

            if (user == null)
            {
                // Register: create kiosk customer user for this site.
                var roleId = await _clientStorage.GetRoleIdByRoleNameAsync("KioskCustomer", cancelToken).ConfigureAwait(false);
                if (!roleId.HasValue)
                    return CreateResponse(response, StatusCode.GeneralError, "Kiosk customer role not configured. Run Migration_AddKioskCustomerRole.sql.");

                user = new User
                {
                    GuidId = Guid.NewGuid(),
                    CreationTime = DateTime.UtcNow,
                    IsDeleted = false,
                    RoleId = roleId.Value,
                    AccountId = site.AccountId,
                    StatusId = (int)UserStatus.Active,
                    FirstName = "Customer",
                    LastName = "",
                    FullName = "Customer",
                    Phone = request.Phone,
                    IsEmailVerified = false,
                    LockoutFailCount = 0,
                };

                user = await _clientStorage.CreateClientAsync(user, new List<int> { request.SiteId }, cancelToken).ConfigureAwait(false);

                // First kiosk registration → ensure a CRM customer + record the SMS-signup on the timeline.
                await BridgeKioskSignupToCrmAsync(request.SiteId, site.AccountId, request.Phone, cancelToken).ConfigureAwait(false);
            }
            else
            {
                if (user.StatusId == (int)UserStatus.Blocked)
                    return CreateResponse(response, StatusCode.BlockedUser);

                if (user.LockoutExpiration.HasValue && DateTime.UtcNow <= user.LockoutExpiration)
                    return CreateResponse(response, StatusCode.UserLockedOut);
            }

            // When Auth:OverrideOtp is set (e.g. in appsettings.User.json), skip sending SMS/voice and set OTP to that value.
            string? overrideOtp = _configuration["Auth:OverrideOtp"];
            if (overrideOtp.HasValue())
            {
                bool set = await _userStorage.SetUserOtpOverrideAsync(user.Id, overrideOtp!, cancelToken).ConfigureAwait(false);
                if (set)
                {
                    response.Data = true;
                    return response;
                }
            }

            string? otp = await _userStorage.SetLoginUserOtpAsync(user.Id, cancelToken).ConfigureAwait(false);
            if (!otp.HasValue() || !user.Phone.HasValue())
            {
                _logger.LogError("Failed to generate OTP or phone missing for user {UserId}", user.Id);
                return CreateResponse(response, StatusCode.GeneralError, "Failed to generate OTP");
            }

            int languageId = 1;
            if (request.Channel == KioskOtpChannel.Voice)
            {
                bool voiceSent = await _twilioVoiceOtpProvider.SendOtpByVoiceAsync(user.Phone, otp!, languageId, cancelToken).ConfigureAwait(false);
                if (!voiceSent)
                {
                    _logger.LogError("Failed to send OTP by voice to phone: {Phone}", user.Phone);
                    return CreateResponse(response, StatusCode.GeneralError,
                        _twilioVoiceOtpProvider.IsConfigured ? "Failed to place voice call. Try SMS." : "Voice OTP is not available. Use SMS.");
                }
                response.Data = true;
                return response;
            }

            bool smsSent = await _smsProvider.SendOtpMessageAsync(user.Phone, languageId, otp!, cancelToken).ConfigureAwait(false);
            if (!smsSent)
            {
                _logger.LogError("Failed to send OTP SMS to phone: {Phone}", user.Phone);
                return CreateResponse(response, StatusCode.GeneralError, "Failed to send OTP SMS");
            }

            response.Data = true;
            return response;
        }

        public async Task<IApiResponse<AuthRes>> VerifyOtpAsync(VerifyKioskCustomerOtpReq request, CancellationToken cancelToken = default)
        {
            IApiResponse<AuthRes> response = new ApiResponse<AuthRes>();

            User? user = await _userStorage.GetUserByPhoneAsync(request.Phone, cancelToken).ConfigureAwait(false);
            if (user == null)
                return CreateResponse(response, StatusCode.InvalidCredentials);

            if (user.StatusId == (int)UserStatus.Blocked)
                return CreateResponse(response, StatusCode.BlockedUser);

            if (user.LockoutExpiration.HasValue && DateTime.UtcNow <= user.LockoutExpiration)
                return CreateResponse(response, StatusCode.UserLockedOut);

            bool isValid = IsValidOtp(user.Otp, request.Otp, user.OtpExpiration);
            if (!isValid)
                return CreateResponse(response, StatusCode.InvalidOtp);

            response.Data = _authHelper.CreateAuthenticationToken(user.Id, (UserRole)user.RoleId);
            response.Data.StatusId = (UserStatus)user.StatusId;
            response.Data.DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? null : user.FullName.Trim();

            await _userStorage.UpdateUserLoginAsync(user.Id, response.Data.RefreshToken!, response.Data.RefreshTokenExpiration,
                false, (UserStatus)user.StatusId, cancelToken).ConfigureAwait(false);

            return response;
        }
    }
}
