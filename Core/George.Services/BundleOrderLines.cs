using George.DB;

namespace George.Services;

// BundleOrderLines (IsBundleParent / IsBundleChild / IsPlainLine) lives in George.DB (Core/George.DB/Models/BundleOrderLines.cs)
// so George.Data storages and the services share one definition. `using George.DB;` resolves it here.

/// <summary>Catalog-side bundle checks (a bundle is a Product whose SetupType is 'bundle').</summary>
public static class BundleProducts
{
    public const string SetupTypeName = "bundle";

    /// <summary>Woo product type registered by the OC Bundles plugin.</summary>
    public const string WooProductType = "oc_bundle";

    public static bool IsBundleSetupType(string? setupTypeName) =>
        string.Equals(setupTypeName?.Trim(), SetupTypeName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Requires <c>Product.SetupType</c> to be loaded (every catalog loader includes it).</summary>
    public static bool IsBundle(Product? product) =>
        product != null && IsBundleSetupType(product.SetupType?.Name);

    public static bool IsWooBundleType(string? wooProductType) =>
        string.Equals(wooProductType?.Trim(), WooProductType, StringComparison.OrdinalIgnoreCase);
}
