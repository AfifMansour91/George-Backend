using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class AccountStorage : StorageBase
    {
        public AccountStorage(GeorgeDBContext dbContext, ILogger<AccountStorage> logger)
            : base(dbContext, logger)
        {
        }
        public async Task<DataListResult<Account>> GetAccountsAsync(
            AccountFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Account>();

            // Base accounts query (exclude soft-deleted)
            var query = _dbContext.Accounts
                .Where(a => !a.IsDeleted)
                .Include(a => a.Users)
                .Include(a => a.Manager)
                .Include(a => a.KioskSettings!).ThenInclude(s => s.HomeVideoMedia)
                .Include(a => a.KioskSettingsHomeImages).ThenInclude(i => i.Media)
                //.Include(a => a.Status)
                .Include(a => a.WizardStatus)
                .Include(a => a.WizardType)
                .Include(a => a.ContentOwner)
                .AsNoTracking();

            // Filter by search (account name OR manager email/name)
            if (filter?.Search?.SearchTerm.HasValue() == true)
            {
                var term = filter.Search.SearchTerm!.Trim();

                query =
                    from a in query
                    where a.Name.Contains(term)
                       || _dbContext.Users
                            .Where(au => au.AccountId == a.Id && !au.IsDeleted && au.RoleId == (int)UserRole.AccountAdmin)
                            .Select(u => u)
                            .Any(u => (u.Email ?? "").Contains(term)
                                   || ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(term))
                    select a;
            }

            // Filter by status (Active/Inactive)
            if (filter?.Status.HasValue() == true)
            {
                var s = filter.Status!.Trim().ToLowerInvariant();
                if (s == "active") query = query.Where(a => a.IsActive);
                else if (s == "inactive") query = query.Where(a => !a.IsActive);
            }


            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.Name);

            // Add paging.
            //query = query.Skip(paging.Skip).Take(paging.Take);

            //// Add includes.
            //query = query.Include(a => a.Organization)
            //                //.Include(a => a.AccountSubscriptions.FirstOrDefault(a => a.IsActive)).ThenInclude(b => b.Subscription)
            //                .Include(a => a.AccountSubscriptions.Where(a => a.IsActive)).ThenInclude(b => b.Subscription)
            //                .Include(a => a.Owner)
            //                .Include(a => a.AccountUsers.Where(b => b.UserId == userId)).ThenInclude(c => c.User);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Account?> GetAccountAsync(long accountId, CancellationToken cancelToken)
        {
            return await _dbContext.Accounts
                .Where(a => a.Id == accountId && !a.IsDeleted)
                .Include(a => a.Users)
                .Include(a => a.Manager)
                .Include(a => a.KioskSettings!).ThenInclude(s => s.HomeVideoMedia)
                .Include(a => a.KioskSettingsHomeImages).ThenInclude(i => i.Media)
                //.Include(a => a.Status)
                .Include(a => a.WizardStatus)
                .Include(a => a.WizardType)
                .Include(a => a.ContentOwner)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        public async Task<Account> CreateAccountAsync(Account account, CancellationToken cancelToken)
        {
            _dbContext.Accounts.Add(account);
            await _dbContext.SaveChangesAsync(cancelToken);
            return account;
        }

        public async Task<Account?> UpdateAccountAsync(Account updated, CancellationToken cancelToken)
        {
            var dbAcc = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAcc == null) return null;

            dbAcc.Name = updated.Name;
            dbAcc.IsActive = updated.IsActive;
            dbAcc.UpdatedDate = DateTime.UtcNow;
            
            // Update WizardStep if provided
            if (updated.WizardStep.HasValue)
            {
                dbAcc.WizardStep = updated.WizardStep;
            }
            
            // Update WizardStatusId if provided
            if (updated.WizardStatusId.HasValue)
            {
                dbAcc.WizardStatusId = updated.WizardStatusId;
            }
            
            // Update LogoUrl
            dbAcc.LogoUrl = updated.LogoUrl;
            
            // Update IsKosherShop, AllowWeighted, KioskEnabled
            dbAcc.IsKosherShop = updated.IsKosherShop;
            dbAcc.AllowWeighted = updated.AllowWeighted;
            dbAcc.KioskEnabled = updated.KioskEnabled;

            // Update address and website fields
            dbAcc.Address = updated.Address;
            dbAcc.City = updated.City;
            dbAcc.State = updated.State;
            dbAcc.Zip = updated.Zip;
            dbAcc.Phone = updated.Phone;
            dbAcc.Website = updated.Website;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAcc;
        }

        public async Task UpsertKioskSettingsAsync(int accountId, George.DB.KioskSettings settings, List<int>? homeImageMediaIds, CancellationToken cancelToken)
        {
            var existing = await _dbContext.KioskSettings
                .FirstOrDefaultAsync(s => s.AccountId == accountId, cancelToken);
            if (existing != null)
            {
                existing.KioskLogoUrl = settings.KioskLogoUrl;
                existing.HeaderBgColor = settings.HeaderBgColor;
                existing.HomeBgType = settings.HomeBgType;
                existing.HomeVideoMediaId = settings.HomeVideoMediaId;
                existing.HomeImageIntervalSeconds = settings.HomeImageIntervalSeconds;
                existing.PrimaryColor = settings.PrimaryColor;
                existing.SecondaryColor = settings.SecondaryColor;
                existing.PosProductsTitle = settings.PosProductsTitle;
                existing.CreditEnabled = settings.CreditEnabled;
                existing.CashAtRegisterEnabled = settings.CashAtRegisterEnabled;
            }
            else
            {
                _dbContext.KioskSettings.Add(settings);
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            if (homeImageMediaIds != null)
            {
                var existingImages = await _dbContext.KioskSettingsHomeImages
                    .Where(i => i.AccountId == accountId)
                    .ToListAsync(cancelToken);
                _dbContext.KioskSettingsHomeImages.RemoveRange(existingImages);
                for (var i = 0; i < homeImageMediaIds.Count; i++)
                {
                    _dbContext.KioskSettingsHomeImages.Add(new George.DB.KioskSettingsHomeImage
                    {
                        AccountId = accountId,
                        MediaId = homeImageMediaIds[i],
                        SortOrder = i
                    });
                }
                await _dbContext.SaveChangesAsync(cancelToken);
            }
        }

        public async Task<Account?> DeleteAccountAsync(int id, CancellationToken cancelToken = default)
        {
            var dbModel = await _dbContext.Accounts
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync(cancelToken)
                .ConfigureAwait(false);
            if (dbModel == null)
                return null;

            // Soft delete: mark account and its sites as deleted
            dbModel.IsDeleted = true;
            dbModel.UpdatedDate = DateTime.UtcNow;

            var sites = await _dbContext.Sites
                .Where(s => s.AccountId == id)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
            foreach (var site in sites)
            {
                site.IsDeleted = true;
                site.UpdatedDate = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return dbModel;
        }

        public async Task<Account?> ActivateAccountAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Accounts
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);

            if (dbModel == null) return null;

            dbModel.IsActive = !dbModel.IsActive;
            dbModel.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbModel;
        }

        //public async Task AddAccountBusinessTypesAsync(long accountId, IEnumerable<int> businessTypeIds, CancellationToken cancelToken)
        //{
        //    if (businessTypeIds == null) return;

        //    foreach (var btId in businessTypeIds.Distinct())
        //    {
        //        _dbContext.AccountBusinessTypes.Add(new AccountBusinessType
        //        {
        //            AccountId = accountId,
        //            BusinessTypeId = btId
        //        });
        //    }

        //    await _dbContext.SaveChangesAsync(cancelToken);
        //}

        //public async Task<AccountUser> AddAccountUserAsync(long accountId, int userId, int roleId, CancellationToken cancelToken)
        //{
        //    var entity = new AccountUser
        //    {
        //        AccountId = accountId,
        //        UserId = userId,
        //        RoleId = roleId,
        //        IsActive = true
        //    };

        //    _dbContext.AccountUsers.Add(entity);
        //    await _dbContext.SaveChangesAsync(cancelToken);

        //    return entity;
        //}

        //public async Task<WizardSession> CreateWizardSessionAsync(long accountId, int startedByUserId, string contentOwner, string? inviteToken, CancellationToken cancelToken)
        //{
        //    var session = new WizardSession
        //    {
        //        AccountId = accountId,
        //        StartedByUserId = startedByUserId,
        //        Step = 1,
        //        Status = "InProgress",
        //        ContentOwner = contentOwner,
        //        InviteToken = inviteToken,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    _dbContext.WizardSessions.Add(session);
        //    await _dbContext.SaveChangesAsync(cancelToken);

        //    return session;
        //}

        //public async Task<WizardSession?> GetWizardSessionAsync(long accountId, CancellationToken cancelToken)
        //{
        //    return await _dbContext.WizardSessions
        //        .AsNoTracking()
        //        .Where(x => x.AccountId == accountId)
        //        .OrderByDescending(x => x.CreatedAt)
        //        .FirstOrDefaultAsync(cancelToken);
        //}

        //public async Task<WizardSession?> UpdateWizardSessionAsync(long wizardSessionId, int? step, string? status, CancellationToken cancelToken)
        //{
        //    var ws = await _dbContext.WizardSessions
        //        .Where(x => x.Id == wizardSessionId)
        //        .FirstOrDefaultAsync(cancelToken);

        //    if (ws == null) return null;

        //    if (step.HasValue)
        //        ws.Step = step.Value;

        //    if (status.HasValue())
        //    {
        //        ws.Status = status!;
        //        if (status == "Completed")
        //            ws.CompletedAt = DateTime.UtcNow;
        //    }

        //    await _dbContext.SaveChangesAsync(cancelToken);
        //    return ws;
        //}

        //// hook to onboarding proc (clone ProductTemplate -> AccountProduct etc.)
        //public async Task RunOnboardProcAsync(long accountId, int startedByUserId, CancellationToken cancelToken)
        //{
        //    // TEMP: in V1 MVP you can keep this empty or do inline clone logic.
        //    // Later you'll EXEC dbo.usp_OnboardAccountFromTemplates.
        //}
    }
}

