using George.Common;
using George.Common.Request;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Data
{
    public class AccountProductStorage : StorageBase
    {
        public AccountProductStorage(GeorgeDBContext dbContext, ILogger<AccountProductStorage> logger)
            : base(dbContext, logger)
        {
        }

        // LIST for wizard step 3 and product mgmt screen
        public async Task<DataListResult<AccountProductListRow>> GetAccountProductsAsync(
            long accountId,
            AccountProductListFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var result = new DataListResult<AccountProductListRow>();

            // base query
            var q =
                from ap in _dbContext.AccountProducts.AsNoTracking()
                where ap.AccountId == accountId
                select new
                {
                    ap,
                    PrimaryMediaUrl = _dbContext.AccountProductMedia
                        .Where(m => m.AccountProductId == ap.Id && m.IsPrimary)
                        .OrderBy(m => m.SortOrder)
                        .Select(m => m.Url)
                        .FirstOrDefault(),
                    BrandName = _dbContext.Brands
                        .Where(b => b.Id == ap.BrandId)
                        .Select(b => b.Name)
                        .FirstOrDefault(),
                    SupplierName = _dbContext.Suppliers
                        .Where(s => s.Id == ap.SupplierId)
                        .Select(s => s.Name)
                        .FirstOrDefault(),
                };

            // filters
            if (filter.Search.HasValue())
            {
                string term = filter.Search!;
                q = q.Where(x =>
                    (x.ap.Title ?? "").Contains(term) ||
                    (x.ap.Sku ?? "").Contains(term));
            }

            if (filter.BrandId.HasValue)
                q = q.Where(x => x.ap.BrandId == filter.BrandId.Value);

            if (filter.SupplierId.HasValue)
                q = q.Where(x => x.ap.SupplierId == filter.SupplierId.Value);

            if (filter.IsEnabled.HasValue)
                q = q.Where(x => x.ap.IsEnabled == filter.IsEnabled.Value);

            if (filter.EditingStatus.HasValue())
                q = q.Where(x => x.ap.EditingStatus == filter.EditingStatus);

            if (filter.CategoryAccountCategoryId.HasValue)
            {
                long catId = filter.CategoryAccountCategoryId.Value;
                q = q.Where(x =>
                    _dbContext.AccountProductCategories
                        .Any(pc => pc.AccountProductId == x.ap.Id && pc.AccountCategoryId == catId));
            }

            // total
            if (paging.IncludeTotal)
                result.Total = await q.CountAsync(cancelToken);

            // sort
            q = q.OrderBy(x => x.ap.Title);

            // page
            q = q.Skip(paging.Skip).Take(paging.Take);

            // execute
            var raw = await q.ToListAsync(cancelToken);

            result.Items = raw.Select(x => new AccountProductListRow
            {
                AccountProductId = x.ap.Id,
                Title = x.ap.Title ?? "",
                Sku = x.ap.Sku,
                Price = x.ap.BaseUnitPrice,
                StockQuantity = x.ap.StockQuantity,
                IsEnabled = x.ap.IsEnabled,
                EditingStatus = x.ap.EditingStatus,
                PrimaryImageUrl = x.PrimaryMediaUrl,
                BrandName = x.BrandName,
                SupplierName = x.SupplierName
            }).ToList();

            return result;
        }

        // toggle IsEnabled for wizard step 3 "in/out of my shop"
        public async Task<AccountProduct?> SetProductEnabledAsync(long accountProductId, bool isEnabled, CancellationToken cancelToken)
        {
            var row = await _dbContext.AccountProducts
                .Where(p => p.Id == accountProductId)
                .FirstOrDefaultAsync(cancelToken);

            if (row == null) return null;

            row.IsEnabled = isEnabled;
            row.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return row;
        }

        // FULL DETAIL for wizard step 4 editor
        public async Task<AccountProduct?> GetAccountProductDetailAsync(long accountProductId, CancellationToken cancelToken)
        {
            // You’ll want includes for media, variants, etc.
            return await _dbContext.AccountProducts
                .AsNoTracking()
                .Include(p => p.AccountProductMedia)
                .Include(p => p.AccountProductVariants)
                .Where(p => p.Id == accountProductId)
                .FirstOrDefaultAsync(cancelToken);
        }

        public async Task<AccountProduct?> UpdateAccountProductAsync(
            long accountProductId,
            AccountProductUpdateModel update,
            int userId,
            CancellationToken cancelToken)
        {
            var row = await _dbContext.AccountProducts
                .Include(p => p.AccountProductVariants)
                .Where(p => p.Id == accountProductId)
                .FirstOrDefaultAsync(cancelToken);

            if (row == null) return null;

            // patch scalar fields
            row.Title = update.Title ?? row.Title;
            row.ShortDescription = update.ShortDescription ?? row.ShortDescription;
            row.DescriptionHtml = update.DescriptionHtml ?? row.DescriptionHtml;

            row.IsKosher = update.IsKosher ?? row.IsKosher;
            row.KosherStatusId = update.KosherStatusId ?? row.KosherStatusId;

            row.WeightPricingModelId = update.WeightPricingModelId ?? row.WeightPricingModelId;
            row.ShowPricePer100g = update.ShowPricePer100g ?? row.ShowPricePer100g;
            row.BaseUnitPrice = update.BaseUnitPrice ?? row.BaseUnitPrice;
            row.BaseUnitId = update.BaseUnitId ?? row.BaseUnitId;
            row.BaseWeightGrams = update.BaseWeightGrams ?? row.BaseWeightGrams;
            row.WeightStepGrams = update.WeightStepGrams ?? row.WeightStepGrams;

            row.StockQuantity = update.StockQuantity ?? row.StockQuantity;

            // override sku?
            row.Sku = update.Sku ?? row.Sku;

            var oldStatus = row.EditingStatus;
            if (update.EditingStatus.HasValue())
            {
                row.EditingStatus = update.EditingStatus!;
            }

            row.UpdatedAt = DateTime.UtcNow;

            // variants
            if (update.Variants != null)
            {
                foreach (var updatedVar in update.Variants)
                {
                    var dbVar = row.AccountProductVariants
                        .FirstOrDefault(v => v.Id == updatedVar.Id);
                    if (dbVar == null) continue;

                    if (updatedVar.VariantTitle != null)
                        dbVar.VariantTitle = updatedVar.VariantTitle;

                    if (updatedVar.Price.HasValue)
                        dbVar.Price = updatedVar.Price.Value;

                    if (updatedVar.StockQuantity.HasValue)
                        dbVar.StockQuantity = updatedVar.StockQuantity.Value;

                    if (updatedVar.WeightGrams.HasValue)
                        dbVar.WeightGrams = updatedVar.WeightGrams.Value;

                    if (updatedVar.IsEnabled.HasValue)
                        dbVar.IsEnabled = updatedVar.IsEnabled.Value;

                    dbVar.SortOrder = updatedVar.SortOrder ?? dbVar.SortOrder;
                }
            }

            // save product + variants
            await _dbContext.SaveChangesAsync(cancelToken);

            // log status change
            if (update.EditingStatus.HasValue() && update.EditingStatus != oldStatus)
            {
                var log = new ProductEditLog
                {
                    AccountProductId = row.Id,
                    UserId = userId,
                    FromStatus = oldStatus,
                    ToStatus = update.EditingStatus!,
                    Notes = update.Notes,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.ProductEditLogs.Add(log);
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return row;
        }
    }
}
