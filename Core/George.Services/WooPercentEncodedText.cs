using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace George.Services;

/// <summary>
/// WooCommerce sends Hebrew taxonomy (pa_*) term slugs percent-encoded ("%d7%9c%d7%9c%d7%90-%d7%a2%d7%95%d7%a8"
/// = "ללא-עור") in variation attributes and order payloads. Decode before persisting or displaying;
/// plain text passes through unchanged.
/// </summary>
public static class WooPercentEncodedText
{
    private static readonly Regex PercentEncodedSequence = new(@"%[0-9a-fA-F]{2}", RegexOptions.Compiled);

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value) || !PercentEncodedSequence.IsMatch(value))
            return value;
        try { return Uri.UnescapeDataString(value).Trim(); }
        catch (UriFormatException) { return value; }
    }
}
