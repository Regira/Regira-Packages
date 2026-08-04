namespace Regira.Security.Abstractions;

/// <summary>
/// Defines methods for hashing and verifying plain text values.
/// </summary>
public interface IHasher
{
    /// <summary>
    /// Computes a hash for the given plain text input.
    /// </summary>
    /// <param name="plainText">The plain text input to be hashed.</param>
    /// <returns>A hashed representation of the input as a <see cref="string"/>.</returns>
    string Hash(string plainText);
    /// <summary>
    /// Verifies whether the provided plain text matches the given hashed value.
    /// </summary>
    /// <param name="plainText">The plain text input to verify.</param>
    /// <param name="hashedValue">The hashed value to compare against.</param>
    /// <returns>
    /// <c>true</c> if the plain text matches the hashed value; otherwise, <c>false</c>.
    /// </returns>
    bool Verify(string plainText, string hashedValue);
}