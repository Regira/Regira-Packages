using Regira.Security.Core;
using Regira.Security.Encryption;

namespace Security.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class Symmetric_Tests
{
    [TestCase("32087901-04E0-47EB-959E-A3A7EBB36DCD", null, "RHpJu+UBzo7ZiE7QEEJZYh4anOdJPjw/IP7JhDD80eXrdq49zx7Pg0Q7CUiTitZ9")]
    [TestCase("32087901-04E0-47EB-959E-A3A7EBB36DCD", "REGIRA_INVOICING", "CdrHcv7NQcmycRt9wJYXtFvJDByJX2mzOPBqwwMdgbYYP3X91y47N5DJt2EHeo7g")]
    [TestCase(LoremIpsum.Value, LoremIpsum.Value, "eZ+FVOTVgVLJNiGJhBDYW17nClUlBut68O+8Cm4HzRc9qwZhzKrX+FLUraXYt5F76upsAbwJ1ZGroii/r+DEKiERplUw3AtHw2huiwTY56P1EBpBAeZEQsgKBTxWGIHexo8lW+ETMpCL4CN8A9903SdcY0CsRhNcKynO1yCL1xg2TngCapo3R1ObP7HIsD2rT7tpQ/++/AKLEPh27TaI75qeQF7PL2UQOl2s9ndFwcegnUYQxVmOTXupwHYCBRWpLqwG/0Dv6kLhawFF+xNDfi4hRmtK+Z3SYl5cLZWSuxJy/ZOqrg4IwUfspKEzRRKu+VxvOURRqfXWKW1CCp5xsb26NKy+KlWLPw2PHjDvf3iykIzTF1EfEdVNy2jtKwV67WUuzB+i9Q3uZs9Uph6vYSg4YLJ7EaRmPZFcaeaQSKl8crpGtiLrA8/1UV+AfLTIfleV6Lefiaz3RS9Nd3KTbMvaBTIae0LHTXODnImL10B+a6KyDzqoopnieWQdOp28IkCaGYjp05f6lDc/7t2Ys1J1O45Jtt8n/q6KoF1z9EGwC1GjTb0gzx8VNJSDKaF5YInnZmgGaya6059vJ3IRgkeUSNGQQPWog+NVHnvjODWbXKrZioYoHVuOLe/CJYK1Y/ZdRqLLJxKsGwzr54z0p7leFD4c1hVFRGRkybfFHT9YU4SLxcrqOkce3hf1bKmrft3CoMVQ+MEBSRxu7K/CBw==")]
    [TestCase("password", "secret", "Xw2CHaLT+CCu6nLY2CJ8Xw==")]
    [TestCase("123456", "secret", "+RQZVwjbwPlJZmEmMk5Hzw==")]
    [TestCase("abc0123DEF", "", "IqaEu4JDFReqHJGziB6F/w==")]
    [TestCase("0123ABCdef", null, "QkMbywZuWzZchd+Jw2Ykiw==")]
    public void Encrypt_Success(string plaintext, string? secret, string? expected = null)
    {
        var encrypter = new SymmetricEncrypter(new CryptoOptions { Secret = secret });
        var encrypted = encrypter.Encrypt(plaintext);
        Assert.That(encrypted, Is.EqualTo(expected));
    }

    [TestCase("32087901-04E0-47EB-959E-A3A7EBB36DCD", null, "RHpJu+UBzo7ZiE7QEEJZYh4anOdJPjw/IP7JhDD80eXrdq49zx7Pg0Q7CUiTitZ9")]
    [TestCase("32087901-04E0-47EB-959E-A3A7EBB36DCD", "REGIRA_INVOICING", "CdrHcv7NQcmycRt9wJYXtFvJDByJX2mzOPBqwwMdgbYYP3X91y47N5DJt2EHeo7g")]
    [TestCase(LoremIpsum.Value, LoremIpsum.Value, "eZ+FVOTVgVLJNiGJhBDYW17nClUlBut68O+8Cm4HzRc9qwZhzKrX+FLUraXYt5F76upsAbwJ1ZGroii/r+DEKiERplUw3AtHw2huiwTY56P1EBpBAeZEQsgKBTxWGIHexo8lW+ETMpCL4CN8A9903SdcY0CsRhNcKynO1yCL1xg2TngCapo3R1ObP7HIsD2rT7tpQ/++/AKLEPh27TaI75qeQF7PL2UQOl2s9ndFwcegnUYQxVmOTXupwHYCBRWpLqwG/0Dv6kLhawFF+xNDfi4hRmtK+Z3SYl5cLZWSuxJy/ZOqrg4IwUfspKEzRRKu+VxvOURRqfXWKW1CCp5xsb26NKy+KlWLPw2PHjDvf3iykIzTF1EfEdVNy2jtKwV67WUuzB+i9Q3uZs9Uph6vYSg4YLJ7EaRmPZFcaeaQSKl8crpGtiLrA8/1UV+AfLTIfleV6Lefiaz3RS9Nd3KTbMvaBTIae0LHTXODnImL10B+a6KyDzqoopnieWQdOp28IkCaGYjp05f6lDc/7t2Ys1J1O45Jtt8n/q6KoF1z9EGwC1GjTb0gzx8VNJSDKaF5YInnZmgGaya6059vJ3IRgkeUSNGQQPWog+NVHnvjODWbXKrZioYoHVuOLe/CJYK1Y/ZdRqLLJxKsGwzr54z0p7leFD4c1hVFRGRkybfFHT9YU4SLxcrqOkce3hf1bKmrft3CoMVQ+MEBSRxu7K/CBw==")]
    [TestCase("password", "secret", "Xw2CHaLT+CCu6nLY2CJ8Xw==")]
    [TestCase("123456", "secret", "+RQZVwjbwPlJZmEmMk5Hzw==")]
    [TestCase("abc0123DEF", "", "IqaEu4JDFReqHJGziB6F/w==")]
    [TestCase("0123ABCdef", null, "QkMbywZuWzZchd+Jw2Ykiw==")]
    public void Decrypt_Encrypted_Success(string plaintext, string? secret, string encrypted)
    {
        var decrypter = new SymmetricEncrypter(new CryptoOptions { Secret = secret });
        var decrypted = decrypter.Decrypt(encrypted);
        Assert.That(decrypted, Is.EqualTo(plaintext));
    }

    [Test]
    public void Decrypt_Encrypted_Failure()
    {
        var plaintext = "password";
        var secret = "secret";
        var encrypter = new SymmetricEncrypter(new CryptoOptions { Secret = secret });
        var encrypted = encrypter.Encrypt("wrong_password");
        var decrypter = new SymmetricEncrypter(new CryptoOptions { Secret = secret });
        var decrypted = decrypter.Decrypt(encrypted);
        Assert.That(plaintext, Is.Not.EqualTo(decrypted));
    }

    [Test]
    public void Encrypt_Without_Secret()
    {
        var encrypter = new SymmetricEncrypter();
        var plaintext = Guid.NewGuid().ToString();
        var encrypted = encrypter.Encrypt(plaintext);
        var decrypter = new SymmetricEncrypter();
        var decrypted = decrypter.Decrypt(encrypted);
        Assert.That(decrypted, Is.EqualTo(plaintext));
    }
}