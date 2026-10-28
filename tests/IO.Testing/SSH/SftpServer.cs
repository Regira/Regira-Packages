using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Regira.IO.Storage.SSH;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace IO.Testing.SSH;

/// <summary>
/// An SFTP server in a container, one per fixture. Gated like <c>tests\DAL.PostgreSQL.Testing</c>: skipped unless
/// <c>REGIRA_PROVIDER_TESTS=containers</c> is set, and skipped rather than failed when Docker is unavailable.
/// <para>
/// The user lands in a chroot whose only writable folder is <see cref="ContainerName"/>, so that folder is the
/// service's root. The server makes new host keys on every start; <see cref="Config"/> pins the one it presents, read
/// from the server's own key files, so every test connects through the fingerprint check.
/// </para>
/// </summary>
public sealed class SftpServer : IAsyncDisposable
{
    public const string EnvVar = "REGIRA_PROVIDER_TESTS";
    public const string EnableValue = "containers";

    // atmoz/sftp publishes no version tags, so the digest pins the image
    private const string Image = "atmoz/sftp:alpine@sha256:7e439fabeae536fa254340724c042e11999924d906417dbb5c73602ad518813f";
    private const ushort SshPort = 22;
    private const string UserName = "deploy";
    private const string Password = "secret";
    private const string ContainerName = "files";

    private readonly IContainer _container;

    private SftpServer(IContainer container, string hostKeyFingerprint)
    {
        _container = container;
        HostKeyFingerprint = hostKeyFingerprint;
    }

    /// <summary>The SHA-256 fingerprint of the host key the server presents, as <c>ssh-keygen -lf</c> prints it.</summary>
    public string HostKeyFingerprint { get; }

    /// <summary>The service's root on the server: the writable folder in the user's chroot.</summary>
    public static string Root => "/" + ContainerName;

    public SftpConfig Config(string? hostKeyFingerprint = null) => new()
    {
        Host = _container.Hostname,
        Port = _container.GetMappedPublicPort(SshPort),
        UserName = UserName,
        Password = Password,
        ContainerName = ContainerName,
        HostKeyFingerprint = hostKeyFingerprint ?? HostKeyFingerprint
    };

    public static async Task<SftpServer> StartAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvVar), EnableValue, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Skipped: set {EnvVar}={EnableValue} to run container-backed tests.");
        }

        // Build is inside the try too: without a Docker endpoint it throws before anything starts
        IContainer? container = null;
        try
        {
            container = new ContainerBuilder(Image)
                .WithCommand($"{UserName}:{Password}:::{ContainerName}")
                .WithPortBinding(SshPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server listening on"))
                .Build();
            await container.StartAsync();
        }
        catch (Exception ex)
        {
            if (container != null) await container.DisposeAsync();
            Assert.Ignore($"SFTP container could not start (Docker unavailable?): {ex.Message}");
        }

        // The fingerprints of the server's own key files, read inside the container: the channel an administrator gives
        // a client its fingerprint through. The one the server presents to SSH.NET has to be among them.
        var listed = await container.ExecAsync(["sh", "-c", "for key in /etc/ssh/ssh_host_*_key.pub; do ssh-keygen -lf \"$key\"; done"]);
        var fingerprints = Regex.Matches(listed.Stdout, @"SHA256:\S+").Select(match => match.Value).ToArray();
        var presented = "SHA256:" + PresentedFingerprint(container.Hostname, container.GetMappedPublicPort(SshPort));
        Assert.That(fingerprints, Does.Contain(presented), "the host key the server presents is one of its key files");

        return new SftpServer(container, presented);
    }

    /// <summary>Connects only as far as the key exchange and refuses the key, which leaves nothing open.</summary>
    private static string PresentedFingerprint(string host, int port)
    {
        string? presented = null;
        using var probe = new SftpClient(host, port, UserName, Password);
        probe.HostKeyReceived += (_, e) =>
        {
            presented = e.FingerPrintSHA256;
            e.CanTrust = false;
        };
        try
        {
            probe.Connect();
        }
        catch (SshConnectionException)
        {
            // refused on purpose, once the key was seen
        }
        return presented ?? throw new InvalidOperationException("The SFTP server presented no host key.");
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
