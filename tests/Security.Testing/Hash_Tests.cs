#pragma warning disable CS0618 // SymmetricEncrypter and SimpleHasher are obsolete
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
        // any count takes the same round trip; Verify_Hasher_Success_At_The_Default_Iterations runs the default count
        var hasher = new Hasher(new CryptoOptions { Secret = secret, Iterations = 1_000 });
        var hashed = hasher.Hash(plaintext);

        var verifier = new Hasher(new CryptoOptions { Secret = secret, Iterations = 1_000 });
        var success = verifier.Verify(plaintext, hashed);
        Assert.That(success, Is.True);
    }
    [Test]
    public void Verify_Hasher_Success_At_The_Default_Iterations()
    {
        var hashed = new Hasher().Hash("password");

        Assert.That(new Hasher().Verify("password", hashed), Is.True);
    }
    // a hash stored at the default count (500,000) keeps verifying: a changed default would sign every stored password out
    [Test]
    public void A_Hash_Stored_At_The_Default_Iterations_Keeps_Verifying()
    {
        const string stored = "tZjL6GAko+DPIvKWo2dcly40pAbsV1lybSa1nXfo42XTp8MCO5aFO6qlQ4djE8TEbEGGRmbZbs4luViCCxg3GUAdxz7TTX7wmg1guyZ6eDU=";

        Assert.That(new Hasher().Verify("correct horse battery staple", stored), Is.True);
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