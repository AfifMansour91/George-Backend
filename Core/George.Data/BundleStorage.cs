using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

/// <summary>Bundle definition read model: config + live component slots (with swaps and catalog navs).</summary>
public sealed class BundleDefinition
{
    public int ProductId { get; set; }
    public ProductBundleConfig Config { get; set; } = null!;
    public List<ProductBundleComponent> Components { get; set; } = new();
}

/// <summary>Write model for one swap row (id = existing row to update; null = create).</summary>
public sealed class BundleComponentSwapUpsert
{
    public int? Id { get; set; }
    public int SwapProductId { get; set; }
    public int? SwapVariantId { get; set; }
    public decimal Surcharge { get; set; }
    /// <summary>Own quantity per bundle in the alternative's unit; null = inherit the slot quantity.</summary>
    public decimal? Qty { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Write model for one component slot (id = existing row to update; null = create).</summary>
public sealed class BundleComponentUpsert
{
    public int? Id { get; set; }
    public int ComponentProductId { get; set; }
    public int? ComponentVariantId { get; set; }
    public decimal Qty { get; set; }
    public int SortOrder { get; set; }
    public bool Swappable { get; set; }
    public string? Description { get; set; }
    /// <summary>Chosen unit weight (kg) of a "choose a weight" product; null = keep what the slot has.</summary>
    public decimal? UnitWeightKg { get; set; }
    public List<BundleComponentSwapUpsert> Swaps { get; set; } = new();
}

/// <summary>Write model for the whole bundle definition of one product.</summary>
public sealed class BundleConfigUpsert
{
    public string PricingMode { get; set; } = "fixed";
    public decimal? FixedPrice { get; set; }
    public string DiscountType { get; set; } = "none";
    public decimal DiscountValue { get; set; }
    public string OosBehavior { get; set; } = "unavailable";
    public string Layout { get; set; } = "grid";
    public string CartDisplay { get; set; } = "name_with_components";
    public string InvoiceDisplay { get; set; } = "bundle";
    public bool HidePriceLabels { get; set; }
    public bool ReweighPrice { get; set; }
    public bool ShowComponentsInDesc { get; set; }
    public List<BundleComponentUpsert> Components { get; set; } = new();
}

/// <summary>What <see cref="BundleStorage.DeleteDefinitionAsync"/> removed (for the conversion log line).</summary>
public sealed class BundleDefinitionDeleteResult
{
    public bool ConfigDeleted { get; set; }
    public int ComponentsDeleted { get; set; }
    public int SwapsDeleted { get; set; }
    public bool Any => ConfigDeleted || ComponentsDeleted > 0 || SwapsDeleted > 0;
}

public sealed class BundleListFilter
{
    public int? AccountId { get; set; }
    public int? SiteId { get; set; }
    public string? Search { get; set; }
    /// <summary>ProductStatus name (active | hidden | draft | archived ...). Null = all.</summary>
    public string? Status { get; set; }
}

public sealed class BundleListRow
{
    public Product Product { get; set; } = null!;
    public int ComponentsCount { get; set; }
    /// <summary>Visibility lookup name (active | hidden ...); "hidden" = the header's "מוסתר".</summary>
    public string? VisibilityName { get; set; }
}

/// <summary>Catalog facts about a product used to validate/price bundle components (includes soft-deleted rows).</summary>
public sealed class BundleCatalogProductInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int? AccountId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? SetupType { get; set; }
    public decimal? Price { get; set; }
    public decimal? SalePrice { get; set; }
    public DateTime? SalePriceStartDate { get; set; }
    public DateTime? SalePriceEndDate { get; set; }
    // Weight configuration: a product priced per kg but sold by units costs price × unit weight per unit.
    public bool IsWeighted { get; set; }
    /// <summary>Weight config unit name: "kg" | "g".</summary>
    public string? WeightUnit { get; set; }
    public string? UnitWeight { get; set; }
    /// <summary>average | variable | by_variant</summary>
    public string? UnitWeightMode { get; set; }
    public bool WeightByVariant { get; set; }
    public string? WeightOptions { get; set; }
}

/// <summary>Catalog facts about a variant used to validate/price bundle components.</summary>
public sealed class BundleCatalogVariantInfo
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public decimal? SalePrice { get; set; }
    /// <summary>Variant weight in the product's weight-config unit ("weight by variant" products).</summary>
    public decimal? Weight { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Values OC Bundles reports back per component after a PUT (read-only mirror on the slot).</summary>
public sealed class BundleComponentWooMirror
{
    public string? Unit { get; set; }
    public string? Mode { get; set; }
    public decimal? UnitWeightKg { get; set; }
}

/// <summary>
/// Bundles (מארזים) data access: bundle definition (config + slots + swaps), list rows, and the
/// catalog/Woo-id lookups the pricing engine and the OC Bundles sync need. Spec: BUNDLES_SYNC_SPEC.md §2/§3.
/// </summary>
public class BundleStorage : StorageBase
{
    public const string BundleSetupTypeName = "bundle";

    public BundleStorage(GeorgeDBContext dbContext, ILogger<BundleStorage> logger)
        : base(dbContext, logger)
    {
    }

    /// <summary>Id of the 'bundle' SetupType row, or null when the SQL script was not run on this DB.</summary>
    public async Task<int?> GetBundleSetupTypeIdAsync(CancellationToken cancelToken)
    {
        return await _dbContext.SetupType
            .Where(s => s.Name == BundleSetupTypeName && !s.IsDeleted)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Bundle products (among <paramref name="productIds"/>) whose config carries a bundle discount
    /// (<c>DiscountType != none</c>). Promotions treat such a bundle as a "discounted product"
    /// (BUNDLES_SYNC_SPEC.md §9, <c>Site.PromotionsApplyToDiscountedProducts</c>).
    /// </summary>
    public async Task<HashSet<int>> GetDiscountedBundleProductIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancelToken)
    {
        if (productIds.Count == 0) return new HashSet<int>();
        var ids = productIds.Distinct().ToList();
        var rows = await _dbContext.ProductBundleConfig
            .AsNoTracking()
            .Where(c => ids.Contains(c.ProductId) && c.DiscountType != "none")
            .Select(c => c.ProductId)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return rows.ToHashSet();
    }

    /// <summary>Full definition of one bundle product (null when the product has no bundle config yet).</summary>
    public async Task<BundleDefinition?> GetDefinitionAsync(int productId, CancellationToken cancelToken)
    {
        var map = await GetDefinitionsAsync(new[] { productId }, cancelToken).ConfigureAwait(false);
        return map.TryGetValue(productId, out var def) ? def : null;
    }

    /// <summary>Definitions of several bundle products at once (products without a config are absent).</summary>
    public async Task<Dictionary<int, BundleDefinition>> GetDefinitionsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancelToken)
    {
        var result = new Dictionary<int, BundleDefinition>();
        if (productIds.Count == 0) return result;
        var ids = productIds.Distinct().ToList();

        var configs = await _dbContext.ProductBundleConfig
            .AsNoTracking()
            .Where(c => ids.Contains(c.ProductId))
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        if (configs.Count == 0) return result;

        // Live slots only. Query filters are bypassed on purpose: with the Product soft-delete filter active, EF
        // turns the required ComponentProduct include into an inner join and a slot whose catalog product was
        // deleted vanishes from the definition (and from the price, the Woo PUT and order expansion) without a
        // trace. Loading it with the deleted product lets callers flag "רכיב נמחק" instead.
        var components = await _dbContext.ProductBundleComponent
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(c => c.ComponentProduct)
                .ThenInclude(p => p.SetupType)   // BundleOrderLineBuilder.IsWeightSlot: by_weight ⇒ kg slot
            .Include(c => c.ComponentProduct)
                .ThenInclude(p => p.WeightConfig!)
                    .ThenInclude(w => w.UnitWeightMode)   // "variable" ⇒ the slot's chosen unit weight is sent to the store
            .Include(c => c.ComponentVariant)
            .Include(c => c.Swaps)
                .ThenInclude(s => s.SwapProduct)
                    .ThenInclude(p => p.SetupType)
            .Include(c => c.Swaps)
                .ThenInclude(s => s.SwapVariant)
            .Where(c => ids.Contains(c.BundleProductId) && !c.IsDeleted)
            .OrderBy(c => c.BundleProductId).ThenBy(c => c.SortOrder).ThenBy(c => c.Id)
            .AsSplitQuery()
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        foreach (var cfg in configs)
        {
            result[cfg.ProductId] = new BundleDefinition
            {
                ProductId = cfg.ProductId,
                Config = cfg,
                Components = components
                    .Where(c => c.BundleProductId == cfg.ProductId)
                    .Select(c =>
                    {
                        // Soft-deleted swap rows are filtered here (the query filter was bypassed above); a swap
                        // whose catalog product was deleted stays, with its product nav marked IsDeleted.
                        c.Swaps = c.Swaps.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToList();
                        return c;
                    })
                    .ToList(),
            };
        }
        return result;
    }

    /// <summary>
    /// Upserts the bundle definition of a product. Components and swaps are matched BY ID: an id that exists
    /// is updated in place (ids are referenced by order lines and by Woo's component keys), a missing id is
    /// created, and existing rows absent from the request are soft-deleted. Never deletes + recreates.
    /// </summary>
    public async Task UpsertDefinitionAsync(int productId, BundleConfigUpsert upsert, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;

        var config = await _dbContext.ProductBundleConfig
            .FirstOrDefaultAsync(c => c.ProductId == productId, cancelToken)
            .ConfigureAwait(false);
        if (config == null)
        {
            config = new ProductBundleConfig { ProductId = productId };
            _dbContext.ProductBundleConfig.Add(config);
        }
        config.PricingMode = upsert.PricingMode;
        config.FixedPrice = upsert.FixedPrice;
        config.DiscountType = upsert.DiscountType;
        config.DiscountValue = upsert.DiscountValue;
        config.OosBehavior = upsert.OosBehavior;
        config.Layout = upsert.Layout;
        config.CartDisplay = upsert.CartDisplay;
        config.InvoiceDisplay = upsert.InvoiceDisplay;
        config.HidePriceLabels = upsert.HidePriceLabels;
        config.ReweighPrice = upsert.ReweighPrice;
        config.ShowComponentsInDesc = upsert.ShowComponentsInDesc;
        config.UpdatedDate = now;

        // All rows of this bundle, soft-deleted included, so a re-sent id can be revived instead of duplicated.
        var existing = await _dbContext.ProductBundleComponent
            .IgnoreQueryFilters()
            .Include(c => c.Swaps)
            .Where(c => c.BundleProductId == productId)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        var existingById = existing.ToDictionary(c => c.Id);

        var keptComponentIds = new HashSet<int>();
        var newComponents = new List<ProductBundleComponent>();
        var sortOrder = 0;
        foreach (var comp in upsert.Components)
        {
            ProductBundleComponent row;
            if (comp.Id is > 0 && existingById.TryGetValue(comp.Id.Value, out var found))
            {
                row = found;
            }
            else
            {
                row = new ProductBundleComponent { BundleProductId = productId, ComponentKey = string.Empty };
                _dbContext.ProductBundleComponent.Add(row);
                newComponents.Add(row);
            }
            // The unit weight belongs to the product in the slot: a different product drops the old one.
            if (comp.UnitWeightKg is > 0m)
                row.UnitWeightKg = comp.UnitWeightKg;
            else if (row.ComponentProductId != comp.ComponentProductId || row.ComponentVariantId != comp.ComponentVariantId)
                row.UnitWeightKg = null;
            row.ComponentProductId = comp.ComponentProductId;
            row.ComponentVariantId = comp.ComponentVariantId;
            row.Qty = comp.Qty;
            row.SortOrder = sortOrder++;
            row.Swappable = comp.Swappable;
            row.Description = string.IsNullOrWhiteSpace(comp.Description) ? null : comp.Description.Trim();
            row.IsDeleted = false;

            // Swaps of this slot, matched by id the same way.
            var existingSwapsById = row.Swaps.ToDictionary(s => s.Id);
            var keptSwapIds = new HashSet<int>();
            var swapSort = 0;
            foreach (var sw in comp.Swaps)
            {
                ProductBundleComponentSwap swapRow;
                if (sw.Id is > 0 && existingSwapsById.TryGetValue(sw.Id.Value, out var foundSwap))
                {
                    swapRow = foundSwap;
                }
                else
                {
                    swapRow = new ProductBundleComponentSwap();
                    row.Swaps.Add(swapRow);
                }
                swapRow.SwapProductId = sw.SwapProductId;
                swapRow.SwapVariantId = sw.SwapVariantId;
                swapRow.Surcharge = sw.Surcharge;
                swapRow.Qty = sw.Qty is > 0m ? sw.Qty : null;
                swapRow.SortOrder = swapSort++;
                swapRow.IsDeleted = false;
                if (swapRow.Id > 0) keptSwapIds.Add(swapRow.Id);
            }
            foreach (var stale in row.Swaps.Where(s => s.Id > 0 && !keptSwapIds.Contains(s.Id)))
                stale.IsDeleted = true;

            if (row.Id > 0) keptComponentIds.Add(row.Id);
        }

        foreach (var stale in existing.Where(c => !keptComponentIds.Contains(c.Id)))
        {
            stale.IsDeleted = true;
            foreach (var sw in stale.Swaps) sw.IsDeleted = true;
        }

        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);

        // Stable key = "c" + Id (spec §5.3); only known after the insert.
        var keyed = false;
        foreach (var row in newComponents.Concat(existing).Where(c => !c.IsDeleted && string.IsNullOrEmpty(c.ComponentKey)))
        {
            row.ComponentKey = "c" + row.Id;
            keyed = true;
        }
        if (keyed)
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clean conversion of a bundle into a regular product: removes the ProductBundleConfig row and soft-deletes
    /// every component + swap (ids stay for order lines that reference them). Returns what was removed so the
    /// caller can log it. Never leaves orphaned bundle rows behind a product whose SetupType is no longer 'bundle'.
    /// </summary>
    public async Task<BundleDefinitionDeleteResult> DeleteDefinitionAsync(int productId, CancellationToken cancelToken)
    {
        var result = new BundleDefinitionDeleteResult();

        var config = await _dbContext.ProductBundleConfig
            .FirstOrDefaultAsync(c => c.ProductId == productId, cancelToken)
            .ConfigureAwait(false);
        if (config != null)
        {
            _dbContext.ProductBundleConfig.Remove(config);
            result.ConfigDeleted = true;
        }

        var components = await _dbContext.ProductBundleComponent
            .IgnoreQueryFilters()
            .Include(c => c.Swaps)
            .Where(c => c.BundleProductId == productId && !c.IsDeleted)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        foreach (var c in components)
        {
            c.IsDeleted = true;
            result.ComponentsDeleted++;
            foreach (var s in c.Swaps.Where(s => !s.IsDeleted))
            {
                s.IsDeleted = true;
                result.SwapsDeleted++;
            }
        }

        if (result.ConfigDeleted || result.ComponentsDeleted > 0)
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Live bundle products (not deleted) that use <paramref name="productId"/> as a component or as a swap of a
    /// live slot. When <paramref name="siteId"/> is given, only bundles assigned to that site. Used by the
    /// delete guard: a product that feeds a bundle must not be deleted / unlinked from the bundle's site.
    /// </summary>
    public async Task<List<(int Id, string Name)>> GetBundlesUsingProductAsync(int productId, int? siteId, CancellationToken cancelToken)
    {
        var viaComponents = _dbContext.ProductBundleComponent
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.ComponentProductId == productId)
            .Select(c => c.BundleProductId);
        var viaSwaps = _dbContext.ProductBundleComponentSwap
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.SwapProductId == productId && !s.Component.IsDeleted)
            .Select(s => s.Component.BundleProductId);

        var bundleIds = await viaComponents.Concat(viaSwaps).Distinct().ToListAsync(cancelToken).ConfigureAwait(false);
        if (bundleIds.Count == 0) return new List<(int, string)>();

        // Only products that are still bundles: a product converted away from bundle (or legacy rows that
        // escaped the conversion cleanup) must not block deleting its former components.
        var query = _dbContext.Product
            .AsNoTracking()
            .Where(p => bundleIds.Contains(p.Id) && !p.IsDeleted
                        && p.SetupType != null && p.SetupType.Name == BundleSetupTypeName);
        if (siteId is > 0)
            query = query.Where(p => p.Site.Any(s => s.Id == siteId.Value));

        var rows = await query
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return rows.Select(r => (r.Id, r.Name)).ToList();
    }

    /// <summary>The subset of <paramref name="productIds"/> that are live (not deleted) and assigned to <paramref name="siteId"/>.</summary>
    public async Task<HashSet<int>> GetProductIdsOnSiteAsync(IReadOnlyCollection<int> productIds, int siteId, CancellationToken cancelToken)
    {
        if (productIds.Count == 0) return new HashSet<int>();
        var ids = productIds.Distinct().ToList();
        var rows = await _dbContext.Product
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && !p.IsDeleted && p.Site.Any(s => s.Id == siteId))
            .Select(p => p.Id)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return rows.ToHashSet();
    }

    /// <summary>The subset of <paramref name="productIds"/> whose SetupType is 'bundle' (deleted products included).</summary>
    public async Task<HashSet<int>> GetBundleProductIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancelToken)
    {
        if (productIds.Count == 0) return new HashSet<int>();
        var ids = productIds.Distinct().ToList();
        var rows = await _dbContext.Product
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.SetupType != null && p.SetupType.Name == BundleSetupTypeName)
            .Select(p => p.Id)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return rows.ToHashSet();
    }

    /// <summary>Bundle products (SetupType 'bundle') of an account/site with paging, search and status filter.</summary>
    public async Task<DataListResult<BundleListRow>> ListBundlesAsync(BundleListFilter filter, int skip, int take, CancellationToken cancelToken)
    {
        var res = new DataListResult<BundleListRow>();

        var query = _dbContext.Product
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.SetupType != null && p.SetupType.Name == BundleSetupTypeName);

        if (filter.AccountId.HasValue)
            query = query.Where(p => p.AccountId == filter.AccountId.Value);
        if (filter.SiteId.HasValue)
            query = query.Where(p => p.Site.Any(s => s.Id == filter.SiteId.Value));
        if (filter.Search.HasValue())
        {
            var term = filter.Search!.Trim();
            query = query.Where(p => p.Name.Contains(term) || (p.Sku != null && p.Sku.Contains(term)));
        }
        if (filter.Status.HasValue())
        {
            // UI alias: an active product is "published"/"public" in the shop UI; the lookup row is "active".
            var statusName = filter.Status!.Trim().ToLowerInvariant();
            if (statusName == "published" || statusName == "public") statusName = "active";
            query = query.Where(p => p.Status != null && p.Status.Name == statusName);
        }

        res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

        var rows = await query
            .OrderBy(p => p.DisplayOrder ?? int.MaxValue)
            .ThenByDescending(p => p.UpdatedDate ?? p.CreationTime)
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(take)
            .Select(p => new BundleListRow
            {
                Product = p,
                ComponentsCount = p.BundleComponents.Count(c => !c.IsDeleted),
            })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        // Cheap navs for the list card (status name, config, first image) loaded per page.
        var ids = rows.Select(r => r.Product.Id).ToList();
        if (ids.Count > 0)
        {
            var extras = await _dbContext.Product
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    StatusName = p.Status != null ? p.Status.Name : null,
                    VisibilityName = p.Visibility != null ? p.Visibility.Name : null,
                    ImageUrl = p.ProductImage.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                    Config = p.BundleConfig,
                })
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
            var extraById = extras.ToDictionary(e => e.Id);
            foreach (var row in rows)
            {
                if (!extraById.TryGetValue(row.Product.Id, out var e)) continue;
                if (e.StatusName != null)
                    row.Product.Status = new ProductStatus { Name = e.StatusName };
                if (e.ImageUrl != null)
                    row.Product.ProductImage = new List<ProductImage> { new ProductImage { Url = e.ImageUrl } };
                row.Product.BundleConfig = e.Config;
                row.VisibilityName = e.VisibilityName;
            }
        }

        res.Items = rows;
        return res;
    }

    /// <summary>Catalog facts for products (soft-deleted rows included so validation can name them).</summary>
    public async Task<Dictionary<int, BundleCatalogProductInfo>> GetCatalogProductInfosAsync(IReadOnlyCollection<int> productIds, CancellationToken cancelToken)
    {
        if (productIds.Count == 0) return new Dictionary<int, BundleCatalogProductInfo>();
        var ids = productIds.Distinct().ToList();
        var rows = await _dbContext.Product
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new BundleCatalogProductInfo
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                AccountId = p.AccountId,
                IsActive = p.IsActive,
                IsDeleted = p.IsDeleted,
                SetupType = p.SetupType != null ? p.SetupType.Name : null,
                Price = p.Price,
                SalePrice = p.SalePrice,
                SalePriceStartDate = p.SalePriceStartDate,
                SalePriceEndDate = p.SalePriceEndDate,
                IsWeighted = p.IsWeighted == true,
                WeightUnit = p.WeightConfig != null && p.WeightConfig.Unit != null ? p.WeightConfig.Unit.Name : null,
                UnitWeight = p.WeightConfig != null ? p.WeightConfig.UnitWeight : null,
                UnitWeightMode = p.WeightConfig != null && p.WeightConfig.UnitWeightMode != null ? p.WeightConfig.UnitWeightMode.Name : null,
                WeightByVariant = p.WeightConfig != null && p.WeightConfig.WeightByVariant == true,
                WeightOptions = p.WeightConfig != null ? p.WeightConfig.WeightOptions : null,
            })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(r => r.Id);
    }

    /// <summary>Catalog facts for variants (soft-deleted rows included).</summary>
    public async Task<Dictionary<int, BundleCatalogVariantInfo>> GetCatalogVariantInfosAsync(IReadOnlyCollection<int> variantIds, CancellationToken cancelToken)
    {
        if (variantIds.Count == 0) return new Dictionary<int, BundleCatalogVariantInfo>();
        var ids = variantIds.Distinct().ToList();
        var rows = await _dbContext.ProductVariant
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new BundleCatalogVariantInfo
            {
                Id = v.Id,
                ProductId = v.ProductId,
                Sku = v.Sku,
                Price = v.Price,
                SalePrice = v.SalePrice,
                Weight = v.Weight,
                IsDeleted = v.IsDeleted,
            })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(r => r.Id);
    }

    /// <summary>productId → this site's Woo product id, for a set of products (per-site map; legacy column as fallback).</summary>
    public async Task<Dictionary<int, int>> GetSiteWooProductIdMapAsync(IReadOnlyCollection<int> productIds, int siteId, CancellationToken cancelToken)
    {
        var map = new Dictionary<int, int>();
        if (productIds.Count == 0) return map;
        var ids = productIds.Distinct().ToList();

        var perSite = await _dbContext.ProductSiteWooId
            .AsNoTracking()
            .Where(x => x.SiteId == siteId && ids.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.WooCommerceProductId })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        foreach (var r in perSite) map[r.ProductId] = r.WooCommerceProductId;

        var missing = ids.Where(id => !map.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var legacy = await _dbContext.Product
                .AsNoTracking()
                .Where(p => missing.Contains(p.Id) && p.WooCommerceId != null && p.Site.Any(s => s.Id == siteId))
                .Select(p => new { p.Id, WooId = p.WooCommerceId!.Value })
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
            foreach (var r in legacy) map[r.Id] = r.WooId;
        }
        return map;
    }

    /// <summary>variantId → this site's Woo variation id, for a set of variants (per-site map; legacy column as fallback).</summary>
    public async Task<Dictionary<int, int>> GetSiteVariantWooIdMapAsync(IReadOnlyCollection<int> variantIds, int siteId, CancellationToken cancelToken)
    {
        var map = new Dictionary<int, int>();
        if (variantIds.Count == 0) return map;
        var ids = variantIds.Distinct().ToList();

        var perSite = await _dbContext.ProductSiteVariantWooId
            .AsNoTracking()
            .Where(x => x.SiteId == siteId && ids.Contains(x.ProductVariantId))
            .Select(x => new { x.ProductVariantId, x.WooCommerceVariationId })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        foreach (var r in perSite) map[r.ProductVariantId] = r.WooCommerceVariationId;

        var missing = ids.Where(id => !map.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var legacy = await _dbContext.ProductVariant
                .AsNoTracking()
                .Where(v => missing.Contains(v.Id) && v.WooCommerceVariationId != null)
                .Select(v => new { v.Id, WooId = v.WooCommerceVariationId!.Value })
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
            foreach (var r in legacy) map[r.Id] = r.WooId;
        }
        return map;
    }

    /// <summary>Stores what OC Bundles reported back per component (unit / mode / unit weight).</summary>
    public async Task UpdateComponentWooMirrorAsync(IReadOnlyDictionary<int, BundleComponentWooMirror> byComponentId, CancellationToken cancelToken)
    {
        if (byComponentId.Count == 0) return;
        var ids = byComponentId.Keys.ToList();
        var rows = await _dbContext.ProductBundleComponent
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            var m = byComponentId[row.Id];
            row.Unit = Truncate(m.Unit, 10);
            row.Mode = Truncate(m.Mode, 20);
            // The store echoes the unit weight it prices with; never wipe a weight chosen in George with an empty echo.
            if (m.UnitWeightKg is > 0m) row.UnitWeightKg = m.UnitWeightKg;
        }
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    private static string? Truncate(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
