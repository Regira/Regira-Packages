using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// The attachment primer stores the content given to an attachment as the save is primed, and a save that fails removes
/// the file it wrote, wherever it fails. The bytes <c>Details</c> loaded are not new content, so a metadata edit leaves
/// the stored file where it is. A replaced or deleted attachment's file is removed after the commit where an
/// <see cref="AttachmentFileReactor{TAttachment, TKey}"/> will run for it, and by the primer where none will.
/// </summary>
[TestFixture]
public class AttachmentPrimerTests
{
    public class FileContext(DbContextOptions<FileContext> options) : DbContext(options)
    {
        public DbSet<Attachment> Attachments => Set<Attachment>();
    }

    /// <summary>A file system store that records what it saves, and fails the save it is told to.</summary>
    public sealed class RecordingFileService(string root) : IFileService
    {
        private readonly BinaryFileService _inner = new(new FileSystemOptions { RootFolder = root });

        public List<(string Identifier, string? ContentType)> Saves { get; } = [];
        /// <summary>The 1-based number of the save that throws; <c>null</c> = none.</summary>
        public int? FailSave { get; set; }

        public string Root => _inner.Root;
        public Task<bool> Exists(string identifier) => _inner.Exists(identifier);
        public Task<byte[]?> GetBytes(string identifier) => _inner.GetBytes(identifier);
        public Task<Stream?> GetStream(string identifier) => _inner.GetStream(identifier);
        public Task<IEnumerable<string>> List(FileSearchObject? so = null) => _inner.List(so);
#if NET10_0_OR_GREATER
        public IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null) => _inner.ListAsync(so);
#endif
        public Task Move(string sourceIdentifier, string targetIdentifier) => _inner.Move(sourceIdentifier, targetIdentifier);
        public Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
            => Record(identifier, contentType, () => _inner.Save(identifier, bytes, contentType));
        public Task<string> Save(string identifier, Stream stream, string? contentType = null)
            => Record(identifier, contentType, () => _inner.Save(identifier, stream, contentType));
        public Task Delete(string identifier) => _inner.Delete(identifier);
        public string GetAbsoluteUri(string identifier) => _inner.GetAbsoluteUri(identifier);
        public string GetIdentifier(string uri) => _inner.GetIdentifier(uri);
        public string? GetRelativeFolder(string identifier) => _inner.GetRelativeFolder(identifier);

        private Task<string> Record(string identifier, string? contentType, Func<Task<string>> save)
        {
            Saves.Add((identifier, contentType));
            if (Saves.Count == FailSave)
            {
                throw new IOException("The store refused the upload");
            }
            return save();
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;
    private RecordingFileService _store = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-primer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _store = new RecordingFileService(_root);
    }

    [TearDown]
    public void TearDown()
    {
        _sp?.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    /// <summary>Answers every save without writing a row, as an interceptor may.</summary>
    private sealed class SuppressingInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    private void Build(DbContextWiring? wiring = null, Action<IServiceCollection>? afterwards = null, IInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FileContext>(db =>
        {
            db.UseSqlite(_connection);
            if (interceptor != null)
            {
                db.AddInterceptors(interceptor);
            }
        });
        services.UseEntities<FileContext>(o =>
            {
                if (wiring == null)
                {
                    o.UseDefaults();
                }
                else
                {
                    o.WireDbContext(wiring.Value);
                }
            })
            .WithAttachments(_ => _store);
        afterwards?.Invoke(services);
        _sp = services.BuildServiceProvider();
        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<FileContext>().Database.EnsureCreated();
    }

    private string[] StoredFiles() => Directory.GetFiles(_root, "*", SearchOption.AllDirectories);

    private async Task<Attachment> AddAttachment(string fileName, string content)
    {
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
        var attachment = new Attachment { FileName = fileName, Bytes = System.Text.Encoding.UTF8.GetBytes(content) };
        await service.Add(attachment);
        await service.SaveChanges();
        return attachment;
    }

    private Attachment Stored(int id)
    {
        using var scope = _sp.CreateScope();
        return scope.ServiceProvider.GetRequiredService<FileContext>().Attachments.AsNoTracking().Single(x => x.Id == id);
    }

    // --- A failed save removes the file it wrote ---

    // the second upload fails inside the primer pass, before EF's own save begins: the first one's file goes too
    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Save_Failing_In_The_Primer_Pass_Removes_The_File_It_Wrote(bool async)
    {
        Build();
        _store.FailSave = 2;
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileContext>();
        db.Attachments.Add(new Attachment { FileName = "first.txt", Bytes = [1] });
        db.Attachments.Add(new Attachment { FileName = "second.txt", Bytes = [2] });

        if (async)
        {
            await Assert.ThrowsAsync<IOException>(() => db.SaveChangesAsync());
        }
        else
        {
            Assert.Throws<IOException>(() => db.SaveChanges());
        }

        Assert.That(StoredFiles(), Is.Empty);
    }

    // a save the database refuses removes the file it wrote and hands the row its stored path back, so a retry stores the
    // content again under a key of its own and the replaced file is the stored one
    [Test]
    public async Task A_Retried_Save_After_A_Refused_One_Leaves_Only_The_File_It_Stored()
    {
        Build();
        var stored = await AddAttachment("report.txt", "first version");
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileContext>();
        var table = db.Model.FindEntityType(typeof(Attachment))!.GetTableName();
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE TRIGGER refuse BEFORE UPDATE ON \"{table}\" WHEN NEW.FileName = 'refused.txt' BEGIN SELECT RAISE(ABORT, 'refused'); END;");
        var attachment = db.Attachments.Single(x => x.Id == stored.Id);
        attachment.FileName = "refused.txt";
        attachment.Bytes = "second version"u8.ToArray();

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.That(attachment.Path, Is.EqualTo(stored.Path));
        Assert.That(attachment.Length, Is.EqualTo(stored.Length));
        Assert.That(StoredFiles(), Has.Length.EqualTo(1));

        attachment.FileName = "report.txt";
        await db.SaveChangesAsync();

        Assert.That(StoredFiles(), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(StoredFiles()[0]), Is.EqualTo("second version"));
    }

    // --- The bytes Details loaded are not new content ---

    // a copy added with the loaded bytes is a new row: it gets a file of its own
    [Test]
    public async Task A_Copy_Added_With_The_Loaded_Bytes_Gets_A_File_Of_Its_Own()
    {
        Build();
        var stored = await AddAttachment("report.txt", "content");

        int copyId;
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var copy = (await service.Details(stored.Id))!;
            copy.Id = 0;
            await service.Add(copy);
            await service.SaveChanges();
            copyId = copy.Id;
        }

        Assert.That(Stored(copyId).Path, Is.Not.EqualTo(stored.Path));
        Assert.That(StoredFiles(), Has.Length.EqualTo(2));
    }

    [Test]
    public async Task A_Metadata_Edit_After_Details_Leaves_The_File_Where_It_Is()
    {
        Build();
        var stored = await AddAttachment("report.txt", "content");
        var savesBefore = _store.Saves.Count;

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = (await service.Details(stored.Id))!;
            Assert.That(attachment.Bytes, Is.Not.Null, "Details loads the bytes for this path");
            attachment.FileName = "renamed.txt";
            await service.Modify(attachment);
            await service.SaveChanges();
        }

        var after = Stored(stored.Id);
        Assert.That(_store.Saves, Has.Count.EqualTo(savesBefore));
        Assert.That(after.Path, Is.EqualTo(stored.Path));
        Assert.That(after.FileName, Is.EqualTo("renamed.txt"));
        Assert.That(StoredFiles(), Has.Length.EqualTo(1));
    }

    // bytes given in place of the loaded ones are new content, and replace the file
    [Test]
    public async Task New_Bytes_After_Details_Replace_The_File()
    {
        Build();
        var stored = await AddAttachment("report.txt", "first version");

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = (await service.Details(stored.Id))!;
            attachment.Bytes = "second version"u8.ToArray();
            await service.Modify(attachment);
            await service.SaveChanges();
        }

        Assert.That(Stored(stored.Id).Path, Is.Not.EqualTo(stored.Path));
        Assert.That(StoredFiles(), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(StoredFiles()[0]), Is.EqualTo("second version"));
    }

    // --- New content: its length and its type ---

    [Test]
    public async Task New_Content_From_A_Stream_Sets_The_Length()
    {
        Build();
        var stored = await AddAttachment("report.txt", "short");

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = (await service.Details(stored.Id))!;
            attachment.Stream = new MemoryStream(new byte[500]);
            await service.Modify(attachment);
            await service.SaveChanges();
        }

        Assert.That(Stored(stored.Id).Length, Is.EqualTo(500));
    }

    // new bytes under a new name go under that name's extension, and are stored with its type
    [Test]
    public async Task New_Bytes_Under_A_Name_Of_Another_Type_Take_Its_Extension()
    {
        Build();
        var stored = await AddAttachment("report.txt", "plain");

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FileContext>();
            var attachment = db.Attachments.Single(x => x.Id == stored.Id);
            attachment.FileName = "report.csv";
            attachment.Bytes = "a,b"u8.ToArray();
            await db.SaveChangesAsync();
        }

        var after = Stored(stored.Id);
        Assert.That(after.Path, Does.EndWith(".csv"));
        Assert.That(after.ContentType, Is.EqualTo("text/csv"));
        Assert.That(_store.Saves[^1].ContentType, Is.EqualTo("text/csv"));
    }

    // --- Who removes the replaced and the deleted file ---

    [Test]
    public async Task Without_The_Reactor_Wiring_New_Bytes_Remove_The_Replaced_File()
    {
        Build(DbContextWiring.PrimerInterceptors);
        var stored = await AddAttachment("report.txt", "first version");

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = (await service.Details(stored.Id))!;
            attachment.Bytes = "second version"u8.ToArray();
            await service.Modify(attachment);
            await service.SaveChanges();
        }

        Assert.That(StoredFiles(), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(StoredFiles()[0]), Is.EqualTo("second version"));
    }

    // a save that wrote no row replaced no file: one an interceptor suppressed, or a later save after the primed change
    // was dropped, leaves the stored file in place
    [Test]
    public async Task A_Suppressed_Save_Keeps_The_Stored_File()
    {
        Build(DbContextWiring.PrimerInterceptors);
        var stored = await AddAttachment("report.txt", "first version");
        _sp.Dispose();
        Build(DbContextWiring.PrimerInterceptors, interceptor: new SuppressingInterceptor());

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = (await service.Details(stored.Id))!;
            attachment.Bytes = "second version"u8.ToArray();
            await service.Modify(attachment);
            await service.SaveChanges();
        }

        Assert.That(File.ReadAllText(Path.Combine(_root, Stored(stored.Id).Path!)), Is.EqualTo("first version"));
    }

    [Test]
    public async Task A_Save_After_Explicitly_Primed_Changes_Were_Dropped_Keeps_The_Stored_File()
    {
        Build(DbContextWiring.PrimerInterceptors);
        var stored = await AddAttachment("report.txt", "first version");
        _sp.Dispose();
        // primed only where ApplyPrimers() is called
        Build(DbContextWiring.None, services => services.RegisterPrimerContainer<FileContext>());

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FileContext>();
            var attachment = db.Attachments.Single(x => x.Id == stored.Id);
            attachment.Bytes = "second version"u8.ToArray();
            db.Entry(attachment).State = EntityState.Modified;
            await scope.ServiceProvider.GetRequiredService<EntityPrimerContainer>().ApplyPrimers();
            // the caller decides not to save these changes
            db.ChangeTracker.Clear();

            db.Attachments.Single(x => x.Id == stored.Id).FileName = "renamed.txt";
            await db.SaveChangesAsync();
        }

        Assert.That(File.ReadAllText(Path.Combine(_root, Stored(stored.Id).Path!)), Is.EqualTo("first version"));
    }

    // the reactor wiring is there, but no AttachmentFileReactor will run for the attachment: the primer removes the file
    [Test]
    public async Task A_Deleted_Attachment_Without_An_AttachmentFileReactor_Has_Its_File_Removed()
    {
        Build(afterwards: services =>
        {
            foreach (var reactor in services.Where(d => typeof(AttachmentFileReactor<Attachment, int>).IsAssignableFrom(d.ImplementationType)).ToArray())
            {
                services.Remove(reactor);
            }
        });
        var stored = await AddAttachment("report.txt", "content");

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            await service.Remove((await service.Details(stored.Id))!);
            await service.SaveChanges();
        }

        Assert.That(StoredFiles(), Is.Empty);
    }

    // the startup warning about a context without the reactor wiring names the attachment files, also for the
    // non-generic AttachmentFileReactor the default attachment registers
    [Test]
    public async Task Startup_Validation_Names_The_Attachment_Files_Without_The_Reactor_Wiring()
    {
        var logs = new StartupValidationTests.CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddDbContext<FileContext>(db => db.UseSqlite(_connection));
        services.UseEntities<FileContext>(o => o.WireDbContext(DbContextWiring.PrimerInterceptors).ConfigureValidation(v => v.Enabled = true))
            .WithAttachments(_ => _store);
        _sp = services.BuildServiceProvider();

        await StartupValidationTests.RunHostedServices(_sp);

        Assert.That(logs.Warnings, Has.Some.Contains("no reactor interceptor").And.Contains("AttachmentFileReactor"));
    }

    [Test]
    public async Task A_Deleted_Attachment_Has_Its_File_Removed_After_The_Commit()
    {
        Build();
        var stored = await AddAttachment("report.txt", "content");

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            await service.Remove((await service.Details(stored.Id))!);
            await service.SaveChanges();
        }

        Assert.That(StoredFiles(), Is.Empty);
    }
}
