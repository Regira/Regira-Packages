using MongoDB.Driver;
using Regira.DAL.MongoDB.Core;

namespace DAL.MongoDB.Testing;

public class MongoSettingsTests
{
    [Test]
    public void BuildConnectionString_Keeps_An_Srv_Uri_On_The_Srv_Scheme()
    {
        var settings = MongoSettings.FromConnectionString("mongodb+srv://app:s3cret@cluster0.abcde.mongodb.net/shop?authSource=admin");

        Assert.Multiple(() =>
        {
            Assert.That(settings.UseSrv, Is.True);
            Assert.That(settings.BuildConnectionString(), Does.StartWith("mongodb+srv://app:s3cret@cluster0.abcde.mongodb.net/shop"));
            // DNS supplies the port for an SRV deployment, and naming one makes the URI invalid
            Assert.That(settings.BuildConnectionString(), Does.Not.Contain(":27017"));
        });
    }

    [Test]
    public void FromConnectionString_Keeps_Every_Replica_Set_Member()
    {
        var settings = MongoSettings.FromConnectionString("mongodb://one.example.com:27017,two.example.com:27018/shop");

        Assert.That(settings.BuildConnectionString(), Is.EqualTo("mongodb://one.example.com:27017,two.example.com:27018/shop"));
    }

    [Test]
    public void BuildConnectionString_Carries_The_Credentials_And_The_Auth_Source()
    {
        var settings = new MongoSettings("mongo.example.com", "shop", username: "app", password: "s3cret")
        {
            AuthenticationDatabase = "admin"
        };

        Assert.That(settings.BuildConnectionString(), Is.EqualTo("mongodb://app:s3cret@mongo.example.com:27017/shop?authSource=admin"));
    }

    [Test]
    public void BuildConnectionString_Leaves_The_Password_Out_On_Request()
    {
        var settings = new MongoSettings("mongo.example.com", "shop", username: "app", password: "s3cret");

        Assert.Multiple(() =>
        {
            Assert.That(settings.BuildConnectionString(includePassword: false), Is.EqualTo("mongodb://app@mongo.example.com:27017/shop"));
            Assert.That(settings.BuildConnectionString(includePassword: false), Does.Not.Contain("s3cret"));
        });
    }

    [Test]
    public void BuildConnectionString_Percent_Encodes_The_Credentials()
    {
        var settings = new MongoSettings("mongo.example.com", "shop", username: "app@corp", password: "p@ss:w/rd");

        Assert.That(settings.BuildConnectionString(), Is.EqualTo("mongodb://app%40corp:p%40ss%3Aw%2Frd@mongo.example.com:27017/shop"));
    }

    [Test]
    public void BuildConnectionString_Announces_Tls_And_Keeps_A_Database_Free_Uri_Valid()
    {
        var settings = new MongoSettings("mongo.example.com", null, useTls: true);

        Assert.That(settings.BuildConnectionString(), Is.EqualTo("mongodb://mongo.example.com:27017/?tls=true"));
    }

    [Test]
    public void BuildConnectionString_Appends_Extra_Options()
    {
        var settings = new MongoSettings("mongo.example.com", "shop");

        Assert.That(settings.BuildConnectionString(new KeyValuePair<string, string>("readPreference", "secondary")),
            Is.EqualTo("mongodb://mongo.example.com:27017/shop?readPreference=secondary"));
    }

    [Test]
    public void FromConnectionString_Keeps_The_Options_It_Does_Not_Model()
    {
        const string uri = "mongodb://mongo-1.example.com:27017/shop?authMechanism=MONGODB-X509&authSource=%24external" +
                           "&replicaSet=rs0&directConnection=true&readPreference=secondaryPreferred&tls=true&tlsCAFile=C%3A%5Ccerts%5Cca.pem";

        var settings = MongoSettings.FromConnectionString(uri);
        var client = settings.ToMongoClientSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.UriOptions, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["authMechanism"] = "MONGODB-X509",
                ["replicaSet"] = "rs0",
                ["directConnection"] = "true",
                ["readPreference"] = "secondaryPreferred",
                ["tlsCAFile"] = @"C:\certs\ca.pem"
            }));
            Assert.That(settings.AuthenticationDatabase, Is.EqualTo("$external"));
            // what the driver makes of the URI the settings write back
            Assert.That(client.Credential.Mechanism, Is.EqualTo("MONGODB-X509"));
            Assert.That(client.Credential.Source, Is.EqualTo("$external"));
            Assert.That(client.ReplicaSetName, Is.EqualTo("rs0"));
            Assert.That(client.DirectConnection, Is.True);
            Assert.That(client.ReadPreference.ReadPreferenceMode, Is.EqualTo(ReadPreferenceMode.SecondaryPreferred));
            Assert.That(client.UseTls, Is.True);
        });
    }

    [Test]
    public void BuildConnectionString_Escapes_Option_Values()
    {
        var settings = new MongoSettings("mongo.example.com", "shop") { AuthenticationDatabase = "$external" };
        settings.UriOptions["appName"] = "orders & invoices";

        var uri = settings.BuildConnectionString(new KeyValuePair<string, string>("replicaSet", "rs=0;eu"));
        var url = MongoUrl.Create(uri);

        Assert.Multiple(() =>
        {
            Assert.That(uri, Is.EqualTo("mongodb://mongo.example.com:27017/shop?authSource=%24external&appName=orders%20%26%20invoices&replicaSet=rs%3D0%3Beu"));
            Assert.That(url.AuthenticationSource, Is.EqualTo("$external"));
            Assert.That(url.ApplicationName, Is.EqualTo("orders & invoices"));
            Assert.That(url.ReplicaSetName, Is.EqualTo("rs=0;eu"));
        });
    }

    [Test]
    public void FromConnectionString_Splits_The_Host_From_The_Port()
    {
        var settings = MongoSettings.FromConnectionString("mongodb://app:s3cret@mongo.example.com:27018/shop?authSource=admin");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("mongo.example.com"));
            Assert.That(settings.Port, Is.EqualTo("27018"));
            Assert.That(settings.DatabaseName, Is.EqualTo("shop"));
            Assert.That(settings.Username, Is.EqualTo("app"));
            Assert.That(settings.Password, Is.EqualTo("s3cret"));
            Assert.That(settings.AuthenticationDatabase, Is.EqualTo("admin"));
        });
    }

    [Test]
    public void FromConnectionString_Falls_Back_To_The_Default_Port()
    {
        var settings = MongoSettings.FromConnectionString("mongodb://mongo.example.com/shop");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("mongo.example.com"));
            Assert.That(settings.Port, Is.EqualTo("27017"));
        });
    }

    [Test]
    public void Clone_Round_Trips_Every_Setting()
    {
        var settings = new MongoSettings("mongo.example.com", "shop", "27018", "app@corp", "p@ss:w/rd", useTls: true)
        {
            AuthenticationDatabase = "admin"
        };

        var clone = settings.Clone<MongoSettings>();

        Assert.Multiple(() =>
        {
            Assert.That(clone.Host, Is.EqualTo("mongo.example.com"));
            Assert.That(clone.Port, Is.EqualTo("27018"));
            Assert.That(clone.DatabaseName, Is.EqualTo("shop"));
            Assert.That(clone.Username, Is.EqualTo("app@corp"));
            Assert.That(clone.Password, Is.EqualTo("p@ss:w/rd"));
            Assert.That(clone.AuthenticationDatabase, Is.EqualTo("admin"));
            Assert.That(clone.UseSecure, Is.True);
        });
    }

    [TestCase(null, "shop", "shop")]
    [TestCase(null, null, "admin")]
    [TestCase("admin", "shop", "admin")]
    public void ResolveAuthenticationDatabase_Applies_MongoDbs_Own_Default(string? authenticationDatabase, string? databaseName, string expected)
    {
        var settings = new MongoSettings("mongo.example.com", databaseName) { AuthenticationDatabase = authenticationDatabase };

        Assert.That(settings.ResolveAuthenticationDatabase(), Is.EqualTo(expected));
    }

    [Test]
    public void ToMongoClientSettings_Carries_The_Credential()
    {
        var settings = new MongoSettings("mongo.example.com", "shop", username: "app", password: "s3cret")
        {
            AuthenticationDatabase = "admin"
        };

        var credential = settings.ToMongoClientSettings().Credential;

        Assert.That(credential, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(credential!.Username, Is.EqualTo("app"));
            Assert.That(credential.Source, Is.EqualTo("admin"));
        });
    }

    [Test]
    public void ToMongoClientSettings_Keeps_The_Srv_Scheme_For_A_Dns_Seedlist()
    {
        var settings = MongoSettings.FromConnectionString("mongodb+srv://app:s3cret@cluster0.abcde.mongodb.net/shop");

        var clientSettings = settings.ToMongoClientSettings();

        Assert.Multiple(() =>
        {
            // naming the seed host as a server instead would connect to a host that need not accept connections
            Assert.That(clientSettings.Scheme.ToString(), Is.EqualTo("MongoDBPlusSrv"));
            Assert.That(clientSettings.Server.Host, Is.EqualTo("cluster0.abcde.mongodb.net"));
            Assert.That(clientSettings.Credential, Is.Not.Null);
        });
    }

    [Test]
    public void ToMongoClientSettings_Keeps_Every_Replica_Set_Member()
    {
        var settings = MongoSettings.FromConnectionString("mongodb://one.example.com:27017,two.example.com:27018/shop");

        var servers = settings.ToMongoClientSettings().Servers.Select(x => $"{x.Host}:{x.Port}");

        Assert.That(servers, Is.EqualTo(new[] { "one.example.com:27017", "two.example.com:27018" }));
    }

    [Test]
    public void ToMongoClientSettings_Stays_Anonymous_Without_A_Username()
    {
        var settings = new MongoSettings("mongo.example.com", "shop");

        Assert.That(settings.ToMongoClientSettings().Credential, Is.Null);
    }
}
