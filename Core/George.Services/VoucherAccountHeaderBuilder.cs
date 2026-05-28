using George.DB;
using George.Services.Response;

namespace George.Services;

public static class VoucherAccountHeaderBuilder
{
    public static VoucherAccountHeaderRes? FromAccount(Account? account)
    {
        if (account == null) return null;

        var header = new VoucherAccountHeaderRes
        {
            ShowLogo = account.VoucherHeaderShowLogo,
            LogoUrl = string.IsNullOrWhiteSpace(account.LogoUrl) ? null : account.LogoUrl.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(account.Name) ? null : account.Name.Trim(),
            CompanyNumber = string.IsNullOrWhiteSpace(account.CompanyNumber) ? null : account.CompanyNumber.Trim(),
            AddressLine = BuildAddressLine(account.Address, account.City),
            Phone = string.IsNullOrWhiteSpace(account.Phone) ? null : account.Phone.Trim(),
            Website = string.IsNullOrWhiteSpace(account.Website) ? null : account.Website.Trim(),
        };

        return HasContent(header) ? header : null;
    }

    public static bool HasContent(VoucherAccountHeaderRes? header)
    {
        if (header == null) return false;
        if (header.ShowLogo && !string.IsNullOrWhiteSpace(header.LogoUrl)) return true;
        return !string.IsNullOrWhiteSpace(header.CompanyName)
            || !string.IsNullOrWhiteSpace(header.CompanyNumber)
            || !string.IsNullOrWhiteSpace(header.AddressLine)
            || !string.IsNullOrWhiteSpace(header.Phone)
            || !string.IsNullOrWhiteSpace(header.Website);
    }

    public static IReadOnlyList<string> BuildLines(VoucherAccountHeaderRes header)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(header.CompanyName))
            lines.Add(header.CompanyName.Trim());
        if (!string.IsNullOrWhiteSpace(header.CompanyNumber))
            lines.Add($"ח.פ {header.CompanyNumber.Trim()}");
        if (!string.IsNullOrWhiteSpace(header.AddressLine))
            lines.Add(header.AddressLine.Trim());
        if (!string.IsNullOrWhiteSpace(header.Phone))
            lines.Add(header.Phone.Trim());
        if (!string.IsNullOrWhiteSpace(header.Website))
            lines.Add(header.Website.Trim());
        return lines;
    }

    public static string? BuildHtml(VoucherAccountHeaderRes? header, Func<string, string> escapeHtml)
    {
        if (header == null || !HasContent(header)) return null;

        var sb = new System.Text.StringBuilder();
        sb.Append("<div style=\"margin-bottom:8px;border-bottom:1px solid #000;padding-bottom:8px;text-align:center;\">");

        if (header.ShowLogo && !string.IsNullOrWhiteSpace(header.LogoUrl))
        {
            sb.Append("<div style=\"margin-bottom:6px;\">");
            sb.Append($"<img src=\"{escapeHtml(header.LogoUrl.Trim())}\" alt=\"\" style=\"display:block;margin:0 auto;max-height:48px;max-width:100%;height:auto;object-fit:contain;\" />");
            sb.Append("</div>");
        }

        var lines = BuildLines(header);
        for (var i = 0; i < lines.Count; i++)
        {
            var isTitle = i == 0 && !string.IsNullOrWhiteSpace(header.CompanyName);
            var size = isTitle ? "15px" : "12px";
            var weight = isTitle ? "700" : "400";
            var lineHeight = isTitle ? "19px" : "16px";
            sb.Append($"<div style=\"font-size:{size};font-weight:{weight};line-height:{lineHeight};\">{escapeHtml(lines[i])}</div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string? BuildAddressLine(string? address, string? city)
    {
        var street = address?.Trim();
        var cityTrim = city?.Trim();
        if (string.IsNullOrEmpty(street) && string.IsNullOrEmpty(cityTrim)) return null;
        if (string.IsNullOrEmpty(street)) return cityTrim;
        if (string.IsNullOrEmpty(cityTrim)) return street;
        return $"{street}, {cityTrim}";
    }
}
