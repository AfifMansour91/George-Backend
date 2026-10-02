using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace George.Services.Marketing;

/// <summary>
/// Builds the exact text one recipient gets: placeholders, the tracked link, and the unsubscribe line.
/// The unsubscribe line is appended by the system to every marketing message - it cannot be edited or
/// removed, and it counts toward the message length (spec §07).
/// </summary>
public static class MarketingMessageRenderer
{
    public const string TokenCustomerName = "[customer_name]";
    public const string TokenStoreName = "[store_name]";
    public const string TokenLink = "[link]";

    public const string UnsubscribePrefix = "להסרה: ";
    public const int MaxBodyLength = 1000;

    // No vowels and no look-alikes (0/o, 1/l/i): tokens are typed from a phone now and then, and must never spell a word.
    private const string TokenAlphabet = "bcdfghjkmnpqrstvwxz23456789";
    public const int TokenLength = 7;

    public static string Render(string body, string? customerName, string? storeName, string? trackedLinkUrl, string unsubscribeUrl)
    {
        var text = (body ?? string.Empty)
            .Replace(TokenCustomerName, FirstName(customerName), StringComparison.OrdinalIgnoreCase)
            .Replace(TokenStoreName, (storeName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(trackedLinkUrl))
        {
            text = text.Contains(TokenLink, StringComparison.OrdinalIgnoreCase)
                ? text.Replace(TokenLink, trackedLinkUrl, StringComparison.OrdinalIgnoreCase)
                : $"{text.TrimEnd()}\n{trackedLinkUrl}";
        }
        else
        {
            text = text.Replace(TokenLink, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // An empty name must not leave "שלום , ..." behind.
        text = Regex.Replace(text, @"[ \t]+([,.!?])", "$1");
        text = Regex.Replace(text, @"[ \t]{2,}", " ").Trim();

        return $"{text}\n{UnsubscribePrefix}{unsubscribeUrl}";
    }

    /// <summary>"דנה לוי" → "דנה". A marketing SMS greets by first name.</summary>
    public static string FirstName(string? fullName)
    {
        var name = (fullName ?? string.Empty).Trim();
        if (name.Length == 0)
            return string.Empty;
        var space = name.IndexOf(' ');
        return space > 0 ? name.Substring(0, space) : name;
    }

    public static string NewToken()
    {
        Span<char> chars = stackalloc char[TokenLength];
        for (var i = 0; i < TokenLength; i++)
            chars[i] = TokenAlphabet[RandomNumberGenerator.GetInt32(TokenAlphabet.Length)];
        return new string(chars);
    }

    public static bool IsValidToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length != TokenLength)
            return false;
        foreach (var ch in token)
            if (TokenAlphabet.IndexOf(ch) < 0)
                return false;
        return true;
    }

    public static string TrackedLinkUrl(string baseUrl, string token) => $"{baseUrl.TrimEnd('/')}/s/{token}";

    public static string UnsubscribeUrl(string baseUrl, string token) => $"{baseUrl.TrimEnd('/')}/x/{token}";
}
