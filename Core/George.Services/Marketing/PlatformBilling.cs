using System.Globalization;
using George.Common.Payment;
using George.Services.Payments;
using George.Services.Payments.Cardcom;
using Microsoft.Extensions.Configuration;

namespace George.Services.Marketing;

/// <summary>
/// The PLATFORM's own Cardcom terminal (Giorgio's, not a shop's) and the price rules of the self-service SMS packages.
/// Everything comes from server configuration:
/// <code>
/// "Billing": {
///   "VatPercent": 18,
///   "Cardcom": { "TerminalNumber": 1000, "ApiName": "...", "ApiPassword": "...", "MaxInstallments": 1,
///                "DocumentType": "TaxInvoiceAndReceipt", "LogoUrl": "", "CssUrl": "", "ProviderExtrasJson": "" }
/// }
/// </code>
/// No terminal configured = the shop still sees the catalog, with "פנה אלינו" instead of a pay button.
/// </summary>
public static class PlatformBilling
{
    public const decimal DefaultVatPercent = 18m;

    public static SitePaymentCredentials? FromConfiguration(IConfiguration configuration)
    {
        var terminal = int.TryParse(configuration["Billing:Cardcom:TerminalNumber"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) ? t : 0;
        var apiName = configuration["Billing:Cardcom:ApiName"]?.Trim();
        if (terminal <= 0 || string.IsNullOrWhiteSpace(apiName))
            return null;

        var maxInstallments = int.TryParse(configuration["Billing:Cardcom:MaxInstallments"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mi) ? Math.Clamp(mi, 1, 12) : 1;
        var documentType = configuration["Billing:Cardcom:DocumentType"]?.Trim();
        return new SitePaymentCredentials
        {
            SiteId = 0,
            ProviderId = PaymentGatewayProviderId.Cardcom,
            TerminalNumber = terminal,
            ApiName = apiName,
            ApiPassword = Blank(configuration["Billing:Cardcom:ApiPassword"]),
            SaveCardEnabled = false,
            MaxInstallments = maxInstallments,
            CssUrl = Blank(configuration["Billing:Cardcom:CssUrl"]),
            LogoUrl = Blank(configuration["Billing:Cardcom:LogoUrl"]),
            ProviderExtrasJson = Blank(configuration["Billing:Cardcom:ProviderExtrasJson"]),
            DocumentTypeToCreate = string.IsNullOrWhiteSpace(documentType) ? CardcomDocumentBuilder.DefaultDocumentType : documentType,
            SendInvoiceSmsAfterCapture = false,
            Currency = "ILS",
        };
    }

    public static decimal VatPercent(IConfiguration configuration) =>
        decimal.TryParse(configuration["Billing:VatPercent"], NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v is >= 0 and <= 50
            ? v
            : DefaultVatPercent;

    /// <summary>Price list is before VAT ("המחירים לא כוללים מע״מ"); the card is charged the gross amount, rounded to agorot.</summary>
    public static decimal TotalWithVat(decimal price, decimal vatPercent) =>
        Math.Round(price * (1m + vatPercent / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>The "מחיר להודעה" column: agorot per message before VAT, one decimal (8 / 7 / 5.9 ...).</summary>
    public static decimal PricePerMessageAgorot(decimal price, int messages) =>
        messages > 0 ? Math.Round(price * 100m / messages, 1, MidpointRounding.AwayFromZero) : 0m;

    public static string PackageDisplayName(string? name, int messages) =>
        !string.IsNullOrWhiteSpace(name) ? name.Trim() : string.Format(CultureInfo.InvariantCulture, "{0:N0} הודעות", messages);

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
