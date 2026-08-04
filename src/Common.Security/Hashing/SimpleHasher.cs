using System.Text;
using Regira.Security.Abstractions;
using Regira.Security.Core;
using Regira.Security.Utilities;

namespace Regira.Security.Hashing;

/// <summary>
/// Don't use this hasher for password hashing or sensitive data. It is a simple implementation that combines a hash with a salt, but it is not designed to be secure against modern attacks. 
/// Use it only for non-sensitive data or when you need a simple hash without the overhead of more secure algorithms like bcrypt or PBKDF2.
/// </summary>
/// <param name="options"></param>
public class SimpleHasher(CryptoOptions? options = null) : IHasher
{
    private readonly string _salt = options?.Secret ?? DefaultSecuritySettings.SaltKey;
    private readonly Encoding _encoding = options?.Encoding ?? DefaultSecuritySettings.Encoding;
    private readonly string _algorithm = options?.AlgorithmType ?? DefaultSecuritySettings.HashAlgorithm;


    public string Hash(string? plainText)
    {
        plainText ??= string.Empty;
        var bytes = _encoding.GetBytes(plainText);
        using var algorithm = CryptoUtility.CreateHasher(_algorithm);
        var hashBytes = algorithm.ComputeHash(bytes);
        var saltedHashBytes = hashBytes.Concat(_encoding.GetBytes(_salt)).ToArray();
        var doubleHashedBytes = algorithm.ComputeHash(saltedHashBytes);
        return Convert.ToBase64String(doubleHashedBytes);
    }
    public bool Verify(string? plainValue, string hashedValue)
    {
        plainValue ??= string.Empty;
        return Hash(plainValue).Equals(hashedValue);
    }
}