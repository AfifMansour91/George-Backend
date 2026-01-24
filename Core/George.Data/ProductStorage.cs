using George.Common;
using George.Common.Request;
using George.Data.Dto;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class ProductStorage : StorageBase
    {
        public ProductStorage(GeorgeDBContext dbContext, ILogger<ProductStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Product>> GetProductsAsync(
            ProductFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Product>();

            var query = _dbContext.Products
                .Include(p => p.Brand)
                .Include(p => p.Supplier)
                .Include(p => p.Status)
                .Include(p => p.Visibility)
                .Include(p => p.StockManagementType)
                .Include(p => p.StockStatus)
                .Include(p => p.ShippingClass)
                .Include(p => p.SetupType)
                .Include(p => p.WeightConfig)
                .Include(p => p.Sites)
                .Include(p => p.Tags)
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductOptions)
                    .ThenInclude(po => po.ProductOptionValues)
                .Include(p => p.ProductVariants)
                    .ThenInclude(pv => pv.ProductVariantOptionValues)
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.AccountId.HasValue)
                {
                    query = query.Where(p => p.AccountId == filter.AccountId.Value);
                }

                if (filter.SiteId.HasValue)
                {
                    query = query.Where(p => p.Sites.Any(s => s.Id == filter.SiteId.Value) || !p.Sites.Any());
                }

                if (filter.CategoryId.HasValue)
                {
                    query = query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == filter.CategoryId.Value));
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(p => p.Name.Contains(term) || 
                                           (p.Sku != null && p.Sku.Contains(term)) ||
                                           (p.ShortDescription != null && p.ShortDescription.Contains(term)));
                }

                if (filter.Status.HasValue())
                {
                    query = query.Where(p => p.Status != null && p.Status.Name == filter.Status);
                }
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query.OrderByDescending(p => p.CreationTime);

            //query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Product?> GetProductAsync(int productId, CancellationToken cancelToken)
        {
            return await _dbContext.Products
                .Include(p => p.Brand)
                .Include(p => p.Supplier)
                .Include(p => p.Status)
                .Include(p => p.Visibility)
                .Include(p => p.StockManagementType)
                .Include(p => p.StockStatus)
                .Include(p => p.ShippingClass)
                .Include(p => p.SetupType)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc.Unit)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc.UnitWeightMode)
                .Include(p => p.Sites)
                .Include(p => p.Tags)
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductOptions)
                    .ThenInclude(po => po.ProductOptionValues)
                .Include(p => p.ProductVariants)
                    .ThenInclude(pv => pv.ProductVariantOptionValues)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);
        }

        public async Task<Product> CreateProductAsync(Product product, List<int>? siteIds, List<int>? categoryIds, List<string>? tags, CancellationToken cancelToken)
        {
            // Normalize empty SKU to NULL to avoid unique constraint violations
            if (string.IsNullOrWhiteSpace(product.Sku))
            {
                product.Sku = null;
            }
            
            _dbContext.Products.Add(product);

            // Add sites
            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Sites
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                foreach (var site in sites)
                {
                    product.Sites.Add(site);
                }
            }

            // Add categories
            if (categoryIds != null && categoryIds.Any())
            {
                var categories = await _dbContext.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync(cancelToken);
                foreach (var category in categories)
                {
                    product.ProductCategories.Add(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = category.Id
                    });
                }
            }

            // Add tags
            if (tags != null && tags.Any())
            {
                foreach (var tagName in tags)
                {
                    var tag = await _dbContext.Tags
                        .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == product.AccountId, cancelToken);
                    
                    if (tag == null)
                    {
                        tag = new Tag
                        {
                            Name = tagName,
                            AccountId = product.AccountId,
                            CreationTime = DateTime.UtcNow
                        };
                        _dbContext.Tags.Add(tag);
                    }
                    product.Tags.Add(tag);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return product;
        }

        public async Task<Product?> UpdateProductAsync(Product updated, List<int>? siteIds, List<int>? categoryIds, List<string>? tags, CancellationToken cancelToken)
        {
            var dbProduct = await _dbContext.Products
                .Include(p => p.Sites)
                .Include(p => p.Tags)
                .Include(p => p.ProductCategories)
                .FirstOrDefaultAsync(p => p.Id == updated.Id, cancelToken);

            if (dbProduct == null) return null;

            // Update basic properties
            dbProduct.Name = updated.Name;
            dbProduct.ShortDescription = updated.ShortDescription;
            dbProduct.LongDescription = updated.LongDescription;
            dbProduct.Price = updated.Price;
            dbProduct.SalePrice = updated.SalePrice;
            dbProduct.SalePriceStartDate = updated.SalePriceStartDate;
            dbProduct.SalePriceEndDate = updated.SalePriceEndDate;
            dbProduct.CostPrice = updated.CostPrice;
            // Normalize empty SKU to NULL to avoid unique constraint violations
            dbProduct.Sku = string.IsNullOrWhiteSpace(updated.Sku) ? null : updated.Sku;
            dbProduct.StockQuantity = updated.StockQuantity;
            dbProduct.Weight = updated.Weight;
            dbProduct.IsKosher = updated.IsKosher;
            dbProduct.IsWeighted = updated.IsWeighted;
            dbProduct.BrandId = updated.BrandId;
            dbProduct.SupplierId = updated.SupplierId;
            dbProduct.StatusId = updated.StatusId;
            dbProduct.VisibilityId = updated.VisibilityId;
            dbProduct.StockManagementTypeId = updated.StockManagementTypeId;
            dbProduct.StockStatusId = updated.StockStatusId;
            dbProduct.ShippingClassId = updated.ShippingClassId;
            dbProduct.SetupTypeId = updated.SetupTypeId;
            dbProduct.WeightConfigId = updated.WeightConfigId;
            dbProduct.SeoTitle = updated.SeoTitle;
            dbProduct.SeoDescription = updated.SeoDescription;
            dbProduct.UpdatedDate = DateTime.UtcNow;
            dbProduct.UpdateUserId = updated.UpdateUserId;

            // Update sites
            if (siteIds != null)
            {
                dbProduct.Sites.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Sites
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);
                    foreach (var site in sites)
                    {
                        dbProduct.Sites.Add(site);
                    }
                }
            }

            // Update categories
            if (categoryIds != null)
            {
                dbProduct.ProductCategories.Clear();
                if (categoryIds.Any())
                {
                    var categories = await _dbContext.Categories
                        .Where(c => categoryIds.Contains(c.Id))
                        .ToListAsync(cancelToken);
                    foreach (var category in categories)
                    {
                        dbProduct.ProductCategories.Add(new ProductCategory
                        {
                            ProductId = dbProduct.Id,
                            CategoryId = category.Id
                        });
                    }
                }
            }

            // Update tags
            if (tags != null)
            {
                dbProduct.Tags.Clear();
                if (tags.Any())
                {
                    foreach (var tagName in tags)
                    {
                        var tag = await _dbContext.Tags
                            .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == dbProduct.AccountId, cancelToken);
                        
                        if (tag == null)
                        {
                            tag = new Tag
                            {
                                Name = tagName,
                                AccountId = dbProduct.AccountId,
                                CreationTime = DateTime.UtcNow
                            };
                            _dbContext.Tags.Add(tag);
                        }
                        dbProduct.Tags.Add(tag);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbProduct;
        }

        public async Task<bool> DeleteProductAsync(int productId, CancellationToken cancelToken)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);

            if (product == null) return false;

            product.IsDeleted = true;
            product.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> UpdateProductWooCommerceIdAsync(int productId, int? wooCommerceId, CancellationToken cancelToken)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);

            if (product == null) return false;

            product.WooCommerceId = wooCommerceId;
            product.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> UpdateProductVariantWooCommerceIdAsync(int variantId, int? wooCommerceVariationId, CancellationToken cancelToken)
        {
            var variant = await _dbContext.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == variantId, cancelToken);

            if (variant == null) return false;

            variant.WooCommerceVariationId = wooCommerceVariationId;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        // Helper methods for service layer
        public async Task CreateProductImagesAsync(int productId, List<string> imageUrls, CancellationToken cancelToken)
        {
            var existingImages = await _dbContext.ProductImages
                .Where(pi => pi.ProductId == productId)
                .ToListAsync(cancelToken);

            _dbContext.ProductImages.RemoveRange(existingImages);

            for (int i = 0; i < imageUrls.Count; i++)
            {
                _dbContext.ProductImages.Add(new ProductImage
                {
                    ProductId = productId,
                    Url = imageUrls[i],
                    SortOrder = i
                });
            }

            await _dbContext.SaveChangesAsync(cancelToken);
        }

        public async Task CreateProductOptionsAsync(int productId, List<ProductOptionDto> options, CancellationToken cancelToken)
        {
            // Get product's sites to create attributes for each site
            var product = await _dbContext.Products
                .Include(p => p.Sites)
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);
            
            var siteIds = product?.Sites?.Select(s => s.Id).ToList() ?? new List<int>();

            foreach (var opt in options)
            {
                var productOption = new ProductOption
                {
                    ProductId = productId,
                    Name = opt.Name,
                    IsDeleted = false
                };
                _dbContext.ProductOptions.Add(productOption);
                await _dbContext.SaveChangesAsync(cancelToken);

                if (opt.Values != null && opt.Values.Any())
                {
                    foreach (var value in opt.Values)
                    {
                        _dbContext.ProductOptionValues.Add(new ProductOptionValue
                        {
                            ProductOptionId = productOption.Id,
                            Value = value
                        });
                    }
                    await _dbContext.SaveChangesAsync(cancelToken);
                }

                // Create/find Attribute and AttributeValue for each site
                foreach (var siteId in siteIds)
                {
                    // Find or create Attribute (use fully qualified name to avoid ambiguity)
                    var attribute = await _dbContext.Attributes
                        .Include(a => a.AttributeValues)
                        .FirstOrDefaultAsync(a => a.Name == opt.Name && a.SiteId == siteId && !a.IsDeleted, cancelToken);

                    if (attribute == null)
                    {
                        attribute = new George.DB.Attribute
                        {
                            Name = opt.Name,
                            SiteId = siteId,
                            CreationTime = DateTime.UtcNow,
                            IsDeleted = false,
                            GuidId = Guid.NewGuid()
                        };
                        _dbContext.Attributes.Add(attribute);
                        await _dbContext.SaveChangesAsync(cancelToken);
                        
                        // Reload to get AttributeValues collection
                        attribute = await _dbContext.Attributes
                            .Include(a => a.AttributeValues)
                            .FirstOrDefaultAsync(a => a.Id == attribute.Id, cancelToken);
                    }

                    // Create AttributeValues for each option value
                    if (opt.Values != null && opt.Values.Any() && attribute != null)
                    {
                        foreach (var value in opt.Values)
                        {
                            // Check if AttributeValue already exists
                            var existingValue = attribute.AttributeValues
                                .FirstOrDefault(av => av.Value == value);

                            if (existingValue == null)
                            {
                                _dbContext.AttributeValues.Add(new AttributeValue
                                {
                                    AttributeId = attribute.Id,
                                    Value = value
                                });
                            }
                        }
                        await _dbContext.SaveChangesAsync(cancelToken);
                    }
                }
            }
        }

        public async Task UpdateProductOptionsAsync(int productId, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            if (options == null) return;

            var existingOptions = await _dbContext.ProductOptions
                .Where(po => po.ProductId == productId)
                .ToListAsync(cancelToken);

            foreach (var existing in existingOptions)
            {
                existing.IsDeleted = true;
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            await CreateProductOptionsAsync(productId, options, cancelToken);
        }

        public async Task CreateProductVariantsAsync(int productId, List<ProductVariantDto> variants, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            foreach (var variant in variants)
            {
                var productVariant = new ProductVariant
                {
                    ProductId = productId,
                    ImageUrl = variant.ImageUrl,
                    Price = variant.Price,
                    SalePrice = variant.SalePrice,
                    StockQuantity = variant.StockQuantity,
                    Sku = string.IsNullOrWhiteSpace(variant.Sku) ? null : variant.Sku,
                    Weight = variant.Weight,
                    IsDeleted = false
                };
                _dbContext.ProductVariants.Add(productVariant);
                await _dbContext.SaveChangesAsync(cancelToken);

                // Map option values if provided
                if (variant.OptionValues != null && variant.OptionValues.Any())
                {
                    foreach (var kvp in variant.OptionValues)
                    {
                        _dbContext.ProductVariantOptionValues.Add(new ProductVariantOptionValue
                        {
                            ProductVariantId = productVariant.Id,
                            OptionName = kvp.Key,
                            OptionValue = kvp.Value
                        });
                    }
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
            }
        }

        public async Task UpdateProductVariantsAsync(int productId, List<ProductVariantDto>? variants, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            if (variants == null) return;

            var existingVariants = await _dbContext.ProductVariants
                .Where(pv => pv.ProductId == productId)
                .ToListAsync(cancelToken);

            foreach (var existing in existingVariants)
            {
                existing.IsDeleted = true;
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            await CreateProductVariantsAsync(productId, variants, options, cancelToken);
        }

        public async Task<Product?> GetProductBySkuAsync(string sku, int? accountId, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;

            var query = _dbContext.Products
                .Where(p => !p.IsDeleted && p.Sku != null && p.Sku.ToLower().Trim() == sku.ToLower().Trim());

            if (accountId.HasValue)
            {
                query = query.Where(p => p.AccountId == accountId.Value);
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
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
                var unit = await _dbContext.Units
                    .FirstOrDefaultAsync(u => u.Name == req.Unit, cancelToken);
                weightConfig.UnitId = unit?.Id;
            }

            if (req.UnitWeightMode.HasValue())
            {
                var mode = await _dbContext.UnitWeightModes
                    .FirstOrDefaultAsync(m => m.Name == req.UnitWeightMode, cancelToken);
                weightConfig.UnitWeightModeId = mode?.Id;
            }

            _dbContext.WeightConfigs.Add(weightConfig);
            await _dbContext.SaveChangesAsync(cancelToken);
            return weightConfig;
        }

        public async Task MapLookupsAsync(Product product, ProductLookupDto req, CancellationToken cancelToken)
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

                var status = await _dbContext.ProductStatuses
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == statusName.ToLower().Trim(), cancelToken);
                product.StatusId = status?.Id;
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

                var visibility = await _dbContext.Visibilities
                    .FirstOrDefaultAsync(v => v.Name.ToLower() == visibilityName.ToLower().Trim(), cancelToken);
                product.VisibilityId = visibility?.Id;
            }

            // Map stock management type
            if (req.StockManagementType.HasValue())
            {
                var smt = await _dbContext.StockManagementTypes
                    .FirstOrDefaultAsync(s => s.Name == req.StockManagementType && !s.IsDeleted, cancelToken);
                
                if (smt == null)
                {
                    // Create the stock management type if it doesn't exist
                    smt = new StockManagementType
                    {
                        Name = req.StockManagementType,
                        IsDeleted = false
                    };
                    _dbContext.StockManagementTypes.Add(smt);
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
                
                product.StockManagementTypeId = smt.Id;
            }

            // Map stock status
            if (req.StockStatus.HasValue())
            {
                var ss = await _dbContext.StockStatuses
                    .FirstOrDefaultAsync(s => s.Name == req.StockStatus, cancelToken);
                product.StockStatusId = ss?.Id;
            }

            // Map shipping class
            if (req.ShippingClass.HasValue())
            {
                var sc = await _dbContext.ShippingClasses
                    .FirstOrDefaultAsync(s => s.Name == req.ShippingClass, cancelToken);
                product.ShippingClassId = sc?.Id;
            }

            // Map setup type
            if (req.SetupType.HasValue())
            {
                var st = await _dbContext.SetupTypes
                    .FirstOrDefaultAsync(s => s.Name == req.SetupType, cancelToken);
                product.SetupTypeId = st?.Id;
            }

            // Map brand
            if (req.Brand.HasValue())
            {
                var brand = await _dbContext.Brands
                    .FirstOrDefaultAsync(b => b.Name == req.Brand && b.AccountId == product.AccountId, cancelToken);
                product.BrandId = brand?.Id;
            }

            // Map supplier
            if (req.Supplier.HasValue())
            {
                var supplier = await _dbContext.Suppliers
                    .FirstOrDefaultAsync(s => s.Name == req.Supplier && s.AccountId == product.AccountId, cancelToken);
                product.SupplierId = supplier?.Id;
            }

            // Map weight config
            if (req.WeightConfig != null)
            {
                var weightConfig = await CreateOrUpdateWeightConfigAsync(req.WeightConfig, cancelToken);
                product.WeightConfigId = weightConfig?.Id;
            }
        }
    }
}

