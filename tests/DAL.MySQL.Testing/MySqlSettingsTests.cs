using MySqlConnector;
using NUnit.Framework;
using Regira.DAL.MySQL.Models;

namespace DAL.MySQL.Testing;

/// <summary>
/// <see cref="MySqlSettings"/> reads and writes connection strings through MySqlConnector's own builder, so any value a
/// connection string can hold survives the round trip.
/// </summary>
[TestFixture]
public class MySqlSettingsTests
{
    [Test]
    public void Defaults_To_The_Local_Root_Login()
    {
        var settings = new MySqlSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("localhost"));
            Assert.That(settings.Port, Is.EqualTo("3306"));
            Assert.That(settings.Username, Is.EqualTo("root"));
        });
    }

    [TestCase("p;a=ss")]
    [TestCase("p\"a'ss")]
    [TestCase(" spaced ")]
    public void A_Password_Survives_The_Round_Trip(string password)
    {
        var connectionString = new MySqlSettings("db01", "shop", username: "app", password: password).BuildConnectionString();

        Assert.Multiple(() =>
        {
            Assert.That(new MySqlConnectionStringBuilder(connectionString).Password, Is.EqualTo(password), "as MySqlConnector reads it");
            Assert.That(MySqlSettings.FromConnectionString(connectionString).Password, Is.EqualTo(password), "as MySqlSettings reads it");
        });
    }

    [Test]
    public void FromConnectionString_Reads_A_Quoted_Value_And_The_Keys_MySqlConnector_Accepts()
    {
        var settings = MySqlSettings.FromConnectionString("Host=db01;Port=3307;Database=shop;Uid=app;Pwd=\"p;a=ss\"");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("db01"));
            Assert.That(settings.Port, Is.EqualTo("3307"));
            Assert.That(settings.DatabaseName, Is.EqualTo("shop"));
            Assert.That(settings.Username, Is.EqualTo("app"));
            Assert.That(settings.Password, Is.EqualTo("p;a=ss"));
        });
    }

    /// <summary>Only <c>host:port</c> is split: a host list or an IPv6 address stays as it is, on the default port.</summary>
    [TestCase("h1:3307,h2")]
    [TestCase("fe80::1")]
    [TestCase("db01:primary")]
    public void A_Host_That_Is_Not_Host_And_Port_Stays_As_It_Is(string host)
    {
        var settings = new MySqlSettings(host, "shop");

        Assert.Multiple(() =>
        {
            Assert.That((settings.Host, settings.Port), Is.EqualTo((host, "3306")));
            Assert.That(() => settings.BuildConnectionString(), Throws.Nothing);
        });
    }

    [Test]
    public void A_Port_The_Connection_String_Gives_Wins_Over_The_Hosts_The_Default_Included()
    {
        var settings = MySqlSettings.FromConnectionString("Server=db01:3307;Port=3306;Database=shop");

        Assert.That((settings.Host, settings.Port), Is.EqualTo(("db01", "3306")));
    }

    [Test]
    public void A_Port_Written_With_The_Host_Is_The_Port()
    {
        var settings = new MySqlSettings("db01:3307", "shop");

        Assert.Multiple(() =>
        {
            Assert.That((settings.Host, settings.Port), Is.EqualTo(("db01", "3307")));
            Assert.That(new MySqlConnectionStringBuilder(settings.BuildConnectionString()).Port, Is.EqualTo(3307u));
        });
    }
}
