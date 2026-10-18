using Regira.IO.Storage.SSH;

namespace IO.Testing.SSH;

// The root is within itself, so containment lets an identifier that resolves to it through; a folder's delete would take
// everything below it. Refused before the service connects, so no server is needed.
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SftpRootTests
{
    [TestCase("files", true, "")]
    [TestCase("files", true, ".")]
    [TestCase("files", true, "x/..")]
    [TestCase("files", false, "x/..")]
    [TestCase("/", true, "")]
    [TestCase("/", true, "x/..")]
    public async Task Delete_Of_The_Root_Is_Refused(string container, bool contained, string identifier)
    {
        using var communicator = new SftpCommunicator(new SftpConfig { Host = "localhost", UserName = "user", ContainerName = container, Contained = contained });
        var service = new SftpService(communicator);

        await Assert.ThrowsAsync<ArgumentException>(() => service.Delete(identifier));
    }
}
