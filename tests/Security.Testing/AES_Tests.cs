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

    // the earlier format, a key derived per value with no version prefix, stays readable: the ciphertext comes from
    // the AesEncrypter that ran the full key derivation on every call
    [TestCase("BE71096123456769", "a-secret-from-configuration", "hT1+ZegMbSKnlVoTnQIIYzpA8gKeOKKW83kkaz9ozLRvkcQpTfmQ58w/XasC8MO1")]
    public void Decrypt_Reads_The_Earlier_Format(string plaintext, string secret, string encrypted)
    {
        var decrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        Assert.That(decrypter.Decrypt(encrypted), Is.EqualTo(plaintext));
    }
    [Test]
    public void Encrypt_Writes_The_Versioned_Format()
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = "secret" });
        Assert.That(encrypter.Encrypt("password"), Does.StartWith("aes1:"));
    }
    [Test]
    public void Encrypting_Twice_Gives_Different_Texts()
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = "secret" });
        Assert.That(encrypter.Encrypt("password"), Is.Not.EqualTo(encrypter.Encrypt("password")));
    }
    [Test]
    public void Another_Instance_With_The_Same_Secret_Decrypts()
    {
        var encrypted = new AesEncrypter(new CryptoOptions { Secret = "secret" }).Encrypt("password");
        Assert.That(new AesEncrypter(new CryptoOptions { Secret = "secret" }).Decrypt(encrypted), Is.EqualTo("password"));
    }
    // the tag tells another secret apart, where an unauthenticated CBC text could decrypt to garbage
    [Test]
    public void Another_Secret_Throws()
    {
        var encrypted = new AesEncrypter(new CryptoOptions { Secret = "secret" }).Encrypt("password");
        var decrypter = new AesEncrypter(new CryptoOptions { Secret = "another secret" });
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => decrypter.Decrypt(encrypted));
    }
    // a changed byte anywhere (salts, cipher or tag) is refused before the cipher is decrypted
    [TestCase(0)]
    [TestCase(20)]
    [TestCase(40)]
    [TestCase(-1)]
    public void A_Changed_Value_Throws(int index)
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = "secret" });
        var bytes = Convert.FromBase64String(encrypter.Encrypt("password")["aes1:".Length..]);
        bytes[index < 0 ? bytes.Length + index : index] ^= 0x01;
        var changed = "aes1:" + Convert.ToBase64String(bytes);
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => encrypter.Decrypt(changed));
    }
    [Test]
    public void A_Truncated_Value_Throws()
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = "secret" });
        var bytes = Convert.FromBase64String(encrypter.Encrypt("password")["aes1:".Length..]);
        var truncated = "aes1:" + Convert.ToBase64String(bytes[..40]);
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => encrypter.Decrypt(truncated));
    }
    // a column too short cuts the text to a length that is not Base64, which a caller's catch of the documented
    // exception must still see
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void A_Value_Cut_To_No_Base64_Throws(int cut)
    {
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = "secret" });
        var encrypted = encrypter.Encrypt("password");
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => encrypter.Decrypt(encrypted[..^cut]));
    }
    // the key derivation (about 0.2 s) runs once per secret, not per value: an EF value converter decrypts every row it
    // reads, and 50 rows took 10 s
    [Test]
    public void Decrypting_Many_Values_Derives_The_Key_Once()
    {
        var values = Enumerable.Range(0, 50).Select(i => $"BE{i:D14}").ToList();
        var encrypter = new AesEncrypter(new CryptoOptions { Secret = "a secret for 50 rows" });
        var encrypted = values.Select(v => encrypter.Encrypt(v)).ToList();

        var decrypter = new AesEncrypter(new CryptoOptions { Secret = "a secret for 50 rows" });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var decrypted = encrypted.Select(v => decrypter.Decrypt(v)).ToList();
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(decrypted, Is.EqualTo(values));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)));
        });
    }
    // a value in the earlier format pays the derivation when it is first read, not on every read
    [TestCase("BE71096123456769", "a-secret-from-configuration", "hT1+ZegMbSKnlVoTnQIIYzpA8gKeOKKW83kkaz9ozLRvkcQpTfmQ58w/XasC8MO1")]
    public void Reading_An_Earlier_Value_Again_Is_Cached(string plaintext, string secret, string encrypted)
    {
        var decrypter = new AesEncrypter(new CryptoOptions { Secret = secret });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var decrypted = Enumerable.Range(0, 30).Select(_ => decrypter.Decrypt(encrypted)).Distinct().ToList();
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(decrypted, Is.EqualTo(new[] { plaintext }));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        });
    }
}
