using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class AccountService : ServiceBase
    {
        private readonly AccountStorage _accountStorage;
        private readonly UserStorage _userStorage;

        public AccountService(
            ILogger<AccountService> logger,
            IMapper mapper,
            CacheManager cache,
            AccountStorage accountStorage,
            UserStorage userStorage
        ) : base(logger, mapper, cache)
        {
            _accountStorage = accountStorage;
            _userStorage = userStorage;
        }

        // 1. Create account (wizard step 1 start)
        public async Task<IApiResponse<CreateAccountRes>> CreateAccountAsync(CreateAccountReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CreateAccountRes>();

            // SuperAdmin-only check (basic version)
            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            // ensure or create a user for the manager
            var managerUser = await _userStorage.GetUserByEmailAsync(req.ManagerEmail, cancelToken);
            if (managerUser == null)
            {
                managerUser = new User
                {
                    FirstName = req.ManagerName, // later split
                    LastName = "",
                    Email = req.ManagerEmail,
                    Password = req.TempPassword ?? "123456", // TODO: hash & generate safely
                    IsEmailVerified = false,
                    StatusId = (int)Common.UserStatus.Pending,
                    RoleId = (int)UserRole.Admin, // or whatever role means client admin
                    LockoutFailCount = 0,
                    IsMaster = false,
                    IsDeleted = false
                };

                //_dbContext.Users.Add(managerUser);
                //await _dbContext.SaveChangesAsync(cancelToken);
            }

            // create account
            var acc = new Account
            {
                Name = req.AccountName,
                IsActive = true,
                IsKosherShop = req.IsKosherShop,
                AllowWeighted = req.AllowWeighted,
                CreatedAt = DateTime.UtcNow
            };

            acc = await _accountStorage.CreateAccountAsync(acc, cancelToken);

            // business types
            await _accountStorage.AddAccountBusinessTypesAsync(acc.Id, req.BusinessTypeIds ?? new List<int>(), cancelToken);

            // link account-user as Admin
            await _accountStorage.AddAccountUserAsync(acc.Id, managerUser.Id, (int)UserRole.Admin, cancelToken);

            // create wizard session
            string owner = req.ContentOwner ?? "Company"; // "Client" or "Company"
            string? inviteToken = req.SendInviteToClient ? Guid.NewGuid().ToString("N") : null;

            var wizard = await _accountStorage.CreateWizardSessionAsync(
                acc.Id,
                AuthUser.Id,
                owner,
                inviteToken,
                cancelToken
            );

            // onboard products/categories from ProductTemplate (future: proc)
            await _accountStorage.RunOnboardProcAsync(acc.Id, AuthUser.Id, cancelToken);

            response.Data = new CreateAccountRes
            {
                AccountId = acc.Id,
                WizardSessionId = wizard.Id,
                InviteToken = inviteToken
            };

            return response;
        }

        // 2. Get account details (after created)
        public async Task<IApiResponse<AccountRes>> GetAccountAsync(long accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountRes>();

            // check access
            if (!AuthUser.IsMaster)
            {
                // TODO: check AccountUser table for this AuthUser.Id
            }

            var acc = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (acc == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new AccountRes
            {
                Id = acc.Id,
                Name = acc.Name,
                IsActive = acc.IsActive,
                IsKosherShop = acc.IsKosherShop,
                AllowWeighted = acc.AllowWeighted
            };

            return response;
        }

        // 3. Update account settings (kosher/weighted/shop name)
        public async Task<IApiResponse<AccountRes>> UpdateAccountAsync(long accountId, UpdateAccountReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountRes>();

            if (!AuthUser.IsMaster)
            {
                // TODO: check AccountUser role for AuthUser
            }

            var model = new Account
            {
                Id = accountId,
                Name = req.Name,
                IsActive = req.IsActive,
                IsKosherShop = req.IsKosherShop,
                AllowWeighted = req.AllowWeighted,
                UpdatedAt = DateTime.UtcNow
            };

            var updated = await _accountStorage.UpdateAccountAsync(model, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new AccountRes
            {
                Id = updated.Id,
                Name = updated.Name,
                IsActive = updated.IsActive,
                IsKosherShop = updated.IsKosherShop,
                AllowWeighted = updated.AllowWeighted
            };

            return response;
        }

        // 4. Wizard session status (get)
        public async Task<IApiResponse<WizardSessionRes>> GetWizardSessionAsync(long accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<WizardSessionRes>();

            // check access (like above)

            var ws = await _accountStorage.GetWizardSessionAsync(accountId, cancelToken);
            if (ws == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new WizardSessionRes
            {
                Id = ws.Id,
                AccountId = ws.AccountId,
                Step = ws.Step,
                Status = ws.Status,
                ContentOwner = ws.ContentOwner,
                InviteToken = ws.InviteToken
            };

            return response;
        }

        // 5. Wizard session status (update step / complete)
        public async Task<IApiResponse<WizardSessionRes>> UpdateWizardSessionAsync(long accountId, UpdateWizardSessionReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<WizardSessionRes>();

            // check access

            var ws = await _accountStorage.GetWizardSessionAsync(accountId, cancelToken);
            if (ws == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var updated = await _accountStorage.UpdateWizardSessionAsync(
                ws.Id,
                req.Step,
                req.Status,
                cancelToken
            );

            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new WizardSessionRes
            {
                Id = updated.Id,
                AccountId = updated.AccountId,
                Step = updated.Step,
                Status = updated.Status,
                ContentOwner = updated.ContentOwner,
                InviteToken = updated.InviteToken
            };

            return response;
        }
    }

}
