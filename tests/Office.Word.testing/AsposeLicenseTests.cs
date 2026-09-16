using System.Text;
using Regira.Office.Word.Aspose;

namespace Office.Word.testing;

/// <summary>
/// Where <see cref="WordService"/> finds its licence. Nothing here hands a licence to Aspose, so these run
/// with or without one; applying it is covered by <see cref="AsposeTests"/> and
/// <see cref="AsposeEvaluationTests"/>.
/// </summary>
[TestFixture]
public class AsposeLicenseTests
{
    private static readonly byte[] ConfigLicense = Encoding.UTF8.GetBytes("<License>from config</License>");
    private static readonly byte[] EnvironmentLicense = Encoding.UTF8.GetBytes("<License>from environment</License>");

    private string _tempDir = null!;

    [SetUp]
    public void CreateTempDir()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "regira-aspose-license-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void RemoveTempDir() => Directory.Delete(_tempDir, true);


    [Test]
    public void Nothing_Configured_Resolves_To_Nothing()
    {
        var (bytes, source) = AsposeLicense.Resolve(new AsposeWordConfig(), NoEnvironment);

        Assert.Multiple(() =>
        {
            Assert.That(bytes, Is.Null);
            Assert.That(source, Is.Null);
        });
    }

    [Test]
    public void LicenseBase64_Is_Decoded()
    {
        var (bytes, source) = AsposeLicense.Resolve(new AsposeWordConfig { LicenseBase64 = Convert.ToBase64String(ConfigLicense) }, NoEnvironment);

        Assert.Multiple(() =>
        {
            Assert.That(bytes, Is.EqualTo(ConfigLicense));
            Assert.That(source, Is.EqualTo("AsposeWordConfig.LicenseBase64"));
        });
    }

    [Test]
    public void LicensePath_Is_Read()
    {
        var path = WriteFile("config.lic", ConfigLicense);

        var (bytes, source) = AsposeLicense.Resolve(new AsposeWordConfig { LicensePath = path }, NoEnvironment);

        Assert.Multiple(() =>
        {
            Assert.That(bytes, Is.EqualTo(ConfigLicense));
            Assert.That(source, Is.EqualTo(path));
        });
    }

    [Test]
    public void LicenseBase64_Wins_Over_LicensePath()
    {
        var path = WriteFile("other.lic", EnvironmentLicense);
        var config = new AsposeWordConfig { LicenseBase64 = Convert.ToBase64String(ConfigLicense), LicensePath = path };

        var (bytes, _) = AsposeLicense.Resolve(config, NoEnvironment);

        Assert.That(bytes, Is.EqualTo(ConfigLicense));
    }

    [Test]
    public void Configuration_Wins_Over_The_Environment()
    {
        var environment = Environment(content: Convert.ToBase64String(EnvironmentLicense));
        var config = new AsposeWordConfig { LicensePath = WriteFile("config.lic", ConfigLicense) };

        var (bytes, _) = AsposeLicense.Resolve(config, environment);

        Assert.That(bytes, Is.EqualTo(ConfigLicense));
    }

    [Test]
    public void The_Base64_Variable_Is_Used_When_Nothing_Is_Configured()
    {
        var environment = Environment(content: Convert.ToBase64String(EnvironmentLicense), path: WriteFile("env.lic", ConfigLicense));

        var (bytes, source) = AsposeLicense.Resolve(null, environment);

        Assert.Multiple(() =>
        {
            // the content variable comes before the path variable
            Assert.That(bytes, Is.EqualTo(EnvironmentLicense));
            Assert.That(source, Is.EqualTo(AsposeLicense.LicenseVariable));
        });
    }

    [Test]
    public void The_Path_Variable_Is_Used_Last()
    {
        var path = WriteFile("env.lic", EnvironmentLicense);

        var (bytes, source) = AsposeLicense.Resolve(new AsposeWordConfig { LicenseBase64 = " ", LicensePath = "" }, Environment(path: path));

        Assert.Multiple(() =>
        {
            // blank settings count as unset
            Assert.That(bytes, Is.EqualTo(EnvironmentLicense));
            Assert.That(source, Is.EqualTo(path));
        });
    }

    [Test]
    public void Invalid_Base64_Names_Its_Source()
    {
        var fromConfig = Assert.Throws<InvalidOperationException>(
            () => AsposeLicense.Resolve(new AsposeWordConfig { LicenseBase64 = "not base64!" }, NoEnvironment));
        var fromEnvironment = Assert.Throws<InvalidOperationException>(
            () => AsposeLicense.Resolve(null, Environment(content: "not base64!")));

        Assert.Multiple(() =>
        {
            Assert.That(fromConfig!.Message, Does.Contain("AsposeWordConfig.LicenseBase64"));
            Assert.That(fromEnvironment!.Message, Does.Contain(AsposeLicense.LicenseVariable));
        });
    }

    [Test]
    public void A_Missing_License_File_Is_Reported()
    {
        var path = Path.Combine(_tempDir, "missing.lic");

        var ex = Assert.Throws<InvalidOperationException>(() => AsposeLicense.Resolve(new AsposeWordConfig { LicensePath = path }, NoEnvironment));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("missing.lic"));
            Assert.That(ex.Message, Does.Contain("AsposeWordConfig.LicensePath"));
            Assert.That(ex.InnerException, Is.InstanceOf<FileNotFoundException>());
        });
    }


    private static string? NoEnvironment(string name) => null;

    private static Func<string, string?> Environment(string? content = null, string? path = null)
        => name => name switch
        {
            AsposeLicense.LicenseVariable => content,
            AsposeLicense.LicensePathVariable => path,
            _ => null
        };

    private string WriteFile(string name, byte[] bytes)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
