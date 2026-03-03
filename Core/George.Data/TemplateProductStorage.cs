using George.Common;
using George.Common.Request;
using George.Data.Dto;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class TemplateProductStorage : StorageBase
    {
        public TemplateProductStorage(GeorgeDBContext dbContext, ILogger<TemplateProductStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<TemplateProduct>> GetTemplateProductsAsync(
            TemplateProductFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<TemplateProduct>();
            bool lightList = filter?.LightList == true;

            IQueryable<TemplateProduct> query = _dbContext.TemplateProduct
                .Include(tp => tp.Brand)
                .Include(tp => tp.Supplier)
                .Include(tp => tp.Status)
                .Include(tp => tp.Visibility)
                .Include(tp => tp.StockManagementType)
                .Include(tp => tp.StockStatus)
                .Include(tp => tp.ShippingClass)
                .Include(tp => tp.SetupType)
                .Include(tp => tp.WeightConfig)
                    .ThenInclude(wc => wc.Unit)
                .Include(tp => tp.WeightConfig)
                    .ThenInclude(wc => wc.UnitWeightMode)
                .Include(tp => tp.Site)
                .Include(tp => tp.Tag)
                .Include(tp => tp.TemplateProductCategory)
                    .ThenInclude(tpc => tpc.GlobalCategory)
                .Include(tp => tp.TemplateProductImage)
                    .ThenInclude(tpi => tpi.Media)
                .Include(tp => tp.TemplateProductOption)
                    .ThenInclude(tpo => tpo.TemplateProductOptionValue)
                .Include(tp => tp.TemplateProductVariant)
                    .ThenInclude(tpv => tpv.TemplateProductVariantOptionValue);

            if (!lightList)
            {
                query = query
                    .Include(tp => tp.RelatedTemplateProduct)
                    .Include(tp => tp.ComplementaryTemplateProduct);
            }

            query = query.AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.TemplateId.HasValue())
                {
                    query = query.Where(tp => tp.TemplateId == filter.TemplateId);
                }

                if (filter.SiteId.HasValue)
                {
                    query = query.Where(tp => tp.Site.Any(s => s.Id == filter.SiteId.Value) || !tp.Site.Any());
                }

                if (filter.GlobalCategoryId.HasValue)
                {
                    query = query.Where(tp => tp.TemplateProductCategory.Any(tpc =>
                        tpc.GlobalCategoryId == filter.GlobalCategoryId.Value));
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(tp => tp.Name.Contains(term) ||
                                           (tp.Sku != null && tp.Sku.Contains(term)) ||
                                           (tp.ShortDescription != null && tp.ShortDescription.Contains(term)));
                }

                if (filter.Status.HasValue())
                {
                    query = query.Where(tp => tp.Status != null && tp.Status.Name == filter.Status);
                }
            }

            // Only get non-deleted template products
            query = query.Where(tp => !tp.IsDeleted);

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query
                .OrderBy(tp => tp.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(tp => tp.CreationTime);

            query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<TemplateProduct?> GetTemplateProductBySkuAsync(string sku, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;

            return await _dbContext.TemplateProduct
                .Where(tp => !tp.IsDeleted && tp.Sku != null && tp.Sku.ToLower().Trim() == sku.ToLower().Trim())
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>Finds a template product by name (case-insensitive). Used as fallback when re-importing and SKU is empty.</summary>
        public async Task<TemplateProduct?> GetTemplateProductByNameAsync(string name, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            return await _dbContext.TemplateProduct
                .Where(tp => !tp.IsDeleted && tp.Name != null && tp.Name.Trim().ToLower() == name.Trim().ToLower())
                .OrderBy(tp => tp.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        public async Task<TemplateProduct?> GetTemplateProductAsync(int templateProductId, CancellationToken cancelToken)
        {
            return await _dbContext.TemplateProduct
                .Include(tp => tp.Brand)
                .Include(tp => tp.Supplier)
                .Include(tp => tp.Status)
                .Include(tp => tp.Visibility)
                .Include(tp => tp.StockManagementType)
                .Include(tp => tp.StockStatus)
                .Include(tp => tp.ShippingClass)
                .Include(tp => tp.SetupType)
                .Include(tp => tp.WeightConfig)
                    .ThenInclude(wc => wc.Unit)
                .Include(tp => tp.WeightConfig)
                    .ThenInclude(wc => wc.UnitWeightMode)
                .Include(tp => tp.Site)
                .Include(tp => tp.Tag)
                .Include(tp => tp.TemplateProductCategory)
                    .ThenInclude(tpc => tpc.GlobalCategory)
                .Include(tp => tp.TemplateProductImage)
                    .ThenInclude(tpi => tpi.Media)
                .Include(tp => tp.TemplateProductOption)
                    .ThenInclude(tpo => tpo.TemplateProductOptionValue)
                .Include(tp => tp.TemplateProductVariant)
                    .ThenInclude(tpv => tpv.TemplateProductVariantOptionValue)
                .Include(tp => tp.RelatedTemplateProduct)
                .Include(tp => tp.ComplementaryTemplateProduct)
                .AsNoTracking()
                .FirstOrDefaultAsync(tp => tp.Id == templateProductId && !tp.IsDeleted, cancelToken);
        }

        public async Task<TemplateProduct> CreateTemplateProductAsync(
            TemplateProduct templateProduct,
            List<int>? siteIds,
            List<int>? globalCategoryIds,
            List<string>? tags,
            List<int>? relatedProductIds,
            List<int>? complementaryProductIds,
            CancellationToken cancelToken)
        {
            // Normalize empty SKU to NULL for consistency
            if (string.IsNullOrWhiteSpace(templateProduct.Sku))
            {
                templateProduct.Sku = null;
            }
            // New products appear at the top of the list (sort by DisplayOrder ascending, then CreationTime desc)
            templateProduct.DisplayOrder = 0;
            _dbContext.TemplateProduct.Add(templateProduct);

            // Add sites
            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Site
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                foreach (var site in sites)
                {
                    templateProduct.Site.Add(site);
                }
            }

            // Add categories (map GlobalCategory IDs to Category IDs)
            if (globalCategoryIds != null && globalCategoryIds.Any())
            {
                // Find Categories that have SourceGlobalCategoryId matching the GlobalCategory IDs
                var categories = await _dbContext.GlobalCategory
                    .Where(c => globalCategoryIds.Contains(c.Id))
                    .ToListAsync(cancelToken);

                foreach (var category in categories)
                {
                    templateProduct.TemplateProductCategory.Add(new TemplateProductCategory
                    {
                        TemplateProductId = templateProduct.Id,
                        GlobalCategoryId = category.Id,
                        IsPrimary = false
                    });
                }
            }

            // Add tags
            if (tags != null && tags.Any())
            {
                foreach (var tagName in tags)
                {
                    // For template products, tags might not have AccountId, so we search more broadly
                    var tag = await _dbContext.Tag
                        .FirstOrDefaultAsync(t => t.Name == tagName, cancelToken);

                    if (tag == null)
                    {
                        tag = new Tag
                        {
                            Name = tagName,
                            AccountId = null, // Template products are global
                            CreationTime = DateTime.UtcNow
                        };
                        _dbContext.Tag.Add(tag);
                    }
                    templateProduct.Tag.Add(tag);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);

            // Add related products (נלווים) and complementary products (מוצרים משלימים) after we have templateProduct.Id
            if ((relatedProductIds != null && relatedProductIds.Any()) || (complementaryProductIds != null && complementaryProductIds.Any()))
            {
                var dbProduct = await _dbContext.TemplateProduct
                    .Include(tp => tp.RelatedTemplateProduct)
                    .Include(tp => tp.ComplementaryTemplateProduct)
                    .FirstAsync(tp => tp.Id == templateProduct.Id, cancelToken);
                if (relatedProductIds != null)
                {
                    foreach (var id in relatedProductIds.Where(id => id != templateProduct.Id))
                    {
                        var related = await _dbContext.TemplateProduct.FindAsync(new object[] { id }, cancelToken);
                        if (related != null && !related.IsDeleted)
                            dbProduct.RelatedTemplateProduct.Add(related);
                    }
                }
                if (complementaryProductIds != null)
                {
                    foreach (var id in complementaryProductIds.Where(id => id != templateProduct.Id))
                    {
                        var comp = await _dbContext.TemplateProduct.FindAsync(new object[] { id }, cancelToken);
                        if (comp != null && !comp.IsDeleted)
                            dbProduct.ComplementaryTemplateProduct.Add(comp);
                    }
                }
                await _dbContext.SaveChangesAsync(cancelToken);
                templateProduct = await GetTemplateProductAsync(templateProduct.Id, cancelToken) ?? templateProduct;
            }

            return templateProduct;
        }

        public async Task<TemplateProduct?> UpdateTemplateProductAsync(
            TemplateProduct updated,
            List<int>? siteIds,
            List<int>? globalCategoryIds,
            List<string>? tags,
            List<int>? relatedProductIds,
            List<int>? complementaryProductIds,
            CancellationToken cancelToken)
        {
            var dbTemplateProduct = await _dbContext.TemplateProduct
                .Include(tp => tp.Site)
                .Include(tp => tp.Tag)
                .Include(tp => tp.TemplateProductCategory)
                .Include(tp => tp.RelatedTemplateProduct)
                .Include(tp => tp.ComplementaryTemplateProduct)
                .FirstOrDefaultAsync(tp => tp.Id == updated.Id && !tp.IsDeleted, cancelToken);

            if (dbTemplateProduct == null) return null;

            // Update basic properties
            dbTemplateProduct.TemplateId = updated.TemplateId;
            dbTemplateProduct.Name = updated.Name;
            dbTemplateProduct.ShortDescription = updated.ShortDescription;
            dbTemplateProduct.LongDescription = updated.LongDescription;
            dbTemplateProduct.Price = updated.Price;
            dbTemplateProduct.SalePrice = updated.SalePrice;
            dbTemplateProduct.SalePriceStartDate = updated.SalePriceStartDate;
            dbTemplateProduct.SalePriceEndDate = updated.SalePriceEndDate;
            dbTemplateProduct.CostPrice = updated.CostPrice;
            // Normalize empty SKU to NULL for consistency
            dbTemplateProduct.Sku = string.IsNullOrWhiteSpace(updated.Sku) ? null : updated.Sku;
            dbTemplateProduct.StockQuantity = updated.StockQuantity;
            dbTemplateProduct.Weight = updated.Weight;
            dbTemplateProduct.IsKosher = updated.IsKosher;
            dbTemplateProduct.IsWeighted = updated.IsWeighted;
            dbTemplateProduct.BrandId = updated.BrandId;
            dbTemplateProduct.SupplierId = updated.SupplierId;
            dbTemplateProduct.StatusId = updated.StatusId;
            dbTemplateProduct.VisibilityId = updated.VisibilityId;
            dbTemplateProduct.StockManagementTypeId = updated.StockManagementTypeId;
            dbTemplateProduct.StockStatusId = updated.StockStatusId;
            dbTemplateProduct.ShippingClassId = updated.ShippingClassId;
            dbTemplateProduct.SetupTypeId = updated.SetupTypeId;
            dbTemplateProduct.WeightConfigId = updated.WeightConfigId;
            dbTemplateProduct.SeoTitle = updated.SeoTitle;
            dbTemplateProduct.SeoDescription = updated.SeoDescription;
            dbTemplateProduct.SourceProductId = updated.SourceProductId;
            dbTemplateProduct.UpdatedDate = DateTime.UtcNow;
            dbTemplateProduct.UpdateUserId = updated.UpdateUserId;

            // Update sites
            if (siteIds != null)
            {
                dbTemplateProduct.Site.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Site
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);
                    foreach (var site in sites)
                    {
                        dbTemplateProduct.Site.Add(site);
                    }
                }
            }

            // Update categories (map GlobalCategory IDs to Category IDs)
            if (globalCategoryIds != null)
            {
                dbTemplateProduct.TemplateProductCategory.Clear();
                if (globalCategoryIds.Any())
                {
                    // Find Categories that have SourceGlobalCategoryId matching the GlobalCategory IDs
                    var categories = await _dbContext.GlobalCategory
                        .Where(c => globalCategoryIds.Contains(c.Id))
                        .ToListAsync(cancelToken);

                    foreach (var category in categories)
                    {
                        dbTemplateProduct.TemplateProductCategory.Add(new TemplateProductCategory
                        {
                            TemplateProductId = dbTemplateProduct.Id,
                            GlobalCategoryId = category.Id,
                            IsPrimary = false
                        });
                    }
                }
            }

            // Update tags
            if (tags != null)
            {
                dbTemplateProduct.Tag.Clear();
                if (tags.Any())
                {
                    foreach (var tagName in tags)
                    {
                        var tag = await _dbContext.Tag
                            .FirstOrDefaultAsync(t => t.Name == tagName, cancelToken);

                        if (tag == null)
                        {
                            tag = new Tag
                            {
                                Name = tagName,
                                AccountId = null,
                                CreationTime = DateTime.UtcNow
                            };
                            _dbContext.Tag.Add(tag);
                        }
                        dbTemplateProduct.Tag.Add(tag);
                    }
                }
            }

            // Update related products (נלווים)
            if (relatedProductIds != null)
            {
                dbTemplateProduct.RelatedTemplateProduct.Clear();
                foreach (var id in relatedProductIds.Where(id => id != dbTemplateProduct.Id))
                {
                    var related = await _dbContext.TemplateProduct.FindAsync(new object[] { id }, cancelToken);
                    if (related != null && !related.IsDeleted)
                        dbTemplateProduct.RelatedTemplateProduct.Add(related);
                }
            }

            // Update complementary products (מוצרים משלימים)
            if (complementaryProductIds != null)
            {
                dbTemplateProduct.ComplementaryTemplateProduct.Clear();
                foreach (var id in complementaryProductIds.Where(id => id != dbTemplateProduct.Id))
                {
                    var comp = await _dbContext.TemplateProduct.FindAsync(new object[] { id }, cancelToken);
                    if (comp != null && !comp.IsDeleted)
                        dbTemplateProduct.ComplementaryTemplateProduct.Add(comp);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbTemplateProduct;
        }

        public async Task<bool> DeleteTemplateProductAsync(int templateProductId, CancellationToken cancelToken)
        {
            var templateProduct = await _dbContext.TemplateProduct
                .FirstOrDefaultAsync(tp => tp.Id == templateProductId && !tp.IsDeleted, cancelToken);

            if (templateProduct == null) return false;

            templateProduct.IsDeleted = true;
            templateProduct.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>Sets DisplayOrder for the given template product IDs (order in list = index). Single query + single save for performance.</summary>
        public async Task UpdateTemplateProductOrderAsync(List<int> templateProductIds, CancellationToken cancelToken)
        {
            if (templateProductIds == null || !templateProductIds.Any()) return;
            var list = await _dbContext.TemplateProduct
                .Where(p => templateProductIds.Contains(p.Id))
                .ToListAsync(cancelToken);
            var idToIndex = templateProductIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var now = DateTime.UtcNow;
            foreach (var tp in list)
            {
                if (idToIndex.TryGetValue(tp.Id, out int order))
                {
                    tp.DisplayOrder = order;
                    tp.UpdatedDate = now;
                }
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        // Helper methods for service layer. Each item has Url and optional MediaId (template products typically use Url only).
        public async Task CreateTemplateProductImagesAsync(int templateProductId, List<(string Url, int? MediaId)> images, CancellationToken cancelToken)
        {
            var existingImages = await _dbContext.TemplateProductImage
                .Where(tpi => tpi.TemplateProductId == templateProductId)
                .ToListAsync(cancelToken);

            _dbContext.TemplateProductImage.RemoveRange(existingImages);

            for (int i = 0; i < images.Count; i++)
            {
                var (url, mediaId) = images[i];
                _dbContext.TemplateProductImage.Add(new TemplateProductImage
                {
                    TemplateProductId = templateProductId,
                    Url = url,
                    MediaId = mediaId,
                    SortOrder = i
                });
            }

            await _dbContext.SaveChangesAsync(cancelToken);
        }

        public async Task CreateTemplateProductOptionsAsync(int templateProductId, List<ProductOptionDto> options, CancellationToken cancelToken)
        {
            // Get template product's sites to create attributes for each site
            var templateProduct = await _dbContext.TemplateProduct
                .Include(tp => tp.Site)
                .FirstOrDefaultAsync(tp => tp.Id == templateProductId, cancelToken);
            
            var siteIds = templateProduct?.Site?.Select(s => s.Id).ToList() ?? new List<int>();

            foreach (var opt in options)
            {
                var templateProductOption = new TemplateProductOption
                {
                    TemplateProductId = templateProductId,
                    Name = opt.Name,
                    IsDeleted = false
                };
                _dbContext.TemplateProductOption.Add(templateProductOption);
                await _dbContext.SaveChangesAsync(cancelToken);

                if (opt.Values != null && opt.Values.Any())
                {
                    foreach (var value in opt.Values)
                    {
                        _dbContext.TemplateProductOptionValue.Add(new TemplateProductOptionValue
                        {
                            TemplateProductOptionId = templateProductOption.Id,
                            Value = value
                        });
                    }
                    await _dbContext.SaveChangesAsync(cancelToken);
                }

                // Create/find TemplateAttribute and TemplateAttributeValue for each site
                foreach (var siteId in siteIds)
                {
                    // Find or create TemplateAttribute
                    var templateAttribute = await _dbContext.TemplateAttribute
                        .Include(ta => ta.TemplateAttributeValue)
                        .Include(ta => ta.Site)
                        .FirstOrDefaultAsync(ta => ta.Name == opt.Name && !ta.IsDeleted, cancelToken);

                    if (templateAttribute == null)
                    {
                        templateAttribute = new TemplateAttribute
                        {
                            Name = opt.Name,
                            CreationTime = DateTime.UtcNow,
                            IsDeleted = false,
                            GuidId = Guid.NewGuid()
                        };
                        _dbContext.TemplateAttribute.Add(templateAttribute);
                        await _dbContext.SaveChangesAsync(cancelToken);
                    }

                    // Add site to TemplateAttribute if not already added
                    if (!templateAttribute.Site.Any(s => s.Id == siteId))
                    {
                        var site = await _dbContext.Site.FindAsync(new object[] { siteId }, cancelToken);
                        if (site != null)
                        {
                            templateAttribute.Site.Add(site);
                            await _dbContext.SaveChangesAsync(cancelToken);
                        }
                    }

                    // Create TemplateAttributeValues for each option value
                    if (opt.Values != null && opt.Values.Any())
                    {
                        foreach (var value in opt.Values)
                        {
                            // Check if TemplateAttributeValue already exists
                            var existingValue = templateAttribute.TemplateAttributeValue
                                .FirstOrDefault(tav => tav.Value == value);

                            if (existingValue == null)
                            {
                                _dbContext.TemplateAttributeValue.Add(new TemplateAttributeValue
                                {
                                    TemplateAttributeId = templateAttribute.Id,
                                    Value = value
                                });
                            }
                        }
                        await _dbContext.SaveChangesAsync(cancelToken);
                    }
                }
            }
        }

        public async Task UpdateTemplateProductOptionsAsync(int templateProductId, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            if (options == null) return;

            var existingOptions = await _dbContext.TemplateProductOption
                .Where(tpo => tpo.TemplateProductId == templateProductId)
                .ToListAsync(cancelToken);

            foreach (var existing in existingOptions)
            {
                existing.IsDeleted = true;
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            await CreateTemplateProductOptionsAsync(templateProductId, options, cancelToken);
        }

        public async Task CreateTemplateProductVariantsAsync(int templateProductId, List<ProductVariantDto> variants, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            foreach (var variant in variants)
            {
                var templateProductVariant = new TemplateProductVariant
                {
                    TemplateProductId = templateProductId,
                    ImageUrl = variant.ImageUrl,
                    Price = variant.Price,
                    SalePrice = variant.SalePrice,
                    StockQuantity = variant.StockQuantity,
                    Sku = string.IsNullOrWhiteSpace(variant.Sku) ? null : variant.Sku,
                    Weight = variant.Weight,
                    IsDeleted = false
                };
                _dbContext.TemplateProductVariant.Add(templateProductVariant);
                await _dbContext.SaveChangesAsync(cancelToken);

                // Map option values if provided
                if (variant.OptionValues != null && variant.OptionValues.Any())
                {
                    foreach (var kvp in variant.OptionValues)
                    {
                        _dbContext.TemplateProductVariantOptionValue.Add(new TemplateProductVariantOptionValue
                        {
                            TemplateProductVariantId = templateProductVariant.Id,
                            OptionName = kvp.Key,
                            OptionValue = kvp.Value
                        });
                    }
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
            }
        }

        public async Task UpdateTemplateProductVariantsAsync(int templateProductId, List<ProductVariantDto>? variants, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            if (variants == null) return;

            var existingVariants = await _dbContext.TemplateProductVariant
                .Where(tpv => tpv.TemplateProductId == templateProductId)
                .ToListAsync(cancelToken);

            foreach (var existing in existingVariants)
            {
                existing.IsDeleted = true;
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            await CreateTemplateProductVariantsAsync(templateProductId, variants, options, cancelToken);
        }

        public async Task<WeightConfig?> CreateOrUpdateWeightConfigAsync(WeightConfigDto req, CancellationToken cancelToken)
        {
            var weightConfig = new WeightConfig
            {
                StartWeight = req.StartWeight,
                Step = req.Step,
                FixedWeightPerUnit = req.FixedWeightPerUnit,
                UnitWeight = req.UnitWeight,
                WeightOptions = req.WeightOptions,
                WeightByVariant = req.WeightByVariant,
                ShowPricePer100g = req.ShowPricePer100g,
                ShowUnitPrice = req.ShowUnitPrice,
                IsDeleted = false
            };

            if (req.Unit.HasValue())
            {
                var unit = await _dbContext.Unit
                    .FirstOrDefaultAsync(u => u.Name == req.Unit, cancelToken);
                weightConfig.UnitId = unit?.Id;
            }

            if (req.UnitWeightMode.HasValue())
            {
                var mode = await _dbContext.UnitWeightMode
                    .FirstOrDefaultAsync(m => m.Name == req.UnitWeightMode, cancelToken);
                weightConfig.UnitWeightModeId = mode?.Id;
            }

            _dbContext.WeightConfig.Add(weightConfig);
            await _dbContext.SaveChangesAsync(cancelToken);
            return weightConfig;
        }

        public async Task MapLookupsAsync(TemplateProduct templateProduct, ProductLookupDto req, CancellationToken cancelToken)
        {
            // Map status
            if (req.Status.HasValue())
            {
                var statusName = req.Status;
                // Normalize status names from client
                if (statusName == "public") statusName = "active";
                if (statusName == "published") statusName = "active";
                if (statusName == "draft") statusName = "hidden";
                if (statusName == "archived") statusName = "hidden";

                var status = await _dbContext.ProductStatus
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == statusName.ToLower().Trim(), cancelToken);
                templateProduct.StatusId = status?.Id;
            }

            // Map visibility
            if (req.Visibility.HasValue())
            {
                var visibilityName = req.Visibility;
                // Normalize visibility names from client
                if (visibilityName == "public") visibilityName = "active";
                if (visibilityName == "published") visibilityName = "active";
                if (visibilityName == "draft") visibilityName = "hidden";
                if (visibilityName == "archived") visibilityName = "hidden";

                var visibility = await _dbContext.Visibility
                    .FirstOrDefaultAsync(v => v.Name.ToLower() == visibilityName.ToLower().Trim(), cancelToken);
                templateProduct.VisibilityId = visibility?.Id;
            }

            // Map stock management type
            if (req.StockManagementType.HasValue())
            {
                var smt = await _dbContext.StockManagementType
                    .FirstOrDefaultAsync(s => s.Name == req.StockManagementType && !s.IsDeleted, cancelToken);
                
                if (smt == null)
                {
                    // Create the stock management type if it doesn't exist
                    smt = new StockManagementType
                    {
                        Name = req.StockManagementType,
                        IsDeleted = false
                    };
                    _dbContext.StockManagementType.Add(smt);
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
                
                templateProduct.StockManagementTypeId = smt.Id;
            }

            // Map stock status
            if (req.StockStatus.HasValue())
            {
                var ss = await _dbContext.StockStatus
                    .FirstOrDefaultAsync(s => s.Name == req.StockStatus, cancelToken);
                templateProduct.StockStatusId = ss?.Id;
            }

            // Map shipping class
            if (req.ShippingClass.HasValue())
            {
                var sc = await _dbContext.ShippingClass
                    .FirstOrDefaultAsync(s => s.Name == req.ShippingClass, cancelToken);
                templateProduct.ShippingClassId = sc?.Id;
            }

            // Map setup type
            if (req.SetupType.HasValue())
            {
                var st = await _dbContext.SetupType
                    .FirstOrDefaultAsync(s => s.Name == req.SetupType, cancelToken);
                templateProduct.SetupTypeId = st?.Id;
            }

            // Map brand (template products might not have AccountId, so search more broadly)
            if (req.Brand.HasValue())
            {
                var brand = await _dbContext.Brand
                    .FirstOrDefaultAsync(b => b.Name == req.Brand && !b.IsDeleted, cancelToken);
                
                if (brand == null)
                {
                    // Create brand if it doesn't exist (for template products, AccountId is null)
                    brand = new Brand
                    {
                        Name = req.Brand.Trim(),
                        AccountId = null, // Template products use global brands
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                        CreationUserId = null
                    };
                    _dbContext.Brand.Add(brand);
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
                templateProduct.BrandId = brand.Id;
            }
            else
            {
                // If empty or null, set to null
                templateProduct.BrandId = null;
            }

            // Map supplier
            if (req.Supplier.HasValue())
            {
                var supplier = await _dbContext.Supplier
                    .FirstOrDefaultAsync(s => s.Name == req.Supplier && !s.IsDeleted, cancelToken);
                
                if (supplier == null)
                {
                    // Create supplier if it doesn't exist (for template products, AccountId is null)
                    supplier = new Supplier
                    {
                        Name = req.Supplier.Trim(),
                        AccountId = null, // Template products use global suppliers
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                        CreationUserId = null
                    };
                    _dbContext.Supplier.Add(supplier);
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
                templateProduct.SupplierId = supplier.Id;
            }
            else
            {
                // If empty or null, set to null
                templateProduct.SupplierId = null;
            }

            // Map weight config
            if (req.WeightConfig != null)
            {
                var weightConfig = await CreateOrUpdateWeightConfigAsync(req.WeightConfig, cancelToken);
                templateProduct.WeightConfigId = weightConfig?.Id;
            }
        }
    }
}

