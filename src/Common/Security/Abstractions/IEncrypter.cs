namespace Regira.Security.Abstractions;

/// <summary>
/// Defines methods for encrypting and decrypting data.
/// </summary>
public interface IEncrypter
{
    /// <summary>
    /// Encrypts the specified plain text using the provided key or a default key if none is specified.
    /// </summary>
    /// <param name="plainText">
    /// The plain text to be encrypted.
    /// </param>
    /// <param name="key">
    /// An optional encryption key. If not provided, a default key will be used.
    /// </param>
    /// <returns>
    /// A base64-encoded string representing the encrypted data.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the encryption key is missing or invalid.
    /// </exception>
    string Encrypt(string plainText, string? key = null);
    /// <summary>
    /// Decrypts the specified encrypted text using the provided key or a default key if none is specified.
    /// </summary>
    /// <param name="encryptedText">
    /// The base64-encoded string representing the encrypted data to be decrypted.
    /// </param>
    /// <param name="key">
    /// An optional decryption key. If not provided, a default key will be used.
    /// </param>
    /// <returns>
    /// The decrypted plain text.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the decryption key is missing or invalid.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when the decryption process fails due to invalid data or an incorrect key.
    /// </exception>
    string Decrypt(string encryptedText, string? key = null);
}