# DB Schema Analysis & Integration Plan

## Current DB Schema Review

### ✅ Already Implemented

### Site Entity
- ✅ `AccountId` - Links to Account
- ✅ `SiteName` - Matches `site_name`
- ✅ `Location` - Matches `location`
- ✅ `Description` - Matches `description`
- ✅ `ContactEmail` - Matches `contact_email`
- ✅ `ContactPhone` - Matches `contact_phone`
- ✅ `IsKosherSite` - Matches `is_kosher_site`
- ✅ `AllowWeightedProducts` - Matches `allow_weighted_products`
- ✅ `Currency` - Matches `currency` (default: "ILS")
- ✅ Many-to-Many with `BusinessTypes` (via `SiteBusinessType`)
- ✅ Many-to-Many with `Products` (via `ProductSite`)
- ✅ Many-to-Many with `Categories` (via `CategorySite`)
- ✅ Many-to-Many with `Users` (via `SiteUser`)

### Product Entity
- ✅ `Name` - Matches `name`
- ✅ `ShortDescription` - Matches `short_description`
- ✅ `LongDescription` - Matches `long_description`
- ✅ `Price` - Matches `price`
- ✅ `SalePrice` - Matches `sale_price`
- ✅ `SalePriceStartDate` - Matches `sale_price_start_date`
- ✅ `SalePriceEndDate` - Matches `sale_price_end_date`
- ✅ `CostPrice` - Matches `cost_price`
- ✅ `Sku` - Matches `sku`
- ✅ `StockManagementTypeId` - Matches `stock_management_type`
- ✅ `StockQuantity` - Matches `stock_quantity`
- ✅ `StockStatusId` - Matches `stock_status`
- ✅ `Weight` - Matches `weight`
- ✅ `ShippingClassId` - Matches `shipping_class`
- ✅ `StatusId` - Matches `status` (published/draft/archived)
- ✅ `VisibilityId` - Matches `visibility` (public/hidden/private)
- ✅ `IsKosher` - Matches `is_kosher`
- ✅ `IsWeighted` - Matches `is_weighted`
- ✅ `SetupTypeId` - Matches `setup_type`
- ✅ `WeightConfigId` - Matches `weight_config`
- ✅ `BrandId` - Matches `brand`
- ✅ `SupplierId` - Matches `supplier`
- ✅ Many-to-Many with `Sites` (via `ProductSite`)
- ✅ `ProductCategories` - For category relationships
- ✅ `ProductImages` - For image URLs
- ✅ `ProductOptions` - For product options
- ✅ `ProductVariants` - For variants

### Category Entity
- ✅ `Name` - Matches `name`
- ✅ `ParentCategoryId` - Matches `parent_id`
- ✅ `Description` - Matches `description`
- ✅ `CustomName` - Matches `customName`
- ✅ `IsEnabled` - Matches `isEnabled`
- ✅ `SortOrder` - Matches `sortOrder`
- ✅ `DisplayAsMain` - Matches `display_as_main`
- ✅ Many-to-Many with `Sites` (via `CategorySite`)

### Attribute Entity
- ✅ `Name` - Matches `name`
- ✅ `SiteId` - Matches `site_id` (required)
- ✅ `AttributeValues` - Matches `values` array

### BusinessType Entity
- ✅ `Name` - Matches `name`
- ✅ `Icon` - Matches `icon`

### GlobalCategory Entity
- ✅ `Name` - Matches `name`
- ✅ `ParentGlobalCategoryId` - Matches `parent_category_id`
- ✅ `SortOrder` - Matches `sort_order`
- ✅ Many-to-Many with `BusinessTypes` (via `GlobalCategoryBusinessType`)

---

## ⚠️ Issues & Required Changes

### 1. Site.Status Data Type Mismatch
**Current:** `string? Status` (nullable string)
**Expected:** Enum `["active", "inactive"]` with default `"active"`

**Action Required:**
- Create/Update `SiteStatus` lookup table OR
- Add constraint to ensure only "active" or "inactive" values
- Update default value to "active"

### 2. Product/Category AccountId vs Site Relationship
**Current:** Both `Product` and `Category` have:
- `AccountId` (nullable int)
- Many-to-Many with `Sites` (via junction tables)

**Issue:** This creates ambiguity. Based on entity schemas:
- Products should belong to Sites (via many-to-many)
- Categories should belong to Sites (via many-to-many)
- Account relationship should be implicit through Site → Account

**Recommendation:**
- Keep `AccountId` for backward compatibility and query optimization
- Ensure `AccountId` is always set based on the Site's AccountId
- Document that Site relationship is primary, AccountId is derived

### 3. User vs Client Schema Mismatch
**Current User Model:**
- `RoleId` (int) - references Role table
- `AccountId` (nullable int)
- Many-to-Many with `Sites` (via `SiteUser`)

**Expected Client Schema:**
- `client_role`: enum `["super_admin", "account_admin", "site_admin"]`
- `accountId`: string
- `site_ids`: array of strings
- `status`: enum `["active", "inactive", "suspended"]`

**Action Required:**
- Map `RoleId` to `client_role` enum values
- Ensure `SiteUser` junction table provides `site_ids` array
- Map `UserStatus` to client status enum

### 4. Product Image URLs
**Current:** `ProductImages` collection (separate table)
**Expected:** `image_urls` array of strings

**Action Required:**
- Keep `ProductImages` table for DB normalization
- Map to `image_urls` array in DTOs/API responses

### 5. Product Tags
**Current:** Many-to-Many with `Tags` table (via `ProductTag`)
**Expected:** `tags` array of strings

**Action Required:**
- Keep `Tags` table for normalization
- Map to `tags` array in DTOs/API responses

### 6. Category Site Relationship Logic
**Entity Schema:** `site_ids` array - "Empty means all sites"
**Current:** Many-to-Many via `CategorySite`

**Action Required:**
- Implement logic: if `CategorySite` is empty, category applies to all sites for that account
- Or add `IsGlobalToAccount` flag to Category

### 7. Product Site Relationship Logic
**Entity Schema:** `site_ids` array - "Empty means all sites"
**Current:** Many-to-Many via `ProductSite`

**Action Required:**
- Implement logic: if `ProductSite` is empty, product applies to all sites for that account

---

## 📋 Recommended DB Changes

### Option 1: Minimal Changes (Recommended)
Keep current structure, add helper fields:

```sql
-- Add SiteStatus constraint or lookup
ALTER TABLE Site ADD CONSTRAINT CK_Site_Status 
    CHECK (Status IS NULL OR Status IN ('active', 'inactive'));

-- Ensure AccountId consistency
-- (Add trigger or computed column to ensure Product/Category AccountId matches Site's AccountId)

-- Add computed/helper columns if needed
```

### Option 2: Add Helper Fields
```sql
-- Add flags to indicate "all sites" behavior
ALTER TABLE Category ADD IsGlobalToAccount BIT DEFAULT 0;
ALTER TABLE Product ADD IsGlobalToAccount BIT DEFAULT 0;
```

### Option 3: No DB Changes Needed
- Current schema supports all requirements
- Handle "all sites" logic in application layer
- Map enums in DTOs/services

---

## 🎯 Integration Strategy

### Phase 1: DB Schema Validation
1. ✅ Verify Site-Product-Category relationships work correctly
2. ✅ Ensure AccountId consistency
3. ✅ Add constraints for Status enums

### Phase 2: Backend DTOs & Mapping
1. Create DTOs matching entity schemas
2. Map DB models to DTOs (handle arrays, enums)
3. Handle "all sites" logic (empty site_ids = all sites)

### Phase 3: Backend Services
1. Update services to work with Site-based queries
2. Implement authorization (user → site → account)
3. Add validation for site_ids arrays

### Phase 4: Backend Controllers
1. Update endpoints to accept site_ids arrays
2. Handle empty site_ids as "all sites"
3. Add site filtering in queries

### Phase 5: Client Integration
1. Create API client functions
2. Map entity schemas to API calls
3. Handle site context in client

---

## 🔍 Key Design Decisions Needed

1. **Site Relationship Logic:**
   - How to handle "empty site_ids = all sites"?
     - Option A: Store all site_ids explicitly
     - Option B: Use NULL/empty junction table + flag
     - Option C: Application logic only

2. **AccountId in Product/Category:**
   - Keep for performance (indexed queries)?
   - Remove and derive from Site?
   - Keep but make it computed/derived?

3. **User/Client Mapping:**
   - Map RoleId to client_role enum in DTOs?
   - Create new Client entity?
   - Use existing User with mapping layer?

---

## ✅ Next Steps

1. Review and approve design decisions
2. Create DB migration (if needed)
3. Update backend models/DTOs
4. Update services and controllers
5. Create client API integration layer
6. Test end-to-end

