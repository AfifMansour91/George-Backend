using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Bundles;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Bundles (מארזים): validation + persistence of a product's bundle definition (called from ProductService),
/// the /Bundle controller operations (list, availability proxy, per-site re-sync, server pricing) and the
/// account feature gate. Spec: BUNDLES_SYNC_SPEC.md §3.1 / §3.2 / §4.
/// </summary>
public class BundleService : ServiceBase
{
    public const string ErrBundlesDisabled = "המארזים אינם מופעלים לחשבון זה";
    public const string ErrSwapsOnNonSwappableSlot = "חלופות מוגדרות רק לרכיב שניתן להחלפה";

    private static readonly HashSet<string> PricingModes = new(StringComparer.OrdinalIgnoreCase) { "fixed", "sum" };
    private static readonly HashSet<string> DiscountTypes = new(StringComparer.OrdinalIgnoreCase) { "none", "percent", "fixed" };
    private static readonly HashSet<string> OosBehaviors = new(StringComparer.OrdinalIgnoreCase) { "unavailable", "swap" };
    private static readonly HashSet<string> Layouts = new(StringComparer.OrdinalIgnoreCase) { "grid", "list" };
    private static readonly HashSet<string> CartDisplays = new(StringComparer.OrdinalIgnoreCase) { "name_with_components", "name_only", "line" };
    private static readonly HashSet<string> InvoiceDisplays = new(StringComparer.OrdinalIgnoreCase) { "bundle", "components" };

    private readonly BundleStorage _bundleStorage;
    private readonly ProductStorage _productStorage;
    private readonly ProductSiteOverrideStorage _overrideStorage;
    private readonly SiteStorage _siteStorage;
    private readonly UserStorage _userStorage;
    private readonly AccountStorage _accountStorage;
    private readonly WooCommerceService _wooCommerceService;

    public BundleService(
        ILogger<BundleService> logger,
        IMapper mapper,
        CacheManager cache,
        BundleStorage bundleStorage,
        ProductStorage productStorage,
        ProductSiteOverrideStorage overrideStorage,
        SiteStorage siteStorage,
        UserStorage userStorage,
        AccountStorage accountStorage,
        WooCommerceService wooCommerceService)
        : base(logger, mapper, cache)
    {
        _bundleStorage = bundleStorage;
        _productStorage = productStorage;
        _overrideStorage = overrideStorage;
        _siteStorage = siteStorage;
        _userStorage = userStorage;
        _accountStorage = accountStorage;
        _wooCommerceService = wooCommerceService;
    }

    // ───────────────────────────── feature gate ─────────────────────────────

    /// <summary>
    /// Whether the caller may use the bundles feature: the account of <paramref name="siteId"/> when given, else the
    /// caller's own account, must have <c>Account.BundlesEnabled</c>. A master with no account and no site is allowed.
    /// </summary>
    /// <summary>
    /// Feature gate. The account is resolved from the site when given; otherwise from the account the client
    /// asked for (<paramref name="requestedAccountId"/>, i.e. the impersonated account) when the caller is
    /// unrestricted (master / system admin, same rule as CategoryService); otherwise from the caller's own account.
    /// </summary>
    public async Task<bool> IsBundlesEnabledForCallerAsync(int? siteId, CancellationToken cancelToken, int? requestedAccountId = null)
    {
        var accountId = await ResolveCallerAccountIdAsync(siteId, requestedAccountId, cancelToken).ConfigureAwait(false);
        if (accountId is not > 0)
            return AuthUser.IsMaster;

        var account = await _accountStorage.GetAccountAsync(accountId.Value, cancelToken).ConfigureAwait(false);
        return account?.BundlesEnabled == true;
    }

    /// <summary>Site → its account; else the requested (impersonated) account for unrestricted callers; else the caller's own account.</summary>
    private async Task<int?> ResolveCallerAccountIdAsync(int? siteId, int? requestedAccountId, CancellationToken cancelToken)
    {
        if (siteId is > 0)
        {
            var site = await _siteStorage.GetSiteAsync(siteId.Value, cancelToken).ConfigureAwait(false);
            if (site?.AccountId > 0) return site.AccountId;
        }
        var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        var unrestricted = AuthUser.IsMaster || user?.RoleId == (int)UserRole.Admin;
        if (unrestricted && requestedAccountId is > 0)
            return requestedAccountId;
        return user?.AccountId;
    }

    // ───────────────────────────── validation + save (ProductService) ─────────────────────────────

    /// <summary>
    /// Validates a <c>ProductReq.Bundle</c> per spec §3.1. Returns a user-facing Hebrew error, or null when valid.
    /// <paramref name="bundleProductId"/> = the product being updated (a bundle can never contain itself).
    /// </summary>
    public async Task<string?> ValidateBundleReqAsync(ProductBundleReq? req, int? accountId, int? bundleProductId, CancellationToken cancelToken)
    {
        if (req == null)
            return "חסרה הגדרת מארז (bundle)";

        if (await _bundleStorage.GetBundleSetupTypeIdAsync(cancelToken).ConfigureAwait(false) is null)
            return "סוג המוצר 'bundle' אינו מוגדר במסד הנתונים (יש להריץ SetupType_AddBundle.sql)";

        var pricingMode = Norm(req.PricingMode, "fixed");
        if (!PricingModes.Contains(pricingMode)) return "pricingMode לא חוקי (fixed | sum)";
        var discountType = Norm(req.DiscountType, "none");
        if (!DiscountTypes.Contains(discountType)) return "discountType לא חוקי (none | percent | fixed)";
        if (!OosBehaviors.Contains(Norm(req.OosBehavior, "unavailable"))) return "oosBehavior לא חוקי (unavailable | swap)";
        if (!Layouts.Contains(Norm(req.Layout, "grid"))) return "layout לא חוקי (grid | list)";
        if (!CartDisplays.Contains(Norm(req.CartDisplay, "name_with_components"))) return "cartDisplay לא חוקי";
        if (!InvoiceDisplays.Contains(Norm(req.InvoiceDisplay, "bundle"))) return "invoiceDisplay לא חוקי (bundle | components)";

        if (pricingMode == "fixed" && (!req.FixedPrice.HasValue || req.FixedPrice.Value < 0m))
            return "יש להזין מחיר קבוע למארז (fixedPrice) במצב מחיר קבוע";

        var discountValue = req.DiscountValue ?? 0m;
        if (discountValue < 0m) return "ערך ההנחה חייב להיות 0 ומעלה";
        if (discountType == "percent" && discountValue > 100m) return "הנחה באחוזים לא יכולה לעלות על 100";

        var components = req.Components ?? new List<ProductBundleComponentReq>();
        if (components.Count == 0)
            return "מארז חייב לכלול לפחות רכיב אחד";

        var productIds = components.Select(c => c.ProductId)
            .Concat(components.SelectMany(c => c.Swaps ?? new List<ProductBundleSwapReq>()).Select(s => s.ProductId))
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var variantIds = components.Where(c => c.ProductVariantId is > 0).Select(c => c.ProductVariantId!.Value)
            .Concat(components.SelectMany(c => c.Swaps ?? new List<ProductBundleSwapReq>()).Where(s => s.ProductVariantId is > 0).Select(s => s.ProductVariantId!.Value))
            .Distinct()
            .ToList();
        var products = await _bundleStorage.GetCatalogProductInfosAsync(productIds, cancelToken).ConfigureAwait(false);
        var variants = await _bundleStorage.GetCatalogVariantInfosAsync(variantIds, cancelToken).ConfigureAwait(false);

        string? CheckProduct(int productId, int? variantId, bool isSwap)
        {
            var what = isSwap ? "מוצר חלופי" : "רכיב";
            if (productId <= 0) return $"{what}: חסר מזהה מוצר";
            if (bundleProductId.HasValue && productId == bundleProductId.Value) return "מארז לא יכול להכיל את עצמו";
            if (!products.TryGetValue(productId, out var p) || p.IsDeleted) return $"{what} #{productId} לא נמצא";
            if (accountId.HasValue && p.AccountId.HasValue && p.AccountId.Value != accountId.Value) return $"{what} '{p.Name}' אינו שייך לחשבון";
            if (BundleProducts.IsBundleSetupType(p.SetupType)) return $"{what} '{p.Name}' הוא מארז בעצמו - לא ניתן לקנן מארזים";
            if (!isSwap && !p.IsActive) return $"רכיב '{p.Name}' אינו פעיל";
            if (variantId is > 0)
            {
                if (!variants.TryGetValue(variantId.Value, out var v) || v.IsDeleted) return $"{what} '{p.Name}': וריאציה #{variantId} לא נמצאה";
                if (v.ProductId != productId) return $"{what} '{p.Name}': הווריאציה אינה שייכת למוצר";
            }
            return null;
        }

        foreach (var c in components)
        {
            if (c.Qty <= 0m) return "כמות הרכיב חייבת להיות גדולה מ-0";
            var err = CheckProduct(c.ProductId, c.ProductVariantId, isSwap: false);
            if (err != null) return err;
            if (!c.Swappable && (c.Swaps?.Count ?? 0) > 0) return ErrSwapsOnNonSwappableSlot;
            foreach (var s in c.Swaps ?? new List<ProductBundleSwapReq>())
            {
                if (s.Surcharge < 0m) return "תוספת מחיר להחלפה חייבת להיות 0 ומעלה";
                err = CheckProduct(s.ProductId, s.ProductVariantId, isSwap: true);
                if (err != null) return err;
            }
        }
        return null;
    }

    /// <summary>Maps the request to the storage write model (defaults applied, enum values normalized).</summary>
    public static BundleConfigUpsert MapToUpsert(ProductBundleReq req)
    {
        var components = (req.Components ?? new List<ProductBundleComponentReq>())
            .Select((c, i) => new { c, i })
            .OrderBy(x => x.c.SortOrder ?? x.i).ThenBy(x => x.i)
            .Select(x => new BundleComponentUpsert
            {
                Id = x.c.Id is > 0 ? x.c.Id : null,
                ComponentProductId = x.c.ProductId,
                ComponentVariantId = x.c.ProductVariantId is > 0 ? x.c.ProductVariantId : null,
                Qty = x.c.Qty,
                SortOrder = x.c.SortOrder ?? x.i,
                Swappable = x.c.Swappable,
                Description = x.c.Description,
                Swaps = (x.c.Swaps ?? new List<ProductBundleSwapReq>())
                    .Select((s, j) => new { s, j })
                    .OrderBy(y => y.s.SortOrder ?? y.j).ThenBy(y => y.j)
                    .Select(y => new BundleComponentSwapUpsert
                    {
                        Id = y.s.Id is > 0 ? y.s.Id : null,
                        SwapProductId = y.s.ProductId,
                        SwapVariantId = y.s.ProductVariantId is > 0 ? y.s.ProductVariantId : null,
                        Surcharge = BundlePricingEngine.Round2(y.s.Surcharge),
                        SortOrder = y.s.SortOrder ?? y.j,
                    })
                    .ToList(),
            })
            .ToList();

        return new BundleConfigUpsert
        {
            PricingMode = Norm(req.PricingMode, "fixed"),
            FixedPrice = req.FixedPrice.HasValue ? BundlePricingEngine.Round2(req.FixedPrice.Value) : null,
            DiscountType = Norm(req.DiscountType, "none"),
            DiscountValue = BundlePricingEngine.Round2(req.DiscountValue ?? 0m),
            OosBehavior = Norm(req.OosBehavior, "unavailable"),
            Layout = Norm(req.Layout, "grid"),
            CartDisplay = Norm(req.CartDisplay, "name_with_components"),
            InvoiceDisplay = Norm(req.InvoiceDisplay, "bundle"),
            HidePriceLabels = req.HidePriceLabels ?? false,
            ReweighPrice = req.ReweighPrice ?? false,
            ShowComponentsInDesc = req.ShowComponentsInDesc ?? false,
            Components = components,
        };
    }

    /// <summary>Persists the (already validated) bundle definition of a product. Upsert by id, never delete + recreate.</summary>
    public Task SaveBundleAsync(int productId, ProductBundleReq req, CancellationToken cancelToken)
    {
        return _bundleStorage.UpsertDefinitionAsync(productId, MapToUpsert(req), cancelToken);
    }

    // ───────────────────────────── ProductRes.Bundle ─────────────────────────────

    /// <summary>
    /// The <c>ProductRes.Bundle</c> block for a bundle product: definition + George-side computed prices.
    /// With a <paramref name="siteId"/> the component prices are that site's effective prices; else catalog prices.
    /// Null when the product has no bundle definition yet.
    /// </summary>
    public async Task<ProductBundleRes?> BuildBundleResAsync(int productId, int? siteId, CancellationToken cancelToken)
    {
        var def = await _bundleStorage.GetDefinitionAsync(productId, cancelToken).ConfigureAwait(false);
        if (def == null) return null;
        var prices = await ResolveComponentPricesAsync(def, siteId, cancelToken).ConfigureAwait(false);
        return MapToRes(def, prices);
    }

    private static ProductBundleRes MapToRes(BundleDefinition def, IReadOnlyDictionary<(int ProductId, int? VariantId), decimal> prices)
    {
        var cfg = def.Config;
        var pricing = BundlePricingEngine.Price(BuildPricingInput(def, prices, bundleQty: 1m, swaps: null));

        var res = new ProductBundleRes
        {
            PricingMode = cfg.PricingMode,
            FixedPrice = cfg.FixedPrice,
            DiscountType = cfg.DiscountType,
            DiscountValue = cfg.DiscountValue,
            OosBehavior = cfg.OosBehavior,
            Layout = cfg.Layout,
            CartDisplay = cfg.CartDisplay,
            InvoiceDisplay = cfg.InvoiceDisplay,
            HidePriceLabels = cfg.HidePriceLabels,
            ReweighPrice = cfg.ReweighPrice,
            ShowComponentsInDesc = cfg.ShowComponentsInDesc,
            ComputedPrice = pricing.UnitPrice,
            ComputedBasePrice = pricing.BasePrice,
            ComputedRawPrice = pricing.RawPrice,
        };
        foreach (var c in def.Components.OrderBy(c => c.SortOrder).ThenBy(c => c.Id))
        {
            res.Components.Add(new ProductBundleComponentRes
            {
                Id = c.Id,
                Key = string.IsNullOrEmpty(c.ComponentKey) ? "c" + c.Id : c.ComponentKey,
                ProductId = c.ComponentProductId,
                ProductVariantId = c.ComponentVariantId,
                ProductName = c.ComponentProduct?.Name,
                Sku = c.ComponentVariant?.Sku ?? c.ComponentProduct?.Sku,
                Qty = c.Qty,
                SortOrder = c.SortOrder,
                Swappable = c.Swappable,
                Description = c.Description ?? string.Empty,
                Unit = c.Unit,
                Mode = c.Mode,
                UnitWeightKg = c.UnitWeightKg,
                ProductDeleted = c.ComponentProduct == null || c.ComponentProduct.IsDeleted,
                Swaps = c.Swaps.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Select(s => new ProductBundleSwapRes
                {
                    Id = s.Id,
                    ProductId = s.SwapProductId,
                    ProductVariantId = s.SwapVariantId,
                    ProductName = s.SwapProduct?.Name,
                    Sku = s.SwapVariant?.Sku ?? s.SwapProduct?.Sku,
                    Surcharge = s.Surcharge,
                    SortOrder = s.SortOrder,
                    ProductDeleted = s.SwapProduct == null || s.SwapProduct.IsDeleted,
                }).ToList(),
            });
        }
        return res;
    }

    /// <summary>A component or swap whose catalog product was soft-deleted after the bundle was defined.</summary>
    public static bool DefinitionHasDeletedProduct(BundleDefinition def) =>
        def.Components.Any(c => c.ComponentProduct == null || c.ComponentProduct.IsDeleted
            || c.Swaps.Any(s => s.SwapProduct == null || s.SwapProduct.IsDeleted));

    /// <summary>Name of the first deleted component/swap product, for user-facing errors.</summary>
    public static string? FirstDeletedProductName(BundleDefinition def)
    {
        foreach (var c in def.Components)
        {
            if (c.ComponentProduct == null || c.ComponentProduct.IsDeleted) return c.ComponentProduct?.Name ?? $"#{c.ComponentProductId}";
            var s = c.Swaps.FirstOrDefault(x => x.SwapProduct == null || x.SwapProduct.IsDeleted);
            if (s != null) return s.SwapProduct?.Name ?? $"#{s.SwapProductId}";
        }
        return null;
    }

    public static string DeletedComponentError(string name) => $"הרכיב '{name}' נמחק מהקטלוג - יש להסיר או להחליף אותו בהגדרת המארז";

    // ───────────────────────────── /Bundle list ─────────────────────────────

    public async Task<IApiResponse<BundleListRes>> ListAsync(BundleListReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<BundleListRes>();
        var page = Math.Max(1, req.Page ?? 1);
        var pageSize = Math.Clamp(req.PageSize ?? 25, 1, 200);

        int? accountId;
        if (req.SiteId is > 0)
        {
            var site = await _siteStorage.GetSiteAsync(req.SiteId.Value, cancelToken).ConfigureAwait(false);
            if (site == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
            accountId = site.AccountId;
        }
        else
        {
            // Impersonating super-admin: the client sends the impersonated account (req.AccountId); an
            // account-scoped user is always pinned to their own account regardless of what was sent.
            accountId = await ResolveCallerAccountIdAsync(null, req.AccountId, cancelToken).ConfigureAwait(false);
            if (accountId is not > 0 && !AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);
        }

        var rows = await _bundleStorage.ListBundlesAsync(
            new BundleListFilter { AccountId = accountId, SiteId = req.SiteId is > 0 ? req.SiteId : null, Search = req.Search, Status = req.Status },
            skip: (page - 1) * pageSize,
            take: pageSize,
            cancelToken).ConfigureAwait(false);

        var ids = rows.Items.Select(r => r.Product.Id).ToList();
        var defs = await _bundleStorage.GetDefinitionsAsync(ids, cancelToken).ConfigureAwait(false);
        var items = new List<BundleListItemRes>(rows.Items.Count);
        foreach (var row in rows.Items)
        {
            var p = row.Product;
            decimal computed = 0m;
            if (defs.TryGetValue(p.Id, out var def))
            {
                var prices = await ResolveComponentPricesAsync(def, req.SiteId is > 0 ? req.SiteId : null, cancelToken).ConfigureAwait(false);
                computed = BundlePricingEngine.Price(BuildPricingInput(def, prices, 1m, null)).UnitPrice;
            }
            var item = new BundleListItemRes
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Status = p.Status?.Name,
                ImageUrl = p.ProductImage?.FirstOrDefault()?.Url,
                PricingMode = p.BundleConfig?.PricingMode ?? def?.Config.PricingMode ?? "fixed",
                ComputedPrice = computed,
                ComponentsCount = row.ComponentsCount,
                HasDeletedComponent = def != null && DefinitionHasDeletedProduct(def),
                UpdatedDate = p.BundleConfig?.UpdatedDate ?? p.UpdatedDate ?? p.CreationTime,
            };
            try
            {
                var statuses = await _overrideStorage.GetSiteWooSyncStatusesAsync(p.Id, cancelToken).ConfigureAwait(false);
                item.WooSyncStatuses = statuses
                    .Where(s => req.SiteId is not > 0 || s.SiteId == req.SiteId.Value)
                    .Select(s => new ProductSiteWooSyncStatusRes
                    {
                        SiteId = s.SiteId,
                        SiteName = s.SiteName,
                        LastSyncAt = s.LastSyncAt,
                        Success = s.Success,
                        Action = s.Action,
                        WooCommerceProductId = s.WooCommerceProductId,
                        Error = s.Error,
                    }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bundles list: failed to load Woo sync statuses for product {ProductId}", p.Id);
            }
            items.Add(item);
        }

        response.Data = new BundleListRes { Items = items, Page = page, PageSize = pageSize, Total = rows.Total ?? items.Count };
        return response;
    }

    // ───────────────────────────── availability proxy ─────────────────────────────

    public async Task<IApiResponse<BundleAvailabilityRes>> GetAvailabilityAsync(int productId, int siteId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<BundleAvailabilityRes>();
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "siteId is required");

        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");

        var product = await _productStorage.GetProductAsync(productId, cancelToken).ConfigureAwait(false);
        if (product == null || !BundleProducts.IsBundle(product))
            return CreateResponse(response, StatusCode.ItemNotFound, "Bundle product not found");

        var wooId = await ResolveWooIdAsync(product, siteId, cancelToken).ConfigureAwait(false);
        if (wooId is not > 0)
            return CreateResponse(response, StatusCode.ItemNotFound, "המארז עדיין לא סונכרן לאתר זה");

        var (availability, error) = await _wooCommerceService.GetBundleAvailabilityFromWooAsync(site, wooId.Value, cancelToken).ConfigureAwait(false);
        if (availability == null)
        {
            response.DisplayMessage = error;
            return CreateResponse(response, StatusCode.ExternalHTTPFailed, error ?? "OC Bundles availability failed");
        }
        response.Data = availability;
        return response;
    }

    // ───────────────────────────── re-sync one site ─────────────────────────────

    public async Task<IApiResponse<BundleSyncRes>> SyncAsync(int productId, int siteId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<BundleSyncRes>();
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "siteId is required");

        var product = await _productStorage.GetProductAsync(productId, cancelToken).ConfigureAwait(false);
        if (product == null || !BundleProducts.IsBundle(product))
            return CreateResponse(response, StatusCode.ItemNotFound, "Bundle product not found");
        if (product.Site?.Any(s => s.Id == siteId) != true)
            return CreateResponse(response, StatusCode.InvalidRequest, "המארז אינו משויך לאתר זה");

        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
        if (site.WooCommerceEnabled != true)
            return CreateResponse(response, StatusCode.InvalidRequest, "WooCommerce אינו מופעל לאתר זה");

        // The regular product sync path: creates/updates the oc_bundle post, then PUTs the definition (§7),
        // and records the outcome in ProductSiteWooSyncStatus.
        var sync = await _wooCommerceService.SyncToWooCommerceAsync(
            new WooCommerceSyncReq { SiteId = siteId, ProductIds = new List<int> { productId } }, cancelToken).ConfigureAwait(false);

        var ok = sync.Data?.Success?.FirstOrDefault(r => r.ProductId == productId);
        var failed = sync.Data?.Failed?.FirstOrDefault(r => r.ProductId == productId);
        response.Data = new BundleSyncRes
        {
            ProductId = productId,
            SiteId = siteId,
            Success = ok != null,
            WooCommerceProductId = ok?.WooCommerceId ?? failed?.WooCommerceId,
            Action = ok?.Action,
            Error = ok != null ? null : (failed?.Error ?? sync.Description ?? sync.Data?.Message ?? "Sync did not run"),
        };
        if (ok == null && failed == null && !sync.IsSuccessful)
        {
            // The run never reached the product (store config etc.) - remember it like ProductService does.
            try
            {
                await _overrideStorage.RecordSiteWooSyncResultAsync(productId, siteId, false, null, null, response.Data.Error, cancelToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bundle sync: failed to record sync failure for product {ProductId} site {SiteId}", productId, siteId);
            }
        }
        return response;
    }

    // ───────────────────────────── POST /Bundle/price ─────────────────────────────

    public async Task<IApiResponse<BundlePriceRes>> PriceAsync(BundlePriceReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<BundlePriceRes>();
        if (req.BundleProductId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "bundleProductId is required");
        if (req.BundleQty <= 0m)
            return CreateResponse(response, StatusCode.InvalidRequest, "bundleQty must be > 0");

        var def = await _bundleStorage.GetDefinitionAsync(req.BundleProductId, cancelToken).ConfigureAwait(false);
        if (def == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "Bundle definition not found");

        var siteId = req.SiteId > 0 ? req.SiteId : (int?)null;
        var swaps = new Dictionary<int, BundleSlotSwap>();
        foreach (var c in req.Components ?? new List<BundlePriceComponentReq>())
        {
            var slot = def.Components.FirstOrDefault(x => x.Id == c.ComponentId);
            if (slot == null)
                return CreateResponse(response, StatusCode.InvalidRequest, $"componentId {c.ComponentId} אינו רכיב של מארז זה");
            var productId = c.ProductId > 0 ? c.ProductId : slot.ComponentProductId;
            var variantId = c.ProductVariantId is > 0 ? c.ProductVariantId : (c.ProductId > 0 ? null : slot.ComponentVariantId);
            var isOriginal = productId == slot.ComponentProductId && (variantId ?? 0) == (slot.ComponentVariantId ?? 0);
            decimal surcharge;
            if (isOriginal)
                surcharge = 0m;
            else if (c.Surcharge.HasValue)
                surcharge = c.Surcharge.Value;
            else
            {
                var configured = slot.Swaps.FirstOrDefault(s => s.SwapProductId == productId && (s.SwapVariantId ?? 0) == (variantId ?? 0));
                surcharge = configured?.Surcharge ?? 0m;
            }
            swaps[slot.Id] = new BundleSlotSwap { ProductId = productId, VariantId = variantId, Surcharge = BundlePricingEngine.Round2(surcharge) };
        }

        var prices = await ResolveComponentPricesAsync(def, siteId, cancelToken).ConfigureAwait(false);
        var pricing = BundlePricingEngine.Price(BuildPricingInput(def, prices, req.BundleQty, swaps));
        response.Data = new BundlePriceRes
        {
            UnitPrice = pricing.UnitPrice,
            BasePrice = pricing.BasePrice,
            RawPrice = pricing.RawPrice,
            DiscountAmount = pricing.DiscountAmount,
            SurchargeTotal = pricing.SurchargeTotal,
            LineTotal = pricing.LineTotal,
            Components = pricing.Components.Select(c => new BundlePriceComponentRes
            {
                ComponentId = c.ComponentId,
                ProductId = c.ProductId,
                ProductVariantId = c.ProductVariantId,
                Qty = c.Qty,
                LineQty = c.LineQty,
                Surcharge = c.Surcharge,
                Share = c.Share,
            }).ToList(),
        };
        return response;
    }

    // ───────────────────────────── pricing helpers ─────────────────────────────

    /// <summary>A slot deviation for pricing (the product actually in the slot + surcharge per bundle).</summary>
    public sealed class BundleSlotSwap
    {
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public decimal Surcharge { get; set; }
    }

    /// <summary>Builds the engine input from a definition, resolved prices and optional slot swaps.</summary>
    public static BundlePricingInput BuildPricingInput(
        BundleDefinition def,
        IReadOnlyDictionary<(int ProductId, int? VariantId), decimal> prices,
        decimal bundleQty,
        IReadOnlyDictionary<int, BundleSlotSwap>? swaps)
    {
        var input = new BundlePricingInput
        {
            PricingMode = def.Config.PricingMode,
            FixedPrice = def.Config.FixedPrice,
            DiscountType = def.Config.DiscountType,
            DiscountValue = def.Config.DiscountValue,
            BundleQty = bundleQty,
        };
        foreach (var c in def.Components.OrderBy(c => c.SortOrder).ThenBy(c => c.Id))
        {
            prices.TryGetValue((c.ComponentProductId, c.ComponentVariantId), out var originalPrice);
            // A component whose catalog product was deleted no longer contributes to the sum-mode price
            // (the bundle is flagged and cannot be synced or ordered until the slot is fixed).
            if (c.ComponentProduct == null || c.ComponentProduct.IsDeleted)
                originalPrice = 0m;
            BundleSlotSwap? swap = null;
            swaps?.TryGetValue(c.Id, out swap);
            input.Components.Add(new BundlePricingComponent
            {
                ComponentId = c.Id,
                ProductId = swap?.ProductId ?? c.ComponentProductId,
                ProductVariantId = swap != null ? swap.VariantId : c.ComponentVariantId,
                Qty = c.Qty,
                OriginalUnitPrice = originalPrice,
                Surcharge = swap?.Surcharge ?? 0m,
            });
        }
        return input;
    }

    /// <summary>
    /// The price the shop charges today for each component product/variant of a definition: site override →
    /// sale price in window → price (spec §4). Without a site: catalog prices.
    /// </summary>
    public async Task<Dictionary<(int ProductId, int? VariantId), decimal>> ResolveComponentPricesAsync(BundleDefinition def, int? siteId, CancellationToken cancelToken)
    {
        var keys = def.Components.Select(c => (ProductId: c.ComponentProductId, VariantId: c.ComponentVariantId))
            .Concat(def.Components.SelectMany(c => c.Swaps).Select(s => (ProductId: s.SwapProductId, VariantId: s.SwapVariantId)))
            .Distinct()
            .ToList();
        return await ResolvePricesAsync(keys, siteId, cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computed unit price (qty 1, no swaps) of several bundle products at once, for list screens: definitions
    /// are loaded once and every component/swap price is resolved in a single pass with the same site-effective
    /// resolver the bundles list uses (<paramref name="siteId"/> = per-site endpoint, null = canonical prices).
    /// Products without a definition are absent from the result. Cheap no-op for an empty id list.
    /// </summary>
    public async Task<Dictionary<int, decimal>> ComputeUnitPricesAsync(IReadOnlyCollection<int> bundleProductIds, int? siteId, CancellationToken cancelToken)
    {
        var result = new Dictionary<int, decimal>();
        if (bundleProductIds.Count == 0) return result;

        var defs = await _bundleStorage.GetDefinitionsAsync(bundleProductIds, cancelToken).ConfigureAwait(false);
        if (defs.Count == 0) return result;

        var keys = defs.Values
            .SelectMany(d => d.Components.Select(c => (ProductId: c.ComponentProductId, VariantId: c.ComponentVariantId))
                .Concat(d.Components.SelectMany(c => c.Swaps).Select(s => (ProductId: s.SwapProductId, VariantId: s.SwapVariantId))))
            .Distinct()
            .ToList();
        var prices = await ResolvePricesAsync(keys, siteId is > 0 ? siteId : null, cancelToken).ConfigureAwait(false);

        foreach (var def in defs.Values)
            result[def.ProductId] = BundlePricingEngine.Price(BuildPricingInput(def, prices, 1m, null)).UnitPrice;
        return result;
    }

    public async Task<Dictionary<(int ProductId, int? VariantId), decimal>> ResolvePricesAsync(
        IReadOnlyCollection<(int ProductId, int? VariantId)> keys, int? siteId, CancellationToken cancelToken)
    {
        var result = new Dictionary<(int, int?), decimal>();
        if (keys.Count == 0) return result;
        var now = DateTime.UtcNow;

        var productIds = keys.Select(k => k.ProductId).Distinct().ToList();
        var variantIds = keys.Where(k => k.VariantId is > 0).Select(k => k.VariantId!.Value).Distinct().ToList();
        var products = await _bundleStorage.GetCatalogProductInfosAsync(productIds, cancelToken).ConfigureAwait(false);
        var variants = await _bundleStorage.GetCatalogVariantInfosAsync(variantIds, cancelToken).ConfigureAwait(false);

        var overrides = new Dictionary<int, SiteOverrideValues>();
        var variantOverrides = new Dictionary<int, Dictionary<int, ProductSiteOverrideStorage.VariantSiteOverride>>();
        if (siteId is > 0)
        {
            try
            {
                var ov = await _overrideStorage.GetOverridesForSiteAsync(productIds, siteId.Value, cancelToken).ConfigureAwait(false);
                foreach (var o in ov) overrides[o.ProductId] = o;
                variantOverrides = await _overrideStorage.GetVariantOverridesForSiteAsync(productIds, siteId.Value, cancelToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bundle pricing: failed to load per-site overrides for site {SiteId}; using catalog prices", siteId);
            }
        }

        foreach (var key in keys)
        {
            products.TryGetValue(key.ProductId, out var p);
            overrides.TryGetValue(key.ProductId, out var ov);
            decimal price;
            if (key.VariantId is > 0 && variants.TryGetValue(key.VariantId.Value, out var v))
            {
                ProductSiteOverrideStorage.VariantSiteOverride? vov = null;
                if (variantOverrides.TryGetValue(key.ProductId, out var perProduct))
                    perProduct.TryGetValue(key.VariantId.Value, out vov);
                price = BundlePricingEngine.ResolveVariantPrice(v.Price, v.SalePrice, vov?.Price, vov?.SalePrice);
                // A variant without its own price falls back to the product price.
                if (price <= 0m && p != null)
                    price = BundlePricingEngine.ResolveProductPrice(p.Price, p.SalePrice, p.SalePriceStartDate, p.SalePriceEndDate,
                        ov?.Price, ov?.SalePrice, ov?.SalePriceStartDate, ov?.SalePriceEndDate, now);
            }
            else if (p != null)
            {
                price = BundlePricingEngine.ResolveProductPrice(p.Price, p.SalePrice, p.SalePriceStartDate, p.SalePriceEndDate,
                    ov?.Price, ov?.SalePrice, ov?.SalePriceStartDate, ov?.SalePriceEndDate, now);
            }
            else
            {
                price = 0m;
            }
            result[key] = price;
        }
        return result;
    }

    private async Task<int?> ResolveWooIdAsync(Product product, int siteId, CancellationToken cancelToken)
    {
        var wooId = await _overrideStorage.GetSiteWooProductIdAsync(product.Id, siteId, cancelToken).ConfigureAwait(false);
        if (wooId is > 0) return wooId;
        if (product.WooCommerceId is > 0 && product.Site?.Any(s => s.Id == siteId) == true)
            return product.WooCommerceId;
        return null;
    }

    private static string Norm(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
}
