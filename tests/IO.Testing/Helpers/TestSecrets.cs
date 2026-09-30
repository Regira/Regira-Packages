using Microsoft.Extensions.Configuration;
using Regira.IO.Storage.GitHub;
using Regira.IO.Storage.SSH;

namespace IO.Testing.Helpers;

/// <summary>
/// Connection settings for the network-backed fixtures, read from this project's user secrets
/// (<c>appsettings.json</c> is the template). A fixture reads them first in its setup: a required secret
/// that is missing or blank ignores the fixture before it creates anything, so a run without secrets
/// reports those fixtures as skipped instead of failed.
/// </summary>
public static class TestSecrets
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(typeof(TestSecrets).Assembly, true)
        .Build();

    public static string AzureConnectionString()
        => Require("Storage:Azure:ConnectionString", "the connection string of an Azure storage account, or UseDevelopmentStorage=true for Azurite");

    public static GitHubOptions GitHub(string contentPath) => new()
    {
        Uri = Require("Storage:GitHub:Uri", "the contents API of a repository the tests may write to (https://api.github.com/repos/{owner}/{repo})"),
        Key = Require("Storage:GitHub:Key", "a GitHub token that may write the contents of that repository"),
        Branch = Configuration["Storage:GitHub:Branch"] ?? "main",
        ContentPath = contentPath
    };

    public static SftpConfig Ssh() => new()
    {
        Host = Require("Storage:SSH:Host", "the host name of an SFTP server the tests may write to"),
        Port = int.Parse(Require("Storage:SSH:Port", "the port of that SFTP server")),
        UserName = Require("Storage:SSH:Username", "a user name on that SFTP server"),
        Password = Configuration["Storage:SSH:Password"],
        ContainerName = Configuration["Storage:SSH:ContainerName"]
    };

    private static string Require(string key, string target)
    {
        var value = Configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Ignore($"Skipped: set the user secret '{key}' to {target} (dotnet user-secrets set \"{key}\" <value> --project tests/IO.Testing).");
        }
        return value;
    }
}
