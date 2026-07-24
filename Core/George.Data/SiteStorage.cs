using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class SiteStorage : StorageBase
    {
        public SiteStorage(GeorgeDBContext dbContext, ILogger<SiteStorage> logger)
            : base(dbContext, logger)
        {
        }
        public async Task<DataListResult<Site>> GetSitesAsync(
            SiteFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Site>();

            // Base sites query (only non-deleted business types)
            var query = _dbContext.Site
                .Include(a => a.Account)
                .Include(s => s.BusinessType.Where(bt => !bt.IsDeleted))
                .AsNoTracking();

            if (filter?.Search?.SearchTerm.HasValue() == true)
            {
                var term = filter.Search.SearchTerm!.Trim();

                query =
                    from a in query
                    where a.SiteName.Contains(term)
                    select a;
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.SiteName);

            // Add paging.
            //query = query.Skip(paging.Skip).Take(paging.Take);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Site?> GetSiteAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.Site
                .Include(s => s.Account)
                    .ThenInclude(a => a.KioskSettings)
                    .ThenInclude(ks => ks!.HomeVideoMedia)
                .Include(s => s.Account)
                    .ThenInclude(a => a.KioskSettingsHomeImage)
                    .ThenInclude(i => i.Media)
                .Include(s => s.BusinessType)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == siteId, cancelToken);
        }

        /// <summary>Get site by internal API key (for external integrations auth, e.g. WooCommerce plugin).</summary>
        public async Task<Site?> GetSiteByInternalApiKeyAsync(string? apiKey, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            return await _dbContext.Site
                .AsNoTracking()
                .FirstOrDefaultAsync(s => !s.IsDeleted && s.InternalApiKey == apiKey.Trim(), cancelToken);
        }

        /// <summary>Site ids with WooCommerce configured AND external price management enabled (targets of the daily price-pull job).</summary>
        public async Task<List<int>> GetExternalPriceManagedSiteIdsAsync(CancellationToken cancelToken)
        {
            return await _dbContext.Site
                .AsNoTracking()
                .Where(s => !s.IsDeleted
                    && s.ExternalPriceManagement == true
                    && s.WooCommerceUrl != null && s.WooCommerceUrl != ""
                    && s.WooCommerceKey != null && s.WooCommerceKey != ""
                    && s.WooCommerceSecret != null && s.WooCommerceSecret != "")
                .Select(s => s.Id)
                .ToListAsync(cancelToken);
        }

        public async Task<List<Site>> GetSitesByAccountAsync(int accountId, CancellationToken cancelToken)
        {
            return await _dbContext.Site
                .Include(s => s.BusinessType.Where(bt => !bt.IsDeleted))
                .Where(s => s.AccountId == accountId && !s.IsDeleted)
                .AsNoTracking()
                .OrderBy(s => s.SiteName)
                .ToListAsync(cancelToken);
        }

        public async Task<List<int>> GetSiteIdsForUserAsync(int userId, CancellationToken cancelToken)
        {
            return await _dbContext.User
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .SelectMany(u => u.Site.Where(s => !s.IsDeleted).Select(s => s.Id))
                .Distinct()
                .ToListAsync(cancelToken);
        }

        public async Task<Site> CreateSiteAsync(Site site, List<int>? businessTypeIds, CancellationToken cancelToken)
        {
            _dbContext.Site.Add(site);
            
            // Add business types if provided (only non-deleted)
            if (businessTypeIds != null && businessTypeIds.Any())
            {
                var businessTypes = await _dbContext.BusinessType
                    .Where(bt => businessTypeIds.Contains(bt.Id) && !bt.IsDeleted)
                    .ToListAsync(cancelToken);
                
                foreach (var bt in businessTypes)
                {
                    site.BusinessType.Add(bt);
                }
            }
            
            await _dbContext.SaveChangesAsync(cancelToken);
            return site;
        }

        public async Task<Site?> UpdateSiteAsync(Site updated, List<int>? businessTypeIds, CancellationToken cancelToken)
        {
            var dbSite = await _dbContext.Site
                .Include(s => s.BusinessType.Where(bt => !bt.IsDeleted))
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbSite == null) return null;

            dbSite.SiteName = updated.SiteName ?? dbSite.SiteName;
            dbSite.AccountId = updated.AccountId;
            dbSite.Location = updated.Location ?? dbSite.Location;
            dbSite.Description = updated.Description ?? dbSite.Description;
            dbSite.Status = updated.Status ?? dbSite.Status;
            dbSite.ContactEmail = updated.ContactEmail ?? dbSite.ContactEmail;
            dbSite.ContactPhone = updated.ContactPhone ?? dbSite.ContactPhone;
            dbSite.IsKosherSite = updated.IsKosherSite ?? dbSite.IsKosherSite;
            dbSite.AllowWeightedProducts = updated.AllowWeightedProducts ?? dbSite.AllowWeightedProducts;
            dbSite.Currency = updated.Currency ?? dbSite.Currency;
            dbSite.WooCommerceUrl = updated.WooCommerceUrl ?? dbSite.WooCommerceUrl;
            dbSite.WooCommerceKey = updated.WooCommerceKey ?? dbSite.WooCommerceKey;
            dbSite.WooCommerceSecret = updated.WooCommerceSecret ?? dbSite.WooCommerceSecret;
            if (updated.WooCommerceEnabled.HasValue)
            {
                dbSite.WooCommerceEnabled = updated.WooCommerceEnabled;
            }
            if (updated.InternalApiKey != null)
            {
                dbSite.InternalApiKey = updated.InternalApiKey;
            }
            if (updated.WoltDispatchToken != null)
            {
                dbSite.WoltDispatchToken = updated.WoltDispatchToken;
            }
            if (updated.WoltEnabled.HasValue)
            {
                dbSite.WoltEnabled = updated.WoltEnabled;
            }
            // Shop settings (Sprint 2)
            if (updated.WeightTolerancePercent.HasValue) dbSite.WeightTolerancePercent = updated.WeightTolerancePercent;
            if (updated.DepreciationEnabled.HasValue) dbSite.DepreciationEnabled = updated.DepreciationEnabled;
            if (updated.DepreciationPercentagesJson != null) dbSite.DepreciationPercentagesJson = updated.DepreciationPercentagesJson;
            if (updated.PrepTimeMinutes.HasValue) dbSite.PrepTimeMinutes = updated.PrepTimeMinutes;
            if (updated.CustomerInactiveAfterDays.HasValue) dbSite.CustomerInactiveAfterDays = updated.CustomerInactiveAfterDays;
            if (updated.IncludeIncompleteOrdersInStats.HasValue) dbSite.IncludeIncompleteOrdersInStats = updated.IncludeIncompleteOrdersInStats;
            if (updated.ShippingCost.HasValue) dbSite.ShippingCost = updated.ShippingCost;
            if (updated.FreeShippingAbove.HasValue) dbSite.FreeShippingAbove = updated.FreeShippingAbove;
            if (updated.IsraelCityPickerEnabled.HasValue) dbSite.IsraelCityPickerEnabled = updated.IsraelCityPickerEnabled;
            if (updated.AutoPrintEnabled.HasValue) dbSite.AutoPrintEnabled = updated.AutoPrintEnabled;
            if (updated.VoucherPrintA4.HasValue) dbSite.VoucherPrintA4 = updated.VoucherPrintA4;
            if (updated.PrintNewOrderImmediate.HasValue) dbSite.PrintNewOrderImmediate = updated.PrintNewOrderImmediate;
            if (updated.PrintMovedToTreatment.HasValue) dbSite.PrintMovedToTreatment = updated.PrintMovedToTreatment;
            if (updated.PrintAfterPicking.HasValue) dbSite.PrintAfterPicking = updated.PrintAfterPicking;
            if (updated.PrintFutureImmediate.HasValue) dbSite.PrintFutureImmediate = updated.PrintFutureImmediate;
            if (updated.PrintFutureAtTimeEnabled.HasValue) dbSite.PrintFutureAtTimeEnabled = updated.PrintFutureAtTimeEnabled;
            if (updated.PrintFutureAtTime != null) dbSite.PrintFutureAtTime = updated.PrintFutureAtTime;
            if (updated.VoucherPrinterSilent.HasValue) dbSite.VoucherPrinterSilent = updated.VoucherPrinterSilent;
            if (updated.VoucherPrinterName != null) dbSite.VoucherPrinterName = updated.VoucherPrinterName;
            dbSite.VoucherPrinterUseAgent = updated.VoucherPrinterUseAgent;
            if (updated.LabelPrinterName != null) dbSite.LabelPrinterName = updated.LabelPrinterName;
            dbSite.LabelPrinterUseAgent = updated.LabelPrinterUseAgent;
            if (updated.PrintLabelsAfterPicking.HasValue) dbSite.PrintLabelsAfterPicking = updated.PrintLabelsAfterPicking;
            if (updated.AskBagsCountAtPickingFinish.HasValue) dbSite.AskBagsCountAtPickingFinish = updated.AskBagsCountAtPickingFinish;
            if (updated.ConfirmPickingAfterScan.HasValue) dbSite.ConfirmPickingAfterScan = updated.ConfirmPickingAfterScan;
            if (updated.ShowPickingDeviation.HasValue) dbSite.ShowPickingDeviation = updated.ShowPickingDeviation;
            if (updated.ShowSkuInPicking.HasValue) dbSite.ShowSkuInPicking = updated.ShowSkuInPicking;
            if (updated.ScaleEnabled.HasValue) dbSite.ScaleEnabled = updated.ScaleEnabled;
            if (updated.ScaleBarcodeEmbedMode != null) dbSite.ScaleBarcodeEmbedMode = updated.ScaleBarcodeEmbedMode;
            if (updated.ExternalPriceManagement.HasValue) dbSite.ExternalPriceManagement = updated.ExternalPriceManagement;
            // Promotion settings (Sprint 4). Without these the per-site Webhook URL never persists,
            // so PromotionWebhookDispatcher.FireAsync no-ops and promotions never sync to WooCommerce.
            if (updated.PromotionOveragePolicyDefault != null) dbSite.PromotionOveragePolicyDefault = updated.PromotionOveragePolicyDefault;
            if (updated.PromotionsApplyToPhoneOrders.HasValue) dbSite.PromotionsApplyToPhoneOrders = updated.PromotionsApplyToPhoneOrders;
            if (updated.PromotionsApplyToDiscountedProducts.HasValue) dbSite.PromotionsApplyToDiscountedProducts = updated.PromotionsApplyToDiscountedProducts;
            if (updated.PromotionWebhookUrl != null) dbSite.PromotionWebhookUrl = updated.PromotionWebhookUrl;
            if (updated.PromotionWebhookSecret != null) dbSite.PromotionWebhookSecret = updated.PromotionWebhookSecret;
            if (!string.IsNullOrWhiteSpace(updated.PaymentGatewayProvider))
                dbSite.PaymentGatewayProvider = updated.PaymentGatewayProvider;
            if (updated.CardcomTerminalNumber.HasValue) dbSite.CardcomTerminalNumber = updated.CardcomTerminalNumber;
            if (updated.CardcomApiName != null) dbSite.CardcomApiName = updated.CardcomApiName;
            if (updated.CardcomApiPasswordEncrypted != null) dbSite.CardcomApiPasswordEncrypted = updated.CardcomApiPasswordEncrypted;
            if (updated.CardcomSaveCardEnabled) dbSite.CardcomSaveCardEnabled = updated.CardcomSaveCardEnabled;
            if (updated.PaymentAuthBufferPercent > 0) dbSite.PaymentAuthBufferPercent = updated.PaymentAuthBufferPercent;
            if (updated.PaymentMaxAuthAmount.HasValue) dbSite.PaymentMaxAuthAmount = updated.PaymentMaxAuthAmount;
            dbSite.PaymentAllowCaptureAboveAuth = updated.PaymentAllowCaptureAboveAuth;
            if (updated.CardcomCssUrl != null) dbSite.CardcomCssUrl = updated.CardcomCssUrl;
            if (updated.CardcomLogoUrl != null) dbSite.CardcomLogoUrl = updated.CardcomLogoUrl;
            dbSite.IsActive = updated.IsActive || dbSite.IsActive;
            dbSite.UpdatedDate = DateTime.UtcNow;

            // Update business types
            if (businessTypeIds != null)
            {
                dbSite.BusinessType.Clear();
                if (businessTypeIds.Any())
                {
                    var businessTypes = await _dbContext.BusinessType
                        .Where(bt => businessTypeIds.Contains(bt.Id) && !bt.IsDeleted)
                        .ToListAsync(cancelToken);
                    
                    foreach (var bt in businessTypes)
                    {
                        dbSite.BusinessType.Add(bt);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbSite;
        }

        /// <summary>Set or clear internal API key for a site (e.g. when generating a new key).</summary>
        public async Task<string?> SetInternalApiKeyAsync(int siteId, string? apiKey, CancellationToken cancelToken)
        {
            var dbSite = await _dbContext.Site.FirstOrDefaultAsync(s => s.Id == siteId && !s.IsDeleted, cancelToken);
            if (dbSite == null) return null;
            dbSite.InternalApiKey = apiKey;
            dbSite.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return dbSite.InternalApiKey;
        }

        public async Task<Site?> DeleteSiteAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Site
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);
            if (dbModel != null)
            {
                // Delete the entity.
                _dbContext.Site.Remove(dbModel);

                // Save to the DB.
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }

            return dbModel;
        }

        public async Task<Site?> ActivateSiteAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Site
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);

            if (dbModel == null) return null;

            dbModel.IsActive = !dbModel.IsActive;
            dbModel.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbModel;
        }
    }
}

