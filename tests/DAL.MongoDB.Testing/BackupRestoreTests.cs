using System.Text;
using System.Text.RegularExpressions;
using Regira.DAL.MongoDB.Core;
using Regira.DAL.MongoDB.Services;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System;
using Regira.System.Abstractions;

namespace DAL.MongoDB.Testing;

/// <summary>
/// Runs mongodump/mongorestore through a stand-in, so what is asserted is the command the services compose —
/// no MongoDB and no Database Tools needed.
/// </summary>
public class BackupRestoreTests
{
    private const string Password = "p@ss'w:rd";

    [Test]
    public void Backup_Keeps_The_Password_Off_The_Command_Line()
    {
        var process = new ToolStandIn { DumpPayload = [1, 2, 3] };

        new MongoBackupService(Options(Settings()), process).Backup().Wait();

        Assert.Multiple(() =>
        {
            Assert.That(process.Arguments, Does.Contain(@"--uri=""mongodb://app@mongo.example.com:27018/shop?authSource=admin"""));
            Assert.That(process.Arguments, Does.Not.Contain(Password));
            Assert.That(process.ConfigFileContents, Is.EqualTo($"password: 'p@ss''w:rd'{Environment.NewLine}"));
        });
    }

    [Test]
    public void Backup_Deletes_The_Password_File_Afterwards()
    {
        var process = new ToolStandIn { DumpPayload = [1, 2, 3] };

        new MongoBackupService(Options(Settings()), process).Backup().Wait();

        Assert.That(process.ConfigPath, Is.Not.Null);
        Assert.That(File.Exists(process.ConfigPath!), Is.False);
    }

    [Test]
    public void Backup_Reports_What_The_Tool_Said_And_Still_Deletes_The_Password_File()
    {
        var process = new ToolStandIn { ExitCode = 1, Stderr = "Failed: could not connect to server: auth error" };
        var service = new MongoBackupService(Options(Settings()), process);

        Assert.That(() => service.Backup().GetAwaiter().GetResult(),
            Throws.Exception.With.Message.Contains("ExitCode 1").And.Message.Contains("auth error"));
        Assert.That(File.Exists(process.ConfigPath!), Is.False);
    }

    [Test]
    public void Backup_Asks_For_The_Tool_Output()
    {
        var process = new ToolStandIn { DumpPayload = [1, 2, 3] };

        new MongoBackupService(Options(Settings()), process).Backup().Wait();

        Assert.That(process.OutputRequested, Is.True);
    }

    [Test]
    public void Restore_Reports_What_The_Tool_Said()
    {
        var process = new ToolStandIn { ExitCode = 1, Stderr = "Failed: no reachable servers" };
        var service = new MongoRestoreService(Options(Settings()), process);

        Assert.That(() => service.Restore(Archive("an archive")).GetAwaiter().GetResult(),
            Throws.Exception.With.Message.Contains("no reachable servers"));
    }

    [Test]
    public void Backup_Passes_No_Config_File_Without_A_Password()
    {
        var settings = new MongoSettings("mongo.example.com", "shop");
        var process = new ToolStandIn { DumpPayload = [1, 2, 3] };

        new MongoBackupService(Options(settings), process).Backup().Wait();

        Assert.Multiple(() =>
        {
            Assert.That(process.Arguments, Does.Not.Contain("--config"));
            Assert.That(process.Arguments, Does.Contain(@"--uri=""mongodb://mongo.example.com:27017/shop"""));
        });
    }

    [Test]
    public void Backup_Rejects_A_Password_Without_A_Username()
    {
        var settings = new MongoSettings("mongo.example.com", "shop", password: Password);
        var service = new MongoBackupService(Options(settings), new ToolStandIn());

        Assert.That(() => service.Backup().GetAwaiter().GetResult(), Throws.ArgumentException.With.Message.Contains("Username"));
    }

    [Test]
    public void Backup_Starts_The_Tool_Itself_Rather_Than_A_Shell()
    {
        var process = new ToolStandIn { DumpPayload = [1, 2, 3] };

        new MongoBackupService(Options(Settings()), process).Backup().Wait();

        Assert.That(process.Filename, Is.EqualTo(Path.Combine(Path.GetTempPath(), OperatingSystem.IsWindows() ? "mongodump.exe" : "mongodump")));
    }

    [Test]
    public void Backup_Returns_What_The_Tool_Wrote()
    {
        var process = new ToolStandIn { DumpPayload = Encoding.UTF8.GetBytes("an archive") };

        var backup = new MongoBackupService(Options(Settings()), process).Backup().Result;

        Assert.That(backup.GetBytes(), Is.EqualTo(Encoding.UTF8.GetBytes("an archive")));
    }

    [Test]
    public void Backup_Deletes_The_Temporary_Dump_Afterwards()
    {
        var process = new ToolStandIn { DumpPayload = [1, 2, 3] };

        new MongoBackupService(Options(Settings()), process).Backup().Wait();

        Assert.That(process.ArchivePath, Is.Not.Null);
        Assert.That(File.Exists(process.ArchivePath!), Is.False, "the dump was left behind in the temp folder");
    }

    [Test]
    public async Task Restore_Deletes_The_Temporary_Archive_Afterwards()
    {
        var process = new ToolStandIn();

        await new MongoRestoreService(Options(Settings()), process).Restore(Archive("an archive"));

        Assert.That(process.ArchivePath, Is.Not.Null);
        Assert.That(File.Exists(process.ArchivePath!), Is.False, "the archive was left behind in the temp folder");
    }

    [Test]
    public async Task Restore_Keeps_The_Password_Off_The_Command_Line()
    {
        var process = new ToolStandIn();

        await new MongoRestoreService(Options(Settings()), process).Restore(Archive("an archive"));

        Assert.Multiple(() =>
        {
            Assert.That(process.Arguments, Does.Contain(@"--uri=""mongodb://app@mongo.example.com:27018/shop?authSource=admin"""));
            Assert.That(process.Arguments, Does.Not.Contain(Password));
            Assert.That(process.ConfigFileContents, Is.EqualTo($"password: 'p@ss''w:rd'{Environment.NewLine}"));
        });
    }

    [Test]
    public async Task Restore_Hands_The_Tool_A_Complete_Archive()
    {
        var process = new ToolStandIn();

        await new MongoRestoreService(Options(Settings()), process).Restore(Archive("an archive"));

        Assert.That(process.ArchiveRead, Is.EqualTo(Encoding.UTF8.GetBytes("an archive")));
    }

    [Test]
    public void Restore_Deletes_The_Password_File_Afterwards()
    {
        var process = new ToolStandIn();

        new MongoRestoreService(Options(Settings()), process).Restore(Archive("an archive")).Wait();

        Assert.That(process.ConfigPath, Is.Not.Null);
        Assert.That(File.Exists(process.ConfigPath!), Is.False);
    }


    private static MongoSettings Settings() => new("mongo.example.com", "shop", "27018", "app", Password) { AuthenticationDatabase = "admin" };
    private static MongoOptions Options(MongoSettings settings) => new() { DbSettings = settings, ToolsDirectory = Path.GetTempPath() };
    private static IMemoryFile Archive(string content) => Encoding.UTF8.GetBytes(content).ToMemoryFile();

    /// <summary>
    /// Stands in for mongodump/mongorestore: records how it was started, reads the files the arguments point at,
    /// and writes <see cref="DumpPayload"/> to the archive the way mongodump would.
    /// </summary>
    private sealed class ToolStandIn : IProcessHelper
    {
        public string? Filename { get; private set; }
        public string? Arguments { get; private set; }
        public bool OutputRequested { get; private set; }
        public string? Stderr { get; init; }
        public string? ConfigPath { get; private set; }
        public string? ConfigFileContents { get; private set; }
        public byte[]? ArchiveRead { get; private set; }
        public string? ArchivePath { get; private set; }
        public byte[]? DumpPayload { get; init; }
        public int ExitCode { get; init; }

        public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
            => throw new NotSupportedException("A command goes through a batch file on disk, which no argument holding a password or a percent sign may reach.");

        public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
        {
            Filename = filename;
            Arguments = arguments;
            OutputRequested = waitForOutput;

            ConfigPath = ValueOf(arguments, "--config");
            if (ConfigPath != null)
            {
                ConfigFileContents = File.ReadAllText(ConfigPath);
            }

            var archivePath = ValueOf(arguments, "--archive");
            ArchivePath = archivePath;
            if (archivePath != null)
            {
                ArchiveRead = File.ReadAllBytes(archivePath);
                if (DumpPayload != null)
                {
                    File.WriteAllBytes(archivePath, DumpPayload);
                }
            }

            return new ProcessOutput { ExitCode = ExitCode, Error = waitForOutput ? Stderr : null };
        }

        private static string? ValueOf(string? arguments, string option)
        {
            var match = Regex.Match(arguments ?? string.Empty, $@"{Regex.Escape(option)}=""([^""]*)""");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
