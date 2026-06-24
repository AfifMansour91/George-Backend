using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Data.Dto;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace George.Services
{
    public class ProductService : ServiceBase
    {
        private readonly ProductStorage _productStorage;
        private readonly CategoryStorage _categoryStorage;
        private readonly UserStorage _userStorage;
        private readonly MediaStorage _mediaStorage;
        private readonly WooCommerceService _wooCommerceService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly OrderStorage _orderStorage;
        private readonly ProductSiteOverrideStorage _overrideStorage;

        public ProductService(
            ILogger<ProductService> logger,
            IMapper mapper,
            CacheManager cache,
            ProductStorage productStorage,
            CategoryStorage categoryStorage,
            UserStorage userStorage,
            MediaStorage mediaStorage,
            WooCommerceService wooCommerceService,
            IServiceScopeFactory serviceScopeFactory,
            OrderStorage orderStorage,
            ProductSiteOverrideStorage overrideStorage
        ) : base(logger, mapper, cache)
        {
            _productStorage = productStorage;
            _categoryStorage = categoryStorage;
            _userStorage = userStorage;
            _mediaStorage = mediaStorage;
            _wooCommerceService = wooCommerceService;
            _serviceScopeFactory = serviceScopeFactory;
            _orderStorage = orderStorage;
            _overrideStorage = overrideStorage;
        }

        public async Task<IApiResponse<ApiListResponse<ProductRes>>> GetProductsAsync(
            ApiListReq<ProductFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<ProductRes>>
            {
                Data = new ApiListResponse<ProductRes>()
            };

            var res = await _productStorage.GetProductsAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(p => MapProductToRes(p));

            // MultiSite Phase 2: when listing for a specific site, overlay that site's per-site overrides
            // (price, stock, availability, exclusion) so the list shows the effective values for the branch.
            if (request.Filter?.SiteId is int listSiteId && listSiteId > 0 && response.Data.Items.Count > 0)
            {
                await ApplyEffectiveSiteValuesAsync(response.Data.Items, listSiteId, cancelToken);
            }

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        /// <summary>MultiSite Phase 2: overlay per-site override values onto a list of mapped products for one site.</summary>
        private async Task ApplyEffectiveSiteValuesAsync(List<ProductRes> items, int siteId, CancellationToken cancelToken)
        {
            var ids = items.Select(i => i.Id).ToList();
            var overrides = await _overrideStorage.GetOverridesForSiteAsync(ids, siteId, cancelToken);
            var byProductId = overrides
                .GroupBy(o => o.ProductId)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var item in items)
            {
                if (!byProductId.TryGetValue(item.Id, out var ov)) continue;
                item.IsExcludedForSite = ov.IsExcluded;
                if (!string.IsNullOrEmpty(ov.Name)) item.Name = ov.Name;
                if (ov.ShortDescription != null) item.ShortDescription = ov.ShortDescription;
                if (ov.LongDescription != null) item.LongDescription = ov.LongDescription;
                if (ov.Weight.HasValue) item.Weight = ov.Weight;
                if (!string.IsNullOrEmpty(ov.WeightUnit)) item.WeightUnit = ov.WeightUnit;
                if (!string.IsNullOrEmpty(ov.Sku)) item.Sku = ov.Sku;
                if (!string.IsNullOrEmpty(ov.SeoTitle)) item.SeoTitle = ov.SeoTitle;
                if (ov.SeoDescription != null) item.SeoDescription = ov.SeoDescription;
                if (ov.Price.HasValue) item.Price = ov.Price;
                if (ov.SalePrice.HasValue) item.SalePrice = ov.SalePrice;
                if (ov.SalePriceStartDate.HasValue) item.SalePriceStartDate = ov.SalePriceStartDate;
                if (ov.SalePriceEndDate.HasValue) item.SalePriceEndDate = ov.SalePriceEndDate;
                if (ov.StockQuantity.HasValue) item.StockQuantity = ov.StockQuantity;
                if (ov.VariationStockByQuantity.HasValue) item.VariationStockByQuantity = ov.VariationStockByQuantity;
                if (ov.LowStockThreshold.HasValue) item.LowStockThreshold = ov.LowStockThreshold;
                if (!string.IsNullOrEmpty(ov.StockStatus)) item.StockStatus = ov.StockStatus;
                if (!string.IsNullOrEmpty(ov.StockManagementType)) item.StockManagementType = ov.StockManagementType;
            }

            // Per-site category assignment: when a product has site categories, they replace the canonical ones.
            var siteCats = await _overrideStorage.GetSiteCategoriesAsync(ids, siteId, cancelToken);
            if (siteCats.Count > 0)
            {
                foreach (var grp in siteCats.GroupBy(c => c.ProductId))
                {
                    var item = items.FirstOrDefault(i => i.Id == grp.Key);
                    if (item == null) continue;
                    item.CategoryIds = grp.Where(c => c.ParentCategoryId == null).Select(c => c.CategoryId).ToList();
                    item.SubcategoryIds = grp.Where(c => c.ParentCategoryId != null).Select(c => c.CategoryId).ToList();
                }
            }

            // Per-site images: when a product has site images, they replace the canonical ones.
            var siteImages = await _overrideStorage.GetSiteImagesAsync(ids, siteId, cancelToken);
            if (siteImages.Count > 0)
            {
                foreach (var grp in siteImages.GroupBy(im => im.ProductId))
                {
                    var item = items.FirstOrDefault(i => i.Id == grp.Key);
                    if (item == null) continue;
                    item.ImageUrls = grp.OrderBy(im => im.SortOrder).Select(im => im.Url).ToList();
                    item.ImageNames = new List<string>();
                }
            }

            // Per-site VARIATIONS: overlay each variant's per-site price/sale/stock and drop variants excluded
            // for this site, so the edit form and lists reflect the branch's effective variations.
            var variantOverrides = await _overrideStorage.GetVariantOverridesForSiteAsync(ids, siteId, cancelToken);
            if (variantOverrides.Count > 0)
            {
                foreach (var item in items)
                {
                    if (item.Variants == null || item.Variants.Count == 0) continue;
                    if (!variantOverrides.TryGetValue(item.Id, out var vmap) || vmap.Count == 0) continue;
                    item.Variants = item.Variants.Where(v => !(vmap.TryGetValue(v.Id, out var ovr) && ovr.IsExcluded)).ToList();
                    foreach (var v in item.Variants)
                    {
                        if (!vmap.TryGetValue(v.Id, out var ov)) continue;
                        if (ov.Price.HasValue) v.Price = ov.Price;
                        if (ov.SalePrice.HasValue) v.SalePrice = ov.SalePrice;
                        if (ov.StockQuantity.HasValue) v.StockQuantity = ov.StockQuantity;
                    }
                }
            }
        }

        /// <summary>Get products the customer (by phone at site) has ordered in the past. For kiosk "past purchases". Returns same shape as GetProductsAsync.</summary>
        public async Task<IApiResponse<ApiListResponse<ProductRes>>> GetPastProductsBySiteAndCustomerPhoneAsync(
            int siteId,
            string? customerPhone,
            ApiListReq<ProductFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<ProductRes>>
            {
                Data = new ApiListResponse<ProductRes>()
            };

            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            if (string.IsNullOrWhiteSpace(customerPhone))
                return CreateResponse(response, StatusCode.InvalidRequest, "Customer phone is required for past purchases.");

            var productIds = await _orderStorage.GetDistinctProductIdsOrderedByCustomerPhoneAsync(siteId, customerPhone, cancelToken).ConfigureAwait(false);
            if (productIds.Count == 0)
            {
                response.Data!.Items = new List<ProductRes>();
                response.Data.Skip = request.Skip;
                response.Data.Limit = request.Take;
                response.Data.Total = 0;
                return response;
            }

            var paging = new PagingExDto { Skip = request.Skip, Take = request.Take, IncludeTotal = request.IncludeTotal };
            var res = await _productStorage.GetProductsBySiteAndIdsAsync(siteId, productIds, paging, cancelToken).ConfigureAwait(false);

            response.Data!.Items = res.Items.ConvertAll(p => MapProductToRes(p));
            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        /// <summary>Get products by site and product IDs (same shape as GetProductsBySite). For kiosk upsell page to load only needed products.</summary>
        public async Task<IApiResponse<ApiListResponse<ProductRes>>> GetProductsBySiteAndIdsAsync(
            int siteId,
            List<int> productIds,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<ProductRes>>
            {
                Data = new ApiListResponse<ProductRes>()
            };
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            if (productIds == null || productIds.Count == 0)
            {
                response.Data!.Items = new List<ProductRes>();
                response.Data.Total = 0;
                return response;
            }
            var take = Math.Min(productIds.Count, 500);
            var paging = new PagingExDto { Skip = 0, Take = take, IncludeTotal = false };
            var res = await _productStorage.GetProductsBySiteAndIdsAsync(siteId, productIds, paging, cancelToken).ConfigureAwait(false);
            response.Data!.Items = res.Items.ConvertAll(p => MapProductToRes(p));
            response.Data.Total = res.Items.Count;
            return response;
        }

        /// <summary>Linked product IDs at the site (RelatedProduct = Woo up-sells, ComplementaryProduct = Woo cross-sells), merged for kiosk when POS shows linked-product suggestions.</summary>
        public async Task<IApiResponse<List<int>>> GetUpsellProductIdsAsync(int siteId, List<int> productIds, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<int>>();
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            if (productIds == null || productIds.Count == 0)
            {
                response.Data = new List<int>();
                return response;
            }
            response.Data = await _productStorage.GetUpsellProductIdsForSiteAsync(siteId, productIds, cancelToken).ConfigureAwait(false);
            return response;
        }

        public async Task<IApiResponse<ProductRes>> GetProductAsync(int productId, CancellationToken cancelToken, int? siteId = null)
        {
            var response = new ApiResponse<ProductRes>();

            var product = await _productStorage.GetProductAsync(productId, cancelToken);
            if (product == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapProductToRes(product);

            // MultiSite Phase 2: when a site is given, overlay that site's per-site override so the edit
            // form shows the branch's effective values (price, stock, availability, exclusion).
            if (siteId is int sid && sid > 0)
            {
                await ApplyEffectiveSiteValuesAsync(new List<ProductRes> { response.Data }, sid, cancelToken);
            }

            return response;
        }

        public async Task<IApiResponse<ProductRes>> CreateProductAsync(CreateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductRes>();

            // Map request to DB model
            var product = MapReqToProduct(req);
            ClearExpiredTimedLabels(product);
            product.CreationUserId = AuthUser.Id;
            product.CreationTime = DateTime.UtcNow;
            product.IsActive = true;
            product.IsDeleted = false;

            // Handle lookups
            var lookupDto = MapToLookupDto(req);
            await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

            // Create product
            product = await _productStorage.CreateProductAsync(
                product, 
                req.SiteIds, 
                CombineCategoryIds(req.CategoryIds, req.SubcategoryIds),
                ResolveBrandIdsForPersistence(req, product),
                req.Tags,
                req.RelatedProductIds,
                req.ComplementaryProductIds,
                cancelToken);

            if (product != null)
            {
                // Create images (link to existing account media when URL matches; resolve in the product's site context so the same URL picks media for that site only)
                if (req.ImageUrls != null && req.ImageUrls.Any())
                {
                    var resolutionSiteId = req.SiteIds != null && req.SiteIds.Any() ? (int?)req.SiteIds.First() : null;
                    var imageList = await ResolveImageUrlsToMediaAsync(product.AccountId, req.ImageUrls, createMediaIfMissing: true, siteId: resolutionSiteId, cancelToken);
                    await _productStorage.CreateProductImagesAsync(product.Id, imageList, cancelToken);
                }

                // Create options and variants
                if (req.ProductOptions != null && req.ProductOptions.Any())
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.CreateProductOptionsAsync(product.Id, optionDtos, cancelToken: cancelToken);
                }

                if (req.Variants != null && req.Variants.Any())
                {
                    var variantDtos = req.Variants.Select(v => new ProductVariantDto
                    {
                        ImageUrl = v.ImageUrl,
                        OptionValues = v.OptionValues,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.StockQuantity,
                        Sku = v.Sku,
                        Weight = v.Weight
                    }).ToList();
                    var optionDtos = req.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.CreateProductVariantsAsync(product.Id, variantDtos, optionDtos, cancelToken);
                }

                // Reload with all relationships
                product = await _productStorage.GetProductAsync(product.Id, cancelToken);
                response.Data = MapProductToRes(product!);
                
                // Sync to WooCommerce for assigned sites. Prefer request SiteIds; if omitted or empty, use sites on the reloaded product (inventory-only saves still push to Woo).
                if (product != null)
                {
                    var assignedSiteIds = (req.SiteIds != null && req.SiteIds.Any())
                        ? req.SiteIds.ToList()
                        : (product.Site?.Select(s => s.Id).ToList() ?? new List<int>());
                    if (assignedSiteIds.Count > 0)
                    {
                        var productIdForSync = product.Id;
                        _ = Task.Run(async () =>
                        {
                            await SyncProductToWooCommerceForAssignedSitesAsync(productIdForSync, assignedSiteIds, CancellationToken.None);
                        }, CancellationToken.None);
                    }
                }
            }

            return response;
        }

        public async Task<IApiResponse<ProductRes>> UpdateProductAsync(int productId, UpdateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductRes>();

            var existingProduct = await _productStorage.GetProductAsync(productId, cancelToken);
            if (existingProduct == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // MultiSite Phase 2: when editing a single branch, write a per-site override instead of mutating
            // the canonical product (so changes do not leak to other sites). 'all_sites' / null = canonical update.
            if (string.Equals(req.EditScope, "selected_site", StringComparison.OrdinalIgnoreCase) && req.SiteId.HasValue)
            {
                var siteId = req.SiteId.Value;

                // A product managed locally on another site cannot be edited from this site.
                if (existingProduct.ManagementMode == "local" && existingProduct.OwnerSiteId.HasValue && existingProduct.OwnerSiteId.Value != siteId)
                    return CreateResponse(response, StatusCode.InvalidRequest, "Product is local to another site and cannot be edited here.");

                await _overrideStorage.UpsertOverrideAsync(
                    productId, siteId, existingProduct.AccountId,
                    isExcluded: null,
                    price: req.Price,
                    salePrice: req.SalePrice,
                    salePriceStartDate: req.SalePriceStartDate,
                    salePriceEndDate: req.SalePriceEndDate,
                    availability: null,
                    stockManagementType: req.StockManagementType,
                    stockStatus: req.StockStatus,
                    stockQuantity: req.StockQuantity,
                    variationStockByQuantity: req.VariationStockByQuantity,
                    lowStockThreshold: req.LowStockThreshold,
                    cancelToken,
                    // Per-site scalar field overrides.
                    name: string.IsNullOrWhiteSpace(req.Name) ? null : req.Name,
                    shortDescription: req.ShortDescription,
                    longDescription: req.LongDescription,
                    weight: req.Weight,
                    weightUnit: req.WeightUnit,
                    sku: req.Sku,
                    seoTitle: req.SeoTitle,
                    seoDescription: req.SeoDescription);

                // Per-site category assignment: when categories are provided, store them for this site only.
                if (req.CategoryIds != null || req.SubcategoryIds != null)
                {
                    var siteCatIds = CombineCategoryIds(req.CategoryIds, req.SubcategoryIds);
                    await _overrideStorage.SetSiteCategoriesAsync(productId, siteId, siteCatIds ?? new List<int>(), cancelToken);
                }

                // Per-site images: when images are provided, store them for this site only.
                if (req.ImageUrls != null)
                {
                    await _overrideStorage.SetSiteImagesAsync(productId, siteId, req.ImageUrls, cancelToken);
                }

                // Product OPTIONS are structural (the variation dimensions + allowed values), not a per-site price/
                // stock value — they are shared by every site's variant dropdown, exactly like the canonical variants
                // created below. Persist them canonically here too. Without this, adding variations to a previously
                // option-less product on a single site leaves ProductOption empty, so the WooCommerce sync registers
                // no variation attribute and skips every variant (variable product with zero variations in Woo).
                if (req.ProductOptions != null)
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.UpdateProductOptionsAsync(productId, optionDtos, cancelToken: cancelToken);
                }

                // Per-site VARIATIONS: a selected-site edit must NOT mutate the canonical variants of OTHER sites.
                // Existing variants get per-site overrides (price/sale/stock); a variant the user removed in this
                // branch is hidden here only; a brand-NEW variant added in this branch is created canonically but
                // excluded from every OTHER site, so it effectively lives only in this branch.
                if (req.Variants != null)
                {
                    var canonicalVariantIds = existingProduct.ProductVariant?.Select(v => v.Id).ToList() ?? new List<int>();
                    var keptIds = new HashSet<int>(req.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value));

                    // 1) Create brand-new variants (no Id) canonically, then exclude them from the other sites.
                    var newVariantReqs = req.Variants.Where(v => !v.Id.HasValue).ToList();
                    if (newVariantReqs.Count > 0)
                    {
                        var newDtos = newVariantReqs.Select(v => new ProductVariantDto
                        {
                            ImageUrl = v.ImageUrl,
                            OptionValues = v.OptionValues,
                            Price = v.Price,
                            SalePrice = v.SalePrice,
                            StockQuantity = v.StockQuantity,
                            Sku = v.Sku,
                            Weight = v.Weight,
                        }).ToList();
                        var createdIds = await _productStorage.CreateProductVariantsAsync(productId, newDtos, null, cancelToken);

                        // Exclude each newly-created variant from all OTHER account sites (it lives only here).
                        if (existingProduct.AccountId.HasValue)
                        {
                            var otherSiteIds = (await _overrideStorage.GetAccountSiteIdsAsync(existingProduct.AccountId.Value, cancelToken))
                                .Where(sid => sid != siteId).ToList();
                            foreach (var otherSite in otherSiteIds)
                            {
                                var excludeNew = createdIds.Select(cid => new ProductSiteOverrideStorage.VariantSiteOverrideUpsert
                                {
                                    VariantId = cid,
                                    IsExcluded = true,
                                }).ToList();
                                if (excludeNew.Count > 0)
                                    await _overrideStorage.UpsertVariantOverridesAsync(productId, otherSite, excludeNew, cancelToken);
                            }
                        }
                    }

                    var variantOverrides = new List<ProductSiteOverrideStorage.VariantSiteOverrideUpsert>();
                    foreach (var v in req.Variants)
                    {
                        if (!v.Id.HasValue) continue; // new variants handled above
                        variantOverrides.Add(new ProductSiteOverrideStorage.VariantSiteOverrideUpsert
                        {
                            VariantId = v.Id.Value,
                            Price = v.Price,
                            SalePrice = v.SalePrice,
                            StockQuantity = v.StockQuantity,
                            StockStatus = null,
                            IsExcluded = v.IsExcluded ?? false,
                        });
                    }
                    // Canonical variants the user removed in THIS branch → hide them here (not deleted canonically).
                    foreach (var cid in canonicalVariantIds.Where(id => !keptIds.Contains(id)))
                    {
                        variantOverrides.Add(new ProductSiteOverrideStorage.VariantSiteOverrideUpsert
                        {
                            VariantId = cid,
                            Price = null,
                            SalePrice = null,
                            StockQuantity = null,
                            StockStatus = null,
                            IsExcluded = true,
                        });
                    }
                    if (variantOverrides.Count > 0)
                        await _overrideStorage.UpsertVariantOverridesAsync(productId, siteId, variantOverrides, cancelToken);
                }

                // Return the canonical product with the site's effective override applied (for the edited site).
                var reloaded = await _productStorage.GetProductAsync(productId, cancelToken);
                response.Data = MapProductToRes(reloaded!);
                await ApplyEffectiveSiteValuesAsync(new List<ProductRes> { response.Data }, siteId, cancelToken);

                // MultiSite Phase 2: push the per-site effective values to THIS site's WooCommerce store only.
                // The override path mutates no canonical data and returns here, so the regular assigned-sites sync
                // (in the canonical branch below) is never reached — without this, per-site edits (name/price/
                // stock/category) never reach WooCommerce. SyncProductAsync overlays the site's override when the
                // account is network-managed. Fire-and-forget, same pattern as the canonical update path.
                var productIdForSiteSync = productId;
                var editedSiteId = siteId;
                _ = Task.Run(async () =>
                {
                    await SyncProductToWooCommerceForAssignedSitesAsync(productIdForSiteSync, new List<int> { editedSiteId }, CancellationToken.None);
                }, CancellationToken.None);
                return CreateResponse(response);
            }

            // Merge request with existing product: if a property is null in req, keep the value from DB (partial update support for table quick-edit)
            var product = MergeReqWithExistingProduct(req, existingProduct);
            ClearExpiredTimedLabels(product);
            product.Id = productId;
            product.UpdateUserId = AuthUser.Id;

            // Handle lookups (only overwrites IDs when req has a value; existing IDs are already on product from merge)
            var lookupDto = MapToLookupDto(req);
            await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

            // For partial updates (e.g. table quick-edit), only pass category/tag/related/complementary when provided.
            // When null, storage keeps existing values; when empty list is passed, we would clear them.
            var categoryIdsToApply = (req.CategoryIds != null || req.SubcategoryIds != null)
                ? CombineCategoryIds(req.CategoryIds, req.SubcategoryIds)
                : null;

            product = await _productStorage.UpdateProductAsync(
                product,
                req.SiteIds,
                categoryIdsToApply,
                ResolveBrandIdsForPersistence(req, product),
                req.Tags,
                req.RelatedProductIds,
                req.ComplementaryProductIds,
                cancelToken);

            if (product != null)
            {
                // Update images (link to existing account media when URL matches; resolve in the product's site context so the same URL picks media for that site only)
                if (req.ImageUrls != null)
                {
                    var resolutionSiteId = req.SiteIds != null && req.SiteIds.Any()
                        ? (int?)req.SiteIds.First()
                        : existingProduct.Site?.OrderBy(s => s.Id).Select(s => s.Id).FirstOrDefault();
                    var imageList = await ResolveImageUrlsToMediaAsync(existingProduct.AccountId, req.ImageUrls, createMediaIfMissing: true, siteId: resolutionSiteId, cancelToken);
                    await _productStorage.CreateProductImagesAsync(productId, imageList, cancelToken);
                }

                // Update options and variants
                if (req.ProductOptions != null)
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.UpdateProductOptionsAsync(productId, optionDtos, cancelToken: cancelToken);
                }
                if (req.Variants != null)
                {
                    var variantDtos = req.Variants.Select(v => new ProductVariantDto
                    {
                        ImageUrl = v.ImageUrl,
                        OptionValues = v.OptionValues,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.StockQuantity,
                        Sku = v.Sku,
                        Weight = v.Weight
                    }).ToList();
                    var optionDtos = req.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.UpdateProductVariantsAsync(productId, variantDtos, optionDtos, cancelToken);
                }

                // Reload with all relationships
                product = await _productStorage.GetProductAsync(productId, cancelToken);
                response.Data = MapProductToRes(product!);
                
                // Sync to WooCommerce for assigned sites. Prefer request SiteIds; if omitted or empty, use sites on the reloaded product.
                if (product != null)
                {
                    var assignedSiteIds = (req.SiteIds != null && req.SiteIds.Any())
                        ? req.SiteIds.ToList()
                        : (product.Site?.Select(s => s.Id).ToList() ?? new List<int>());
                    if (assignedSiteIds.Count > 0)
                    {
                        var productIdForSync = product.Id;
                        _ = Task.Run(async () =>
                        {
                            await SyncProductToWooCommerceForAssignedSitesAsync(productIdForSync, assignedSiteIds, CancellationToken.None);
                        }, CancellationToken.None);
                    }
                }
            }

            return response;
        }

        public async Task<IApiResponse<bool>> UpdateProductOrderAsync(UpdateProductOrderReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();
            if (req?.ProductIds == null || !req.ProductIds.Any())
            {
                return CreateResponse(response, StatusCode.InvalidRequest, "ProductIds required");
            }

            var previousSequence = await _productStorage.GetProductIdsInDisplayOrderAsync(req.ProductIds, cancelToken);
            var previousIndexById = previousSequence.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            var changedProductIds = new List<int>();
            for (var i = 0; i < req.ProductIds.Count; i++)
            {
                var id = req.ProductIds[i];
                if (!previousIndexById.TryGetValue(id, out var previousIndex) || previousIndex != i)
                    changedProductIds.Add(id);
            }

            await _productStorage.UpdateProductOrderAsync(req.ProductIds, cancelToken);
            response.Data = true;

            if (changedProductIds.Count == 0)
                return response;

            _logger.LogInformation(
                "Product reorder: syncing menu_order for {ChangedCount} of {TotalCount} products to WooCommerce",
                changedProductIds.Count,
                req.ProductIds.Count);

            // Sync only products whose index changed (fast). Full-catalog alignment uses PushProductOrderToWooCommerce in Integrations.
            var siteToProductIds = await _productStorage.GetProductIdsBySiteForProductIdsAsync(changedProductIds, cancelToken);
            if (siteToProductIds.Count > 0)
            {
                var productIdsCopy = req.ProductIds.ToList();
                var changedSet = changedProductIds.ToHashSet();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var siteStorage = scope.ServiceProvider.GetRequiredService<George.Data.SiteStorage>();
                        var wooService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                        foreach (var (siteId, siteProductIds) in siteToProductIds)
                        {
                            try
                            {
                                var site = await siteStorage.GetSiteAsync(siteId, CancellationToken.None);
                                if (site == null || !site.WooCommerceEnabled.HasValue || !site.WooCommerceEnabled.Value)
                                    continue;
                                var changedOnSite = siteProductIds.Where(changedSet.Contains).ToHashSet();
                                if (changedOnSite.Count == 0)
                                    continue;
                                await wooService.SyncMenuOrderOnlyAsync(siteId, productIdsCopy, changedOnSite, CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "WooCommerce menu_order sync failed for site {SiteId}", siteId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WooCommerce menu_order sync after order update failed");
                    }
                }, CancellationToken.None);
            }

            return response;
        }

        /// <param name="siteId">When provided, only removes the product from this site (unlinks ProductSite). Other sites keep the product. When null, soft-deletes the product for all sites.</param>
        public async Task<IApiResponse<bool>> DeleteProductAsync(int productId, int? siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            if (siteId.HasValue)
            {
                var productBefore = await _productStorage.GetProductAsync(productId, cancelToken);
                var hadSite = productBefore != null && productBefore.Site.Any(s => s.Id == siteId.Value);
                var siteCountBefore = productBefore?.Site?.Count ?? 0;
                var wooIdForDelete = siteCountBefore == 1 ? productBefore!.WooCommerceId : null;
                var skuForDelete = productBefore?.Sku;

                var result = await _productStorage.RemoveProductFromSiteAsync(productId, siteId.Value, cancelToken);
                response.Data = result;

                if (hadSite)
                {
                    var sid = siteId.Value;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var woo = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                            await woo.DeleteProductFromWooCommerceForSiteAsync(sid, wooIdForDelete, skuForDelete, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "WooCommerce delete after RemoveProductFromSite failed for product {ProductId} site {SiteId}", productId, sid);
                        }
                    }, CancellationToken.None);
                }

                return response;
            }

            var productSnapshot = await _productStorage.GetProductAsync(productId, cancelToken);
            var siteIds = productSnapshot?.Site?.Select(s => s.Id).ToList() ?? new List<int>();
            var multiSite = siteIds.Count > 1;
            var wooIdFull = multiSite ? null : productSnapshot?.WooCommerceId;
            var skuFull = productSnapshot?.Sku;

            var deleteResult = await _productStorage.DeleteProductAsync(productId, cancelToken);
            response.Data = deleteResult;

            if (productSnapshot != null && siteIds.Count > 0)
            {
                var copySiteIds = siteIds.ToList();
                var copyWooId = wooIdFull;
                var copySku = skuFull;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var woo = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                        foreach (var sid in copySiteIds)
                        {
                            try
                            {
                                await woo.DeleteProductFromWooCommerceForSiteAsync(sid, copyWooId, copySku, CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "WooCommerce delete failed for product {ProductId} site {SiteId}", productId, sid);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WooCommerce delete after DeleteProduct failed for product {ProductId}", productId);
                    }
                }, CancellationToken.None);
            }

            return response;
        }

        public async Task<IApiResponse<BulkImportProductRes>> BulkImportProductsAsync(
            BulkImportProductReq req, 
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<BulkImportProductRes>
            {
                Data = new BulkImportProductRes()
            };

            if (req.Products == null || !req.Products.Any())
            {
                return CreateResponse(response, StatusCode.InvalidRequest, "No products provided");
            }

            var results = new List<BulkImportProductItemRes>();
            int created = 0;
            int updated = 0;
            int failed = 0;

            // Dictionary to cache category lookups/resolutions
            var categoryCache = new Dictionary<string, Category>();

            // Get user's AccountId once at the beginning (cache it)
            int? userAccountId = null;
            if (AuthUser != null && AuthUser.Id > 0)
            {
                var user = await _userStorage.GetUserAsync(AuthUser.Id, cancelToken);
                userAccountId = user?.AccountId;
            }

            // Process each product
            foreach (var productReqItem in req.Products)
            {
                CreateProductReq productReq = productReqItem;
                var itemResult = new BulkImportProductItemRes
                {
                    Name = productReq.Name,
                    Sku = productReq.Sku,
                    Success = false,
                    Action = "failed"
                };

                try
                {
                    Product? existingProduct = null;

                    // Use AccountId from request if provided, otherwise use user's AccountId
                    var accountIdForLookup = productReq.AccountId ?? userAccountId;
                    var targetSiteIds = req.SiteIds ?? productReq.SiteIds;

                    // Only match existing product when it is already on the target site(s). This keeps each site's data isolated: importing for Site2 must not update products that exist only on Site1.
                    // When we have target sites, require the product to be exclusively on those sites (exclusiveSitesOnly). Otherwise a product shared across Site1 and Site2 would get its images/data updated for both sites when importing to Site2; each site has its own media/products.
                    if (req.UpdateIfExists)
                    {
                        if (!string.IsNullOrWhiteSpace(productReq.Sku))
                        {
                            if (targetSiteIds != null && targetSiteIds.Any())
                            {
                                existingProduct = await _productStorage.GetProductBySkuAndSitesAsync(
                                    productReq.Sku!,
                                    accountIdForLookup,
                                    targetSiteIds,
                                    exclusiveSitesOnly: true,
                                    cancelToken);
                            }
                            // Do not fall back to account-wide SKU match when we have target sites: a product with same SKU on another site stays separate
                            else
                            {
                                existingProduct = await _productStorage.GetProductBySkuAsync(
                                    productReq.Sku!,
                                    accountIdForLookup,
                                    cancelToken);
                            }
                        }
                        if (existingProduct == null && !string.IsNullOrWhiteSpace(productReq.Name))
                        {
                            // Only match by name when product is exclusively on one of the target sites (no shared product across sites)
                            var siteIdsForLookup = (targetSiteIds != null && targetSiteIds.Any()) ? targetSiteIds : null;
                            existingProduct = await _productStorage.GetProductByNameAndAccountAsync(
                                productReq.Name!,
                                accountIdForLookup,
                                siteIdsForLookup,
                                exclusiveSitesOnly: siteIdsForLookup != null && siteIdsForLookup.Any(),
                                cancelToken);
                        }
                    }

                    // Resolve categories - find or create them by path or ID
                    List<int>? resolvedCategoryIds = null;
                    List<int>? resolvedSubcategoryIds = null;

                    BulkImportProductItemReq? bulkProductReq = productReqItem as BulkImportProductItemReq;

                    // If category paths provided, resolve them first
                    if (bulkProductReq?.CategoryPaths != null && bulkProductReq.CategoryPaths.Any())
                    {
                        var allResolvedCategoryIds = new HashSet<int>(); // Use HashSet to avoid duplicates
                        
                        foreach (var categoryPath in bulkProductReq.CategoryPaths)
                        {
                            if (string.IsNullOrWhiteSpace(categoryPath)) continue;

                            // Find or create the leaf category (last in hierarchy)
                            var leafCategory = await _categoryStorage.FindOrCreateCategoryByPathAsync(
                                categoryPath,
                                accountIdForLookup,
                                req.SiteIds ?? productReq.SiteIds,
                                AuthUser?.Id,
                                cancelToken);

                            if (leafCategory != null)
                            {
                                // Add the leaf category and all its parents recursively
                                var categoryToAdd = leafCategory;
                                while (categoryToAdd != null)
                                {
                                    allResolvedCategoryIds.Add(categoryToAdd.Id);
                                    
                                    // Get parent if exists
                                    if (categoryToAdd.ParentCategoryId.HasValue)
                                    {
                                        categoryToAdd = await _categoryStorage.GetCategoryAsync(
                                            categoryToAdd.ParentCategoryId.Value, 
                                            cancelToken);
                                    }
                                    else
                                    {
                                        categoryToAdd = null;
                                    }
                                }
                            }
                        }

                        if (allResolvedCategoryIds.Any())
                        {
                            // Separate main categories from subcategories
                            resolvedCategoryIds = new List<int>();
                            resolvedSubcategoryIds = new List<int>();

                            foreach (var catId in allResolvedCategoryIds)
                            {
                                var cat = await _categoryStorage.GetCategoryAsync(catId, cancelToken);
                                if (cat?.ParentCategoryId == null)
                                {
                                    resolvedCategoryIds.Add(catId);
                                }
                                else
                                {
                                    resolvedSubcategoryIds.Add(catId);
                                }
                            }
                        }
                    }
                    // Otherwise use existing category IDs
                    else if (productReq.CategoryIds != null || productReq.SubcategoryIds != null)
                    {
                        var allCategoryIds = CombineCategoryIds(productReq.CategoryIds, productReq.SubcategoryIds);
                        var resolvedIds = await ResolveCategoryIdsAsync(
                            allCategoryIds, 
                            accountIdForLookup,
                            req.SiteIds ?? productReq.SiteIds,
                            categoryCache,
                            cancelToken);

                        // Separate main categories from subcategories
                        resolvedCategoryIds = new List<int>();
                        resolvedSubcategoryIds = new List<int>();

                        foreach (var catId in resolvedIds)
                        {
                            var cat = await _categoryStorage.GetCategoryAsync(catId, cancelToken);
                            if (cat?.ParentCategoryId == null)
                            {
                                resolvedCategoryIds.Add(catId);
                            }
                            else
                            {
                                resolvedSubcategoryIds.Add(catId);
                                // Also add parent as main category if not already included
                                if (cat.ParentCategoryId.HasValue && !resolvedCategoryIds.Contains(cat.ParentCategoryId.Value))
                                {
                                    resolvedCategoryIds.Add(cat.ParentCategoryId.Value);
                                }
                            }
                        }
                    }

                    if (existingProduct != null)
                    {
                        // Update existing product (only reached when product is already on target site(s); we do not merge from other sites)
                        var product = MapReqToProduct(productReq);
                        product.Id = existingProduct.Id;
                        product.UpdateUserId = AuthUser?.Id;
                        // Preserve AccountId from existing product or use from request/user
                        product.AccountId = existingProduct.AccountId ?? productReq.AccountId ?? userAccountId;

                        var lookupDto = MapToLookupDto(productReq);
                        await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);
                        ApplyIsWeightedFromSetupType(product, productReq.SetupType);

                        // Keep product on target site(s) only; no merge from other sites so other sites' data stays unchanged
                        var siteIdsForUpdate = targetSiteIds ?? productReq.SiteIds;

                        product = await _productStorage.UpdateProductAsync(
                            product,
                            siteIdsForUpdate,
                            CombineCategoryIds(resolvedCategoryIds, resolvedSubcategoryIds),
                            ResolveBrandIdsForPersistence(productReq, product),
                            productReq.Tags,
                            productReq.RelatedProductIds,
                            productReq.ComplementaryProductIds,
                            cancelToken);

                        if (product != null)
                        {
                            // Update images (resolve in product's site context so same URL picks media for that site only)
                            if (productReq.ImageUrls != null)
                            {
                                var accountIdForImages = product.AccountId ?? userAccountId;
                                var resolutionSiteId = productReq.SiteIds != null && productReq.SiteIds.Any() ? (int?)productReq.SiteIds.First() : targetSiteIds?.FirstOrDefault();
                                var imageList = await ResolveImageUrlsToMediaAsync(accountIdForImages, productReq.ImageUrls, createMediaIfMissing: true, siteId: resolutionSiteId, cancelToken);
                                await _productStorage.CreateProductImagesAsync(product.Id, imageList, cancelToken);
                            }

                            if (productReq.ProductOptions != null)
                            {
                                var optionDtos = productReq.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.UpdateProductOptionsAsync(product.Id, optionDtos, limitAttributeToSiteIds: targetSiteIds, cancelToken);
                            }

                            if (productReq.Variants != null)
                            {
                                var variantDtos = productReq.Variants.Select(v => new ProductVariantDto
                                {
                                    ImageUrl = v.ImageUrl,
                                    OptionValues = v.OptionValues,
                                    Price = v.Price,
                                    SalePrice = v.SalePrice,
                                    StockQuantity = v.StockQuantity,
                                    Sku = v.Sku,
                                    Weight = v.Weight
                                }).ToList();
                                var optionDtos = productReq.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.UpdateProductVariantsAsync(product.Id, variantDtos, optionDtos, cancelToken);
                            }

                            itemResult.Success = true;
                            itemResult.Action = "updated";
                            itemResult.ProductId = product.Id;
                            updated++;
                        }
                    }
                    else
                    {
                        // Create new product. When we have target sites and no product was found that is exclusive to those sites (product exists only on another site or is shared across sites), we intentionally create a new product for the target site(s) only. Result: two separate products per site, each with its own media/data (no shared product across sites).
                        var product = MapReqToProduct(productReq);
                        product.CreationUserId = AuthUser?.Id;
                        product.CreationTime = DateTime.UtcNow;
                        product.IsActive = true;
                        product.IsDeleted = false;
                        // Set AccountId from request or user if not provided
                        if (product.AccountId == null)
                        {
                            product.AccountId = productReq.AccountId ?? userAccountId;
                        }

                        var lookupDto = MapToLookupDto(productReq);
                        await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);
                        ApplyIsWeightedFromSetupType(product, productReq.SetupType);

                        product = await _productStorage.CreateProductAsync(
                            product,
                            targetSiteIds,
                            CombineCategoryIds(resolvedCategoryIds, resolvedSubcategoryIds),
                            ResolveBrandIdsForPersistence(productReq, product),
                            productReq.Tags,
                            productReq.RelatedProductIds,
                            productReq.ComplementaryProductIds,
                            cancelToken);

                        if (product != null)
                        {
                            // Create images (resolve in product's site context so same URL picks media for that site only)
                            if (productReq.ImageUrls != null && productReq.ImageUrls.Any())
                            {
                                var accountIdForImages = product.AccountId ?? userAccountId;
                                var resolutionSiteId = targetSiteIds != null && targetSiteIds.Any() ? (int?)targetSiteIds.First() : productReq.SiteIds?.FirstOrDefault();
                                var imageList = await ResolveImageUrlsToMediaAsync(accountIdForImages, productReq.ImageUrls, createMediaIfMissing: true, siteId: resolutionSiteId, cancelToken);
                                await _productStorage.CreateProductImagesAsync(product.Id, imageList, cancelToken);
                            }

                            if (productReq.ProductOptions != null && productReq.ProductOptions.Any())
                            {
                                var optionDtos = productReq.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.CreateProductOptionsAsync(product.Id, optionDtos, limitAttributeToSiteIds: targetSiteIds, cancelToken);
                            }

                            if (productReq.Variants != null && productReq.Variants.Any())
                            {
                                var variantDtos = productReq.Variants.Select(v => new ProductVariantDto
                                {
                                    ImageUrl = v.ImageUrl,
                                    OptionValues = v.OptionValues,
                                    Price = v.Price,
                                    SalePrice = v.SalePrice,
                                    StockQuantity = v.StockQuantity,
                                    Sku = v.Sku,
                                    Weight = v.Weight
                                }).ToList();
                                var optionDtos = productReq.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.CreateProductVariantsAsync(product.Id, variantDtos, optionDtos, cancelToken);
                            }

                            itemResult.Success = true;
                            itemResult.Action = "created";
                            itemResult.ProductId = product.Id;
                            created++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    itemResult.Success = false;
                    itemResult.Action = "failed";
                    itemResult.ErrorMessage = ex.Message;
                    failed++;
                    _logger.LogError(ex, $"Failed to import product '{productReq.Name}' (SKU: {productReq.Sku})");
                }

                results.Add(itemResult);
            }

            response.Data.Total = req.Products.Count;
            response.Data.Created = created;
            response.Data.Updated = updated;
            response.Data.Failed = failed;
            response.Data.Results = results;

            return response;
        }

        /// <summary>Resolve image URLs to (Url, MediaId). Uses existing account/site media when URL matches; when createMediaIfMissing is true, creates Media + AccountMedia for external URLs. When siteId is null, first site of account is used.</summary>
        private async Task<List<(string Url, int? MediaId)>> ResolveImageUrlsToMediaAsync(int? accountId, List<string>? imageUrls, bool createMediaIfMissing, int? siteId, CancellationToken cancelToken)
        {
            if (imageUrls == null || !imageUrls.Any()) return new List<(string, int?)>();
            if (!accountId.HasValue) return imageUrls.Select(u => (u, (int?)null)).ToList();
            var effectiveSiteId = siteId ?? (await _mediaStorage.GetFirstSiteIdForAccountAsync(accountId.Value, cancelToken));
            var urlToMediaId = await _mediaStorage.GetMediaIdsByUrlsForAccountAsync(accountId.Value, imageUrls, effectiveSiteId, cancelToken);
            var result = new List<(string Url, int? MediaId)>();
            foreach (var u in imageUrls)
            {
                var trimmed = u?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (urlToMediaId.TryGetValue(trimmed, out var id))
                {
                    result.Add((u!, id));
                    continue;
                }
                if (createMediaIfMissing && effectiveSiteId.HasValue)
                {
                    var newId = await _mediaStorage.GetOrCreateMediaByUrlForAccountAsync(accountId.Value, effectiveSiteId.Value, trimmed, AuthUser?.Id, cancelToken);
                    result.Add((u!, newId));
                }
                else
                    result.Add((u!, (int?)null));
            }
            return result;
        }

        /// <summary>
        /// Resolve category IDs - verify they exist
        /// </summary>
        private async Task<List<int>> ResolveCategoryIdsAsync(
            List<int>? categoryIds,
            int? accountId,
            List<int>? siteIds,
            Dictionary<string, Category> categoryCache,
            CancellationToken cancelToken)
        {
            var resolvedIds = new List<int>();

            if (categoryIds == null || !categoryIds.Any()) return resolvedIds;

            // Verify categories exist
            foreach (var categoryId in categoryIds)
            {
                var category = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
                if (category != null && !category.IsDeleted)
                {
                    resolvedIds.Add(category.Id);
                }
            }

            return resolvedIds;
        }

        // Helper methods

        /// <summary>Remove timed labels whose end date has passed (persisted cleanup on save).</summary>
        private static void ClearExpiredTimedLabels(Product p)
        {
            var now = DateTime.UtcNow;
            if (p.LabelKosherForPassover && p.LabelKosherForPassoverEndDate.HasValue && p.LabelKosherForPassoverEndDate.Value <= now)
            {
                p.LabelKosherForPassover = false;
                p.LabelKosherForPassoverEndDate = null;
            }
            if (p.LabelNew && p.LabelNewEndDate.HasValue && p.LabelNewEndDate.Value <= now)
            {
                p.LabelNew = false;
                p.LabelNewEndDate = null;
            }
        }

        /// <summary>
        /// Apply IsWeighted from SetupType when not explicitly set (same logic as WooCommerce sync and frontend edit form).
        /// by_weight, by_unit, by_unit_and_weight => treat as weighted when IsWeighted is not false.
        /// </summary>
        private static void ApplyIsWeightedFromSetupType(Product product, string? setupTypeName)
        {
            var name = setupTypeName ?? "";
            var isWeightedBySetup = name is "by_weight" or "by_unit" or "by_unit_and_weight";
            var isWeighted = product.IsWeighted == true || (product.IsWeighted != false && isWeightedBySetup);
            product.IsWeighted = isWeighted;
        }

        private Product MapReqToProduct(ProductReq req)
        {
            var labelNew = req.LabelNew ?? false;
            var labelPassover = req.LabelKosherForPassover ?? false;
            return new Product
            {
                Name = req.Name,
                ShortDescription = req.ShortDescription,
                LongDescription = req.LongDescription,
                Price = req.Price,
                SalePrice = req.SalePrice,
                SalePriceStartDate = req.SalePriceStartDate,
                SalePriceEndDate = req.SalePriceEndDate,
                CostPrice = req.CostPrice,
                Sku = req.Sku,
                StockQuantity = req.StockQuantity,
                VariationStockByQuantity = req.VariationStockByQuantity,
                Weight = req.Weight,
                IsKosher = req.IsKosher,
                IsWeighted = req.IsWeighted,
                AccountId = req.AccountId,
                ManagementMode = req.ManagementMode,
                OwnerSiteId = req.OwnerSiteId,
                DisplayOrder = req.DisplayOrder,
                SeoTitle = req.SeoTitle,
                SeoDescription = req.SeoDescription,
                Slug = string.IsNullOrWhiteSpace(req.Slug) ? null : req.Slug.Trim(),
                ShowAsMl = req.ShowAsMl ?? (req.WeightUnit == "ml" ? true : null),
                WeightUnit = req.WeightUnit ?? (req.ShowAsMl == true ? "ml" : null),
                LabelFrozen = req.LabelFrozen ?? false,
                LabelGlutenFree = req.LabelGlutenFree ?? false,
                LabelNotKosher = req.LabelNotKosher ?? false,
                LabelBestseller = req.LabelBestseller ?? false,
                LabelLowAvailability = req.LabelLowAvailability ?? false,
                LabelReadyToCook = req.LabelReadyToCook ?? false,
                LabelNatural = req.LabelNatural ?? false,
                LabelSugarFree = req.LabelSugarFree ?? false,
                LabelLactoseFree = req.LabelLactoseFree ?? false,
                LabelKosherForPassover = labelPassover,
                LabelKosherForPassoverEndDate = !labelPassover ? null : req.LabelKosherForPassoverEndDate,
                LabelNew = labelNew,
                LabelNewEndDate = !labelNew ? null : req.LabelNewEndDate,
            };
        }

        /// <summary>
        /// Merges update request with existing product for partial updates (e.g. quick-edit from table).
        /// If a property is null in the request, the existing value from the DB is kept.
        /// </summary>
        private static Product MergeReqWithExistingProduct(UpdateProductReq req, Product existing)
        {
            var labelPassover = req.LabelKosherForPassover ?? existing.LabelKosherForPassover;
            var labelNew = req.LabelNew ?? existing.LabelNew;
            return new Product
            {
                Id = existing.Id,
                Name = !string.IsNullOrWhiteSpace(req.Name) ? req.Name : existing.Name,
                ShortDescription = req.ShortDescription ?? existing.ShortDescription,
                LongDescription = req.LongDescription ?? existing.LongDescription,
                Price = req.Price ?? existing.Price,
                SalePrice = req.SalePrice,
                SalePriceStartDate = req.SalePriceStartDate ?? existing.SalePriceStartDate,
                SalePriceEndDate = req.SalePriceEndDate ?? existing.SalePriceEndDate,
                CostPrice = req.CostPrice ?? existing.CostPrice,
                Sku = req.Sku != null ? req.Sku : existing.Sku,
                StockQuantity = req.StockQuantity ?? existing.StockQuantity,
                VariationStockByQuantity = req.VariationStockByQuantity ?? existing.VariationStockByQuantity,
                Weight = req.Weight ?? existing.Weight,
                IsKosher = req.IsKosher ?? existing.IsKosher,
                IsWeighted = req.IsWeighted ?? existing.IsWeighted,
                AccountId = existing.AccountId ?? req.AccountId,
                ManagementMode = req.ManagementMode ?? existing.ManagementMode,
                OwnerSiteId = req.OwnerSiteId ?? existing.OwnerSiteId,
                SeoTitle = req.SeoTitle ?? existing.SeoTitle,
                SeoDescription = req.SeoDescription ?? existing.SeoDescription,
                Slug = req.Slug != null
                    ? (string.IsNullOrWhiteSpace(req.Slug) ? null : req.Slug.Trim())
                    : existing.Slug,
                ShowAsMl = req.ShowAsMl.HasValue ? (req.ShowAsMl ?? (req.WeightUnit == "ml" ? true : null)) : existing.ShowAsMl,
                WeightUnit = req.WeightUnit != null ? (req.WeightUnit == "ml" || req.ShowAsMl == true ? "ml" : req.WeightUnit) : existing.WeightUnit,
                DisplayOrder = req.DisplayOrder ?? existing.DisplayOrder,
                // Per-product low-stock threshold:
                //   null (not sent)  → keep existing (partial update safety)
                //   0 or negative    → clear the override (store null)
                //   positive number  → store as custom threshold
                LowStockThreshold = req.LowStockThreshold.HasValue
                    ? (req.LowStockThreshold.Value <= 0 ? (decimal?)null : req.LowStockThreshold.Value)
                    : existing.LowStockThreshold,
                LabelFrozen = req.LabelFrozen ?? existing.LabelFrozen,
                LabelGlutenFree = req.LabelGlutenFree ?? existing.LabelGlutenFree,
                LabelNotKosher = req.LabelNotKosher ?? existing.LabelNotKosher,
                LabelBestseller = req.LabelBestseller ?? existing.LabelBestseller,
                LabelLowAvailability = req.LabelLowAvailability ?? existing.LabelLowAvailability,
                LabelReadyToCook = req.LabelReadyToCook ?? existing.LabelReadyToCook,
                LabelNatural = req.LabelNatural ?? existing.LabelNatural,
                LabelSugarFree = req.LabelSugarFree ?? existing.LabelSugarFree,
                LabelLactoseFree = req.LabelLactoseFree ?? existing.LabelLactoseFree,
                LabelKosherForPassover = labelPassover,
                LabelKosherForPassoverEndDate = !labelPassover ? null : (req.LabelKosherForPassoverEndDate ?? existing.LabelKosherForPassoverEndDate),
                LabelNew = labelNew,
                LabelNewEndDate = !labelNew ? null : (req.LabelNewEndDate ?? existing.LabelNewEndDate),
                // Preserve lookup IDs from existing; MapLookupsAsync will overwrite only when req has values
                BrandId = existing.BrandId,
                SupplierId = existing.SupplierId,
                StatusId = existing.StatusId,
                VisibilityId = existing.VisibilityId,
                StockManagementTypeId = existing.StockManagementTypeId,
                StockStatusId = existing.StockStatusId,
                ShippingClassId = existing.ShippingClassId,
                SetupTypeId = existing.SetupTypeId,
                WeightConfigId = existing.WeightConfigId
            };
        }


        /// <summary>
        /// MultiSite Phase 2: applies a per-site override onto a ProductRes to produce the effective view
        /// for a single site. Only fields present on the override are overlaid.
        /// </summary>
        private static void ApplyOverrideToRes(ProductRes res, ProductSiteOverride? ov)
        {
            if (ov == null) return;
            res.IsExcludedForSite = ov.IsExcluded;
            if (ov.Price.HasValue) res.Price = ov.Price;
            if (ov.SalePrice.HasValue) res.SalePrice = ov.SalePrice;
            if (ov.SalePriceStartDate.HasValue) res.SalePriceStartDate = ov.SalePriceStartDate;
            if (ov.SalePriceEndDate.HasValue) res.SalePriceEndDate = ov.SalePriceEndDate;
            if (ov.StockQuantity.HasValue) res.StockQuantity = ov.StockQuantity;
            if (ov.VariationStockByQuantity.HasValue) res.VariationStockByQuantity = ov.VariationStockByQuantity;
            if (ov.LowStockThreshold.HasValue) res.LowStockThreshold = ov.LowStockThreshold;
        }

        /// <summary>Maps Product entity to API response. Price and SalePrice are always from the Product entity (never from Template Product).</summary>
        private ProductRes MapProductToRes(Product product)
        {
            var res = new ProductRes
            {
                Id = product.Id,
                CreationTime = product.CreationTime,
                UpdatedDate = product.UpdatedDate,
                CreationUserId = product.CreationUserId,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                LongDescription = product.LongDescription,
                Price = product.Price,
                SalePrice = product.SalePrice,
                SalePriceStartDate = product.SalePriceStartDate,
                SalePriceEndDate = product.SalePriceEndDate,
                CostPrice = product.CostPrice,
                Sku = product.Sku,
                StockQuantity = product.StockQuantity,
                VariationStockByQuantity = product.VariationStockByQuantity,
                Weight = product.Weight,
                IsKosher = product.IsKosher,
                IsWeighted = product.IsWeighted,
                AccountId = product.AccountId,
                ManagementMode = product.ManagementMode,
                OwnerSiteId = product.OwnerSiteId,
                DisplayOrder = product.DisplayOrder,
                ShowAsMl = product.ShowAsMl ?? (product.WeightUnit == "ml" ? true : null),
                WeightUnit = product.WeightUnit,
                SeoTitle = product.SeoTitle,
                SeoDescription = product.SeoDescription,
                Slug = product.Slug,
                LowStockThreshold = product.LowStockThreshold,
                LabelFrozen = product.LabelFrozen,
                LabelGlutenFree = product.LabelGlutenFree,
                LabelNotKosher = product.LabelNotKosher,
                LabelBestseller = product.LabelBestseller,
                LabelLowAvailability = product.LabelLowAvailability,
                LabelReadyToCook = product.LabelReadyToCook,
                LabelNatural = product.LabelNatural,
                LabelSugarFree = product.LabelSugarFree,
                LabelLactoseFree = product.LabelLactoseFree,
                LabelKosherForPassover = product.LabelKosherForPassover,
                LabelKosherForPassoverEndDate = product.LabelKosherForPassoverEndDate,
                LabelNew = product.LabelNew,
                LabelNewEndDate = product.LabelNewEndDate,
            };

            // Map images
            if (product.ProductImage != null && product.ProductImage.Any())
            {
                var ordered = product.ProductImage.OrderBy(pi => pi.SortOrder).ToList();
                res.ImageUrls = ordered.Select(pi => pi.Url).ToList();
                res.ImageNames = ordered.Select(pi => pi.Media?.Name ?? string.Empty).ToList();
            }

            // Map categories
            if (product.ProductCategory != null && product.ProductCategory.Any())
            {
                var mainCategories = product.ProductCategory
                    .Where(pc => pc.Category?.ParentCategoryId == null)
                    .Select(pc => pc.CategoryId)
                    .ToList();
                var subCategories = product.ProductCategory
                    .Where(pc => pc.Category?.ParentCategoryId != null)
                    .Select(pc => pc.CategoryId)
                    .ToList();
                
                res.CategoryIds = mainCategories;
                res.SubcategoryIds = subCategories;
            }

            // Map tags
            if (product.Tag != null && product.Tag.Any())
            {
                res.Tags = product.Tag.Select(t => t.Name).ToList();
            }

            // Map sites
            if (product.Site != null && product.Site.Any())
            {
                res.SiteIds = product.Site.Select(s => s.Id).ToList();
            }

            // Map related products (נלווים)
            if (product.RelatedProduct != null && product.RelatedProduct.Any())
            {
                res.RelatedProductIds = product.RelatedProduct.Select(p => p.Id).ToList();
            }

            // Map complementary products (מוצרים משלימים)
            if (product.ComplementaryProduct != null && product.ComplementaryProduct.Any())
            {
                res.ComplementaryProductIds = product.ComplementaryProduct.Select(p => p.Id).ToList();
            }

            // Brands: prefer many-to-many join; fall back to legacy Product.BrandId
            if (product.ProductBrand != null && product.ProductBrand.Count > 0)
            {
                var orderedBrands = product.ProductBrand
                    .OrderByDescending(pb => pb.IsPrimary)
                    .ThenBy(pb => pb.BrandId)
                    .ToList();
                var seenIds = new HashSet<int>();
                res.BrandIds = new List<int>();
                foreach (var pb in orderedBrands)
                {
                    if (seenIds.Add(pb.BrandId))
                        res.BrandIds.Add(pb.BrandId);
                }
                res.Brand = orderedBrands.FirstOrDefault()?.Brand?.Name ?? product.Brand?.Name;
            }
            else if (product.BrandId.HasValue)
            {
                res.BrandIds = new List<int> { product.BrandId.Value };
                res.Brand = product.Brand?.Name;
            }

            // Map lookups
            res.Status = product.Status?.Name;
            res.Visibility = product.Visibility?.Name;
            res.StockManagementType = product.StockManagementType?.Name;
            res.StockStatus = product.StockStatus?.Name;
            res.ShippingClass = product.ShippingClass?.Name;
            res.SetupType = product.SetupType?.Name;
            if (string.IsNullOrEmpty(res.Brand))
                res.Brand = product.Brand?.Name;
            res.Supplier = product.Supplier?.Name;

            // Map options
            if (product.ProductOption != null && product.ProductOption.Any())
            {
                res.ProductOptions = product.ProductOption
                    .Where(po => !po.IsDeleted)
                    .Select(po => new ProductOptionRes
                    {
                        Name = po.Name,
                        Values = po.ProductOptionValue?.Select(pov => pov.Value).ToList() ?? new List<string>()
                    })
                    .ToList();
            }

            // Map variants
            if (product.ProductVariant != null && product.ProductVariant.Any())
            {
                res.Variants = product.ProductVariant
                    .Where(pv => !pv.IsDeleted)
                    .Select(pv => new ProductVariantRes
                    {
                        Id = pv.Id,
                        ImageUrl = pv.ImageUrl,
                        Price = pv.Price,
                        SalePrice = pv.SalePrice,
                        StockQuantity = pv.StockQuantity,
                        Sku = pv.Sku,
                        Weight = pv.Weight,
                        OptionValues = pv.ProductVariantOptionValue?
                            .ToDictionary(pvov => pvov.OptionName ?? "", 
                                         pvov => pvov.OptionValue ?? "")
                    })
                    .ToList();
            }

            // Map weight config
            if (product.WeightConfig != null)
            {
                res.WeightConfig = new WeightConfigRes
                {
                    Unit = product.WeightConfig.Unit?.Name,
                    StartWeight = product.WeightConfig.StartWeight,
                    Step = product.WeightConfig.Step,
                    FixedWeightPerUnit = product.WeightConfig.FixedWeightPerUnit,
                    UnitWeight = product.WeightConfig.UnitWeight,
                    UnitWeightMode = product.WeightConfig.UnitWeightMode?.Name,
                    WeightOptions = product.WeightConfig.WeightOptions,
                    WeightByVariant = product.WeightConfig.WeightByVariant,
                    ShowPricePer100g = product.WeightConfig.ShowPricePer100g,
                    ShowUnitPrice = product.WeightConfig.ShowUnitPrice,
                    SoldByLabel = product.WeightConfig.SoldByLabel
                };
            }

            return res;
        }

        private List<int> CombineCategoryIds(List<int>? categoryIds, List<int>? subcategoryIds)
        {
            var combined = new List<int>();
            if (categoryIds != null) combined.AddRange(categoryIds);
            if (subcategoryIds != null) combined.AddRange(subcategoryIds);
            return combined;
        }

        /// <summary>
        /// Null = do not change ProductBrand rows (partial update). Non-null = replace join (may be empty).
        /// When only legacy <see cref="ProductReq.Brand"/> is sent, rebuild join from resolved <see cref="Product.BrandId"/>.
        /// </summary>
        private static List<int>? ResolveBrandIdsForPersistence(ProductReq req, Product productAfterLookups)
        {
            if (req.BrandIds != null)
                return req.BrandIds;
            if (req.Brand != null && req.Brand.HasValue())
                return productAfterLookups.BrandId.HasValue ? new List<int> { productAfterLookups.BrandId.Value } : new List<int>();
            return null;
        }

        private ProductLookupDto MapToLookupDto(ProductReq req)
        {
            return new ProductLookupDto
            {
                Status = req.Status,
                Visibility = req.Visibility,
                StockManagementType = req.StockManagementType,
                StockStatus = req.StockStatus,
                ShippingClass = req.ShippingClass,
                SetupType = req.SetupType,
                BrandIds = req.BrandIds,
                Brand = req.Brand,
                Supplier = req.Supplier,
                WeightConfig = req.WeightConfig != null ? new WeightConfigDto
                {
                    Unit = req.WeightConfig.Unit,
                    StartWeight = req.WeightConfig.StartWeight,
                    Step = req.WeightConfig.Step,
                    FixedWeightPerUnit = req.WeightConfig.FixedWeightPerUnit,
                    UnitWeight = req.WeightConfig.UnitWeight,
                    UnitWeightMode = req.WeightConfig.UnitWeightMode,
                    WeightOptions = req.WeightConfig.WeightOptions,
                    WeightByVariant = req.WeightConfig.WeightByVariant,
                    ShowPricePer100g = req.WeightConfig.ShowPricePer100g,
                    ShowUnitPrice = req.WeightConfig.ShowUnitPrice,
                    SoldByLabel = req.WeightConfig.SoldByLabel
                } : null
            };
        }

        private async Task SyncProductToWooCommerceForAssignedSitesAsync(int productId, List<int> assignedSiteIds, CancellationToken cancelToken)
        {
            if (assignedSiteIds == null || !assignedSiteIds.Any())
                return;

            // Create a scope for the background task to ensure services are available
            using var scope = _serviceScopeFactory.CreateScope();
            var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
            var siteStorage = scope.ServiceProvider.GetRequiredService<SiteStorage>();

            // Sync product to WooCommerce only for assigned sites that have WooCommerce enabled
            foreach (var siteId in assignedSiteIds)
            {
                try
                {
                    // Check if this site has WooCommerce enabled
                    var site = await siteStorage.GetSiteAsync(siteId, cancelToken);
                    if (site == null || !site.WooCommerceEnabled.HasValue || !site.WooCommerceEnabled.Value)
                    {
                        _logger.LogDebug(
                            "Skipping WooCommerce sync for product {ProductId} to site {SiteId} - WooCommerce not enabled",
                            productId, siteId);
                        continue;
                    }

                    var syncReq = new WooCommerceSyncReq
                    {
                        SiteId = siteId,
                        ProductIds = new List<int> { productId }
                    };
                    var syncResponse = await wooCommerceService.SyncToWooCommerceAsync(syncReq, cancelToken);

                    if (syncResponse.Data?.Success == null || !syncResponse.Data.Success.Any())
                    {
                        _logger.LogWarning(
                            "Failed to sync product {ProductId} to WooCommerce for site {SiteId}: {Message}",
                            productId, siteId, syncResponse.Data?.Message ?? "Unknown error");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Successfully synced product {ProductId} to WooCommerce for site {SiteId}",
                            productId, siteId);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - we don't want WooCommerce sync failures to block product operations
                    _logger.LogError(ex,
                        "Error syncing product {ProductId} to WooCommerce for site {SiteId}",
                        productId, siteId);
                }
            }
        }

    }
}

