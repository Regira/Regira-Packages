using Regira.Security.Core;
using Regira.Security.Encryption;

namespace Security.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AES_Tests
{
    [TestCase(LoremIpsum.Value, LoremIpsum.Value)]
    [TestCase("password", "secret")]
    [TestCase("123456", "secret")]
    public void Decrypt_Encrypted_Success(string plaintext, string? secret)
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        var encrypted = encrypter.Encrypt(plaintext);
        var decrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        var decrypted = decrypter.Decrypt(encrypted);
        Assert.That(decrypted, Is.EqualTo(plaintext));
    }
    [Test]
    public void Decrypt_Encrypted_Failure()
    {
        var plaintext = "password";
        var secret = "secret";
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        var encrypted = encrypter.Encrypt("wrong_password");
        var decrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        var decrypted = decrypter.Decrypt(encrypted);
        Assert.That(plaintext, Is.Not.EqualTo(decrypted));
    }
    // the default key is published with the source, so encrypting with it protects nothing
    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase(" ", null)]
    [TestCase(null, "")]
    [TestCase(null, " ")]
    public void Encrypt_Without_A_Secret_Throws(string? secret, string? key)
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        Assert.Throws<InvalidOperationException>(() => encrypter.Encrypt("password", key));
    }
    [Test]
    public void Encrypt_With_A_Key_Needs_No_Secret()
    {
        var encrypter = new AesEncrypter();
        var encrypted = encrypter.Encrypt("password", "secret");
        Assert.That(encrypter.Decrypt(encrypted, "secret"), Is.EqualTo("password"));
    }
    // what an application encrypted without a secret before stays readable: the ciphertext comes from Regira.Security 6.5.0
    [TestCase("32087901-04E0-47EB-959E-A3A7EBB36DCD", "qFoyP6+Y4L9woVv7jsYfWLyD2jJUteufD4WdNA4QJP+Y+PeoNMKveqte6ZbKTJoq6BoOXZHBemBI6GS5Py9Y2Q==")]
    public void Decrypt_Without_A_Secret_Reads_What_The_Default_Key_Encrypted(string plaintext, string encrypted)
    {
        var decrypter = new AesEncrypter();
        Assert.That(decrypter.Decrypt(encrypted), Is.EqualTo(plaintext));
    }
}
