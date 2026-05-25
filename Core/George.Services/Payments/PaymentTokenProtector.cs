using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace George.Services.Payments;

/// <summary>Encrypts Cardcom tokens and approval numbers at rest.</summary>
public sealed class PaymentTokenProtector
{
    private readonly IDataProtector _protector;

    public PaymentTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("George.Payment.Token.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);

    public bool TryUnprotect(string? ciphertext, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrWhiteSpace(ciphertext))
            return false;

        try
        {
            plaintext = _protector.Unprotect(ciphertext);
            return !string.IsNullOrWhiteSpace(plaintext);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
