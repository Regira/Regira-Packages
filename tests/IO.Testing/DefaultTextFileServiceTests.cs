using System.Text;
using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;

namespace IO.Testing;

[TestFixture]
public class DefaultTextFileServiceTests
{
    [Test]
    public async Task Save_Contents_Keeps_Stream_Alive_Until_Inner_Save_Completes()
    {
        const string json = "{\"greeting\":\"Héllo wörld\"}";
        var readGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var binaryService = new DeferredReadFileService(readGate.Task);
        var text = new DefaultTextFileService(binaryService, Encoding.UTF8);

        // The inner Save suspends before touching the stream, as a remote store does on its first network call;
        // the stream is only read once the outer Save has already handed back its task
        var saving = text.Save("config/app.json", json);
        readGate.SetResult();
        var identifier = await saving;

        Assert.That(identifier, Is.EqualTo("config/app.json"));
        Assert.That(Encoding.UTF8.GetString(binaryService.Files["config/app.json"]), Is.EqualTo(json));
    }


    private class DeferredReadFileService(Task beforeRead) : IFileService
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public string Root => string.Empty;

        public async Task<string> Save(string identifier, Stream stream, string? contentType = null)
        {
            await beforeRead;
            stream.Position = 0;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            Files[identifier] = ms.ToArray();
            return identifier;
        }
        public Task<string> Save(string identifier, byte[] bytes, string? contentType = null) => throw new NotSupportedException();

        public Task<bool> Exists(string identifier) => throw new NotSupportedException();
        public Task<byte[]?> GetBytes(string identifier) => throw new NotSupportedException();
        public Task<Stream?> GetStream(string identifier) => throw new NotSupportedException();
        public Task<IEnumerable<string>> List(FileSearchObject? so = null) => throw new NotSupportedException();
#if NET10_0_OR_GREATER
        public IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null) => throw new NotSupportedException();
#endif
        public Task Move(string sourceIdentifier, string targetIdentifier) => throw new NotSupportedException();
        public Task Delete(string identifier) => throw new NotSupportedException();

        public string GetAbsoluteUri(string identifier) => identifier;
        public string GetIdentifier(string uri) => uri;
        public string? GetRelativeFolder(string identifier) => null;
    }
}
