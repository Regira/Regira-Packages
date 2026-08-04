using Microsoft.Extensions.DependencyInjection;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

[TestFixture]
public class NetworkShareTests
{
    // --- FileNameUtility.GetUncShareRoot ---

    [TestCase(@"\\server\share", @"\\server\share")]
    [TestCase(@"\\server\share\", @"\\server\share")]
    [TestCase(@"\\server\share\sub\folder", @"\\server\share")]
    [TestCase("//server/share/sub/folder", @"\\server\share")]
    [TestCase(@"\\?\UNC\server\share\sub", @"\\server\share")]
    public void GetUncShareRoot_Unc_Paths(string input, string expected)
        => Assert.That(FileNameUtility.GetUncShareRoot(input), Is.EqualTo(expected));

    [TestCase(@"C:\data\storage")]
    [TestCase("/var/app/storage")]
    [TestCase(@"\\server")]
    [TestCase(@"\\.\pipe\name")]
    [TestCase("")]
    [TestCase(null)]
    public void GetUncShareRoot_Non_Unc_Returns_Null(string? input)
        => Assert.That(FileNameUtility.GetUncShareRoot(input), Is.Null);

    // --- NetworkShareCommunicator ---

    [Test]
    public void Communicator_Requires_UserName()
    {
        var options = new NetworkFileSystemOptions { RootFolder = @"\\server\share\folder" };
        Assert.Throws<ArgumentException>(() => _ = new NetworkShareCommunicator(options));
    }

    [Test]
    public void Communicator_Requires_Unc_RootFolder()
    {
        var options = new NetworkFileSystemOptions { RootFolder = @"C:\data\storage", UserName = "user" };
        Assert.Throws<ArgumentException>(() => _ = new NetworkShareCommunicator(options));
    }

    [Test]
    public void Communicator_Derives_ShareRoot_From_RootFolder()
    {
        var communicator = new NetworkShareCommunicator(new NetworkFileSystemOptions
        {
            RootFolder = @"\\server\share\sub\folder",
            UserName = "user",
            Password = "secret"
        });
        Assert.That(communicator.ShareRoot, Is.EqualTo(@"\\server\share"));
        Assert.That(communicator.IsConnected, Is.False);
    }

    [Test]
    public void Communicator_Dispose_Without_Connection_Does_Not_Throw()
    {
        var communicator = new NetworkShareCommunicator(new NetworkFileSystemOptions
        {
            RootFolder = @"\\server\share",
            UserName = "user"
        });
        Assert.DoesNotThrow(communicator.Dispose);
    }

    // --- NetworkFileService ---

    [Test]
    public void FileService_Uses_Communicator_Options()
    {
        using var communicator = new NetworkShareCommunicator(new NetworkFileSystemOptions
        {
            RootFolder = @"\\server\share\folder",
            UserName = "user",
            Password = "secret"
        });
        var svc = new NetworkFileService(communicator);
        Assert.That(svc.Root, Is.EqualTo(@"\\server\share\folder"));
    }

    [Test]
    public void FileService_Uri_Helpers_Do_Not_Connect()
    {
        using var communicator = new NetworkShareCommunicator(new NetworkFileSystemOptions
        {
            RootFolder = @"\\server\share\folder",
            UserName = "user"
        });
        var svc = new NetworkFileService(communicator);
        Assert.That(svc.GetIdentifier(@"\\server\share\folder\sub\file.txt"), Is.EqualTo("sub/file.txt"));
        Assert.That(communicator.IsConnected, Is.False);
    }

    // --- DI registration ---

    [Test]
    public void Di_Resolves_BinaryFileService_When_Communicator_Also_Registered()
    {
        // a second public ctor on BinaryFileService would make this ambiguous and throw
        var services = new ServiceCollection();
        services.AddSingleton(new FileSystemOptions { RootFolder = @"C:\data\storage" });
        services.AddSingleton(new NetworkShareCommunicator(new NetworkFileSystemOptions
        {
            RootFolder = @"\\server\share\folder",
            UserName = "user"
        }));
        services.AddSingleton<IFileService, BinaryFileService>();

        var svc = services.BuildServiceProvider().GetRequiredService<IFileService>();
        Assert.That(svc.Root, Is.EqualTo(@"C:\data\storage"));
    }

    [Test]
    public void Di_Resolves_NetworkFileService_From_Communicator()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NetworkFileSystemOptions
        {
            RootFolder = @"\\server\share\folder",
            UserName = "user"
        });
        services.AddSingleton<NetworkShareCommunicator>();
        services.AddSingleton<IFileService, NetworkFileService>();

        var svc = services.BuildServiceProvider().GetRequiredService<IFileService>();
        Assert.That(svc.Root, Is.EqualTo(@"\\server\share\folder"));
    }
}
