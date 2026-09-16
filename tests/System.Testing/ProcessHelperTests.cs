using Regira.System.Abstractions;

namespace Regira.System.Testing;

/// <summary>
/// Drives real processes through <see cref="ProcessHelper"/>. <see cref="ProcessHelper.ExecuteCommand"/> runs a
/// batch file, so the fixture is skipped off Windows.
/// </summary>
public class ProcessHelperTests
{
    /// <summary>
    /// Long enough that the flood below passes the largest pipe buffer either platform hands out
    /// </summary>
    private const int FloodLines = 2000;
    private static readonly string Line = new('x', 60);

    [SetUp]
    public void SetUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("ExecuteCommand runs a .bat file, so these tests are Windows-only.");
        }
    }

    [Test]
    public void Captures_A_Process_That_Writes_Past_The_Pipe_Buffer_On_Stderr()
    {
        // reading stdout to the end first leaves this process blocked on its own write, and both sides wait forever
        var result = Run($"for /L %%i in (1,1,{FloodLines}) do @echo {Line} 1>&2");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Length, Is.GreaterThan(64 * 1024), "the whole stderr stream should have been read");
    }

    [Test]
    public void Captures_A_Process_That_Writes_Past_The_Pipe_Buffer_On_Both_Streams()
    {
        var result = Run($"for /L %%i in (1,1,{FloodLines}) do @(echo {Line}& echo {Line} 1>&2)");

        Assert.Multiple(() =>
        {
            Assert.That(result.Output!.Length, Is.GreaterThan(64 * 1024));
            Assert.That(result.Error!.Length, Is.GreaterThan(64 * 1024));
        });
    }

    [Test]
    public void Keeps_Each_Stream_To_Itself_And_Reports_The_Exit_Code()
    {
        var result = Run($"echo to stdout{Environment.NewLine}echo to stderr 1>&2{Environment.NewLine}exit /b 3");

        Assert.Multiple(() =>
        {
            Assert.That(result.Output!.Trim(), Is.EqualTo("to stdout"));
            Assert.That(result.Error!.Trim(), Is.EqualTo("to stderr"));
            Assert.That(result.ExitCode, Is.EqualTo(3));
        });
    }

    [Test]
    public void Leaves_Both_Streams_Empty_Without_WaitForOutput()
    {
        var result = new ProcessHelper().ExecuteCommand($"echo to stdout{Environment.NewLine}exit /b 3");

        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Is.Null);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.ExitCode, Is.EqualTo(3));
        });
    }

    [Test]
    public void Does_Not_Echo_The_Command_Back_Into_The_Captured_Output()
    {
        // the command's own text is where a secret sits — pg_dump is driven by `set PGPASSWORD=...` ahead of it
        var result = Run($"set SOME_SECRET=hunter2{Environment.NewLine}echo the tool ran{Environment.NewLine}set SOME_SECRET=");

        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Does.Not.Contain("hunter2"));
            Assert.That(result.Output, Does.Not.Contain("SOME_SECRET"));
            Assert.That(result.Output!.Trim(), Is.EqualTo("the tool ran"));
        });
    }

    [Test]
    public void Concurrent_Commands_On_One_Instance_Keep_Their_Own_Scripts()
    {
        var folder = Directory.CreateTempSubdirectory("regira-process-").FullName;
        try
        {
            // one instance, as a singleton registration shares it, with a folder the first call has to create; the pause
            // keeps every script running while others end
            var scripts = Path.Combine(folder, "scripts");
            var helper = new ProcessHelper(new ProcessHelper.Options { TempFolder = scripts });
            var runs = Enumerable.Range(0, 8)
                .Select(i => Task.Run(() => helper.ExecuteCommand($"ping -n {1 + i % 3} 127.0.0.1 >nul{Environment.NewLine}echo run {i}", waitForOutput: true)))
                .ToArray();

            Assert.That(Task.WaitAll(runs, TimeSpan.FromSeconds(60)), Is.True);
            Assert.Multiple(() =>
            {
                for (var i = 0; i < runs.Length; i++)
                {
                    Assert.That(runs[i].Result.ExitCode, Is.Zero, runs[i].Result.Error);
                    Assert.That(runs[i].Result.Output!.Trim(), Is.EqualTo($"run {i}"));
                }
                Assert.That(Directory.GetFiles(scripts), Is.Empty, "every script is removed again");
            });
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Bounded, so a helper that stops draining concurrently fails the test instead of hanging the run
    /// </summary>
    private static IProcessOutput Run(string command)
    {
        var run = Task.Run(() => new ProcessHelper().ExecuteCommand(command, waitForOutput: true));

        Assert.That(run.Wait(TimeSpan.FromSeconds(30)), Is.True, "ExecuteCommand never returned — the two pipes are not being drained at the same time.");

        return run.Result;
    }
}
