# VoucherPrinterUseAgent – backend wiring

After running the migration that adds `Site.VoucherPrinterUseAgent` (default 1), the API must expose it so the frontend can read and update it.

1. **Site entity** (table mapping)  
   Add:
   ```csharp
   public bool VoucherPrinterUseAgent { get; set; }
   ```
   If your Site class is generated (e.g. scaffold), re-scaffold or add a partial class with this property.

2. **Site response DTO** (e.g. `SiteRes`)  
   Add:
   ```csharp
   public bool VoucherPrinterUseAgent { get; set; }
   ```
   Map from the entity when returning a site (GET/PUT response).

3. **Site create/update request**  
   Add:
   ```csharp
   public bool? VoucherPrinterUseAgent { get; set; }
   ```
   When updating a site (PUT), set the entity property from the request so the new column is persisted.

The frontend already sends and reads `voucherPrinterUseAgent`; it defaults to **true** when the value is missing.
