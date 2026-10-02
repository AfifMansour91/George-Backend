namespace George.Services.Marketing;

/// <summary>
/// Billable SMS segments of a text. GSM-7 text: 160 chars in one message, 153 per part when split.
/// Anything outside GSM-7 (Hebrew, emoji) forces UCS-2: 70 in one message, 67 per part.
/// </summary>
public static class SmsUnits
{
    private const string Gsm7Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    /// <summary>GSM-7 extension table - each of these costs two septets.</summary>
    private const string Gsm7Extended = "^{}\\[~]|€";

    public static int Calculate(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var septets = 0;
        var isGsm7 = true;
        foreach (var ch in text)
        {
            if (Gsm7Basic.IndexOf(ch) >= 0) septets += 1;
            else if (Gsm7Extended.IndexOf(ch) >= 0) septets += 2;
            else { isGsm7 = false; break; }
        }

        if (isGsm7)
            return septets <= 160 ? 1 : (int)Math.Ceiling(septets / 153d);

        // UCS-2 counts UTF-16 code units, so an emoji outside the BMP costs two.
        var units = text.Length;
        return units <= 70 ? 1 : (int)Math.Ceiling(units / 67d);
    }
}
