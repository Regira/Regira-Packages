using Regira.Security.Core;
using Regira.Security.Encryption;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Security.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AES_Tests
{
    [TestCase(LoremIpsum.Value, LoremIpsum.Value)]
    [TestCase("password", "secret")]
    [TestCase("123456", "secret")]
    [TestCase("abc0123DEF", "")]
    [TestCase("0123ABCdef", null)]
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
    [Test]
    public void Encrypt_Without_Secret()
    {
        var encrypter = new AesEncrypter();
        var plaintext = Guid.NewGuid().ToString();
        var encrypted = encrypter.Encrypt(plaintext);
        var decrypter = new AesEncrypter();
        var decrypted = decrypter.Decrypt(encrypted);
        Assert.That(decrypted, Is.EqualTo(plaintext));
    }
}