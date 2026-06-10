namespace George.Services;

/// <summary>
/// Thermal voucher HTML — keep in sync with shop-manager
/// <c>OrderVoucherPrint.tsx</c> (<c>buildVoucherHtmlString</c>, <c>VOUCHER_*</c> constants).
/// </summary>
public static class VoucherPrintHtml
{
    /// <summary>Logical print width (mm); match driver "printing width" on 80mm roll (~72mm).</summary>
    public const int PaperWidthMm = 72;

    public const string InnerPadding = "2.5mm 2.5mm";

    public const int QrSizePx = 106;

    public const int QrFramePx = 110;

    /// <summary>Match TS <c>VOUCHER_PRODUCT_MARKER</c> (Figma bullet).</summary>
    public const string ProductBullet = "•";

    public const int Caption = 13;
    public const int Small = 11;
    public const int Body = 16;
    public const int Lead = 17;
    public const int OrderNumber = 30;
    public const int CustomerName = 34;
    /// <summary>Variations / attributes under product name (• lines).</summary>
    public const int ProductMeta = 15;

    public const int ProductMetaLineHeight = 20;

    /// <summary>Per-line item note under product.</summary>
    public const int ItemLineNote = 16;

    public const int ItemLineNoteLineHeight = 21;

    public const int PickedLine = 13;
    public const int Note = 17;
    public const int ShipLabel = 14;
    public const int ShipStreet = 24;
    public const int ShipExtras = 18;
    public const int ShippingRow = 17;
    public const int TotalLabel = 12;
    public const int TotalAmount = 26;

    /// <summary>Legacy prefix; prefer <see cref="ProductBullet"/> + layout in HTML.</summary>
    public const string ProductLinePrefix = "• ";

    public const string ItemLineNoteLabel = "הערה:";

    /// <summary>Underlined "הערה:" for voucher HTML (note body appended separately, escaped).</summary>
    public static string ItemLineNoteLabelHtml() =>
        $"<span style=\"text-decoration:underline;\">{ItemLineNoteLabel}</span>";
}
