using George.Common.Payment;
using George.DB;

namespace George.Services.Payments;

/// <summary>Cardcom sandbox terminal credentials - seeded on new site creation when payment is not configured.</summary>
public static class CardcomTestTerminalDefaults
{
    public const int TerminalNumber = 1000;
    public const string ApiName = "CardTest1994";
    public const string ApiPassword = "Terminaltest2026";

    /// <summary>Apply test terminal only when the site has no Cardcom credentials yet.</summary>
    public static void ApplyToNewSiteIfUnset(Site site, PaymentTokenProtector protector)
    {
        if (site.CardcomTerminalNumber is > 0 && !string.IsNullOrWhiteSpace(site.CardcomApiName))
            return;
        if (!string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted))
            return;

        site.PaymentGatewayProvider = PaymentGatewayProviderId.Cardcom;
        site.CardcomTerminalNumber = TerminalNumber;
        site.CardcomApiName = ApiName;
        site.CardcomApiPasswordEncrypted = protector.Protect(ApiPassword);
    }
}
