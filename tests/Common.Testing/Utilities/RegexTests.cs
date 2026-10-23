using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RegexTests
{
    const string TestInput = @"Lorem ipsum dolor sit amet, (info@regira.com) consectetur adipiscing elit. Quisque ipsum eros, laoreet vitae vulputate eget, iaculis vel tortor. 
Aliquam eleifend risus pellentesque est ornare auctor. Nullam et tempus odio. Donec mattis justo id nulla lacinia venenatis. Aliquam auctor purus ac turpis consequat, 
eu pretium ante venenatis. In in nunc quis orci finibus molestie. Nunc metus lorem, facilisis eu congue sit amet, sollicitudin eget ligula. Maecenas regira.com hendrerit 
erat consectetur ante dapibus volutpat. Cras tincidunt elit nec dignissim congue. Proin in posuere ligula. Aenean efficitur massa nunc, eu vestibulum magna vestibulum ac. 
Cras eget hendrerit nisi. Nulla ut rhoncus quam, eget efficitur enim. Nulla facilisi. Morbi volutpat arcu congue mi porttitor rhoncus. Ut rutrum blandit elit ut bibendum.
Proin nisl ante, vehicula id sodales sed, imperdiet ut arcu. Fusce aliquet, neque a vestibulum tristique, sapien nisi fringilla massa, quis rhoncus nibh lacus et lectus. 
Proin et lacinia lorem, +32 3 384 30 44 vitae condimentum nisl. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; 
Quisque eget dui eu mi pretium sollicitudin ut non enim. Nunc cursus pulvinar felis, in venenatis diam ullamcorper eu. Etiam tincidunt sit amet tortor eget commodo. 

Vestibulum lobortis in 192.168.0.1 orci nec egestas. Cras consectetur 0032 (0)3 384 30 44 massa ut ipsum hendrerit vulputate bbv.info@regira.com.
Pellentesque sit amet quam tellus. Mauris commodo est ut ligula tempor, at fringilla ante dignissim. Etiam justo ligula, malesuada non libero porttitor, 
tincidunt vulputate felis. Curabitur ipsum nulla, luctus sed ultricies non, convallis vel nisl. In quis risus a ex porta luctus. Donec faucibus varius nisl, 
vitae egestas lorem eleifend sed. Vestibulum accumsan orci vitae pulvinar semper. Phasellus tellus nibh, pharetra ac odio eget, condimentum volutpat erat. 
Vivamus congue tellus ut sapien ultrices viverra. Vestibulum tincidunt euismod est, vitae congue tortor accumsan quis. Curabitur tincidunt augue eget massa finibus, 
id ultrices dui suscipit. Integer https://www.regira.com rutrum risus efficitur turpis tempor 0475 99 99 99 vehicula id eget felis.

Pellentesque finibus consectetur mauris, a laoreet tortor. Mauris ac magna scelerisque lectus posuere  rutrum vitae ut elit. Aenean augue arcu, sagittis at hendrerit at, 
bibendum eget magna. Cras et ex vel dui luctus convallis. Praesent commodo sem nunc, a pretium massa pellentesque et. Suspendisse cursus metus eu libero scelerisque posuere. 
Vivamus tempus enim lacinia nisi molestie vulputate. Praesent 1:2:3:4:5:6:7:8 turpis sapien, placerat et risus at, rhoncus sagittis ligula.";

    [TestCase("info@regira.com", true)]
    [TestCase("bbv.info@regira.com", true)]
    [TestCase("bbv-info@regira.com", true)]
    [TestCase("bbv_info@regira.something", true)]
    [TestCase("ftp://user:pwd@domain.com", false)]
    [TestCase("info@regira.com.", false)]
    [TestCase("info@regira.com\n", false)]
    [TestCase("+32 3 384 30 44", false)]
    [TestCase("a@b.c", false)]
    [TestCase("xxx", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void TestValidEmail(string? input, bool expected)
    {
        var isValid = RegexUtility.IsValidEmail(input);
        Assert.That(isValid, Is.EqualTo(expected));
    }

    [TestCase("http://regira.com", true)]
    [TestCase("https://regira.com", true)]
    [TestCase("http://www.regira.com", true)]
    [TestCase("https://www.regira.com", true)]
    [TestCase("http://services.regira.com", true)]
    [TestCase("https://services.regira.com", true)]
    [TestCase("https://regira.something", true)]
    [TestCase("https://some.regira.thing", true)]
    [TestCase("ftp://user:pwd@domain.com", false)]
    [TestCase("www.regira.com", true)]
    [TestCase("regira.com", true)]
    public void TestValidUrl(string input, bool expected)
    {
        var isValid = RegexUtility.IsValidUrl(input);
        Assert.That(isValid, Is.EqualTo(expected));
    }

    [TestCase("0032 3 384 30 44", true)]
    [TestCase("0032 (0)3 384 30 44", true)]
    [TestCase("+32 3 384 30 44", true)]
    [TestCase("033843044", true)]
    [TestCase("03 384 30 44", true)]
    [TestCase("03/384.30.44", true)]
    [TestCase("0475 99 99 99", true)]
    [TestCase("0032 475 99 99 99", true)]
    [TestCase("+32 475 99 99 99", true)]
    [TestCase("+32 (0)475 99 99 99", true)]
    [TestCase("xx xxx xx xx", false)]
    [TestCase("info@regira.com", false)]
    [TestCase("http://www.regira.com", false)]
    // digit groups are separated by a space, a non-breaking one too, "-", "." or "/": a tab or a line break is no
    // separator
    [TestCase("+32\n3 384 30 44", false)]
    [TestCase("0475\t99 99 99", false)]
    [TestCase("+32\u2028" + "3 384 30 44", false)]
    [TestCase("+32\u00a03\u00a0384\u00a030\u00a044", true)]
    [TestCase("0475\u202f99\u202f99\u202f99", true)]
    // sms numbers
    [TestCase("4466", false)]
    [TestCase("44667", false)]
    [TestCase("446688", false)]
    [TestCase("xxxx", false)]
    [TestCase("44xx", false)]
    public void TestValidPhone(string input, bool expected)
    {
        var isValid = RegexUtility.IsValidPhoneNumber(input);
        Assert.That(isValid, Is.EqualTo(expected));
    }
    [TestCase("0032 3 384 30 44", true)]
    [TestCase("0032 (0)3 384 30 44", true)]
    [TestCase("+32 3 384 30 44", true)]
    [TestCase("033843044", true)]
    [TestCase("03 384 30 44", true)]
    [TestCase("03/384.30.44", true)]
    [TestCase("0475 99 99 99", true)]
    [TestCase("0032 475 99 99 99", true)]
    [TestCase("+32 475 99 99 99", true)]
    [TestCase("+32 (0)475 99 99 99", true)]
    [TestCase("xx xxx xx xx", false)]
    [TestCase("info@regira.com", false)]
    [TestCase("http://www.regira.com", false)]
    // digit groups are separated by a space, a non-breaking one too, "-", "." or "/": a tab or a line break is no
    // separator
    [TestCase("+32\n3 384 30 44", false)]
    [TestCase("0475\t99 99 99", false)]
    [TestCase("+32\u2028" + "3 384 30 44", false)]
    [TestCase("+32\u00a03\u00a0384\u00a030\u00a044", true)]
    [TestCase("0475\u202f99\u202f99\u202f99", true)]
    // sms numbers
    [TestCase("4466", true)]
    [TestCase("44667", true)]
    [TestCase("446688", true)]
    [TestCase("xxxx", false)]
    [TestCase("44xx", false)]
    [TestCase("anything 1234", false)]
    [TestCase("1234 anything", false)]
    public void TestValidPhone_AllowShortNumbers(string input, bool expected)
    {
        var isValid = RegexUtility.IsValidPhoneNumber(input, true);
        Assert.That(isValid, Is.EqualTo(expected));
    }

    [TestCase("192.168.0.1", true)]
    [TestCase("66.249.66.85", true)]
    [TestCase("216.58.216.47", true)]
    [TestCase("1:2:3:4:5:6:7:8", true)]
    [TestCase("256.256.256.256", false)]
    [TestCase("255.256.255.255", false)]
    [TestCase("::1", true)]
    [TestCase("fe80::1ff:fe23:4567:890a", true)]
    [TestCase("::ffff:192.168.0.1", true)]
    // every IPv6 notation: an IPv4 part after six groups or after a compression, and a zone ID
    [TestCase("1:2:3:4:5:6:192.0.2.1", true)]
    [TestCase("fe80::5efe:192.0.2.1", true)]
    [TestCase("64:ff9b::192.0.2.1", true)]
    [TestCase("fe80::1%eth0", true)]
    [TestCase("ff02::1%eth0", true)]
    [TestCase("fe80::1%eth0.100", true)]
    // the whole input is the address: one inside a longer text, in brackets or before a newline is not valid
    [TestCase("192.168.0.1.1", false)]
    [TestCase("300.255.0.255", false)]
    [TestCase("1:2:3:4:5:6:7:8:9", false)]
    [TestCase("[::1]", false)]
    [TestCase("[::1]:80", false)]
    [TestCase("::1/64", false)]
    [TestCase("192.168.0.1\n", false)]
    [TestCase("::1\n", false)]
    // a zone ID is letters, digits and ._~-: nothing else rides along after the '%'
    [TestCase("fe80::1%eth0\r\nX-Injected: 1", false)]
    [TestCase("fe80::1%eth0\n", false)]
    [TestCase("::1%<script>", false)]
    [TestCase("::1%", false)]
    // 66.249.66.01 and 0.0.0.0 are well-formed: rejecting a leading zero or the unspecified address is a choice the
    // validator does not make
    public void TestValidIP(string input, bool expected)
    {
        var isValid = RegexUtility.IsValidIPAddress(input);
        Assert.That(isValid, Is.EqualTo(expected));
    }


    [Test]
    public void TestExtractEmails()
    {
        var matches = RegexUtility.ExtractEmails(TestInput);
        Assert.That(matches, Is.Not.Empty);
        Assert.That(matches.Length == 2, Is.True);
        Assert.That(matches, Has.Member("info@regira.com"));
        Assert.That(matches, Has.Member("bbv.info@regira.com"));
    }
    [Test]
    public void TestExtractUrls()
    {
        var matches = RegexUtility.ExtractUrls(TestInput);
        Assert.That(matches, Is.Not.Empty);
        //Assert.IsTrue(matches.Length == 2);//ToDo: pattern is also extracting parts of email-addresses for now
        Assert.That(matches, Has.Member("regira.com"));
        Assert.That(matches, Has.Member("https://www.regira.com"));
    }
    [Test]
    public void TestExtractPhoneNumbers()
    {
        var matches = RegexUtility.ExtractPhoneNumbers(TestInput);
        Assert.That(matches, Is.Not.Empty);
        //Assert.IsTrue(matches.Length == 3); // also extracting IP 192.168.0.1
        Assert.That(matches, Has.Member("0032 (0)3 384 30 44"));
    }
    // a number is extracted whole, every digit group of it
    [TestCase("Proin et lacinia lorem, +32 3 384 30 44 vitae", "+32 3 384 30 44")]
    [TestCase("rutrum risus efficitur turpis tempor 0475 99 99 99 vehicula", "0475 99 99 99")]
    [TestCase("+32 (0)899.99.99.99 (test)", "+32 (0)899.99.99.99")]
    public void ExtractPhoneNumbers_Takes_The_Whole_Number(string input, string expected)
    {
        Assert.That(RegexUtility.ExtractPhoneNumbers(input), Is.EqualTo(new[] { expected }));
    }
    // a number never runs over a tab or a line break into the next one
    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\t")]
    public void ExtractPhoneNumbers_Keeps_Numbers_On_Separate_Lines_Apart(string between)
    {
        var input = $"03 384 30 44{between}0475 99 99 99";

        Assert.That(RegexUtility.ExtractPhoneNumbers(input), Is.EqualTo(new[] { "03 384 30 44", "0475 99 99 99" }));
    }
    [Test]
    public void TestExtractPhoneNumbers_AllowShortNumbers()
    {
        var matches = RegexUtility.ExtractPhoneNumbers(TestInput + " Extension 4711.", allowShortNumbers: true);
        Assert.That(matches, Is.SupersetOf(RegexUtility.ExtractPhoneNumbers(TestInput)), "a short number allowed loses none of the full ones");
        Assert.That(matches, Has.Member("4711"));
    }
    [Test]
    public void TestExtractIPs()
    {
        var matches = RegexUtility.ExtractIPAddresses(TestInput);
        Assert.That(matches, Is.Not.Empty);
        Assert.That(matches.Length == 2, Is.True);
        Assert.That(matches, Has.Member("192.168.0.1"));
        Assert.That(matches, Has.Member("1:2:3:4:5:6:7:8"));
    }
}