using System.Security.Cryptography;
using System.Text;
using Regira.Security.Abstractions;
using Regira.Security.Core;
using Regira.Security.Utilities;

namespace Regira.Security.Encryption;

public class AesEncrypter(CryptoOptions? options = null) : IEncrypter
{
    private readonly string _defaultKey = options?.Secret ?? DefaultSecuritySettings.SaltKey;
    private readonly Encoding _encoding = options?.Encoding ?? DefaultSecuritySettings.Encoding;


    public string Encrypt(string plainText, string? key = null)
    {
        key ??= _defaultKey;

        var plainTextBytes = _encoding.GetBytes(plainText);

        // generate per-encryption salt
        var salt = CryptoUtility.GenerateSalt();
        var keyBytes = _encoding.GetBytes(key);

        // derive key and IV using PBKDF2 with salt
        var derived = CryptoUtility.DeriveBytes(keyBytes, 48, salt, CryptoUtility.DefaultIterations); // 32 + 16
        var aesKey = new byte[32];
        var aesIV = new byte[16];
        Buffer.BlockCopy(derived, 0, aesKey, 0, 32);
        Buffer.BlockCopy(derived, 32, aesIV, 0, 16);

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = aesIV;
        using var memoryStream = new MemoryStream();
        using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
        }

        var cipherBytes = memoryStream.ToArray();

        // store salt + cipher
        var result = new byte[salt.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }
    public string Decrypt(string encryptedText, string? key = null)
    {
        key ??= _defaultKey;

        var allBytes = Convert.FromBase64String(encryptedText);
        if (allBytes.Length <= CryptoUtility.DefaultSaltSize) return string.Empty;

        var salt = new byte[CryptoUtility.DefaultSaltSize];
        Buffer.BlockCopy(allBytes, 0, salt, 0, salt.Length);
        var cipher = new byte[allBytes.Length - salt.Length];
        Buffer.BlockCopy(allBytes, salt.Length, cipher, 0, cipher.Length);

        var keyBytes = _encoding.GetBytes(key);
        var derived = CryptoUtility.DeriveBytes(keyBytes, 48, salt, CryptoUtility.DefaultIterations);
        var aesKey = new byte[32];
        var aesIV = new byte[16];
        Buffer.BlockCopy(derived, 0, aesKey, 0, 32);
        Buffer.BlockCopy(derived, 32, aesIV, 0, 16);

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = aesIV;
        using var memoryStream = new MemoryStream();
        using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cryptoStream.Write(cipher, 0, cipher.Length);
            cryptoStream.FlushFinalBlock();
        }
        var bytes = memoryStream.ToArray();
        return _encoding.GetString(bytes);
    }
}