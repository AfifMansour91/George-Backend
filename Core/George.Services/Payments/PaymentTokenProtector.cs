using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace George.Services.Payments;

/// <summary>
/// Encrypts payment secrets at rest (Cardcom API password, tokens).
/// Prefer <c>Payment:EncryptionKey</c> (AES, DB-portable across hosts) when configured;
/// otherwise falls back to ASP.NET Data Protection (machine-specific).
/// </summary>
public sealed class PaymentTokenProtector
{
    /// <summary>Config-based ciphertext prefix (readable on any host with the same key).</summary>
    public const string DatabaseKeyPrefix = "v2:";

    private readonly IDataProtector _dataProtection;
    private readonly byte[]? _databaseKey;
    private readonly ILogger<PaymentTokenProtector> _logger;

    public PaymentTokenProtector(
        IDataProtectionProvider provider,
        IConfiguration configuration,
        ILogger<PaymentTokenProtector> logger)
    {
        _dataProtection = provider.CreateProtector("George.Payment.Token.v1");
        _databaseKey = TryParseEncryptionKey(configuration["Payment:EncryptionKey"]);
        _logger = logger;
        if (_databaseKey == null)
        {
            _logger.LogWarning(
                "Payment:EncryptionKey is not configured. Secrets use Data Protection keys on this server only. " +
                "Set a shared base64 key for DB-backed secrets (Cardcom passwords, saved cards) when debugging QA/PROD from local or running multiple instances.");
        }
    }

    public bool UsesDatabaseEncryptionKey => _databaseKey != null;

    public string Protect(string plaintext)
    {
        if (_databaseKey != null)
            return DatabaseKeyPrefix + Convert.ToBase64String(EncryptAesGcm(plaintext, _databaseKey));

        return _dataProtection.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        if (!TryUnprotect(ciphertext, out var plaintext))
            throw new CryptographicException("Could not decrypt payment secret.");
        return plaintext;
    }

    public bool TryUnprotect(string? ciphertext, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrWhiteSpace(ciphertext))
            return false;

        if (ciphertext.StartsWith(DatabaseKeyPrefix, StringComparison.Ordinal))
        {
            if (_databaseKey == null)
            {
                _logger.LogWarning(
                    "Payment secret uses DB encryption (v2) but Payment:EncryptionKey is not set on this host.");
                return false;
            }

            try
            {
                var payload = Convert.FromBase64String(ciphertext[DatabaseKeyPrefix.Length..]);
                plaintext = DecryptAesGcm(payload, _databaseKey);
                return !string.IsNullOrWhiteSpace(plaintext);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to decrypt v2 payment secret.");
                return false;
            }
        }

        try
        {
            plaintext = _dataProtection.Unprotect(ciphertext);
            return !string.IsNullOrWhiteSpace(plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogDebug(ex, "Failed to decrypt legacy Data Protection payment secret.");
            return false;
        }
    }

    internal static byte[]? TryParseEncryptionKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var key = Convert.FromBase64String(value.Trim());
            return key.Length == 32 ? key : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[] EncryptAesGcm(string plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);
        var result = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + cipher.Length, tag.Length);
        return result;
    }

    private static string DecryptAesGcm(byte[] payload, byte[] key)
    {
        const int nonceSize = 12;
        const int tagSize = 16;
        if (payload.Length < nonceSize + tagSize)
            throw new CryptographicException("Invalid encrypted payload.");

        var nonce = payload.AsSpan(0, nonceSize);
        var tag = payload.AsSpan(payload.Length - tagSize, tagSize);
        var cipher = payload.AsSpan(nonceSize, payload.Length - nonceSize - tagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
