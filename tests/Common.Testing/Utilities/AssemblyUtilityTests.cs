using System.Reflection;
using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AssemblyUtilityTests
{
    [Test]
    public void The_Directory_Of_An_Assembly_Is_The_Folder_Of_Its_File()
    {
        var assembly = typeof(AssemblyUtilityTests).Assembly;

        Assert.That(AssemblyUtility.GetAssemblyDirectory(assembly), Is.EqualTo(Path.GetDirectoryName(assembly.Location)));
    }

    [Test]
    public void Without_An_Assembly_It_Is_The_Folder_Of_Regira_Common()
    {
        var expected = Path.GetDirectoryName(typeof(AssemblyUtility).Assembly.Location);

        Assert.That(AssemblyUtility.GetAssemblyDirectory(), Is.EqualTo(expected));
    }

    [Test]
    public void A_Folder_Name_Holding_Hash_And_Percent_Is_Kept_As_Written()
    {
        var folder = Path.Combine(Path.GetTempPath(), "regira #1 %41%20");
        var assembly = new LocatedAssembly(Path.Combine(folder, "Regira.Sample.dll"));

        Assert.That(AssemblyUtility.GetAssemblyDirectory(assembly), Is.EqualTo(folder));
    }

    [Test]
    [Platform("Win", Reason = "a UNC path is a Windows path; elsewhere '\\' is not a separator")]
    public void A_Network_Share_Keeps_Its_Server()
    {
        var assembly = new LocatedAssembly(@"\\server\share\app\Regira.Sample.dll");

        Assert.That(AssemblyUtility.GetAssemblyDirectory(assembly), Is.EqualTo(@"\\server\share\app"));
    }

    [Test]
    public void An_Assembly_Without_A_File_Answers_The_App_Base_Directory()
    {
        var assembly = Assembly.Load(File.ReadAllBytes(typeof(AssemblyUtility).Assembly.Location));
        Assume.That(assembly.Location, Is.Empty);

        Assert.That(AssemblyUtility.GetAssemblyDirectory(assembly), Is.EqualTo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)));
    }

    private class LocatedAssembly(string location) : Assembly
    {
        public override string Location => location;
    }
}
