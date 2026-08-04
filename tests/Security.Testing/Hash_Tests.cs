using Regira.Security.Core;
using Regira.Security.Hashing;

namespace Security.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class Hash_Tests
{
    [TestCase(LoremIpsum.Value, LoremIpsum.Value)]
    [TestCase("password", "secret")]
    [TestCase("123456", "secret")]
    [TestCase("abc0123DEF", "")]
    [TestCase("0123ABCdef", null)]
    [TestCase(null, "secret")]
    [TestCase(null, null)]
    public void Verify_Hasher_Success(string? plaintext, string? secret)
    {
        var hasher = new Hasher(new CryptoOptions { Secret = secret });
        var hashed = hasher.Hash(plaintext);

        var verifier = new Hasher(new CryptoOptions { Secret = secret });
        var success = verifier.Verify(plaintext, hashed);
        Assert.That(success, Is.True);
    }
    [Test]
    public void Verify_Hasher_Failure()
    {
        var secret = "secret";
        var hasher = new Hasher(new CryptoOptions { Secret = secret });
        var hashed = hasher.Hash("password");

        var verifier = new Hasher(new CryptoOptions { Secret = secret });
        var success = verifier.Verify("wrong_password", hashed);
        Assert.That(success, Is.False);
    }

    [TestCase(LoremIpsum.Value, LoremIpsum.Value)]
    [TestCase("password", "secret")]
    [TestCase("123456", "secret")]
    [TestCase("abc0123DEF", "")]
    [TestCase("0123ABCdef", null)]
    [TestCase(null, "secret")]
    [TestCase(null, null)]
    public void Verify_SimpleHasher_Success(string? plaintext, string? secret)
    {
        var hasher = new SimpleHasher(new CryptoOptions { Secret = secret });
        var hashed = hasher.Hash(plaintext);

        var verifier = new SimpleHasher(new CryptoOptions { Secret = secret });
        var success = verifier.Verify(plaintext, hashed);
        Assert.That(success, Is.True);
    }
    [Test]
    public void Verify_SimpleHasher_Failure()
    {
        var secret = "secret";
        var hasher = new SimpleHasher(new CryptoOptions { Secret = secret });
        var hashed = hasher.Hash("password");

        var verifier = new SimpleHasher(new CryptoOptions { Secret = secret });
        var success = verifier.Verify("wrong_password", hashed);
        Assert.That(success, Is.False);
    }
}