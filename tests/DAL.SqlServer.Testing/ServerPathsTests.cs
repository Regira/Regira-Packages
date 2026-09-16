using Regira.DAL.SqlServer.Services;

namespace DAL.SqlServer.Testing;

public class ServerPathsTests
{
    [TestCase(@"D:\SqlBackups", @"D:\SqlBackups\shop.bak")]
    [TestCase(@"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\", @"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\shop.bak")]
    [TestCase(@"\\db01\SqlBackups", @"\\db01\SqlBackups\shop.bak")]
    [TestCase("/var/opt/mssql/backup", "/var/opt/mssql/backup/shop.bak")]
    [TestCase("/var/opt/mssql/data/", "/var/opt/mssql/data/shop.bak")]
    public void Combine_Uses_The_Separator_Of_The_Server_Directory(string directory, string expected)
        => Assert.That(ServerPaths.Combine(directory, "shop.bak"), Is.EqualTo(expected));

    [TestCase(@"C:\DATA\shop.mdf", @"C:\DATA")]
    [TestCase("/var/opt/mssql/data/shop.mdf", "/var/opt/mssql/data")]
    [TestCase("shop.mdf", null)]
    public void GetDirectory_Reads_Either_Separator(string path, string? expected)
        => Assert.That(ServerPaths.GetDirectory(path), Is.EqualTo(expected));

    [TestCase(@"C:\DATA\shop.mdf", ".mdf")]
    [TestCase("/var/opt/mssql/data/shop_log.ldf", ".ldf")]
    [TestCase(@"C:\DATA\shop_fs", "")]
    [TestCase(@"C:\my.data\shop_fs", "")]
    public void GetExtension_Reads_Only_The_File_Name(string path, string expected)
        => Assert.That(ServerPaths.GetExtension(path), Is.EqualTo(expected));

    [Test]
    public void ToFileName_Replaces_What_A_File_Name_Cannot_Hold()
        => Assert.That(ServerPaths.ToFileName("shop:eu/2026"), Is.EqualTo("shop_eu_2026"));
}
