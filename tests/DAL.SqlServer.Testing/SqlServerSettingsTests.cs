using Microsoft.Data.SqlClient;
using Regira.DAL.SqlServer.Core;

namespace DAL.SqlServer.Testing;

public class SqlServerSettingsTests
{
    [Test]
    public void Defaults_To_Windows_Authentication_On_Localhost()
    {
        var builder = new SqlConnectionStringBuilder(new SqlServerSettings(databaseName: "shop").BuildConnectionString());

        Assert.Multiple(() =>
        {
            Assert.That(builder.DataSource, Is.EqualTo("localhost"));
            Assert.That(builder.InitialCatalog, Is.EqualTo("shop"));
            Assert.That(builder.IntegratedSecurity, Is.True);
            Assert.That(builder.UserID, Is.Empty);
        });
    }

    [TestCase("Encrypt=Strict", "Strict")]
    [TestCase("Encrypt=Mandatory", "Mandatory")]
    [TestCase("Encrypt=Optional", "Optional")]
    public void The_Encryption_Mode_Survives_The_Round_Trip(string encrypt, string expected)
    {
        var settings = SqlServerSettings.FromConnectionString($"Server=db01;Database=shop;Integrated Security=true;{encrypt}");

        var builder = new SqlConnectionStringBuilder(settings.Clone<SqlServerSettings>().BuildConnectionString());

        Assert.That(builder.Encrypt, Is.EqualTo(SqlConnectionEncryptOption.Parse(expected)));
    }

    [Test]
    public void Uses_A_Sql_Login_When_A_Username_Is_Given()
    {
        var builder = new SqlConnectionStringBuilder(new SqlServerSettings("db01", "shop", "app", "secret").BuildConnectionString());

        Assert.Multiple(() =>
        {
            Assert.That(builder.IntegratedSecurity, Is.False);
            Assert.That(builder.UserID, Is.EqualTo("app"));
            Assert.That(builder.Password, Is.EqualTo("secret"));
        });
    }

    [TestCase("1433", "db01")]
    [TestCase("1444", "db01,1444")]
    public void Appends_Only_A_Non_Default_Port_With_A_Comma(string port, string expectedDataSource)
    {
        var builder = new SqlConnectionStringBuilder(new SqlServerSettings("db01", port: port).BuildConnectionString());

        Assert.That(builder.DataSource, Is.EqualTo(expectedDataSource));
    }

    [Test]
    public void FromConnectionString_Splits_The_Port_From_The_Host()
    {
        var settings = SqlServerSettings.FromConnectionString("Server=tcp:db01.example.com,1444;Database=shop;User ID=app;Password=secret;Encrypt=true;TrustServerCertificate=true");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("tcp:db01.example.com"));
            Assert.That(settings.Port, Is.EqualTo("1444"));
            Assert.That(settings.DatabaseName, Is.EqualTo("shop"));
            Assert.That(settings.Username, Is.EqualTo("app"));
            Assert.That(settings.Password, Is.EqualTo("secret"));
            Assert.That(settings.UseSecure, Is.True);
            Assert.That(settings.TrustServerCertificate, Is.True);
        });
    }

    [Test]
    public void FromConnectionString_Keeps_Windows_Authentication_And_A_Named_Instance()
    {
        var settings = SqlServerSettings.FromConnectionString(@"Server=.\SQLEXPRESS;Database=shop;Trusted_Connection=True;Encrypt=false");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo(@".\SQLEXPRESS"));
            Assert.That(settings.Port, Is.EqualTo(SqlServerDefaults.Port));
            Assert.That(settings.Username, Is.Null);
            Assert.That(settings.Password, Is.Null);
            Assert.That(settings.UseSecure, Is.False);
        });
    }

    [Test]
    public void Clone_Round_Trips_The_Connection_String()
    {
        var settings = new SqlServerSettings("db01", "shop", "app", "secret", "1444") { UseSecure = true, TrustServerCertificate = true };

        var clone = settings.Clone<SqlServerSettings>();

        Assert.That(clone.BuildConnectionString(), Is.EqualTo(settings.BuildConnectionString()));
    }
}
