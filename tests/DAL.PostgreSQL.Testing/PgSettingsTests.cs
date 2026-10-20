using Npgsql;
using NUnit.Framework;
using Regira.DAL.PostgreSQL.Core;

namespace DAL.PostgreSQL.Testing;

/// <summary>
/// <see cref="PgSettings"/> reads and writes connection strings through Npgsql's own builder, so any value a
/// connection string can hold survives the round trip.
/// </summary>
[TestFixture]
public class PgSettingsTests
{
    [Test]
    public void Defaults_To_The_Local_Postgres_Login()
    {
        var settings = new PgSettings(databaseName: "shop");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("localhost"));
            Assert.That(settings.Port, Is.EqualTo("5432"));
            Assert.That(settings.Username, Is.EqualTo("postgres"));
            Assert.That(settings.Password, Is.EqualTo("postgres"));
        });
    }

    [TestCase("p;a=ss")]
    [TestCase("p\"a'ss")]
    [TestCase(" spaced ")]
    public void A_Password_Survives_The_Round_Trip(string password)
    {
        var connectionString = new PgSettings("db01", "shop", "app", password).BuildConnectionString();

        Assert.Multiple(() =>
        {
            Assert.That(new NpgsqlConnectionStringBuilder(connectionString).Password, Is.EqualTo(password), "as Npgsql reads it");
            Assert.That(PgSettings.FromConnectionString(connectionString).Password, Is.EqualTo(password), "as PgSettings reads it");
        });
    }

    [Test]
    public void FromConnectionString_Reads_A_Quoted_Value_And_The_Keys_Npgsql_Accepts()
    {
        var settings = PgSettings.FromConnectionString("Server=db01;Port=5433;Database=shop;User ID=app;Password=\"p;a=ss\"");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("db01"));
            Assert.That(settings.Port, Is.EqualTo("5433"));
            Assert.That(settings.DatabaseName, Is.EqualTo("shop"));
            Assert.That(settings.Username, Is.EqualTo("app"));
            Assert.That(settings.Password, Is.EqualTo("p;a=ss"));
        });
    }

    [Test]
    public void A_Port_Written_With_The_Host_Is_The_Port()
    {
        var settings = new PgSettings("db01:5433", "shop");
        var parsed = PgSettings.FromConnectionString("Host=db01:5433;Database=shop");

        Assert.Multiple(() =>
        {
            Assert.That((settings.Host, settings.Port), Is.EqualTo(("db01", "5433")));
            Assert.That((parsed.Host, parsed.Port), Is.EqualTo(("db01", "5433")));
            Assert.That(new NpgsqlConnectionStringBuilder(settings.BuildConnectionString()).Port, Is.EqualTo(5433));
        });
    }

    /// <summary>Only <c>host:port</c> is split: a host list or an IPv6 address stays as it is, on the default port.</summary>
    [TestCase("h1:5433,h2")]
    [TestCase("fe80::1")]
    [TestCase("db01:primary")]
    public void A_Host_That_Is_Not_Host_And_Port_Stays_As_It_Is(string host)
    {
        var settings = new PgSettings(host, "shop");

        Assert.Multiple(() =>
        {
            Assert.That((settings.Host, settings.Port), Is.EqualTo((host, "5432")));
            Assert.That(() => settings.BuildConnectionString(), Throws.Nothing);
        });
    }

    [Test]
    public void A_Port_The_Connection_String_Gives_Wins_Over_The_Hosts_The_Default_Included()
    {
        var settings = PgSettings.FromConnectionString("Host=db01:5433;Port=5432;Database=shop");

        Assert.That((settings.Host, settings.Port), Is.EqualTo(("db01", "5432")));
    }

    [Test]
    public void An_Explicit_Port_Wins_Over_The_Hosts()
    {
        var settings = new PgSettings("db01:5433", "shop", port: "5434");

        Assert.That((settings.Host, settings.Port), Is.EqualTo(("db01", "5434")));
    }
}
