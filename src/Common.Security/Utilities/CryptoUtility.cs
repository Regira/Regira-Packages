using System.Security.Cryptography;

namespace Regira.Security.Utilities;

public static class CryptoUtility
{
    public const int DefaultSaltSize = 16; // 128-bit salt
    public const int DefaultIterations = 500_000;

    public static HashAlgorithm CreateHasher(string? algorithm = null)
    {
        switch (algorithm?.ToUpper())
        {
            case "SHA384":
                return SHA384.Create();
            case "MD5":
                return MD5.Create();
            default:
                //case "SHA512":
                return SHA512.Create();
        }
    }

    public static byte[] GenerateSalt(int size = DefaultSaltSize)
    {
        var salt = new byte[size];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    public static Rfc2898DeriveBytes GetRfc2898DeriveBytes(byte[] key, byte[]? salt = null, int iterations = DefaultIterations)
    {
        salt ??= GenerateSalt();
#pragma warning disable SYSLIB0060
        return new Rfc2898DeriveBytes(key, salt, iterations, HashAlgorithmName.SHA512);
#pragma warning restore SYSLIB0060
    }

    // convenience: derive bytes directly
    public static byte[] DeriveBytes(byte[] key, int count, byte[]? salt = null, int iterations = DefaultIterations)
    {
        salt ??= GenerateSalt(DefaultSaltSize);
        return Rfc2898DeriveBytes.Pbkdf2(key, salt, iterations, HashAlgorithmName.SHA512, count);
    }

    public static bool FixedTimeEquals(byte[]? a, byte[]? b)
    {
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}