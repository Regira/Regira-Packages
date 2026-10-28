using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;

namespace IO.Testing;

/// <summary>
/// <see cref="IFileService.GetInfo"/> as an <see cref="IFileService"/> of an app's own gets it when it implements only the
/// rest: measured from <see cref="IFileService.GetStream"/>, with no last-modified time.
/// </summary>
[TestFixture]
public class DefaultGetInfoTests
{
    // a store whose stream knows no length, and whose GetBytes copies it into memory, as remote stores do: measuring
    // through GetBytes held a 64 MB file in memory, more than twice over while the copy grew
    [Test]
    public async Task A_Stream_That_Knows_No_Length_Is_Measured_Without_Holding_It()
    {
        const long size = 64 * 1024 * 1024;
        var files = new Dictionary<string, long> { ["big.bin"] = size, ["empty.txt"] = 0 };
        IFileService store = new StreamOnlyFileService(identifier => files.TryGetValue(identifier, out var length) ? new ForwardOnlyStream(length) : null);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var info = await store.GetInfo("big.bin");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(info, Is.EqualTo(new StoredFileInfo(size, null)));
        Assert.That(allocated, Is.LessThan(size / 16));
        Assert.That(await store.GetInfo("empty.txt"), Is.EqualTo(new StoredFileInfo(0, null)));
        Assert.That(await store.GetInfo("missing.txt"), Is.Null);
    }

    // a stream that knows its length answers it, unread, and the stream is closed afterwards either way
    [Test]
    public async Task A_Stream_That_Knows_Its_Length_Answers_It_And_Is_Disposed()
    {
        var stream = new DisposeRecordingStream(new byte[12345]);
        IFileService store = new StreamOnlyFileService(identifier => identifier == "report.pdf" ? stream : null);

        var info = await store.GetInfo("report.pdf");

        Assert.That(info, Is.EqualTo(new StoredFileInfo(12345, null)));
        Assert.That(stream.Disposed, Is.True);
    }

    private class StreamOnlyFileService(Func<string, Stream?> open) : IFileService
    {
        public string Root => string.Empty;

        public Task<Stream?> GetStream(string identifier) => Task.FromResult(open(identifier));
        public async Task<byte[]?> GetBytes(string identifier)
        {
            await using var stream = await GetStream(identifier);
            if (stream == null)
            {
                return null;
            }
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            return copy.ToArray();
        }
        public Task<bool> Exists(string identifier) => throw new NotSupportedException();

        public Task<IEnumerable<string>> List(FileSearchObject? so = null) => throw new NotSupportedException();
#if NET10_0_OR_GREATER
        public IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null) => throw new NotSupportedException();
#endif
        public Task Move(string sourceIdentifier, string targetIdentifier) => throw new NotSupportedException();
        public Task<string> Save(string identifier, byte[] bytes, string? contentType = null) => throw new NotSupportedException();
        public Task<string> Save(string identifier, Stream stream, string? contentType = null) => throw new NotSupportedException();
        public Task Delete(string identifier) => throw new NotSupportedException();

        public string GetAbsoluteUri(string identifier) => identifier;
        public string GetIdentifier(string uri) => uri;
        public string? GetRelativeFolder(string identifier) => null;
    }

    private sealed class DisposeRecordingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    // zeros, read once from the start; the reads complete at once, so the whole measurement stays on the test's thread
    private class ForwardOnlyStream(long length) : Stream
    {
        private long _position;

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var read = (int)Math.Min(buffer.Length, length - _position);
            buffer[..read].Clear();
            _position += read;
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer, offset, count));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(Read(buffer.Span));

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
