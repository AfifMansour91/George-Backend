using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services;

/// <summary>דוח מלאי — מוצרי קטלוג לאתר עם מלאי ווריאציות (תואם SPA <c>InventoryReport</c>).</summary>
public class InventoryReportService : ServiceBase
{
    private readonly ProductsReportStorage _productsReportStorage;
    private readonly CategoryStorage _categoryStorage;

    public InventoryReportService(
        ILogger<InventoryReportService> logger,
        IMapper mapper,
        CacheManager cache,
        ProductsReportStorage productsReportStorage,
        CategoryStorage categoryStorage)
        : base(logger, mapper, cache)
    {
        _productsReportStorage = productsReportStorage;
        _categoryStorage = categoryStorage;
    }

    public async Task<IApiResponse<InventoryReportRes>> GetReportAsync(int siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<InventoryReportRes>();
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

        var products = await _productsReportStorage
            .GetSiteCatalogProductsForInventoryReportAsync(siteId, cancelToken)
            .ConfigureAwait(false);

        var account = await _productsReportStorage.GetAccountForSiteAsync(siteId, cancelToken).ConfigureAwait(false);

        var categories = await _categoryStorage
            .GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId, IsEnabled = true },
                new PagingExDto(10_000) { IncludeTotal = false, Skip = 0 },
                cancelToken)
            .ConfigureAwait(false);

        var res = new InventoryReportRes
        {
            Categories = categories.Items
                .Where(c => !c.IsDeleted && c.IsActive)
                .Select(c => new InventoryNamedOptionDto { Id = c.Id, Name = c.Name ?? "" })
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        var supplierMap = new Dictionary<int, string>();
        var brandMap = new Dictionary<int, string>();
        foreach (var p in products)
        {
            if (p.SupplierId is > 0 && p.Supplier != null && !supplierMap.ContainsKey(p.SupplierId.Value))
                supplierMap[p.SupplierId.Value] = p.Supplier.Name ?? "";
            if (p.BrandId is > 0 && p.Brand != null && !brandMap.ContainsKey(p.BrandId.Value))
                brandMap[p.BrandId.Value] = p.Brand.Name ?? "";
        }

        res.Suppliers = supplierMap
            .Select(kv => new InventoryNamedOptionDto { Id = kv.Key, Name = kv.Value })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        res.Brands = brandMap
            .Select(kv => new InventoryNamedOptionDto { Id = kv.Key, Name = kv.Value })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        res.ProductGroups = products
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => MapProductGroup(p, account))
            .ToList();

        response.Data = res;
        return response;
    }

    private static InventoryProductGroupDto MapProductGroup(Product p, Account? account)
    {
        var categoryId = ResolveCategoryId(p);
        var variants = ProductCatalogStockClassification.ActiveVariants(p);
        var useVariationRows =
            ProductCatalogStockClassification.IsVariationManagement(p) && variants.Count > 0;

        var dto = new InventoryProductGroupDto
        {
            ProductId = p.Id,
            Name = p.Name ?? "",
            CategoryId = categoryId,
            CategoryDisplayName = CategoryDisplayName(p),
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name,
            BrandId = p.BrandId,
            BrandName = p.Brand?.Name,
            StockManagementType = ProductCatalogStockClassification.StockManagementTypeForApi(p),
            VariationStockByQuantity = p.VariationStockByQuantity,
            UnitPrice = useVariationRows ? 0m : Round2(EffectiveUnitPrice(p)),
            StockStatus = ProductCatalogStockClassification.ClassifyStock(p, account),
        };

        if (useVariationRows)
        {
            dto.StockQuantity = null;
            dto.MinStock = null;
            dto.UnitLabel = UnitLabelForProduct(p);
            dto.Variations = variants
                .OrderBy(v => v.Id)
                .Select(v => new InventoryVariationLineDto
                {
                    VariationId = v.Id,
                    Label = FormatVariantLabel(v),
                    StockQuantity = Round4ToDecimal(v.StockQuantity ?? 0m),
                    MinStock = p.LowStockThreshold is > 0m ? p.LowStockThreshold : null,
                    UnitLabel = dto.UnitLabel,
                    UnitPrice = Round2(EffectiveUnitPrice(v)),
                    SupplierName = p.Supplier?.Name,
                    StockStatus = ProductCatalogStockClassification.ClassifySingleVariant(p, v, account),
                })
                .ToList();
        }
        else
        {
            dto.StockQuantity = p.StockQuantity;
            dto.MinStock = p.LowStockThreshold;
            dto.UnitLabel = UnitLabelForProduct(p);
            dto.Variations = new List<InventoryVariationLineDto>();
        }

        return dto;
    }

    private static decimal Round4ToDecimal(decimal d) => Math.Round(d, 4, MidpointRounding.AwayFromZero);

    private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    private static int ResolveCategoryId(Product p)
    {
        var primary = PrimaryCategoryId(p);
        if (primary != null) return primary.Value;
        var first = p.ProductCategory?.FirstOrDefault()?.CategoryId;
        return first ?? 0;
    }

    private static int? PrimaryCategoryId(Product? p)
    {
        if (p?.ProductCategory == null || p.ProductCategory.Count == 0) return null;
        var primary = p.ProductCategory.FirstOrDefault(x => x.IsPrimary);
        if (primary != null) return primary.CategoryId;
        return p.ProductCategory.First().CategoryId;
    }

    private static string CategoryDisplayName(Product p)
    {
        var pcs = p.ProductCategory?.Where(x => x.Category != null).ToList() ?? new List<ProductCategory>();
        if (pcs.Count == 0) return "";
        if (pcs.Count == 1) return pcs[0].Category!.Name ?? "";
        var nonPrimary = pcs.Where(x => !x.IsPrimary).OrderBy(x => x.Category!.Name).FirstOrDefault();
        if (nonPrimary != null) return nonPrimary.Category!.Name ?? "";
        return pcs.OrderBy(x => x.Category!.Name).First().Category!.Name ?? "";
    }

    private static string? UnitLabelForProduct(Product p)
    {
        if (p.IsWeighted == true)
            return string.IsNullOrWhiteSpace(p.WeightUnit) ? "ק״ג" : p.WeightUnit!.Trim();
        var setup = (p.SetupType?.Name ?? "").Trim().ToLowerInvariant();
        if (setup == "by_weight")
            return string.IsNullOrWhiteSpace(p.WeightUnit) ? "ק״ג" : p.WeightUnit!.Trim();
        return "יח׳";
    }

    private static string FormatVariantLabel(ProductVariant v)
    {
        var opts = v.ProductVariantOptionValue?.OrderBy(o => o.OptionName).ToList() ?? new List<ProductVariantOptionValue>();
        if (opts.Count == 0)
            return $"#{v.Id}";
        return string.Join(" · ", opts.Select(o => $"{o.OptionName}: {o.OptionValue}"));
    }

    private static decimal EffectiveUnitPrice(Product p)
    {
        if (IsSaleActive(p) && p.SalePrice is > 0m)
            return p.SalePrice.Value;
        return p.Price ?? 0m;
    }

    private static decimal EffectiveUnitPrice(ProductVariant v)
    {
        if (v.SalePrice is > 0m)
            return v.SalePrice.Value;
        return v.Price ?? 0m;
    }

    private static bool IsSaleActive(Product p)
    {
        if (p.SalePrice is null or <= 0m) return false;
        var now = DateTime.UtcNow;
        if (p.SalePriceStartDate.HasValue && now < p.SalePriceStartDate.Value) return false;
        if (p.SalePriceEndDate.HasValue && now > p.SalePriceEndDate.Value) return false;
        return true;
    }
}
