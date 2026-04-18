namespace George.Services.Response;

public class InventoryNamedOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class InventoryVariationLineDto
{
    public int VariationId { get; set; }
    public string Label { get; set; } = "";
    public decimal StockQuantity { get; set; }
    public decimal? MinStock { get; set; }
    public string? UnitLabel { get; set; }
    public decimal UnitPrice { get; set; }
    public string? SupplierName { get; set; }
    /// <summary>ok | low | out</summary>
    public string StockStatus { get; set; } = "ok";
}

public class InventoryProductGroupDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryDisplayName { get; set; } = "";
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int? BrandId { get; set; }
    public string? BrandName { get; set; }
    /// <summary>quantity | status | variation — תואם SPA (<c>stock_management_type</c>).</summary>
    public string? StockManagementType { get; set; }
    /// <summary>כש־<see cref="StockManagementType"/> הוא variation: האם מלאי לפי כמות בכל וריאציה.</summary>
    public bool? VariationStockByQuantity { get; set; }
    public decimal? StockQuantity { get; set; }
    public decimal? MinStock { get; set; }
    public string? UnitLabel { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>ok | low | out</summary>
    public string StockStatus { get; set; } = "ok";
    public List<InventoryVariationLineDto> Variations { get; set; } = new();
}

public class InventoryReportRes
{
    public List<InventoryNamedOptionDto> Categories { get; set; } = new();
    public List<InventoryNamedOptionDto> Suppliers { get; set; } = new();
    public List<InventoryNamedOptionDto> Brands { get; set; } = new();
    public List<InventoryProductGroupDto> ProductGroups { get; set; } = new();
}
