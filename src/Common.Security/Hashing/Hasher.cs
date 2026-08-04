using Regira.Security.Abstractions;
using Regira.Security.Core;
using Regira.Security.Utilities;
using System.Text;

namespace Regira.Security.Hashing;

public class Hasher(CryptoOptions? options = null) : IHasher
{
    private readonly Encoding _encoding = options?.Encoding ?? DefaultSecuritySettings.Encoding;
    private readonly int _iterations = options?.Iterations ?? CryptoUtility.DefaultIterations;


    public string Hash(string? plainText)
    {
        plainText ??= string.Empty;
        var plainTextBytes = _encoding.GetBytes(plainText);

        // generate per-item salt
        var salt = CryptoUtility.GenerateSalt();
        // derive a 64-byte hash (512 bits) using PBKDF2
        var hashBytes = CryptoUtility.DeriveBytes(plainTextBytes, 64, salt, _iterations);

        var result = new byte[salt.Length + hashBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(hashBytes, 0, result, salt.Length, hashBytes.Length);

        return Convert.ToBase64String(result);
    }
    public bool Verify(string? plainText, string hashedValue)
    {
        plainText ??= string.Empty;
        if (string.IsNullOrEmpty(hashedValue)) return false;

        byte[] stored;
        try
        {
            stored = Convert.FromBase64String(hashedValue);
        }
        catch
        {
            return false;
        }

        if (stored.Length <= CryptoUtility.DefaultSaltSize) return false;

        var salt = new byte[CryptoUtility.DefaultSaltSize];
        Buffer.BlockCopy(stored, 0, salt, 0, salt.Length);
        var hash = new byte[stored.Length - salt.Length];
        Buffer.BlockCopy(stored, salt.Length, hash, 0, hash.Length);

        var derived = CryptoUtility.DeriveBytes(_encoding.GetBytes(plainText), hash.Length, salt, _iterations);

        return CryptoUtility.FixedTimeEquals(derived, hash);
    }
}