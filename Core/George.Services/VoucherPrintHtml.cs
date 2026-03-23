namespace George.Services;

/// <summary>
/// Pixel sizes for thermal voucher HTML. Keep in sync with shop-manager
/// <c>src/components/orders/OrderVoucherPrint.tsx</c> → <c>VoucherPrintPx</c> and <c>VOUCHER_QR_PX</c>.
/// </summary>
public static class VoucherPrintHtml
{
    public const int QrSizePx = 120;
    public const int Caption = 13;
    public const int Small = 11;
    public const int Body = 16;
    public const int Lead = 17;
    public const int OrderNumber = 30;
    public const int CustomerName = 34;
    public const int ProductMeta = 11;
    public const int PickedLine = 13;
    public const int Note = 17;
    public const int ShipLabel = 14;
    public const int ShipStreet = 24;
    public const int ShipExtras = 18;
    public const int ShippingRow = 17;
    public const int TotalLabel = 12;
    public const int TotalAmount = 26;

    /// <summary>Prefix before each product line (keep in sync with TS <c>VOUCHER_PRODUCT_LINE_PREFIX</c>).</summary>
    public const string ProductLinePrefix = "• ";
}
