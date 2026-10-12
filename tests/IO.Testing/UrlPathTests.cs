using Regira.IO.Storage.Utilities;

namespace IO.Testing;

/// <summary>
/// The <c>/</c>-separated paths of the blob, GitHub and SFTP backends, which their <c>FileNameUtility</c> classes forward to.
/// </summary>
[TestFixture]
public class UrlPathTests
{
    [Test]
    public void A_Path_Is_Put_Under_The_Root_Once()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UrlPath.GetUri(@"dir\file.txt", "container"), Is.EqualTo("container/dir/file.txt"));
            Assert.That(UrlPath.GetUri("container/dir/file.txt", "container"), Is.EqualTo("container/dir/file.txt"), "already under the root");
            Assert.That(UrlPath.GetUri("dir/file.txt", "/home/user", rooted: true), Is.EqualTo("/home/user/dir/file.txt"), "an SFTP path");
        });
    }

    [Test]
    public void An_Identifier_Is_The_Path_Below_The_Root()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UrlPath.GetRelativeUri("container/dir/file.txt", "container"), Is.EqualTo("dir/file.txt"));
            Assert.That(UrlPath.GetRelativeUri("container/file.txt", "container"), Is.EqualTo("file.txt"));
            Assert.That(UrlPath.GetRelativeUri("/home/user/dir/file.txt", "/home/user", rooted: true), Is.EqualTo("dir/file.txt"));
            Assert.That(UrlPath.GetRelativeFolder("container/dir/sub/file.txt", "container"), Is.EqualTo("dir/sub"));
            Assert.That(UrlPath.GetRelativeFolder("container/file.txt", "container"), Is.Null);
        });
    }

    [Test]
    public void A_Path_Climbing_Out_Of_The_Root_Is_Refused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UrlPath.EnsureContained("/home/user/dir/../file.txt", "/home/user"), Is.EqualTo("/home/user/file.txt"));
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained("/home/user/../other/file.txt", "/home/user"));
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained("/home/username/file.txt", "/home/user"), "a sibling sharing the root's name");
        });
    }

    [Test]
    public void A_Null_Character_Is_Refused()
    {
        // a server that ends the path there reads "/home/user/.." and leaves the root
        Assert.Multiple(() =>
        {
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained("/home/user/..\0/file.txt", "/home/user"));
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained("/home/user/file\0.txt", "/home/user"));
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/..%00/inv-001.pdf"), new Uri("https://api.github.com/repos/owner/repo/contents/files/")));
        });
    }

    [Test]
    public void An_Address_Leaving_The_Root_Is_Refused()
    {
        var root = new Uri("https://api.github.com/repos/owner/repo/contents/files/");

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/invoices/../inv-001.pdf"), root));
            Assert.DoesNotThrow(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/inv..001.pdf?ref=../../main"), root), "the query is not the path");
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/../other/inv-001.pdf"), root));
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/%2e%2e/other/inv-001.pdf"), root), "a URL reads %2e as a dot");
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/..%2Fother%2Finv-001.pdf"), root), "a server may read %2F as a /");
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files/..%5Cother%5Cinv-001.pdf"), root), "or %5C");
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://api.github.com/repos/owner/repo/contents/files-archive/inv-001.pdf"), root), "a sibling sharing the root's name");
            Assert.Throws<UnauthorizedAccessException>(() => UrlPath.EnsureContained(new Uri("https://example.com/repos/owner/repo/contents/files/inv-001.pdf"), root), "another host");
        });
    }
}
