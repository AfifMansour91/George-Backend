namespace George.Services.Marketing;

/// <summary>
/// Billable SMS units of a text, by the platform's SMS contract rather than by carrier segments.
/// Giorgio's Inforu package bills one message per up to 201 characters (measured 28.10.2026 on the live parent account:
/// a 150-char Hebrew SMS cost 1 unit of RemainingQuota, a 202-char one cost 2), so a Hebrew text of up to
/// <see cref="Ucs2CharsPerUnit"/> UTF-16 code units (default 201 = three carrier segments of 67) is ONE unit, and a Latin
/// GSM-7 text of up to <see cref="Gsm7CharsPerUnit"/> septets (default 459 = three segments of 153) is one unit.
/// Both thresholds come from config <c>Sms:Units:HebrewCharsPerUnit</c> / <c>Sms:Units:LatinCharsPerUnit</c>
/// (<see cref="Configure"/>, read once at startup) so a new package deal is a config change, not a build.
/// Anything outside GSM-7 (Hebrew, emoji) forces UCS-2; an emoji outside the BMP costs two code units.
/// </summary>
public static class SmsUnits
{
    /// <summary>Carrier segment sizes - the package threshold is a multiple of these.</summary>
    public const int Ucs2SegmentChars = 67;
    public const int Gsm7SegmentChars = 153;
    public const int DefaultUcs2CharsPerUnit = 3 * Ucs2SegmentChars;   // 201
    public const int DefaultGsm7CharsPerUnit = 3 * Gsm7SegmentChars;   // 459

    private const string Gsm7Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    /// <summary>GSM-7 extension table - each of these costs two septets.</summary>
    private const string Gsm7Extended = "^{}\\[~]|€";

    private static int _ucs2CharsPerUnit = DefaultUcs2CharsPerUnit;
    private static int _gsm7CharsPerUnit = DefaultGsm7CharsPerUnit;

    /// <summary>Characters (UTF-16 code units) of a Hebrew / non-Latin message that fit in one billable unit.</summary>
    public static int Ucs2CharsPerUnit => _ucs2CharsPerUnit;
    /// <summary>GSM-7 septets of a Latin-only message that fit in one billable unit.</summary>
    public static int Gsm7CharsPerUnit => _gsm7CharsPerUnit;

    /// <summary>
    /// Apply the server's package thresholds. Null / blank / non-positive values keep the defaults, so an unconfigured server
    /// bills by the current Inforu deal (201 Hebrew characters per unit).
    /// </summary>
    public static void Configure(string? hebrewCharsPerUnit, string? latinCharsPerUnit)
    {
        _ucs2CharsPerUnit = Parse(hebrewCharsPerUnit, DefaultUcs2CharsPerUnit);
        _gsm7CharsPerUnit = Parse(latinCharsPerUnit, DefaultGsm7CharsPerUnit);
    }

    private static int Parse(string? value, int fallback) =>
        int.TryParse(value?.Trim(), out var n) && n > 0 ? n : fallback;

    public static int Calculate(string? text) => Calculate(text, _ucs2CharsPerUnit, _gsm7CharsPerUnit);

    /// <summary>Units of <paramref name="text"/> for explicit thresholds (tests, what-if pricing).</summary>
    public static int Calculate(string? text, int ucs2CharsPerUnit, int gsm7CharsPerUnit)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        if (ucs2CharsPerUnit <= 0) ucs2CharsPerUnit = DefaultUcs2CharsPerUnit;
        if (gsm7CharsPerUnit <= 0) gsm7CharsPerUnit = DefaultGsm7CharsPerUnit;

        var septets = 0;
        var isGsm7 = true;
        foreach (var ch in text)
        {
            if (Gsm7Basic.IndexOf(ch) >= 0) septets += 1;
            else if (Gsm7Extended.IndexOf(ch) >= 0) septets += 2;
            else { isGsm7 = false; break; }
        }

        if (isGsm7)
            return (int)Math.Ceiling(septets / (double)gsm7CharsPerUnit);

        // UCS-2 counts UTF-16 code units, so an emoji outside the BMP costs two.
        return (int)Math.Ceiling(text.Length / (double)ucs2CharsPerUnit);
    }

    /// <summary>True when <paramref name="text"/> is pure GSM-7 (Latin); Hebrew or emoji make it false.</summary>
    public static bool IsGsm7(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return true;
        foreach (var ch in text)
            if (Gsm7Basic.IndexOf(ch) < 0 && Gsm7Extended.IndexOf(ch) < 0)
                return false;
        return true;
    }
}
