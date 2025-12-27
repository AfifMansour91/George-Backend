using George.Common;
using George.Common.Request;
using George.Data.Models;
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
        public async Task<DataListResult<AccountListEntityRow>> GetAccountsAsync(
            AccountListFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var result = new DataListResult<AccountListEntityRow>();

            // Base accounts query
            var accountsQ = _dbContext.Accounts.AsNoTracking();

            // Filter by search (account name OR manager email/name)
            if (filter?.Search.HasValue() == true)
            {
                var term = filter.Search!.Trim();

                accountsQ =
                    from a in accountsQ
                    where a.Name.Contains(term)
                       || _dbContext.AccountUsers
                            .Where(au => au.AccountId == a.Id && au.IsActive && au.RoleId == (int)UserRole.Admin)
                            .Join(_dbContext.Users, au => au.UserId, u => u.Id, (au, u) => u)
                            .Any(u => (u.Email ?? "").Contains(term)
                                   || ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(term))
                    select a;
            }

            // Filter by status (Active/Inactive)
            if (filter?.Status.HasValue() == true)
            {
                var s = filter.Status!.Trim().ToLowerInvariant();
                if (s == "active") accountsQ = accountsQ.Where(a => a.IsActive);
                else if (s == "inactive") accountsQ = accountsQ.Where(a => !a.IsActive);
            }

            // Total
            if (paging.IncludeTotal)
                result.Total = await accountsQ.CountAsync(cancelToken);

            // Sort (default: created desc like base44 sort=-created_date)
            accountsQ = accountsQ.OrderByDescending(a => a.CreatedAt);

            // Paging
            accountsQ = accountsQ.Skip(paging.Skip).Take(paging.Take);

            // Materialize account ids (small page)
            var accounts = await accountsQ.ToListAsync(cancelToken);
            var accountIds = accounts.Select(a => a.Id).ToList();

            // Latest wizard sessions for these accounts (entity)
            var latestWizardSessions = await _dbContext.WizardSessions
                .AsNoTracking()
                .Where(ws => accountIds.Contains(ws.AccountId))
                .GroupBy(ws => ws.AccountId)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).FirstOrDefault()!)
                .ToListAsync(cancelToken);

            var wsByAccountId = latestWizardSessions
                .Where(x => x != null)
                .ToDictionary(x => x.AccountId, x => x);

            // Manager users (Admin role) for these accounts (entity)
            // If you can have multiple admins, pick the first by UserId (or CreatedAt if you store it)
            var managerUsers = await (
                from au in _dbContext.AccountUsers.AsNoTracking()
                join u in _dbContext.Users.AsNoTracking() on au.UserId equals u.Id
                where accountIds.Contains(au.AccountId)
                      && au.IsActive
                      && au.RoleId == (int)UserRole.Admin
                orderby au.AccountId, au.UserId
                select new { au.AccountId, User = u }
            ).ToListAsync(cancelToken);

            var managerByAccountId = managerUsers
                .GroupBy(x => x.AccountId)
                .ToDictionary(g => g.Key, g => g.First().User);

            // Optional filter by wizard status in storage (still entity-based)
            // (We can do it after fetching latest sessions)
            IEnumerable<Account> filteredAccounts = accounts;

            if (filter?.WizardStatus.HasValue() == true)
            {
                var ws = filter.WizardStatus!.Trim().ToLowerInvariant();

                filteredAccounts = filteredAccounts.Where(a =>
                {
                    if (!wsByAccountId.TryGetValue(a.Id, out var w)) return false;

                    var status = (w.Status ?? "").ToLowerInvariant();
                    if (ws.Contains("progress")) return status != "completed";
                    if (ws.Contains("complete")) return status == "completed";
                    return true;
                });
            }

            // Build result rows (entities only)
            result.Items = filteredAccounts.Select(a => new AccountListEntityRow
            {
                Account = a,
                LatestWizardSession = wsByAccountId.TryGetValue(a.Id, out var w) ? w : null,
                ManagerUser = managerByAccountId.TryGetValue(a.Id, out var mu) ? mu : null
            }).ToList();

            // IMPORTANT NOTE:
            // If you filter by wizard status AFTER paging, total count may be slightly "off".
            // If you need total count to respect wizard filters, we can move wizard filtering into SQL
            // with a more complex query. Most apps are okay without that initially.

            return result;
        }


        public async Task<Account> CreateAccountAsync(Account account, CancellationToken cancelToken)
        {
            _dbContext.Accounts.Add(account);
            await _dbContext.SaveChangesAsync(cancelToken);
            return account;
        }

        public async Task AddAccountBusinessTypesAsync(long accountId, IEnumerable<int> businessTypeIds, CancellationToken cancelToken)
        {
            if (businessTypeIds == null) return;

            foreach (var btId in businessTypeIds.Distinct())
            {
                _dbContext.AccountBusinessTypes.Add(new AccountBusinessType
                {
                    AccountId = accountId,
                    BusinessTypeId = btId
                });
            }

            await _dbContext.SaveChangesAsync(cancelToken);
        }

        public async Task<AccountUser> AddAccountUserAsync(long accountId, int userId, int roleId, CancellationToken cancelToken)
        {
            var entity = new AccountUser
            {
                AccountId = accountId,
                UserId = userId,
                RoleId = roleId,
                IsActive = true
            };

            _dbContext.AccountUsers.Add(entity);
            await _dbContext.SaveChangesAsync(cancelToken);

            return entity;
        }

        public async Task<WizardSession> CreateWizardSessionAsync(long accountId, int startedByUserId, string contentOwner, string? inviteToken, CancellationToken cancelToken)
        {
            var session = new WizardSession
            {
                AccountId = accountId,
                StartedByUserId = startedByUserId,
                Step = 1,
                Status = "InProgress",
                ContentOwner = contentOwner,
                InviteToken = inviteToken,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.WizardSessions.Add(session);
            await _dbContext.SaveChangesAsync(cancelToken);

            return session;
        }

        public async Task<WizardSession?> GetWizardSessionAsync(long accountId, CancellationToken cancelToken)
        {
            return await _dbContext.WizardSessions
                .AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancelToken);
        }

        public async Task<WizardSession?> UpdateWizardSessionAsync(long wizardSessionId, int? step, string? status, CancellationToken cancelToken)
        {
            var ws = await _dbContext.WizardSessions
                .Where(x => x.Id == wizardSessionId)
                .FirstOrDefaultAsync(cancelToken);

            if (ws == null) return null;

            if (step.HasValue)
                ws.Step = step.Value;

            if (status.HasValue())
            {
                ws.Status = status!;
                if (status == "Completed")
                    ws.CompletedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return ws;
        }

        // hook to onboarding proc (clone ProductTemplate -> AccountProduct etc.)
        public async Task RunOnboardProcAsync(long accountId, int startedByUserId, CancellationToken cancelToken)
        {
            // TEMP: in V1 MVP you can keep this empty or do inline clone logic.
            // Later you'll EXEC dbo.usp_OnboardAccountFromTemplates.
        }

        public async Task<Account?> GetAccountAsync(long accountId, CancellationToken cancelToken)
        {
            return await _dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, cancelToken);
        }

        public async Task<Account?> UpdateAccountAsync(Account updated, CancellationToken cancelToken)
        {
            var dbAcc = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAcc == null) return null;

            dbAcc.Name = updated.Name;
            dbAcc.IsActive = updated.IsActive;
            dbAcc.IsKosherShop = updated.IsKosherShop;
            dbAcc.AllowWeighted = updated.AllowWeighted;
            dbAcc.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAcc;
        }
    }
}

