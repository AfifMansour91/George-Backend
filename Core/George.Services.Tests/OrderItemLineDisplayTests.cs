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
    public void Legacy_unit_weight_hint_only_for_variable_weight_per_unit_choice()
    {
        var variable = new OrderItem
        {
            Title = "דג | בחירת משקל ליחידה",
            Quantity = 1,
            UnitWeightGrams = 500,
            OrderLineQuantityMode = null,
        };
        var hint = OrderItemLineDisplay.FormatVoucherLegacyUnitWeightHint(variable, newVoucher: true);
        Assert.NotNull(hint);
        Assert.Contains("משקל יחידה", hint, StringComparison.Ordinal);

        var averageUnit = new OrderItem
        {
            Title = "Fish",
            Quantity = 1,
            UnitWeightGrams = 500,
            OrderLineQuantityMode = "units",
        };
        Assert.Null(OrderItemLineDisplay.FormatVoucherLegacyUnitWeightHint(averageUnit, newVoucher: true));
    }

    [Fact]
    public void Variable_weight_choice_detected_by_line_fields_when_title_lacks_phrase()
    {
        // by_unit+variable snapshot: per-unit label, units mode, no UnitWeightGrams.
        var variableByFields = new OrderItem
        {
            Title = "סלמון שלם",
            Quantity = 2,
            OrderLineQuantityMode = "units",
            OrderLinePerUnitWeightLabel = "500 גרם ליח'",
        };
        Assert.True(OrderItemLineDisplay.IsOrderItemVariableWeightPerUnitChoice(variableByFields));
        var hint = OrderItemLineDisplay.FormatVoucherLegacyUnitWeightHint(variableByFields, newVoucher: true);
        Assert.NotNull(hint);
        Assert.Contains("500 גרם ליח'", hint, StringComparison.Ordinal);

        // Fixed/by-variant lines carry UnitWeightGrams alongside the label — must stay excluded.
        var fixedWeight = new OrderItem
        {
            Title = "מארז קציצות",
            Quantity = 1,
            OrderLineQuantityMode = "units",
            OrderLinePerUnitWeightLabel = "800 גרם ליח'",
            UnitWeightGrams = 800,
        };
        Assert.False(OrderItemLineDisplay.IsOrderItemVariableWeightPerUnitChoice(fixedWeight));
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
    public void OrderMeaningfulPick_false_for_stock_baseline_without_user_confirm()
    {
        var item = new OrderItem
        {
            Quantity = 2,
            PickedQuantity = 2,
            PickingUserConfirmed = false,
        };
        Assert.False(OrderItemLineDisplay.OrderMeaningfulPick(item));
    }

    [Fact]
    public void OrderMeaningfulPick_true_after_user_confirmed_pick()
    {
        var item = new OrderItem
        {
            Quantity = 2,
            PickedQuantity = 1.8m,
            PickingUserConfirmed = true,
        };
        Assert.True(OrderItemLineDisplay.OrderMeaningfulPick(item));
    }

    [Fact]
    public void OrderHasOcStoreosPickingAdjustments_true_when_one_line_confirmed()
    {
        var items = new[]
        {
            new OrderItem { PickingUserConfirmed = true, PickedQuantity = 1m, TotalPrice = 5m },
            new OrderItem { PickingUserConfirmed = false, PickedQuantity = 1m, TotalPrice = 12m },
        };
        Assert.True(OrderItemLineDisplay.OrderHasOcStoreosPickingAdjustments(items));
    }

    [Fact]
    public void OrderHasOcStoreosPickingAdjustments_true_when_line_explicitly_unpicked()
    {
        var items = new[]
        {
            new OrderItem { PickingUserConfirmed = false, PickedQuantity = 0m, TotalPrice = null },
        };
        Assert.True(OrderItemLineDisplay.OrderHasOcStoreosPickingAdjustments(items));
    }

    [Fact]
    public void OrderHasOcStoreosPickingAdjustments_false_for_stock_baseline_only()
    {
        var items = new[]
        {
            new OrderItem { PickingUserConfirmed = false, PickedQuantity = 2m, TotalPrice = 12m },
            new OrderItem { PickingUserConfirmed = false, PickedQuantity = 1m, TotalPrice = 5m },
        };
        Assert.False(OrderItemLineDisplay.OrderHasOcStoreosPickingAdjustments(items));
    }

    [Fact]
    public void IsOcStoreosBillableLine_requires_confirmed_pick_with_qty()
    {
        Assert.False(OrderItemLineDisplay.IsOcStoreosBillableLine(new OrderItem
        {
            PickingUserConfirmed = false,
            PickedQuantity = 1m,
            TotalPrice = 12m,
        }));
        Assert.True(OrderItemLineDisplay.IsOcStoreosBillableLine(new OrderItem
        {
            PickingUserConfirmed = true,
            PickedQuantity = 1m,
            TotalPrice = 5m,
        }));
        Assert.False(OrderItemLineDisplay.IsOcStoreosBillableLine(new OrderItem
        {
            PickingUserConfirmed = false,
            PickedQuantity = 0m,
            TotalPrice = null,
        }));
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
    public void Attribute_line_keeps_size_with_cutting_when_OmitOrderLineSizeLabel()
    {
        // Zano: משקל לפי גודל with explicit cutting — the cutting label occupies the variantTitle-fallback
        // slot, so omitting size here erased it from the voucher entirely. Size must stay.
        var item = new OrderItem
        {
            Title = "Product",
            OrderLineSizeLabel = "2-3 קילו (כ 3 ק\"ג)",
            OrderLineCuttingLabel = "פרוס",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 3000,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.Contains("2-3 קילו", line, StringComparison.Ordinal);
        Assert.Contains("פרוס", line, StringComparison.Ordinal);
    }

    [Fact]
    public void HideWeightDetails_strips_approx_weight_and_per_unit_label()
    {
        // Site.HideUnitWeightInOrders: size name stays, "(כ X ק"ג)" suffix and per-unit weight go.
        var item = new OrderItem
        {
            Title = "Product",
            OrderLineSizeLabel = "2-3 קילו (כ 3 ק\"ג)",
            OrderLinePerUnitWeightLabel = "3 ק\"ג ליח'",
            OrderLineCuttingLabel = "פרוס",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 3000,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true, HideWeightDetails = true });
        Assert.NotNull(line);
        Assert.Contains("2-3 קילו", line, StringComparison.Ordinal);
        Assert.Contains("פרוס", line, StringComparison.Ordinal);
        Assert.DoesNotContain("(כ 3", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ליח'", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Variable_choice_detected_without_persisted_label_via_saleTotalWeight()
    {
        // Woo variable line: no orderLinePerUnitWeightLabel, no unitWeightGrams — per-unit weight
        // derivable from saleTotalWeight. Must count as variable-choice so prints keep the weight.
        var item = new OrderItem
        {
            Title = "פילה סלמון",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            SaleTotalWeight = "500 גרם",
            PricePerUnit = 50m,
            TotalPrice = 100m,
        };
        Assert.True(OrderItemLineDisplay.IsOrderItemVariableWeightPerUnitChoice(item));
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { VoucherPerUnitWeightVariableOnly = true });
        Assert.NotNull(line);
        Assert.Contains("גרם ליח'", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Variable_choice_not_detected_for_average_line_with_grams()
    {
        // by_unit average line always carries unitWeightGrams — not a variable choice; voucher hides its weight.
        var item = new OrderItem
        {
            Title = "מוצר לפי יחידה",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 700,
            OrderLinePerUnitWeightLabel = "700 גרם ליח'",
        };
        Assert.False(OrderItemLineDisplay.IsOrderItemVariableWeightPerUnitChoice(item));
    }

    [Fact]
    public void Voucher_shows_per_unit_weight_for_average_line()
    {
        // Zano order #33: מכירה-לפי-יחידה line (label + grams). Prints now show the weight by default
        // (card parity) — the old variable-only voucher rule hid it.
        var item = new OrderItem
        {
            Title = "דניס (כ 600 עד 800 גרם)",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 700,
            OrderLinePerUnitWeightLabel = "700 גרם ליח'",
            OrderLineCuttingLabel = "פילה ללא עור",
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.Contains("700 גרם ליח'", line, StringComparison.Ordinal);
        Assert.Contains("פילה ללא עור", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Per_unit_weight_dropped_when_size_segment_shows_same_weight()
    {
        // משקל לפי גודל: size "(כ 3 ק"ג)" already carries the weight — matching per-unit label is redundant.
        var item = new OrderItem
        {
            Title = "Product",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 3000,
            OrderLinePerUnitWeightLabel = "3 ק\"ג ליח'",
            OrderLineSizeLabel = "2-3 קילו (כ 3 ק\"ג)",
            OrderLineCuttingLabel = "פרוס",
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.Contains("2-3 קילו", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ליח'", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Per_unit_weight_dropped_when_size_name_contains_other_kg_number()
    {
        // Zano order #34: size name "בין 5-6 ק״ג" first-match parses to 6kg — the redundancy check must
        // compare against the "(כ 5.5 ק"ג)" approx suffix, not the size name, so per is still dropped.
        var item = new OrderItem
        {
            Title = "דג סלמון שלם טרי",
            OrderLineQuantityMode = "units",
            Quantity = 3,
            UnitWeightGrams = 5500,
            OrderLinePerUnitWeightLabel = "5.5 ק\"ג ליח'",
            OrderLineSizeLabel = "בין 5-6 ק״ג (כ 5.5 ק\"ג)",
            OrderLineCuttingLabel = "שלם נקי",
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.Contains("בין 5-6 ק״ג", line, StringComparison.Ordinal);
        Assert.Contains("שלם נקי", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ליח'", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Woo_line_without_cutting_label_renders_like_phone_line()
    {
        // Zano order #35 (Woo): no orderLineCuttingLabel; variantTitle = "size | cutting".
        // Must render size-with-approx + cutting-only (phone parity), not per-unit + raw variantTitle.
        var item = new OrderItem
        {
            Title = "דג סלמון שלם טרי",
            VariantTitle = "בין 5-6 ק״ג | פילה פרוס בלי עור",
            OrderLineQuantityMode = "units",
            Quantity = 3,
            UnitWeightGrams = 5500,
            OrderLinePerUnitWeightLabel = "5.5 ק\"ג ליח'",
            OrderLineSizeLabel = "בין 5-6 ק״ג (כ 5.5 ק\"ג)",
            OrderLineCuttingLabel = null,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.Contains("בין 5-6 ק״ג (כ 5.5 ק\"ג)", line, StringComparison.Ordinal);
        Assert.Contains("פילה פרוס בלי עור", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ליח'", line, StringComparison.Ordinal);
        Assert.DoesNotContain("|  בין", line, StringComparison.Ordinal);
    }

    [Fact]
    public void HideWeightDetails_keeps_customer_chosen_variable_weight()
    {
        // "בחירת משקל ליחידה": the weight IS the ordered spec — never hidden.
        var item = new OrderItem
        {
            Title = "מוצר בחירת משקל ליחידה",
            OrderLinePerUnitWeightLabel = "500 גרם ליח'",
            OrderLineQuantityMode = "units",
            Quantity = 2,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { HideWeightDetails = true });
        Assert.NotNull(line);
        Assert.Contains("500 גרם ליח'", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Attribute_line_omits_size_when_variant_title_already_carries_it()
    {
        // Size-only line: no cutting label, variantTitle fallback shows the size — the size label would duplicate it.
        var item = new OrderItem
        {
            Title = "Product",
            VariantTitle = "2-3 קילו",
            OrderLineSizeLabel = "2-3 קילו (כ 3 ק\"ג)",
            OrderLineQuantityMode = "units",
            Quantity = 2,
            UnitWeightGrams = 3000,
        };
        var line = OrderItemLineDisplay.GetOrderItemAttributeSummaryLine(
            item,
            new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true });
        Assert.NotNull(line);
        Assert.DoesNotContain("(כ 3", line, StringComparison.Ordinal);
        Assert.Contains("2-3 קילו", line, StringComparison.Ordinal);
    }
}
