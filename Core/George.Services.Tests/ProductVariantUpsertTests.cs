using George.Data;
using George.Data.Dto;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace George.Services.Tests;

/// <summary>
/// Saving a product must keep ProductVariant / ProductOption ids stable (upsert), not delete+recreate them:
/// OrderItem.ProductVariantId, ProductSiteVariantWooId and ProductSiteVariantStock reference those ids.
/// Zano Dagim 23/08/2026: every save orphaned order lines → picking priced a 145 ₪/kg cutting variant at the 95 base.
/// </summary>
public class ProductVariantUpsertTests
{
    private const int ProductId = 100;

    private static GeorgeDBContext NewContext()
    {
        var options = new DbContextOptionsBuilder<GeorgeDBContextBase>()
            .UseInMemoryDatabase($"variant-upsert-{Guid.NewGuid()}")
            .EnableServiceProviderCaching(false)
            .Options;
        return new GeorgeDBContext(options);
    }

    private static ProductStorage NewStorage(GeorgeDBContext ctx) =>
        new(ctx, NullLogger<ProductStorage>.Instance);

    private static void SeedProduct(GeorgeDBContext ctx)
    {
        ctx.Product.Add(new Product { Id = ProductId, Name = "לברק", IsDeleted = false, GuidId = Guid.NewGuid(), CreationTime = DateTime.UtcNow });
        ctx.SaveChanges();
    }

    private static ProductVariantDto Dto(string size, string cut, decimal price, int? id = null) => new()
    {
        Id = id,
        Price = price,
        Weight = 0.5m,
        OptionValues = new Dictionary<string, string> { ["גודל"] = size, ["צורת חיתוך"] = cut },
    };

    private static List<ProductVariant> Live(GeorgeDBContext ctx) =>
        ctx.ProductVariant.Include(v => v.ProductVariantOptionValue).Where(v => v.ProductId == ProductId && !v.IsDeleted).OrderBy(v => v.Id).ToList();

    [Fact]
    public async Task Resave_keeps_variant_ids_and_updates_in_place()
    {
        using var ctx = NewContext();
        SeedProduct(ctx);
        var storage = NewStorage(ctx);

        await storage.UpdateProductVariantsAsync(ProductId, new() { Dto("בינוני", "שלם", 95), Dto("בינוני", "פילה", 145) }, null, default);
        var first = Live(ctx);
        Assert.Equal(2, first.Count);
        var fileteId = first.Single(v => v.ProductVariantOptionValue.Any(o => o.OptionValue == "פילה")).Id;

        // Edit form resends both (with ids) and changes the fillet price.
        await storage.UpdateProductVariantsAsync(ProductId, new()
        {
            Dto("בינוני", "שלם", 95, first[0].Id),
            Dto("בינוני", "פילה", 150, fileteId),
        }, null, default);

        var second = Live(ctx);
        Assert.Equal(first.Select(v => v.Id), second.Select(v => v.Id));
        Assert.Equal(150, second.Single(v => v.Id == fileteId).Price);
        Assert.Equal(2, ctx.ProductVariant.IgnoreQueryFilters().Count(v => v.ProductId == ProductId)); // no dead copies
    }

    [Fact]
    public async Task Matches_by_option_values_when_ids_are_missing()
    {
        using var ctx = NewContext();
        SeedProduct(ctx);
        var storage = NewStorage(ctx);

        await storage.UpdateProductVariantsAsync(ProductId, new() { Dto("בינוני", "שלם", 95), Dto("גדול", "שלם", 105) }, null, default);
        var ids = Live(ctx).Select(v => v.Id).ToList();

        // Import/CSV path: no ids, same option sets, different spacing/case — still the same variants.
        await storage.UpdateProductVariantsAsync(ProductId, new()
        {
            new ProductVariantDto { Price = 99, OptionValues = new() { ["גודל"] = " בינוני", ["צורת חיתוך"] = "שלם " } },
            new ProductVariantDto { Price = 105, OptionValues = new() { ["צורת חיתוך"] = "שלם", ["גודל"] = "גדול" } },
        }, null, default);

        var live = Live(ctx);
        Assert.Equal(ids, live.Select(v => v.Id).ToList());
        Assert.Equal(99, live[0].Price);
    }

    [Fact]
    public async Task Removed_variant_is_soft_deleted_and_new_one_created()
    {
        using var ctx = NewContext();
        SeedProduct(ctx);
        var storage = NewStorage(ctx);

        await storage.UpdateProductVariantsAsync(ProductId, new() { Dto("בינוני", "שלם", 95), Dto("בינוני", "פילה", 145) }, null, default);
        var before = Live(ctx);
        var keepId = before[0].Id;
        var dropId = before[1].Id;

        await storage.UpdateProductVariantsAsync(ProductId, new() { Dto("בינוני", "שלם", 95, keepId), Dto("גדול", "שלם", 105) }, null, default);

        var after = Live(ctx);
        Assert.Equal(2, after.Count);
        Assert.Contains(after, v => v.Id == keepId);
        Assert.DoesNotContain(after, v => v.Id == dropId);
        Assert.True(ctx.ProductVariant.IgnoreQueryFilters().Single(v => v.Id == dropId).IsDeleted);
        Assert.Contains(after, v => v.ProductVariantOptionValue.Any(o => o.OptionValue == "גדול"));
    }

    [Fact]
    public async Task Options_keep_ids_and_only_values_change()
    {
        using var ctx = NewContext();
        SeedProduct(ctx);
        var storage = NewStorage(ctx);

        await storage.UpdateProductOptionsAsync(ProductId, new() { new ProductOptionDto { Name = "צורת חיתוך", Values = new() { "שלם", "פילה" } } }, null, default);
        var optId = ctx.ProductOption.Single(o => o.ProductId == ProductId && !o.IsDeleted).Id;

        await storage.UpdateProductOptionsAsync(ProductId, new() { new ProductOptionDto { Name = "צורת חיתוך", Values = new() { "שלם", "טחון" } } }, null, default);

        var live = ctx.ProductOption.Include(o => o.ProductOptionValue).Where(o => o.ProductId == ProductId && !o.IsDeleted).ToList();
        Assert.Single(live);
        Assert.Equal(optId, live[0].Id);
        Assert.Equal(new[] { "טחון", "שלם" }, live[0].ProductOptionValue.Select(v => v.Value).OrderBy(v => v).ToArray());
    }
}
