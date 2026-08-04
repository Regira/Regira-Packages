using BCrypt.Net;
using Regira.Security.Abstractions;
using Regira.Security.Core;

namespace Regira.Security.Hashing.BCryptNet;

public class Hasher(CryptoOptions? options = null) : IHasher
{
    // https://github.com/BcryptNet/bcrypt.net
    private readonly string _algorithm = options?.AlgorithmType ?? DefaultSecuritySettings.HashAlgorithm;

    public string Hash(string plainText)
    {
        var hashType = GetHashType(_algorithm);
        var hashed = BCrypt.Net.BCrypt.EnhancedHashPassword(plainText, hashType);
        return hashed;
    }

    public bool Verify(string plainText, string hashedValue)
    {
        var hashType = GetHashType(_algorithm);
        return BCrypt.Net.BCrypt.EnhancedVerify(plainText, hashedValue, hashType);
    }

    private HashType GetHashType(string algorithm)
    {
        if (Enum.TryParse<HashType>(algorithm, true, out var hashType))
        {
            return hashType;
        }

        return HashType.SHA384;
    }
}