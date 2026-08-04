using System.Security.Cryptography;
using System.Text;
using Regira.Security.Abstractions;
using Regira.Security.Core;
using Regira.Security.Utilities;

namespace Regira.Security.Encryption;

public class SymmetricEncrypter : IEncrypter
{
    private readonly byte[] _defaultKey = new byte[32];
    private readonly byte[] _defaultVector = new byte[16];
    private readonly Encoding _encoding;
    private readonly string _algorithm;
    public SymmetricEncrypter(CryptoOptions? options = null)
    {
        _encoding = options?.Encoding ?? DefaultSecuritySettings.Encoding;
        _algorithm = options?.AlgorithmType ?? DefaultSecuritySettings.HashAlgorithm;
        GenerateKey(options?.Secret ?? DefaultSecuritySettings.SaltKey, ref _defaultKey, ref _defaultVector);
    }


    public string Encrypt(string plainText, string? secret = null)
    {
        var key = _defaultKey;
        var vector = _defaultVector;
        if (secret != null)
        {
            GenerateKey(secret, ref key, ref vector);
        }
        if (key == null)
        {
            throw new InvalidOperationException("EncryptionKey missing");
        }

        using var crypto = Aes.Create();
        if (crypto.LegalBlockSizes.Any() && crypto.LegalKeySizes.Any())
        {
            crypto.BlockSize = crypto.LegalBlockSizes[0].MaxSize;
            crypto.KeySize = crypto.LegalKeySizes[0].MaxSize;
        }
        crypto.Key = key;
        crypto.IV = vector;
        using var encryptor = crypto.CreateEncryptor(crypto.Key, crypto.IV);
        using var memoryStream = new MemoryStream();
        using var crptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
        using (var sw = new StreamWriter(crptoStream))
        {
            // Don't convert to 'using' declaration !!
            sw.Write(plainText);
        }
        return Convert.ToBase64String(memoryStream.ToArray());
    }
    public string Decrypt(string encryptedText, string? secret = null)
    {
        var key = _defaultKey;
        var vector = _defaultVector;
        if (secret != null)
        {
            GenerateKey(secret, ref key, ref vector);
        }

        if (key == null)
        {
            throw new InvalidOperationException("EncryptionKey missing");
        }


        using var crypto = Aes.Create();
        if (crypto.LegalBlockSizes.Any() && crypto.LegalKeySizes.Any())
        {
            crypto.BlockSize = crypto.LegalBlockSizes[0].MaxSize;
            crypto.KeySize = crypto.LegalKeySizes[0].MaxSize;
        }
        crypto.Key = key;
        crypto.IV = vector;
        using var decryptor = crypto.CreateDecryptor(crypto.Key, crypto.IV);
        var cipher = Convert.FromBase64String(encryptedText);
        using var memoryStream = new MemoryStream(cipher);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cryptoStream);
        return sr.ReadToEnd();
    }

    private void GenerateKey(string secret, ref byte[] key, ref byte[] vector)
    {
        var hasher = CryptoUtility.CreateHasher(_algorithm);
        var hashedBytes = hasher.ComputeHash(_encoding.GetBytes(secret));

        Array.Copy(hashedBytes, 0, key, 0, key.Length - 1);
        Array.Copy(hashedBytes, key.Length - 1, vector, 0, vector.Length - 1);
    }
}