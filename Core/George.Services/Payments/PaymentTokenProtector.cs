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
}
