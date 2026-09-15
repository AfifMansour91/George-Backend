using George.Services.Request;

namespace George.Services.Tests;

/// <summary>
/// Bundles (מארזים) catalog guards: request invariants (no fake price / out-of-stock on a bundle), the delete-guard
/// and bulk-import messages, and the Woo sync ordering that pushes components before the bundles that use them.
/// </summary>
public class BundleGuardsTests
{
    // ───────────────────────────── request invariants ─────────────────────────────

    private static UpdateProductReq BundleReqWithFakeValues() => new()
    {
        Id = 7,
        SetupType = "bundle",
        Price = 99m,
        SalePrice = 79m,
        SalePriceStartDate = new DateTime(2026, 1, 1),
        SalePriceEndDate = new DateTime(2026, 2, 1),
        StockStatus = "out_of_stock",
        StockManagementType = "quantity",
        StockQuantity = 12,
        VariationStockByQuantity = true,
        ProductOptions = new List<ProductOptionReq> { new() { Name = "גודל", Values = new List<string> { "S" } } },
        Variants = new List<ProductVariantReq> { new() { Sku = "V1" } },
    };

    [Fact]
    public void Invariants_ClearPriceSaleAndDates()
    {
        var req = BundleReqWithFakeValues();
        BundleGuards.ApplyBundleRequestInvariants(req, existingHasVariants: false);
        Assert.Null(req.Price);
        Assert.Null(req.SalePrice);
        Assert.Null(req.SalePriceStartDate);
        Assert.Null(req.SalePriceEndDate);
    }

    [Fact]
    public void Invariants_ForceInStockByStatus_EvenWhenRequestSaysOutOfStock()
    {
        var req = BundleReqWithFakeValues();
        BundleGuards.ApplyBundleRequestInvariants(req, existingHasVariants: false);
        Assert.Equal("in_stock", req.StockStatus);
        Assert.Equal("status", req.StockManagementType);
        Assert.Null(req.StockQuantity);
        Assert.Null(req.VariationStockByQuantity);
    }

    [Fact]
    public void Invariants_BlankStockStatus_BecomesInStock()
    {
        var req = new UpdateProductReq { Id = 1, SetupType = "bundle", StockStatus = "  " };
        BundleGuards.ApplyBundleRequestInvariants(req, existingHasVariants: false);
        Assert.Equal("in_stock", req.StockStatus);
    }

    [Fact]
    public void Invariants_DropVariants_EmptyListsWhenExistingHasVariants_NullOtherwise()
    {
        var withVariants = BundleReqWithFakeValues();
        BundleGuards.ApplyBundleRequestInvariants(withVariants, existingHasVariants: true);
        Assert.NotNull(withVariants.ProductOptions);
        Assert.Empty(withVariants.ProductOptions!);
        Assert.NotNull(withVariants.Variants);
        Assert.Empty(withVariants.Variants!);

        var without = BundleReqWithFakeValues();
        BundleGuards.ApplyBundleRequestInvariants(without, existingHasVariants: false);
        Assert.Null(without.ProductOptions);
        Assert.Null(without.Variants);
    }

    // ───────────────────────────── delete guard message ─────────────────────────────

    [Fact]
    public void ComponentInUseMessage_ListsAllNamesUpToThree()
    {
        var msg = BundleGuards.ComponentInUseMessage(new[] { "מארז שבת", "מארז על האש" });
        Assert.Equal("לא ניתן למחוק - המוצר משמש כרכיב במארזים: מארז שבת, מארז על האש", msg);
    }

    [Fact]
    public void ComponentInUseMessage_TruncatesAfterThreeNames()
    {
        var msg = BundleGuards.ComponentInUseMessage(new[] { "א", "ב", "ג", "ד", "ה" });
        Assert.Equal("לא ניתן למחוק - המוצר משמש כרכיב במארזים: א, ב, ג ועוד 2", msg);
    }

    // ───────────────────────────── bulk import messages ─────────────────────────────

    [Fact]
    public void BulkImportErrors_NameTheProduct()
    {
        Assert.Equal("המוצר מארז שבת הוא מארז - יש לערוך אותו במסך המארזים", BundleGuards.BulkImportExistingBundleError(" מארז שבת "));
        Assert.Equal("המוצר מארז חדש הוא מארז - יש ליצור אותו במסך המארזים", BundleGuards.BulkImportCreateBundleError("מארז חדש"));
        Assert.Contains("(ללא שם)", BundleGuards.BulkImportExistingBundleError(null));
    }

    // ───────────────────────────── Woo sync ordering ─────────────────────────────

    [Fact]
    public void PartitionSyncBatches_RegularProductsFirstInBatches_ThenEachBundleAlone()
    {
        var ids = Enumerable.Range(1, 20).ToList(); // 1..20
        var bundles = new HashSet<int> { 3, 18 };

        var batches = BundleGuards.PartitionSyncBatches(ids, bundles, batchSize: 16);

        // 18 regular products → 16 + 2, then the two bundles one per batch, in their original order.
        Assert.Equal(4, batches.Count);
        Assert.Equal(16, batches[0].Count);
        Assert.Equal(2, batches[1].Count);
        Assert.DoesNotContain(3, batches[0]);
        Assert.DoesNotContain(18, batches[0].Concat(batches[1]));
        Assert.Equal(new[] { 3 }, batches[2]);
        Assert.Equal(new[] { 18 }, batches[3]);

        // Original order of the regular products is preserved.
        var regularInOrder = ids.Where(i => !bundles.Contains(i)).ToList();
        Assert.Equal(regularInOrder, batches[0].Concat(batches[1]).ToList());
    }

    [Fact]
    public void PartitionSyncBatches_DeduplicatesAndIgnoresUnknownBundleIds()
    {
        var batches = BundleGuards.PartitionSyncBatches(new[] { 5, 5, 9, 9 }, new HashSet<int> { 9, 42 }, batchSize: 16);
        Assert.Equal(2, batches.Count);
        Assert.Equal(new[] { 5 }, batches[0]);
        Assert.Equal(new[] { 9 }, batches[1]);
    }

    [Fact]
    public void PartitionSyncBatches_OnlyBundles_OneBatchEach()
    {
        var batches = BundleGuards.PartitionSyncBatches(new[] { 1, 2 }, new HashSet<int> { 1, 2 }, batchSize: 16);
        Assert.Equal(2, batches.Count);
        Assert.All(batches, b => Assert.Single(b));
    }

    [Fact]
    public void PartitionSyncBatches_Empty_NoBatches()
    {
        Assert.Empty(BundleGuards.PartitionSyncBatches(Array.Empty<int>(), new HashSet<int>(), batchSize: 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => BundleGuards.PartitionSyncBatches(new[] { 1 }, new HashSet<int>(), batchSize: 0));
    }
}
