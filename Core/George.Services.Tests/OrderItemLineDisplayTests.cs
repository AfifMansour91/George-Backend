using George.DB;
using George.Services;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Parity tests for <see cref="OrderItemLineDisplay"/> vs <c>src/lib/orderItemLineDisplay.ts</c>.
/// </summary>
public class OrderItemLineDisplayTests
{
    /// <summary>Real DB-shaped row: kiosk salmon by weight (user example).</summary>
    [Fact]
    public void Weight_mode_salmon_kiosk_uses_sale_total_weight_badge_and_hides_generic_kg_variant()
    {
        var item = new OrderItem
        {
            Title = "סלמון טחון",
            VariantTitle = "ק\"ג",
            Quantity = 1.0000m,
            UnitWeightGrams = 2500.0000m,
            PricePerUnit = 120.0000m,
            TotalPrice = 300.00m,
            OrderLineQuantityMode = "weight",
            SaleUnits = "1 יח'",
            SaleTotalWeight = "2.5 ק\"ג",
        };

        Assert.Equal("2.5 ק\"ג", OrderItemLineDisplay.FormatOrderItemQuantityBadge(item));
        Assert.Equal("סלמון טחון", OrderItemLineDisplay.GetOrderItemProductName(item));
        Assert.Null(OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(item));
    }

    [Fact]
    public void Units_mode_returns_piece_badge()
    {
        var item = new OrderItem
        {
            Title = "Item",
            Quantity = 3,
            OrderLineQuantityMode = "units",
        };
        Assert.Equal("3 יח'", OrderItemLineDisplay.FormatOrderItemQuantityBadge(item));
    }

    [Fact]
    public void ParseGramsFromHebrewWeightLabel_parses_kg_and_grams()
    {
        Assert.Equal(1500, OrderItemLineDisplay.ParseGramsFromHebrewWeightLabel("1.5 ק\"ג"));
        Assert.Equal(200, OrderItemLineDisplay.ParseGramsFromHebrewWeightLabel("200 גרם"));
    }

    [Fact]
    public void Legacy_unit_weight_hint_when_quantity_mode_missing()
    {
        var item = new OrderItem
        {
            Title = "Fish",
            Quantity = 1,
            UnitWeightGrams = 500,
            OrderLineQuantityMode = null,
        };
        var hint = OrderItemLineDisplay.FormatVoucherLegacyUnitWeightHint(item, newVoucher: true);
        Assert.NotNull(hint);
        Assert.Contains("משקל יחידה", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Voucher_picked_display_uses_kg_when_unit_weight_present()
    {
        var item = new OrderItem
        {
            UnitWeightGrams = 2500,
            PickedQuantity = 2.5m,
        };
        Assert.Equal("2.5 ק\"ג", OrderItemLineDisplay.FormatVoucherPickedDisplay(item));
    }

    [Fact]
    public void Attribute_line_includes_size_and_cut_with_dedupe()
    {
        var item = new OrderItem
        {
            Title = "Product",
            OrderLineSizeLabel = "גדול",
            OrderLineCuttingLabel = "פרוס",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 800,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(item);
        Assert.NotNull(line);
        Assert.Contains("גדול", line, StringComparison.Ordinal);
        Assert.Contains("פרוס", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Attribute_line_omits_size_when_OmitOrderLineSizeLabel()
    {
        var item = new OrderItem
        {
            Title = "Product",
            OrderLineSizeLabel = "גדול",
            OrderLineCuttingLabel = "פרוס",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 800,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.DoesNotContain("גדול", line, StringComparison.Ordinal);
        Assert.Contains("פרוס", line, StringComparison.Ordinal);
    }
}
