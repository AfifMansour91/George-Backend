namespace George.Services.Response;

/// <summary>Account details for the top block on printed order vouchers (non–new-order).</summary>
public class VoucherAccountHeaderRes
{
    /// <summary>When false, only <see cref="CompanyName"/> is printed (legacy single line).</summary>
    public bool DetailsEnabled { get; set; }

    public bool ShowLogo { get; set; }
    public string? LogoUrl { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
}
