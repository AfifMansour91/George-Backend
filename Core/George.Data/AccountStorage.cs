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
            var query = _dbContext.Account
                .Where(a => !a.IsDeleted)
                .Include(a => a.User)
                .Include(a => a.Manager)
                .Include(a => a.KioskSettings!).ThenInclude(s => s.HomeVideoMedia)
                .Include(a => a.KioskSettingsHomeImage).ThenInclude(i => i.Media)
                .Include(a => a.AccountNotificationSettings)
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
                       || _dbContext.User
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
            return await _dbContext.Account
                .Where(a => a.Id == accountId && !a.IsDeleted)
                .Include(a => a.User)
                .Include(a => a.Manager)
                .Include(a => a.KioskSettings!).ThenInclude(s => s.HomeVideoMedia)
                .Include(a => a.KioskSettingsHomeImage).ThenInclude(i => i.Media)
                .Include(a => a.AccountNotificationSettings)
                //.Include(a => a.Status)
                .Include(a => a.WizardStatus)
                .Include(a => a.WizardType)
                .Include(a => a.ContentOwner)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        public async Task<Account> CreateAccountAsync(Account account, CancellationToken cancelToken)
        {
            _dbContext.Account.Add(account);
            await _dbContext.SaveChangesAsync(cancelToken);
            return account;
        }

        public async Task<Account?> UpdateAccountAsync(Account updated, CancellationToken cancelToken)
        {
            var dbAcc = await _dbContext.Account
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

            // Update WizardTypeId if provided
            if (updated.WizardTypeId.HasValue)
            {
                dbAcc.WizardTypeId = updated.WizardTypeId;
            }

            // MultiSite Phase 2: persist management mode (explicit, independent of wizard).
            dbAcc.ManagementMode = updated.ManagementMode;

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

            // Update low-stock threshold defaults (null = keep existing, already resolved in service)
            dbAcc.DefaultLowStockThresholdWeighted = updated.DefaultLowStockThresholdWeighted;
            dbAcc.DefaultLowStockThresholdUnits = updated.DefaultLowStockThresholdUnits;
            dbAcc.DefaultNewLabelDays = updated.DefaultNewLabelDays > 0 ? updated.DefaultNewLabelDays : 7;

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
                existing.PosProductsType = settings.PosProductsType;
                existing.PosProductsCategoryId = settings.PosProductsCategoryId;
                existing.CreditEnabled = settings.CreditEnabled;
                existing.CashAtRegisterEnabled = settings.CashAtRegisterEnabled;
                existing.ShowDuplicateOrderButton = settings.ShowDuplicateOrderButton;
                existing.ShowOutOfStockProducts = settings.ShowOutOfStockProducts;
                existing.ShowOutOfStockAtBottom = settings.ShowOutOfStockAtBottom;
                existing.PosProductsEnabled = settings.PosProductsEnabled;
                existing.ButtonTextToPaymentOrViewOrder = settings.ButtonTextToPaymentOrViewOrder;
                existing.ButtonTextCartToPayment = settings.ButtonTextCartToPayment;
                existing.ButtonTextUpsellContinueToPayment = settings.ButtonTextUpsellContinueToPayment;
                existing.InactivityPopupSeconds = settings.InactivityPopupSeconds;
                existing.PrivacyPolicyCheckboxCheckedByDefault = settings.PrivacyPolicyCheckboxCheckedByDefault;
                existing.PrivacyPolicyContent = settings.PrivacyPolicyContent;
                existing.ProductImageAspectRatio = settings.ProductImageAspectRatio;
            }
            else
            {
                _dbContext.KioskSettings.Add(settings);
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            if (homeImageMediaIds != null)
            {
                var existingImages = await _dbContext.KioskSettingsHomeImage
                    .Where(i => i.AccountId == accountId)
                    .ToListAsync(cancelToken);
                _dbContext.KioskSettingsHomeImage.RemoveRange(existingImages);
                for (var i = 0; i < homeImageMediaIds.Count; i++)
                {
                    _dbContext.KioskSettingsHomeImage.Add(new George.DB.KioskSettingsHomeImage
                    {
                        AccountId = accountId,
                        MediaId = homeImageMediaIds[i],
                        SortOrder = i
                    });
                }
                await _dbContext.SaveChangesAsync(cancelToken);
            }
        }

        /// <summary>Upsert one notification-settings row: siteId == null targets the account default, otherwise the site's full override row.</summary>
        public async Task UpsertNotificationSettingsAsync(int accountId, int? siteId, George.DB.AccountNotificationSettings settings, CancellationToken cancelToken)
        {
            var existing = await _dbContext.AccountNotificationSettings
                .FirstOrDefaultAsync(s => s.AccountId == accountId && s.SiteId == siteId, cancelToken);
            if (existing != null)
            {
                existing.NewOrderManagerSoundEnabled = settings.NewOrderManagerSoundEnabled;
                existing.NewOrderManagerSoundKey = settings.NewOrderManagerSoundKey;
                existing.NewOrderManagerSoundTriggerWebsite = settings.NewOrderManagerSoundTriggerWebsite;
                existing.NewOrderManagerSoundTriggerKiosk = settings.NewOrderManagerSoundTriggerKiosk;
                existing.NewOrderManagerSoundTriggerWhatsapp = settings.NewOrderManagerSoundTriggerWhatsapp;
                existing.NewOrderManagerSoundTriggerPhone = settings.NewOrderManagerSoundTriggerPhone;
                existing.NewOrderManagerMessageChannel = settings.NewOrderManagerMessageChannel;
                existing.NewOrderManagerPhoneNumbers = settings.NewOrderManagerPhoneNumbers;
                existing.NewOrderManagerMessageTemplate = settings.NewOrderManagerMessageTemplate;
                existing.NewOrderManagerReminderBeforeDeliveryEnabled = settings.NewOrderManagerReminderBeforeDeliveryEnabled;
                existing.NewOrderManagerReminderBeforeDeliveryMinutes = settings.NewOrderManagerReminderBeforeDeliveryMinutes;
                existing.NewOrderManagerReminderNoTreatmentEnabled = settings.NewOrderManagerReminderNoTreatmentEnabled;
                existing.NewOrderManagerReminderNoTreatmentMinutes = settings.NewOrderManagerReminderNoTreatmentMinutes;
                existing.NewOrderManagerReminderNoTreatmentSoundKey = settings.NewOrderManagerReminderNoTreatmentSoundKey;
                existing.NewOrderCustomerChannel = settings.NewOrderCustomerChannel;
                existing.NewOrderCustomerMessageShipping = settings.NewOrderCustomerMessageShipping;
                existing.NewOrderCustomerMessagePickup = settings.NewOrderCustomerMessagePickup;
                existing.NewOrderCustomerMessageKiosk = settings.NewOrderCustomerMessageKiosk;
                existing.NewOrderCustomerSmsOnPhoneOrderEnabled = settings.NewOrderCustomerSmsOnPhoneOrderEnabled;
                existing.NewOrderCustomerMessagePhoneOrder = settings.NewOrderCustomerMessagePhoneOrder;
                existing.OrderReadyManagerNotifyEnabled = settings.OrderReadyManagerNotifyEnabled;
                existing.OrderReadyCustomerChannel = settings.OrderReadyCustomerChannel;
                existing.OrderReadyCustomerMessageShipping = settings.OrderReadyCustomerMessageShipping;
                existing.OrderReadyCustomerMessagePickup = settings.OrderReadyCustomerMessagePickup;
                existing.OrderReadyCustomerMessageKiosk = settings.OrderReadyCustomerMessageKiosk;
                existing.OrderNotPickedUpManagerNotifyEnabled = settings.OrderNotPickedUpManagerNotifyEnabled;
                existing.OrderNotPickedUpAutoReminderEnabled = settings.OrderNotPickedUpAutoReminderEnabled;
                existing.OrderNotPickedUpMinutesAfterScheduledPickup = settings.OrderNotPickedUpMinutesAfterScheduledPickup;
                existing.OrderNotPickedUpCustomerMessageTemplate = settings.OrderNotPickedUpCustomerMessageTemplate;
                existing.AfterDeliveryEnabled = settings.AfterDeliveryEnabled;
                existing.AfterDeliveryTriggerType = settings.AfterDeliveryTriggerType;
                existing.AfterDeliveryTriggerAfterValue = settings.AfterDeliveryTriggerAfterValue;
                existing.AfterDeliveryTriggerAfterUnit = settings.AfterDeliveryTriggerAfterUnit;
                existing.AfterDeliveryCustomerMessageTemplate = settings.AfterDeliveryCustomerMessageTemplate;
                existing.PaymentCustomerMessageInvoice = settings.PaymentCustomerMessageInvoice;
                existing.PaymentCustomerMessageRefund = settings.PaymentCustomerMessageRefund;
                existing.PaymentCustomerMessagePaymentLink = settings.PaymentCustomerMessagePaymentLink;
                existing.PaymentSendInvoiceSmsAfterCapture = settings.PaymentSendInvoiceSmsAfterCapture;
                existing.UpdatedDate = DateTime.UtcNow;
            }
            else
            {
                settings.SiteId = siteId;
                settings.CreationTime = DateTime.UtcNow;
                _dbContext.AccountNotificationSettings.Add(settings);
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        public Task<bool> SiteBelongsToAccountAsync(int accountId, int siteId, CancellationToken cancelToken) =>
            _dbContext.Site.AnyAsync(s => s.Id == siteId && s.AccountId == accountId && !s.IsDeleted, cancelToken);

        /// <summary>Remove a site's notification-settings override so it inherits the account default again. Hard delete — the unique (AccountId, SiteId) index must stay free for a future override.</summary>
        public async Task<bool> DeleteNotificationSettingsOverrideAsync(int accountId, int siteId, CancellationToken cancelToken)
        {
            var existing = await _dbContext.AccountNotificationSettings
                .FirstOrDefaultAsync(s => s.AccountId == accountId && s.SiteId == siteId, cancelToken);
            if (existing == null)
                return false;
            _dbContext.AccountNotificationSettings.Remove(existing);
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<AccountSmsSettings?> GetSmsSettingsAsync(int accountId, CancellationToken cancelToken)
        {
            return await _dbContext.AccountSmsSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.AccountId == accountId, cancelToken);
        }

        public async Task<AccountSmsSettings> UpsertSmsSettingsAsync(AccountSmsSettings settings, CancellationToken cancelToken)
        {
            var existing = await _dbContext.AccountSmsSettings
                .FirstOrDefaultAsync(s => s.AccountId == settings.AccountId, cancelToken);
            if (existing != null)
            {
                existing.IsEnabled = settings.IsEnabled;
                existing.Provider = settings.Provider;
                existing.ApiBaseUrl = settings.ApiBaseUrl;
                existing.ApiToken = settings.ApiToken;
                existing.FromName = settings.FromName;
                existing.SourcePhone = settings.SourcePhone;
                existing.UpdatedDate = DateTime.UtcNow;
                existing.UpdateUserId = settings.UpdateUserId;
                await _dbContext.SaveChangesAsync(cancelToken);
                return existing;
            }

            settings.CreationTime = DateTime.UtcNow;
            _dbContext.AccountSmsSettings.Add(settings);
            await _dbContext.SaveChangesAsync(cancelToken);
            return settings;
        }

        /// <summary>Remove the account's SMS credentials row so it goes back to the system-wide SMS account. Hard delete — the unique AccountId index must stay free for a future row.</summary>
        public async Task<bool> DeleteSmsSettingsAsync(int accountId, CancellationToken cancelToken)
        {
            var existing = await _dbContext.AccountSmsSettings
                .FirstOrDefaultAsync(s => s.AccountId == accountId, cancelToken);
            if (existing == null)
                return false;
            _dbContext.AccountSmsSettings.Remove(existing);
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<Account?> DeleteAccountAsync(int id, CancellationToken cancelToken = default)
        {
            var dbModel = await _dbContext.Account
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync(cancelToken)
                .ConfigureAwait(false);
            if (dbModel == null)
                return null;

            // Soft delete: mark account and its sites as deleted
            dbModel.IsDeleted = true;
            dbModel.UpdatedDate = DateTime.UtcNow;

            var sites = await _dbContext.Site
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
            var dbModel = await _dbContext.Account
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

        /// <summary>Get stored wizard session JSON for an account (and optional site). Returns null if no row or table missing.</summary>
        public async Task<string?> GetWizardSessionJsonAsync(int accountId, string? siteId, CancellationToken cancelToken)
        {
            const int stepNumber = 0; // full session blob
            int? siteIdInt = null;
            if (!string.IsNullOrEmpty(siteId) && int.TryParse(siteId, out var sid))
                siteIdInt = sid;
            try
            {
                var set = _dbContext.Set<AccountWizardStepData>();
                var row = await set
                    .AsNoTracking()
                    .Where(a => a.AccountId == accountId && a.StepNumber == stepNumber &&
                        (siteIdInt == null ? a.SiteId == null : a.SiteId == siteIdInt))
                    .FirstOrDefaultAsync(cancelToken)
                    .ConfigureAwait(false);
                return row?.DataJson;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
            {
                _logger.LogWarning(ex, "AccountWizardStepData table missing for GetWizardSessionJsonAsync. Run Scripts/Create_AccountWizardStepData.sql.");
                return null;
            }
        }

        /// <summary>Save wizard session JSON for an account (and optional site). Creates or updates the row.</summary>
        public async Task SaveWizardSessionJsonAsync(int accountId, string? siteId, string dataJson, CancellationToken cancelToken)
        {
            const int stepNumber = 0;
            int? siteIdInt = null;
            if (!string.IsNullOrEmpty(siteId) && int.TryParse(siteId, out var sid))
                siteIdInt = sid;
            try
            {
                var set = _dbContext.Set<AccountWizardStepData>();
                var row = await set
                    .Where(a => a.AccountId == accountId && a.StepNumber == stepNumber &&
                        (siteIdInt == null ? a.SiteId == null : a.SiteId == siteIdInt))
                    .FirstOrDefaultAsync(cancelToken)
                    .ConfigureAwait(false);
                var now = DateTime.UtcNow;
                if (row != null)
                {
                    row.DataJson = dataJson;
                    row.UpdatedDate = now;
                }
                else
                {
                    set.Add(new AccountWizardStepData
                    {
                        AccountId = accountId,
                        SiteId = siteIdInt,
                        StepNumber = stepNumber,
                        DataJson = dataJson,
                        CreationTime = now,
                        UpdatedDate = now
                    });
                }
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
            {
                _logger.LogWarning(ex, "AccountWizardStepData table missing for SaveWizardSessionJsonAsync. Run Scripts/Create_AccountWizardStepData.sql.");
            }
        }
    }
}

