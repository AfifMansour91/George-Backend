using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Common.Utils;
using George.Data;
using George.DB;
using George.Services.Response;
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

        public async Task<IApiResponse<ApiListResponse<AccountRes>>> GetAccountsAsync(
            ApiListReq<AccountFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<AccountRes>>
            {
                Data = new ApiListResponse<AccountRes>()
            };

            var res = await _accountStorage.GetAccountsAsync(request.Filter, request, cancelToken);

            response.Data.Items = (res.Items ?? new List<Account>())
                .Select(account =>
                {
                    return new AccountRes
                    {
                        Id = account.Id,
                        AccountName = account.Name,

                        // If you don’t have these columns yet, keep null/empty
                        AccountDescription = account.Description,
                        AccountAddress = account.Address,
                        AccountCity = account.City,
                        AccountState = account.State,
                        AccountZip = account.Zip,
                        AccountPhone = account.Phone,

                        ManagerName = account.ManagerName,
                        ManagerEmail = account?.ManagerEmail,

                        Status = account.IsActive ? "Active" : "Inactive",

                        WizardStatusNamePair = account.WizardStatus != null
                            ? new IdNamePair
                            {
                                Id = account.WizardStatus.Id,
                                Name = account.WizardStatus.Name
                            }
                            : null,
                        WizardStatus = account.Status == null
                            ? "Not Started"
                            : (account.Status == "Completed" ? "Completed" : "In Progress"),

                        WizardTypeIdNamePair = account.WizardType != null
                            ? new IdNamePair
                            {
                                Id = account.WizardType.Id,
                                Name = account.WizardType.Name
                            }
                            : null,
                        WizardType = "all_sites", // until you store it
                        WizardStep = account?.WizardStep ?? 0,

                        ContentOwner = account?.ContentOwner?.Name ?? "Company",

                        CreatedDate = account.CreationTime,
                        UpdatedDate = account.UpdatedDate,

                        CreatedById = null,
                        CreatedBy = null
                    };
                })
                .ToList();

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<AccountRes>> CreateAccountAsync(CreateAccountReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountRes>();

            // ensure or create a user for the manager
            var managerUser = await _userStorage.GetUserByEmailAsync(req.ManagerEmail, cancelToken);
            var isExistingUser = managerUser != null;
            
            if (managerUser == null)
            {
                // Hash password
                string password = req.TempPassword ?? Cryptography.GeneratePassword(12);
                string passwordHash = Cryptography.GeneratePasswordHash(password);

                managerUser = new User
                {
                    FirstName = req.ManagerName, // later split
                    LastName = "",
                    Email = req.ManagerEmail,
                    Password = password,//passwordHash,
                    IsEmailVerified = false,
                    StatusId = (int)Common.UserStatus.Active,
                    RoleId = (int)UserRole.AccountAdmin, // or whatever role means client admin
                    LockoutFailCount = 0,
                    IsDeleted = false
                };

                // Create user in database
                managerUser = await _userStorage.CreateUserAsync(managerUser, cancelToken);
            }

            // create account
            var acc = new Account
            {
                Name = req.AccountName,
                Description = req.AccountDescription,
                Address = req.AccountAddress,
                City = req.AccountCity,

                State = req.AccountState,
                Zip = req.AccountZip,
                Phone = req.AccountPhone,
                ManagerName = req.ManagerName,
                ManagerEmail = req.ManagerEmail,
                ManagerId = managerUser.Id, // Set the manager ID
                //contentOwnerId = req.ContentOwnerId, TODO: lookup later
                Status = req.Status,
                //WizardStatus = req.WizardStatus, TODO: lookup later
                WizardStep = req.WizardStep,
                //WizardType = req.WizardType,  TODO: lookup later

                IsActive = true,
                CreationTime = DateTime.UtcNow,
                //Users = new List<User> { managerUser },
            };

            acc = await _accountStorage.CreateAccountAsync(acc, cancelToken);

            // If using an existing user, update their AccountId to link them to this account
            if (isExistingUser)
            {
                managerUser.AccountId = acc.Id;
                // Optionally update role to AccountAdmin if not already set
                if (managerUser.RoleId != (int)UserRole.AccountAdmin && managerUser.RoleId != (int)UserRole.Admin)
                {
                    managerUser.RoleId = (int)UserRole.AccountAdmin;
                }
                await _userStorage.UpdateUserAsync(managerUser, cancelToken);
            }
            else
            {
                // For new users, the AccountId should already be set via the Users collection relationship
                // But let's also set it explicitly to be safe
                managerUser.AccountId = acc.Id;
                await _userStorage.UpdateUserAsync(managerUser, cancelToken);
            }

            // link account-user as Admin
            //await _accountStorage.AddAccountUserAsync(acc.Id, managerUser.Id, (int)UserRole.Admin, cancelToken);

            //// create wizard session
            //string owner = req.ContentOwner ?? "Company"; // "Client" or "Company"
            //string? inviteToken = req.SendInviteToClient ? Guid.NewGuid().ToString("N") : null;

            //var wizard = await _accountStorage.CreateWizardSessionAsync(
            //    acc.Id,
            //    AuthUser.Id,
            //    owner,
            //    inviteToken,
            //    cancelToken
            //);

            //// onboard products/categories from ProductTemplate (future: proc)
            //await _accountStorage.RunOnboardProcAsync(acc.Id, AuthUser.Id, cancelToken);

            response.Data = _mapper.Map<AccountRes>(acc);

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

            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);


            response.Data = _mapper.Map<AccountRes>(account);

            return response;
        }

        // 3. Update account settings (kosher/weighted/shop name)
        public async Task<IApiResponse<AccountRes>> UpdateAccountAsync(int accountId, UpdateAccountReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountRes>();

            // Get existing account to preserve fields not being updated
            var existingAccount = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (existingAccount == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var model = new Account
            {
                Id = accountId,
                // Preserve existing name if not provided or empty, otherwise use provided name
                Name = string.IsNullOrWhiteSpace(req.Name) ? existingAccount.Name : req.Name,
                // Update IsActive (required field in UpdateAccountReq, so always provided)
                IsActive = req.IsActive,
                UpdatedDate = DateTime.UtcNow
            };

            var updated = await _accountStorage.UpdateAccountAsync(model, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new AccountRes
            {
                Id = updated.Id,
                AccountName = updated.Name,
                IsActive = updated.IsActive,
            };

            return response;
        }

        public async Task<IApiResponse<AccountRes?>> DeleteAccountAsync(int id, CancellationToken cancelToken = default)
        {
            IApiResponse<AccountRes?> response = new ApiResponse<AccountRes?>();

            //// Verify authorization.

            //// Check for dependencies.
            //if (await _taskStorage.TaskHasDependenciesAsync(id))
            //	return CreateResponse(response, StatusCode.ItemHasDependencies);

            // Delete from the DB.
            Account? model = await _accountStorage.DeleteAccountAsync(id, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Convert to response.
                response.Data = _mapper.Map<AccountRes>(model);
            }

            return response;
        }

        //// 4. Wizard session status (get)
        //public async Task<IApiResponse<WizardSessionRes>> GetWizardSessionAsync(long accountId, CancellationToken cancelToken)
        //{
        //    var response = new ApiResponse<WizardSessionRes>();

        //    // check access (like above)

        //    var ws = await _accountStorage.GetWizardSessionAsync(accountId, cancelToken);
        //    if (ws == null)
        //        return CreateResponse(response, StatusCode.ItemNotFound);

        //    response.Data = new WizardSessionRes
        //    {
        //        Id = ws.Id,
        //        AccountId = ws.AccountId,
        //        Step = ws.Step,
        //        Status = ws.Status,
        //        ContentOwner = ws.ContentOwner,
        //        InviteToken = ws.InviteToken
        //    };

        //    return response;
        //}

        //// 5. Wizard session status (update step / complete)
        //public async Task<IApiResponse<WizardSessionRes>> UpdateWizardSessionAsync(long accountId, UpdateWizardSessionReq req, CancellationToken cancelToken)
        //{
        //    var response = new ApiResponse<WizardSessionRes>();

        //    // check access

        //    var ws = await _accountStorage.GetWizardSessionAsync(accountId, cancelToken);
        //    if (ws == null)
        //        return CreateResponse(response, StatusCode.ItemNotFound);

        //    var updated = await _accountStorage.UpdateWizardSessionAsync(
        //        ws.Id,
        //        req.Step,
        //        req.Status,
        //        cancelToken
        //    );

        //    if (updated == null)
        //        return CreateResponse(response, StatusCode.ItemNotFound);

        //    response.Data = new WizardSessionRes
        //    {
        //        Id = updated.Id,
        //        AccountId = updated.AccountId,
        //        Step = updated.Step,
        //        Status = updated.Status,
        //        ContentOwner = updated.ContentOwner,
        //        InviteToken = updated.InviteToken
        //    };

        //    return response;
        //}
    }

}
