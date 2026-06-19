namespace George.Services;

/// <summary>Parsing and sending helpers for notification SMS recipients.</summary>
public static class NotificationSmsHelper
{
    /// <summary>Split comma/semicolon/newline-separated phone list from notification settings.</summary>
    public static IReadOnlyList<string> ParseRecipientPhones(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var parts = raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (var part in parts)
        {
            var phone = part.Trim();
            if (phone.Length < 4 || !seen.Add(phone))
                continue;
            list.Add(phone);
        }
        return list;
    }

    public static bool IsSmsChannel(string? channel) =>
        string.Equals(channel?.Trim(), "sms", StringComparison.OrdinalIgnoreCase);
}
