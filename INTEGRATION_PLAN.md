# Step-by-Step Integration Plan

## Overview
This document outlines the step-by-step process to integrate the backend with the client, ensuring the DB schema aligns with the entity schemas.

---

## Step 1: DB Schema Validation & Minor Updates

### 1.1 Review Current Schema
- ✅ Already done - see `DB_SCHEMA_ANALYSIS.md`
- Current schema supports Site-based architecture

### 1.2 Add Status Constraints (Optional but Recommended)
```sql
-- Ensure Site.Status only allows valid values
ALTER TABLE Site 
ADD CONSTRAINT CK_Site_Status 
CHECK (Status IS NULL OR Status IN ('active', 'inactive'));

-- Set default if not already set
ALTER TABLE Site 
ADD CONSTRAINT DF_Site_Status DEFAULT 'active' FOR Status;
```

### 1.3 Decision: Handle "All Sites" Logic
**Recommendation:** Handle in application layer (no DB changes needed)
- Empty `site_ids` array = query all sites for the account
- When saving, if `site_ids` is empty, don't create junction table entries
- When reading, if no junction entries exist, return empty array (client interprets as "all sites")

---

## Step 2: Update Backend Models (if needed)

### 2.1 Review Model Files
- Models are already correctly structured
- No changes needed to entity classes

### 2.2 Add Helper Properties (Optional)
Consider adding computed properties to models for easier access:
```csharp
// In Product.cs (example)
public bool IsGlobalToAccount => !Sites.Any();
```

---

## Step 3: Create/Update DTOs

### 3.1 Product DTOs
**File:** `Core/George.Services/Response/ProductRes.cs`

```csharp
public class ProductRes
{
    public string Id { get; set; } // GuidId as string
    public string Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public decimal? Price { get; set; }
    public decimal? SalePrice { get; set; }
    public DateTime? SalePriceStartDate { get; set; }
    public DateTime? SalePriceEndDate { get; set; }
    public decimal? CostPrice { get; set; }
    public string? Sku { get; set; }
    public string StockManagementType { get; set; } // "quantity" or "status"
    public int? StockQuantity { get; set; }
    public string StockStatus { get; set; } // "in_stock", "out_of_stock", "on_backorder"
    public decimal? Weight { get; set; }
    public string? ShippingClass { get; set; }
    public List<ProductOptionRes> ProductOptions { get; set; } = new();
    public List<ProductVariantRes> Variants { get; set; } = new();
    public string Status { get; set; } // "published", "draft", "archived"
    public string Visibility { get; set; } // "public", "hidden", "private"
    public List<string> CategoryIds { get; set; } = new();
    public List<string> SubcategoryIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string? Brand { get; set; }
    public string? Supplier { get; set; }
    public bool? IsKosher { get; set; }
    public bool? IsWeighted { get; set; }
    public string? SetupType { get; set; }
    public WeightConfigRes? WeightConfig { get; set; }
    public List<string> SiteIds { get; set; } = new(); // Empty = all sites
}
```

### 3.2 Category DTOs
**File:** `Core/George.Services/Response/CategoryRes.cs`

```csharp
public class CategoryRes
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public string? CustomName { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
    public bool DisplayAsMain { get; set; }
    public List<string> SiteIds { get; set; } = new(); // Empty = all sites
}
```

### 3.3 Site DTOs
**File:** `Core/George.Services/Response/SiteRes.cs`

```csharp
public class SiteRes
{
    public string Id { get; set; }
    public string AccountId { get; set; }
    public string SiteName { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public List<string> BusinessTypeIds { get; set; } = new();
    public string Status { get; set; } // "active", "inactive"
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsKosherSite { get; set; }
    public bool AllowWeightedProducts { get; set; }
    public string Currency { get; set; }
}
```

### 3.4 Request DTOs
**File:** `Core/George.Common/Request/ProductReq.cs`

```csharp
public class CreateProductReq
{
    public string Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public decimal? Price { get; set; }
    // ... other fields matching entity schema
    public List<string> SiteIds { get; set; } = new(); // Empty = all sites
}
```

---

## Step 4: Update Storage Layer

### 4.1 ProductStorage Updates
**File:** `Core/George.Data/ProductStorage.cs`

Key changes:
1. Query products by Site (not just Account)
2. Handle empty site_ids (all sites)
3. Map ProductImages to image_urls array
4. Map Tags to tags array

```csharp
public async Task<List<ProductDto>> GetProductsBySiteAsync(int siteId, CancellationToken cancelToken)
{
    // Query products for a specific site
    // Include products with no site assignment (global to account)
}

public async Task<List<ProductDto>> GetProductsByAccountAsync(int accountId, CancellationToken cancelToken)
{
    // Get all products for account (across all sites)
}
```

### 4.2 CategoryStorage Updates
**File:** `Core/George.Data/CategoryStorage.cs`

Similar pattern:
1. Query by Site
2. Handle empty site_ids
3. Map to DTOs with site_ids array

---

## Step 5: Update Services Layer

### 5.1 ProductService Updates
**File:** `Core/George.Services/ProductService.cs`

```csharp
public async Task<IApiResponse<List<ProductRes>>> GetProductsAsync(
    int? siteId, 
    int? accountId, 
    CancellationToken cancelToken)
{
    // If siteId provided, get products for that site
    // If accountId provided, get all products for account
    // Map DB models to DTOs with arrays
}

public async Task<IApiResponse<ProductRes>> CreateProductAsync(
    CreateProductReq req, 
    CancellationToken cancelToken)
{
    // Create product
    // Handle site_ids array:
    //   - If empty, don't create ProductSite entries (means all sites)
    //   - If has values, create ProductSite entries
}
```

### 5.2 Authorization Checks
Add site-based authorization:
```csharp
// Check if user has access to site
private async Task<bool> ValidateSiteAccessAsync(int siteId, CancellationToken cancelToken)
{
    if (_authUser.IsMaster) return true;
    
    // Check if user's sites include this site
    var userSites = await _userStorage.GetUserSitesAsync(_authUser.Id, cancelToken);
    return userSites.Contains(siteId);
}
```

---

## Step 6: Update Controllers

### 6.1 ProductController Updates
**File:** `Api/George.Api/Controllers/ProductController.cs`

```csharp
[HttpGet("Site/{siteId:int}")]
public async Task<IActionResult> GetProductsBySiteAsync(
    [FromRoute] int siteId, 
    CancellationToken cancelToken = default)
{
    // Validate site access
    // Get products for site
}

[HttpGet("Account/{accountId:int}")]
public async Task<IActionResult> GetProductsByAccountAsync(
    [FromRoute] int accountId, 
    CancellationToken cancelToken = default)
{
    // Get all products for account
}

[HttpPost]
public async Task<IActionResult> CreateProductAsync(
    [FromBody] CreateProductReq req, 
    CancellationToken cancelToken = default)
{
    // Create product with site_ids handling
}
```

### 6.2 SiteController Updates
Ensure Site endpoints support the entity schema:
- GET /Site/{siteId}
- POST /Site
- PUT /Site/{siteId}
- GET /Account/{accountId}/Sites

---

## Step 7: Create Client API Integration

### 7.1 API Client Setup
**File:** `src/services/api/client.ts` (or similar)

```typescript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: process.env.REACT_APP_API_URL || 'http://localhost:5000',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add auth token interceptor
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('authToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default apiClient;
```

### 7.2 Product API Functions
**File:** `src/services/api/productApi.ts`

```typescript
import apiClient from './client';
import { Product } from '../types';

export const productApi = {
  // Get products by site
  getProductsBySite: async (siteId: string): Promise<Product[]> => {
    const response = await apiClient.get(`/Product/Site/${siteId}`);
    return response.data.data;
  },

  // Get products by account
  getProductsByAccount: async (accountId: string): Promise<Product[]> => {
    const response = await apiClient.get(`/Product/Account/${accountId}`);
    return response.data.data;
  },

  // Create product
  createProduct: async (product: Partial<Product>): Promise<Product> => {
    const response = await apiClient.post('/Product', product);
    return response.data.data;
  },

  // Update product
  updateProduct: async (
    productId: string, 
    product: Partial<Product>
  ): Promise<Product> => {
    const response = await apiClient.put(`/Product/${productId}`, product);
    return response.data.data;
  },

  // Delete product
  deleteProduct: async (productId: string): Promise<void> => {
    await apiClient.delete(`/Product/${productId}`);
  },
};
```

### 7.3 Category API Functions
**File:** `src/services/api/categoryApi.ts`

Similar pattern for categories.

### 7.4 Site API Functions
**File:** `src/services/api/siteApi.ts`

```typescript
export const siteApi = {
  getSitesByAccount: async (accountId: string): Promise<Site[]> => {
    const response = await apiClient.get(`/Account/${accountId}/Sites`);
    return response.data.data;
  },

  getSite: async (siteId: string): Promise<Site> => {
    const response = await apiClient.get(`/Site/${siteId}`);
    return response.data.data;
  },

  createSite: async (site: Partial<Site>): Promise<Site> => {
    const response = await apiClient.post('/Site', site);
    return response.data.data;
  },
};
```

---

## Step 8: Type Definitions

### 8.1 TypeScript Types
**File:** `src/types/entities.ts`

```typescript
export interface Product {
  id?: string;
  name: string;
  short_description?: string;
  long_description?: string;
  image_urls?: string[];
  price?: number;
  sale_price?: number;
  sale_price_start_date?: string;
  sale_price_end_date?: string;
  cost_price?: number;
  sku?: string;
  stock_management_type?: 'quantity' | 'status';
  stock_quantity?: number;
  stock_status?: 'in_stock' | 'out_of_stock' | 'on_backorder';
  weight?: number;
  shipping_class?: string;
  product_options?: ProductOption[];
  variants?: ProductVariant[];
  status?: 'published' | 'draft' | 'archived';
  visibility?: 'public' | 'hidden' | 'private';
  category_ids?: string[];
  subcategory_ids?: string[];
  tags?: string[];
  brand?: string;
  supplier?: string;
  is_kosher?: boolean;
  is_weighted?: boolean;
  setup_type?: string;
  weight_config?: WeightConfig;
  site_ids?: string[]; // Empty = all sites
}

export interface Category {
  id?: string;
  name: string;
  parent_id?: string;
  description?: string;
  customName?: string;
  isEnabled?: boolean;
  sortOrder?: number;
  display_as_main?: boolean;
  site_ids?: string[]; // Empty = all sites
}

export interface Site {
  id?: string;
  accountId: string;
  site_name: string;
  location?: string;
  description?: string;
  business_type_ids?: string[];
  status?: 'active' | 'inactive';
  contact_email?: string;
  contact_phone?: string;
  is_kosher_site?: boolean;
  allow_weighted_products?: boolean;
  currency?: string;
}
```

---

## Step 9: Testing

### 9.1 Backend Unit Tests
- Test storage layer queries
- Test service layer mapping
- Test "all sites" logic

### 9.2 Integration Tests
- Test API endpoints
- Test site-based filtering
- Test authorization

### 9.3 End-to-End Tests
- Test client → API → DB flow
- Test site context switching
- Test empty site_ids behavior

---

## Step 10: Documentation

### 10.1 API Documentation
- Update Swagger/OpenAPI docs
- Document site_ids behavior
- Document authorization requirements

### 10.2 Client Documentation
- Document API client usage
- Document type definitions
- Document site context handling

---

## Implementation Order

1. ✅ **Step 1:** DB Schema Validation (DONE - see analysis)
2. **Step 2:** Update Backend Models (if needed)
3. **Step 3:** Create/Update DTOs
4. **Step 4:** Update Storage Layer
5. **Step 5:** Update Services Layer
6. **Step 6:** Update Controllers
7. **Step 7:** Create Client API Integration
8. **Step 8:** Type Definitions
9. **Step 9:** Testing
10. **Step 10:** Documentation

---

## Notes

- **Site Context:** Always consider site context in queries
- **Authorization:** Implement site-based authorization checks
- **Empty site_ids:** Treat as "all sites for account"
- **Performance:** Consider indexing on SiteId in junction tables
- **Backward Compatibility:** Keep AccountId for existing queries if needed

