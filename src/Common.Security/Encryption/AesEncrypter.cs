using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Regira.Security.Abstractions;
using Regira.Security.Core;
using Regira.Security.Utilities;

namespace Regira.Security.Encryption;

/// <summary>
/// AES-256 with a random salt per value, so a value encrypts to a different text each time.
/// <para>
/// The costly step, PBKDF2 over the secret (<see cref="CryptoUtility.DefaultIterations"/> iterations, about 0.2 s), runs
/// once per secret and key salt and is cached for the process; each value then derives its own AES key and IV from that
/// master key and its random salt with HKDF, in microseconds. The text is <c>aes1:</c> followed by the Base64 of the key
/// salt, the value salt, the cipher and an HMAC-SHA256 tag over the three, so a value that was changed, or encrypted with
/// another secret, throws a <see cref="CryptographicException"/> instead of decrypting to garbage.
/// </para>
/// <para>
/// A text without that prefix is the earlier format, a PBKDF2 derivation per value, and still decrypts; its derived key is
/// cached once read, so it pays the derivation only the first time. Decrypt it and encrypt it again to move it to the
/// current format.
/// </para>
/// Encrypting needs a secret: <see cref="CryptoOptions.Secret"/>, or a key passed to <see cref="Encrypt"/>. Decrypting
/// without one uses <see cref="DefaultSecuritySettings.SaltKey"/>, so what was encrypted with that key stays readable.
/// </summary>
public class AesEncrypter(CryptoOptions? options = null) : IEncrypter
{
    private const string FormatPrefix = "aes1:";
    private const int SaltSize = CryptoUtility.DefaultSaltSize;
    private const int TagSize = HMACSHA256.HashSizeInBytes;
    // the shortest aes1 value: two salts, one AES block (an empty text pads to one) and the tag
    private const int MinFormatSize = 2 * SaltSize + 16 + TagSize;
    private static readonly byte[] ValueKeyInfo = Encoding.ASCII.GetBytes("Regira.AesEncrypter.v1");

    // PBKDF2 results by secret, salt and length: the derivation is what costs, and an application reads its values again.
    // Bounded, so the earlier format's salt per value cannot grow it without limit.
    private const int MaxDerivedKeys = 10_000;
    private static readonly ConcurrentDictionary<string, byte[]> DerivedKeys = new();
    // one key salt per secret for everything the process encrypts, so its master key is derived once
    private static readonly ConcurrentDictionary<string, byte[]> KeySalts = new();

    private readonly string? _secret = options?.Secret;
    private readonly Encoding _encoding = options?.Encoding ?? DefaultSecuritySettings.Encoding;


    /// <exception cref="InvalidOperationException">Neither <paramref name="key"/> nor <see cref="CryptoOptions.Secret"/> holds a secret</exception>
    public string Encrypt(string plainText, string? key = null)
    {
        key ??= _secret;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"{nameof(AesEncrypter)} needs a secret to encrypt: set {nameof(CryptoOptions)}.{nameof(CryptoOptions.Secret)} or pass a key. " +
                "The default key is published with the Regira source, so anyone could read what it encrypts.");
        }

        var keyBytes = _encoding.GetBytes(key);
        var secretId = SecretId(keyBytes);
        var keySalt = KeySalts.GetOrAdd(secretId, _ => CryptoUtility.GenerateSalt(SaltSize));
        var masterKey = DeriveCached(keyBytes, secretId, keySalt, 32);

        var valueSalt = CryptoUtility.GenerateSalt(SaltSize);
        var (aesKey, aesIV, macKey) = ValueKeys(masterKey, valueSalt);
        var cipher = Transform(aesKey, aesIV, _encoding.GetBytes(plainText), encrypt: true);

        // encrypt-then-MAC: the tag covers both salts and the cipher
        byte[] body = [.. keySalt, .. valueSalt, .. cipher];
        return FormatPrefix + Convert.ToBase64String([.. body, .. HMACSHA256.HashData(macKey, body)]);
    }
    /// <exception cref="CryptographicException">An <c>aes1:</c> text that was encrypted with another secret, changed or cut short</exception>
    public string Decrypt(string encryptedText, string? key = null)
    {
        key ??= _secret ?? DefaultSecuritySettings.SaltKey;
        var keyBytes = _encoding.GetBytes(key);
        var secretId = SecretId(keyBytes);

        if (encryptedText.StartsWith(FormatPrefix, StringComparison.Ordinal))
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(encryptedText[FormatPrefix.Length..]);
            }
            catch (FormatException ex)
            {
                // cut to a length Base64 cannot have, by a column too short: refused as any other changed text is
                throw new CryptographicException("The text is not an aes1 value.", ex);
            }
            if (bytes.Length < MinFormatSize) throw new CryptographicException("The text is too short to be an aes1 value.");

            var body = bytes[..^TagSize];
            var masterKey = DeriveCached(keyBytes, secretId, body[..SaltSize], 32);
            var (aesKey, aesIV, macKey) = ValueKeys(masterKey, body[SaltSize..(2 * SaltSize)]);
            // checked before decrypting, in constant time, so a changed text never reaches the padding check
            if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(macKey, body), bytes[^TagSize..]))
                throw new CryptographicException("The value was encrypted with another secret, or it was changed.");
            return _encoding.GetString(Transform(aesKey, aesIV, body[(2 * SaltSize)..], encrypt: false));
        }

        // the earlier format: the salt, then the cipher, with a key and IV derived from the secret and that salt
        var allBytes = Convert.FromBase64String(encryptedText);
        if (allBytes.Length <= SaltSize) return string.Empty;

        var derived = DeriveCached(keyBytes, secretId, allBytes[..SaltSize], 48); // 32 + 16
        return _encoding.GetString(Transform(derived[..32], derived[32..], allBytes[SaltSize..], encrypt: false));
    }

    // the cache knows a secret by its hash, so no other copy of it is kept
    private static string SecretId(byte[] keyBytes) => Convert.ToBase64String(SHA256.HashData(keyBytes));

    private static byte[] DeriveCached(byte[] keyBytes, string secretId, byte[] salt, int length)
    {
        var cacheKey = $"{secretId}:{Convert.ToBase64String(salt)}:{length}";
        if (DerivedKeys.TryGetValue(cacheKey, out var cached)) return cached;

        var derived = CryptoUtility.DeriveBytes(keyBytes, length, salt, CryptoUtility.DefaultIterations);
        if (DerivedKeys.Count >= MaxDerivedKeys) DerivedKeys.Clear();
        DerivedKeys[cacheKey] = derived;
        return derived;
    }

    // an AES key, an IV and an HMAC key of the value's own, from the master key and the value's random salt
    private static (byte[] Key, byte[] IV, byte[] MacKey) ValueKeys(byte[] masterKey, byte[] valueSalt)
    {
        var okm = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 80, valueSalt, ValueKeyInfo); // 32 + 16 + 32
        return (okm[..32], okm[32..48], okm[48..]);
    }

    // CBC with PKCS7 padding, as the earlier format was written
    private static byte[] Transform(byte[] key, byte[] iv, byte[] input, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return encrypt ? aes.EncryptCbc(input, iv) : aes.DecryptCbc(input, iv);
    }
}
