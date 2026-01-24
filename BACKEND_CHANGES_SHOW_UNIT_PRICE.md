# Backend & Database Changes for Show Unit Price Feature

## Summary
This document outlines all the changes needed in the backend and database to support the new `show_unit_price` feature for products sold by unit.

---

## 1. Database Schema Changes

### File: `george-backend\Core\George.DB\Scripts\DBSchema.sql`

**Add new column to WeightConfig table:**

```sql
ALTER TABLE [dbo].[WeightConfig]
ADD [ShowUnitPrice] [bit] NULL;
GO
```

**Or if recreating the table, add to the CREATE TABLE statement:**

```sql
CREATE TABLE [dbo].[WeightConfig](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[UnitId] [int] NULL,
	[StartWeight] [nvarchar](50) NULL,
	[Step] [nvarchar](50) NULL,
	[FixedWeightPerUnit] [bit] NULL,
	[UnitWeight] [nvarchar](50) NULL,
	[UnitWeightModeId] [int] NULL,
	[WeightOptions] [nvarchar](2000) NULL,
	[WeightByVariant] [bit] NULL,
	[ShowPricePer100g] [bit] NULL,
	[ShowUnitPrice] [bit] NULL,  -- ADD THIS LINE
	...
)
```

---

## 2. Model Changes

### File: `george-backend\Core\George.DB\Models\WeightConfig.cs`

**Add property after `ShowPricePer100g`:**

```csharp
public bool? ShowPricePer100g { get; set; }

public bool? ShowUnitPrice { get; set; }  // ADD THIS LINE
```

---

## 3. DTO Changes

### File: `george-backend\Core\George.Data\Dto\ProductDto.cs`

**Add property to `WeightConfigDto` class:**

```csharp
public class WeightConfigDto
{
    public string? Unit { get; set; }
    public string? StartWeight { get; set; }
    public string? Step { get; set; }
    public bool? FixedWeightPerUnit { get; set; }
    public string? UnitWeight { get; set; }
    public string? UnitWeightMode { get; set; }
    public string? WeightOptions { get; set; }
    public bool? WeightByVariant { get; set; }
    public bool? ShowPricePer100g { get; set; }
    public bool? ShowUnitPrice { get; set; }  // ADD THIS LINE
}
```

### File: `george-backend\Core\George.Services\Request\ProductReq.cs`

**Add property to `WeightConfigReq` class:**

```csharp
public class WeightConfigReq
{
    public string? Unit { get; set; }
    public string? StartWeight { get; set; }
    public string? Step { get; set; }
    public bool? FixedWeightPerUnit { get; set; }
    public string? UnitWeight { get; set; }
    public string? UnitWeightMode { get; set; }
    public string? WeightOptions { get; set; }
    public bool? WeightByVariant { get; set; }
    public bool? ShowPricePer100g { get; set; }
    public bool? ShowUnitPrice { get; set; }  // ADD THIS LINE
}
```

### File: `george-backend\Core\George.Services\Response\ProductRes.cs`

**Add property to `WeightConfigRes` class:**

```csharp
public class WeightConfigRes
{
    public string? Unit { get; set; }
    public string? StartWeight { get; set; }
    public string? Step { get; set; }
    public bool? FixedWeightPerUnit { get; set; }
    public string? UnitWeight { get; set; }
    public string? UnitWeightMode { get; set; }
    public string? WeightOptions { get; set; }
    public bool? WeightByVariant { get; set; }
    public bool? ShowPricePer100g { get; set; }
    public bool? ShowUnitPrice { get; set; }  // ADD THIS LINE
}
```

---

## 4. Service Mapping Changes

### File: `george-backend\Core\George.Services\ProductService.cs`

**Update response mapping (around line 671):**

```csharp
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
    ShowUnitPrice = product.WeightConfig.ShowUnitPrice  // ADD THIS LINE
};
```

**Update request mapping (around line 708):**

```csharp
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
    ShowUnitPrice = req.WeightConfig.ShowUnitPrice  // ADD THIS LINE
} : null
```

### File: `george-backend\Core\George.Services\TemplateProductService.cs`

**Update response mapping (around line 633):**

```csharp
res.WeightConfig = new George.Services.Response.WeightConfigRes
{
    Unit = templateProduct.WeightConfig.Unit?.Name,
    StartWeight = templateProduct.WeightConfig.StartWeight,
    Step = templateProduct.WeightConfig.Step,
    FixedWeightPerUnit = templateProduct.WeightConfig.FixedWeightPerUnit,
    UnitWeight = templateProduct.WeightConfig.UnitWeight,
    UnitWeightMode = templateProduct.WeightConfig.UnitWeightMode?.Name,
    WeightOptions = templateProduct.WeightConfig.WeightOptions,
    WeightByVariant = templateProduct.WeightConfig.WeightByVariant,
    ShowPricePer100g = templateProduct.WeightConfig.ShowPricePer100g,
    ShowUnitPrice = templateProduct.WeightConfig.ShowUnitPrice  // ADD THIS LINE
};
```

**Update request mapping (around line 670):**

```csharp
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
    ShowUnitPrice = req.WeightConfig.ShowUnitPrice  // ADD THIS LINE
} : null
```

---

## 5. Storage Layer Changes

### File: `george-backend\Core\George.Data\ProductStorage.cs`

**Update `CreateOrUpdateWeightConfigAsync` method (around line 518):**

```csharp
var weightConfig = new WeightConfig
{
    StartWeight = req.StartWeight,
    Step = req.Step,
    FixedWeightPerUnit = req.FixedWeightPerUnit,
    UnitWeight = req.UnitWeight,
    WeightOptions = req.WeightOptions,
    WeightByVariant = req.WeightByVariant,
    ShowPricePer100g = req.ShowPricePer100g,
    ShowUnitPrice = req.ShowUnitPrice,  // ADD THIS LINE
    IsDeleted = false
};
```

### File: `george-backend\Core\George.Data\TemplateProductStorage.cs`

**Update `CreateOrUpdateWeightConfigAsync` method (around line 515):**

```csharp
var weightConfig = new WeightConfig
{
    StartWeight = req.StartWeight,
    Step = req.Step,
    FixedWeightPerUnit = req.FixedWeightPerUnit,
    UnitWeight = req.UnitWeight,
    WeightOptions = req.WeightOptions,
    WeightByVariant = req.WeightByVariant,
    ShowPricePer100g = req.ShowPricePer100g,
    ShowUnitPrice = req.ShowUnitPrice,  // ADD THIS LINE
    IsDeleted = false
};
```

---

## 6. Database Migration Script

### Create a new migration file or add to existing migration:

```sql
-- Migration: Add ShowUnitPrice to WeightConfig
-- Date: 2026-01-24

IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[WeightConfig]') 
    AND name = 'ShowUnitPrice'
)
BEGIN
    ALTER TABLE [dbo].[WeightConfig]
    ADD [ShowUnitPrice] [bit] NULL;
    
    PRINT 'Added ShowUnitPrice column to WeightConfig table';
END
ELSE
BEGIN
    PRINT 'ShowUnitPrice column already exists in WeightConfig table';
END
GO
```

---

## 7. Optional: WooCommerce Sync

The `WooCommerceService.cs` file handles syncing weight config to WooCommerce. If you want to sync `ShowUnitPrice` to WooCommerce as well, you would need to add it to the metadata around line 652. However, this is optional and depends on whether your WooCommerce plugin supports this field.

---

## Summary Checklist

- [ ] Add `ShowUnitPrice` column to database (ALTER TABLE or migration)
- [ ] Add `ShowUnitPrice` property to `WeightConfig.cs` model
- [ ] Add `ShowUnitPrice` to `WeightConfigDto` in `ProductDto.cs`
- [ ] Add `ShowUnitPrice` to `WeightConfigReq` in `ProductReq.cs`
- [ ] Add `ShowUnitPrice` to `WeightConfigRes` in `ProductRes.cs`
- [ ] Update response mapping in `ProductService.cs` (2 places)
- [ ] Update response mapping in `TemplateProductService.cs` (2 places)
- [ ] Update `CreateOrUpdateWeightConfigAsync` in `ProductStorage.cs`
- [ ] Update `CreateOrUpdateWeightConfigAsync` in `TemplateProductStorage.cs`
- [ ] (Optional) Update WooCommerce sync if needed

---

## Notes

- The field is nullable (`bool?`) to maintain backward compatibility
- Existing records will have `NULL` for this field, which should be treated as `false`
- The frontend already handles this field, so once backend changes are deployed, the feature will work end-to-end
