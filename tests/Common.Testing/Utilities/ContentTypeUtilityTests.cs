using Regira.IO.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
public class ContentTypeUtilityTests
{
    [TestCase("photo.webp", "image/webp")]
    [TestCase("photo.avif", "image/avif")]
    [TestCase("photo.HEIC", "image/heic")]
    [TestCase("data.json", "application/json")]
    [TestCase("README.md", "text/markdown")]
    [TestCase("clip.webm", "video/webm")]
    [TestCase("font.woff2", "font/woff2")]
    [TestCase("module.mjs", "text/javascript")]
    [TestCase("report.PDF", "application/pdf")]
    [TestCase("archive", "application/octet-stream")]
    [TestCase("archive.unknown", "application/octet-stream")]
    public void GetContentType_Reads_The_Extension_In_Any_Case(string fileName, string expected)
        => Assert.That(ContentTypeUtility.GetContentType(fileName), Is.EqualTo(expected));

    [Test]
    public void The_Map_Is_One_Instance_That_Ignores_Case()
    {
        var mimeTypes = ContentTypeUtility.MimeTypesDictionary;
        var signatures = ContentTypeUtility.MimeTypeByteSequences;

        Assert.Multiple(() =>
        {
            Assert.That(ContentTypeUtility.MimeTypesDictionary, Is.SameAs(mimeTypes));
            Assert.That(mimeTypes.ContainsKey("PNG"), Is.True);
            Assert.That(ContentTypeUtility.MimeTypeByteSequences, Is.SameAs(signatures));
        });
    }

    [Test]
    public void Extend_Changes_What_GetContentType_Reads()
    {
        var extension = $"x{Guid.NewGuid():N}";
        Assert.That(ContentTypeUtility.GetContentType($"file.{extension}"), Is.EqualTo("application/octet-stream"));

        ContentTypeUtility.Extend(new Dictionary<string, string[]> { { extension, ["application/x-test"] } });

        Assert.That(ContentTypeUtility.GetContentType($"file.{extension.ToUpperInvariant()}"), Is.EqualTo("application/x-test"));
    }

    // several extensions share these types: the answer is fixed, not the order the map happens to hold them in
    [TestCase("application/xml", "xml")]
    [TestCase("text/javascript", "js")]
    [TestCase("image/jpeg", "jpg")]
    [TestCase("text/html", "html")]
    [TestCase("image/tiff", "tif")]
    [TestCase("audio/mpeg", "mp3")]
    [TestCase("text/plain", "txt")]
    [TestCase("application/yaml", "yaml")]
    [TestCase("IMAGE/WEBP", "webp")]
    [TestCase("application/x-javascript", "js")]
    [TestCase("image/x-unknown", "x-unknown")]
    public void GetExtension_Answers_One_Extension_Per_Type(string mimeType, string expected)
        => Assert.That(ContentTypeUtility.GetExtension(mimeType), Is.EqualTo(expected));

    [Test]
    public void GetExtension_Prefers_An_Extension_Whose_First_Type_It_Is()
    {
        // listed second by an extension that sorts first, and first by one that sorts later
        var listing = $"a{Guid.NewGuid():N}";
        var owner = $"z{Guid.NewGuid():N}";
        var type = $"application/x-{Guid.NewGuid():N}";
        ContentTypeUtility.Extend(new Dictionary<string, string[]> { { listing, ["application/x-other", type] }, { owner, [type] } });

        Assert.That(ContentTypeUtility.GetExtension(type), Is.EqualTo(owner));
    }

    [Test]
    public void Every_Signature_Has_A_Type()
    {
        // a signature without a MIME type made GetContentType(bytes) throw for a matching file
        var missing = ContentTypeUtility.MimeTypeByteSequences.Keys.Where(x => !ContentTypeUtility.MimeTypesDictionary.ContainsKey(x));
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void GetContentType_Of_Bytes_Reads_The_Signature()
    {
        byte[] rar = [82, 97, 114, 33, 26, 7, 0, 1];
        Assert.That(ContentTypeUtility.GetContentType(rar), Is.EqualTo("application/vnd.rar"));
    }
}
