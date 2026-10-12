using Regira.System.Abstractions;

namespace Regira.System.Testing;

/// <summary>
/// The default implementations of the <c>environment</c> overloads, for an <see cref="IProcessHelper"/> that does not
/// override them: they cannot reach the process, so given variables they throw rather than write them anywhere.
/// </summary>
public class IProcessHelperDefaultsTests
{
    private sealed class PlainProcessHelper : IProcessHelper
    {
        public List<string> Started { get; } = [];
        public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
        {
            Started.Add(command);
            return new ProcessOutput();
        }
        public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
        {
            Started.Add(filename);
            return new ProcessOutput();
        }
    }

    private static readonly Dictionary<string, string> Password = new() { ["PGPASSWORD"] = "s3cr%t" };

    [Test]
    public void ExecuteCommand_Refuses_Variables_It_Cannot_Set_On_The_Process()
    {
        var helper = new PlainProcessHelper();

        var ex = Assert.Throws<NotSupportedException>(() => ((IProcessHelper)helper).ExecuteCommand("pg_dump", Password));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("PGPASSWORD").And.Not.Contain("s3cr%t"));
            Assert.That(helper.Started, Is.Empty, "nothing ran");
        });
    }

    [Test]
    public void ExecuteFile_Refuses_Variables_It_Cannot_Set_On_The_Process()
    {
        var helper = new PlainProcessHelper();

        var ex = Assert.Throws<NotSupportedException>(() => ((IProcessHelper)helper).ExecuteFile("pg_dump", Password));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("PGPASSWORD").And.Not.Contain("s3cr%t"));
            Assert.That(helper.Started, Is.Empty, "nothing ran");
        });
    }

    [Test]
    public void Without_Variables_The_Overloads_Run_The_Plain_Calls()
    {
        IProcessHelper helper = new PlainProcessHelper();

        helper.ExecuteCommand("dir", new Dictionary<string, string>());
        helper.ExecuteFile("pg_dump", new Dictionary<string, string>());

        Assert.That(((PlainProcessHelper)helper).Started, Is.EqualTo(new[] { "dir", "pg_dump" }));
    }
}
