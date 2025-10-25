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

