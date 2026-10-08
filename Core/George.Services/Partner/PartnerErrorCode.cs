namespace George.Services.Partner;

/// <summary>
/// Machine-readable error codes of the Partner API. Every failed response carries
/// <c>description = "CODE: human message"</c>; quote responses list them per line. Documented for
/// integrators in docs/PARTNER_API.md "Error codes" - add new codes there too.
/// </summary>
public static class PartnerErrorCode
{
    public const string EmptyCart = "EMPTY_CART";
    public const string InvalidQuantity = "INVALID_QUANTITY";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string VariantRequired = "VARIANT_REQUIRED";
    public const string VariantNotFound = "VARIANT_NOT_FOUND";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string NoPrice = "NO_PRICE";
    public const string InvalidDeliveryType = "INVALID_DELIVERY_TYPE";
    public const string SupplyDateRequired = "SUPPLY_DATE_REQUIRED";
    public const string SupplyDateInPast = "SUPPLY_DATE_IN_PAST";
    public const string ShopClosed = "SHOP_CLOSED";
    public const string AddressRequired = "ADDRESS_REQUIRED";
    public const string CustomerRequired = "CUSTOMER_REQUIRED";
    public const string PhoneRequired = "PHONE_REQUIRED";
    public const string InvalidSource = "INVALID_SOURCE";
    public const string PaymentMethodNotAllowed = "PAYMENT_METHOD_NOT_ALLOWED";
    public const string SavedCardMissing = "SAVED_CARD_MISSING";
    public const string GatewayNotConfigured = "GATEWAY_NOT_CONFIGURED";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderNotCancellable = "ORDER_NOT_CANCELLABLE";
    public const string InvalidRequest = "INVALID_REQUEST";

    public static string Format(string code, string message) => $"{code}: {message}";
}
