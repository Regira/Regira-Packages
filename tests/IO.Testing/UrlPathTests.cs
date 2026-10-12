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
}
