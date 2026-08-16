using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace IO.Testing.Helpers;

public static class StreamAssert
{
    // "PK\3\4" - the zip local-file header
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// Asserts the file hands out a stream a sequential reader can actually consume.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT rewind before reading. That mirrors how FileStreamResult writes a
    /// response body: it advertises Content-Length from Stream.Length but copies from the current
    /// position, so a producer that leaves its stream at the end sends a truncated (usually empty)
    /// body while every Length-based assertion still passes.
    /// <br />Asserting on <c>GetLength()</c> or <c>GetBytes()</c> cannot catch this, because Length
    /// is position-independent and <see cref="Regira.IO.Utilities.FileUtility.GetBytes(Stream?)"/> rewinds internally.
    /// </remarks>
    public static void AssertReadableWithoutRewind(IMemoryFile? file)
        => AssertReadableWithoutRewind(file, ZipHeader);
    /// <param name="file">The file whose streams are asserted.</param>
    /// <param name="expectedHeader">
    /// The magic bytes the content must start with; pass <c>null</c> for files with arbitrary
    /// content (e.g. unzipped entry payloads) to skip the header check.
    /// </param>
    public static void AssertReadableWithoutRewind(IMemoryFile? file, byte[]? expectedHeader)
    {
        Assert.That(file, Is.Not.Null);

        using var stream = file!.GetStream();
        Assert.That(stream, Is.Not.Null);

        var advertisedLength = stream!.Length;
        Assert.That(advertisedLength, Is.GreaterThan(0), "Stream advertises no content.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        Assert.That(bytes.Length, Is.EqualTo(advertisedLength),
            $"Stream was not rewound: a sequential reader gets {bytes.Length} bytes while Length advertises {advertisedLength}. Over HTTP this truncates the response body.");
        if (expectedHeader != null)
        {
            // Take at most what is there: a regressed producer returning a few bytes should fail on
            // the header assertion, not throw out of the slice.
            var header = bytes.Take(expectedHeader.Length).ToArray();
            Assert.That(header, Is.EqualTo(expectedHeader).AsCollection, "Content does not start with the expected header.");
        }

        // GetStream() hands back a rewound copy, so it hides a producer that parked its own stream
        // at the end. Consumers reading file.Stream directly get no such protection.
        if (file.HasStream())
        {
            Assert.That(file.Stream!.Position, Is.Zero,
                "Backing stream is not rewound: anything reading file.Stream directly gets a truncated result.");
        }
    }
}
