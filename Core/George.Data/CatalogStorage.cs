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
    public class CatalogStorage : StorageBase
    {
        public CatalogStorage(GeorgeDBContext dbContext, ILogger<CatalogStorage> logger)
            : base(dbContext, logger)
        {
        }

        // LIST ProductTemplate for SuperAdmin catalog screen
        public async Task<DataListResult<CatalogListRow>> GetProductTemplatesAsync(
            CatalogProductListFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var result = new DataListResult<CatalogListRow>();

            var q =
                from pt in _dbContext.ProductTemplates.AsNoTracking()
                select new
                {
                    pt,
                    PrimaryImageUrl = _dbContext.ProductTemplateMedia
                        .Where(m => m.ProductTemplateId == pt.Id && m.IsPrimary)
                        .OrderBy(m => m.SortOrder)
                        .Select(m => m.Url)
                        .FirstOrDefault(),
                    BrandName = _dbContext.Brands
                        .Where(b => b.Id == pt.BrandId)
                        .Select(b => b.Name)
                        .FirstOrDefault(),
                    SupplierName = _dbContext.Suppliers
                        .Where(s => s.Id == pt.SupplierId)
                        .Select(s => s.Name)
                        .FirstOrDefault()
                };

            // filters
            if (filter.Search.HasValue())
            {
                string term = filter.Search!;
                q = q.Where(x =>
                    (x.pt.Title ?? "").Contains(term) ||
                    (x.pt.Sku ?? "").Contains(term));
            }

            if (filter.BrandId.HasValue)
                q = q.Where(x => x.pt.BrandId == filter.BrandId.Value);

            if (filter.SupplierId.HasValue)
                q = q.Where(x => x.pt.SupplierId == filter.SupplierId.Value);

            if (filter.IsActive.HasValue)
                q = q.Where(x => x.pt.IsActive == filter.IsActive.Value);

            if (filter.BusinessTypeId.HasValue)
            {
                int btId = filter.BusinessTypeId.Value;
                q = q.Where(x =>
                    _dbContext.ProductTemplateBusinessTypes
                        .Any(bt => bt.ProductTemplateId == x.pt.Id &&
                                   bt.BusinessTypeId == btId));
            }

            if (filter.CategoryId.HasValue)
            {
                int catId = filter.CategoryId.Value;
                q = q.Where(x =>
                    _dbContext.ProductTemplateCategories
                        .Any(pc => pc.ProductTemplateId == x.pt.Id &&
                                   pc.CategoryId == catId));
            }

            // total
            if (paging.IncludeTotal)
                result.Total = await q.CountAsync(cancelToken);

            // sort
            q = q.OrderBy(x => x.pt.Title);

            // paging
            q = q.Skip(paging.Skip).Take(paging.Take);

            // exec
            var rows = await q.ToListAsync(cancelToken);

            result.Items = rows.Select(x => new CatalogListRow
            {
                ProductTemplateId = x.pt.Id,
                Title = x.pt.Title ?? "",
                Sku = x.pt.Sku,
                BaseUnitPrice = x.pt.BaseUnitPrice,
                IsActive = x.pt.IsActive,
                PrimaryImageUrl = x.PrimaryImageUrl,
                BrandName = x.BrandName,
                SupplierName = x.SupplierName
            }).ToList();

            return result;
        }

        public async Task<ProductTemplate?> GetProductTemplateDetailAsync(long templateId, CancellationToken cancelToken)
        {
            // Include child tables
            return await _dbContext.ProductTemplates
                .AsNoTracking()
                .Include(p => p.ProductTemplateMedia)
                .Include(p => p.ProductTemplateAttributes)
                    .ThenInclude(a => a.ProductTemplateAttributeOptions)
                .Include(p => p.ProductTemplateSelectableWeights)
                .Include(p => p.ProductTemplateCategories)
                .Include(p => p.ProductTemplateBusinessTypes)
                .FirstOrDefaultAsync(p => p.Id == templateId, cancelToken);
        }

        public async Task<ProductTemplate> CreateProductTemplateAsync(ProductTemplate template, CancellationToken cancelToken)
        {
            _dbContext.ProductTemplates.Add(template);
            await _dbContext.SaveChangesAsync(cancelToken);
            return template;
        }

        public async Task<ProductTemplate?> UpdateProductTemplateAsync(long templateId, ProductTemplatePatchModel patch, int userId, CancellationToken cancelToken)
        {
            var pt = await _dbContext.ProductTemplates
                .Include(p => p.ProductTemplateMedia)
                .Include(p => p.ProductTemplateAttributes)
                    .ThenInclude(a => a.ProductTemplateAttributeOptions)
                .Include(p => p.ProductTemplateSelectableWeights)
                .Include(p => p.ProductTemplateCategories)
                .Include(p => p.ProductTemplateBusinessTypes)
                .FirstOrDefaultAsync(p => p.Id == templateId, cancelToken);

            if (pt == null) return null;

            // patch scalar fields
            if (patch.SkuSet) pt.Sku = patch.Sku;
            if (patch.TitleSet) pt.Title = patch.Title!;
            if (patch.ShortDescriptionSet) pt.ShortDescription = patch.ShortDescription;
            if (patch.DescriptionHtmlSet) pt.DescriptionHtml = patch.DescriptionHtml;

            if (patch.ProductTypeIdSet) pt.ProductTypeId = patch.ProductTypeId!.Value;
            if (patch.BrandIdSet) pt.BrandId = patch.BrandId;
            if (patch.SupplierIdSet) pt.SupplierId = patch.SupplierId;

            if (patch.IsKosherDefaultSet) pt.IsKosherDefault = patch.IsKosherDefault!.Value;
            if (patch.KosherStatusIdSet) pt.KosherStatusId = patch.KosherStatusId;

            if (patch.WeightPricingModelIdSet) pt.WeightPricingModelId = patch.WeightPricingModelId;
            if (patch.ShowPricePer100gSet) pt.ShowPricePer100g = patch.ShowPricePer100g!.Value;

            if (patch.BaseUnitPriceSet) pt.BaseUnitPrice = patch.BaseUnitPrice;
            if (patch.BaseUnitIdSet) pt.BaseUnitId = patch.BaseUnitId;
            if (patch.BaseWeightGramsSet) pt.BaseWeightGrams = patch.BaseWeightGrams;

            if (patch.IsActiveSet) pt.IsActive = patch.IsActive!.Value;

            pt.UpdatedAt = DateTime.UtcNow;

            // TODO: sync child collections here with SyncItems<T>:
            // - ProductTemplateMedias
            // - ProductTemplateAttributes (+options)
            // - ProductTemplateSelectableWeights
            // - ProductTemplateCategories
            // - ProductTemplateBusinessTypes
            //
            // For MVP you can skip child editing or handle minimal.

            await _dbContext.SaveChangesAsync(cancelToken);

            // AuditLog insert
            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "CatalogProduct.Update",
                EntityName = "ProductTemplate",
                EntityId = pt.Id,
                Payload = null,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancelToken);

            return pt;
        }

        public async Task<ProductTemplate?> SetProductTemplateStatusAsync(long templateId, bool isActive, int userId, CancellationToken cancelToken)
        {
            var pt = await _dbContext.ProductTemplates
                .Where(p => p.Id == templateId)
                .FirstOrDefaultAsync(cancelToken);

            if (pt == null) return null;

            pt.IsActive = isActive;
            pt.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "CatalogProduct.Status",
                EntityName = "ProductTemplate",
                EntityId = pt.Id,
                Payload = isActive ? "Activated" : "Deactivated",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancelToken);

            return pt;
        }

        // LOOKUPS for dropdowns
        public async Task<(List<IdNamePair> Brands, List<IdNamePair> Suppliers, List<IdNamePair> KosherStatuses, List<IdNamePair> WeightPricingModels,
            List<IdNamePair> Units, List<IdNamePair> BusinessTypes)> GetLookupsAsync(CancellationToken cancelToken)
        {
            var brands = await _dbContext.Brands
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new IdNamePair { Id = b.Id, Name = b.Name })
                .ToListAsync(cancelToken);

            var suppliers = await _dbContext.Suppliers
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new IdNamePair { Id = s.Id, Name = s.Name })
                .ToListAsync(cancelToken);

            var kosherStatuses = await _dbContext.KosherStatuses
                .AsNoTracking()
                .OrderBy(k => k.Name)
                .Select(k => new IdNamePair { Id = k.Id, Name = k.Name })
                .ToListAsync(cancelToken);

            var wpm = await _dbContext.WeightPricingModels
                .AsNoTracking()
                .OrderBy(w => w.Name)
                .Select(w => new IdNamePair { Id = w.Id, Name = w.Name })
                .ToListAsync(cancelToken);

            var units = await _dbContext.Units
                .AsNoTracking()
                .OrderBy(u => u.Name)
                .Select(u => new IdNamePair { Id = u.Id, Name = u.Name })
                .ToListAsync(cancelToken);

            var businessTypes = await _dbContext.BusinessTypes
                .AsNoTracking()
                .OrderBy(bt => bt.Name)
                .Select(bt => new IdNamePair { Id = bt.Id, Name = bt.Name })
                .ToListAsync(cancelToken);

            return (brands, suppliers, kosherStatuses, wpm, units, businessTypes);
        }

        // Global category tree (for SuperAdmin editor, not account)
        public async Task<List<Category>> GetGlobalCategoriesAsync(CancellationToken cancelToken)
        {
            // Category + CategoryHierarchy → build tree; for first pass just flat list:
            var cats = await _dbContext.Categories
                .AsNoTracking()
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancelToken);

            return cats;
        }

        public async Task<Category?> UpdateGlobalCategoryAsync(int categoryId, UpdateGlobalCategoryReq req, CancellationToken cancelToken)
        {
            var cat = await _dbContext.Categories
                .Where(c => c.Id == categoryId)
                .FirstOrDefaultAsync(cancelToken);

            if (cat == null) return null;

            if (req.Name.HasValue()) cat.Name = req.Name!;
            if (req.Slug.HasValue()) cat.Slug = req.Slug!;
            if (req.SortOrder.HasValue) cat.SortOrder = req.SortOrder.Value;
            if (req.IsActive.HasValue) cat.IsActive = req.IsActive.Value;

            await _dbContext.SaveChangesAsync(cancelToken);

            // parent change / hierarchy reorder is extra work:
            // need to update CategoryHierarchy table accordingly
            // (you can extend later)

            return cat;
        }

        public async Task<Category> CreateGlobalCategoryAsync(CreateGlobalCategoryReq req, CancellationToken cancelToken)
        {
            var cat = new Category
            {
                Name = req.Name,
                Slug = req.Slug,
                SortOrder = req.SortOrder ?? 0,
                IsActive = req.IsActive ?? true
            };

            _dbContext.Categories.Add(cat);
            await _dbContext.SaveChangesAsync(cancelToken);

            // optionally insert row into CategoryHierarchy if provided ParentCategoryId
            if (req.ParentCategoryId.HasValue)
            {
                _dbContext.CategoryHierarchies.Add(new CategoryHierarchy
                {
                    ParentCategoryId = req.ParentCategoryId.Value,
                    ChildCategoryId = cat.Id,
                    SortOrder = req.SortOrder ?? 0
                });

                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return cat;
        }
    }
}
