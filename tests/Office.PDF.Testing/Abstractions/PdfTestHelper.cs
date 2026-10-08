using System.Text;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace Office.PDF.Testing.Abstractions;

public static class PdfTestHelper
{
    /// <summary>
    /// Asserts the file hands out a stream a sequential reader can actually consume.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT rewind before reading. That mirrors how FileStreamResult writes a
    /// response body: it advertises Content-Length from Stream.Length but copies from the current
    /// position, so a producer that leaves its stream at the end sends a truncated (usually empty)
    /// body while every Length-based assertion still passes.
    /// <br />Asserting on <c>GetLength()</c> or <c>GetBytes()</c> cannot catch this, because Length
    /// is position-independent and <see cref="FileUtility.GetBytes(Stream?)"/> rewinds internally.
    /// </remarks>
    public static void AssertReadableWithoutRewind(IMemoryFile? file)
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
        // Take at most what is there: a regressed backend returning 1-4 bytes would otherwise throw out of
        // GetString with a framework stack trace instead of failing on the header assertion below.
        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));
        Assert.That(header, Is.EqualTo("%PDF-"), "Content does not start with a PDF header.");

        // GetStream() hands back a rewound copy, so it hides a producer that parked its own stream
        // at the end. Consumers reading file.Stream directly get no such protection.
        if (file.HasStream())
        {
            Assert.That(file.Stream!.Position, Is.Zero,
                "Backing stream is not rewound: anything reading file.Stream directly gets a truncated result.");
        }
    }
}
