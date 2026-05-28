namespace George.Services.Response;

/// <summary>Account details for the top block on printed order vouchers (non–new-order).</summary>
public class VoucherAccountHeaderRes
{
    public bool ShowLogo { get; set; }
    public string? LogoUrl { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
}
