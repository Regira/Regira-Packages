using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// An attachment's content type follows its file name, whoever writes the row: the type the row holds is the type
/// <c>File()</c> serves, so neither a client's input nor a nested <c>Attachment</c> decides it.
/// </summary>
[TestFixture]
public class AttachmentContentTypeTests
{
    public class Note : IEntity<int>, IHasAttachments, IHasAttachments<NoteAttachment>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<NoteAttachment>? Attachments { get; set; }
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<NoteAttachment>().ToArray();
        }
    }

    public class NoteAttachment : EntityAttachment
    {
        public NoteAttachment() => ObjectType = nameof(Note);
    }

    public class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<NoteAttachment> NoteAttachments => Set<NoteAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Note>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-content-type-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<NotesContext>(db => db.UseSqlite(_connection));
        services.UseEntities<NotesContext>(o => o.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Note>(e => e.HasAttachments(x => x.Attachments));
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotesContext>().Database.EnsureCreatedAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    private async Task<Attachment> Stored(int attachmentId)
    {
        using var scope = _sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotesContext>().Attachments.AsNoTracking().SingleAsync(x => x.Id == attachmentId);
    }

    [Test]
    public async Task A_Nested_Attachment_Is_Typed_By_Its_File_Name()
    {
        int attachmentId;
        using (var scope = _sp.CreateScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<IEntityService<Note, int>>();
            var note = new Note { Title = "Profile" };
            await notes.Add(note);
            await notes.SaveChanges();

            var links = scope.ServiceProvider.GetRequiredService<IEntityService<NoteAttachment, int>>();
            var link = new NoteAttachment
            {
                ObjectId = note.Id,
                Attachment = new Attachment { FileName = "avatar.png", ContentType = "text/html", Bytes = "<script>"u8.ToArray() }
            };
            await links.Add(link);
            await links.SaveChanges();
            attachmentId = link.AttachmentId;
        }

        Assert.That((await Stored(attachmentId)).ContentType, Is.EqualTo("image/png"));
    }

    [Test]
    public async Task An_Update_Of_The_Attachment_Row_Is_Typed_By_Its_File_Name()
    {
        int attachmentId;
        using (var scope = _sp.CreateScope())
        {
            var attachments = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = new Attachment { FileName = "notes.txt", Bytes = "content"u8.ToArray() };
            await attachments.Add(attachment);
            await attachments.SaveChanges();
            attachmentId = attachment.Id;
        }

        using (var scope = _sp.CreateScope())
        {
            var attachments = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            var attachment = (await attachments.Details(attachmentId))!;
            attachment.ContentType = "text/html";
            await attachments.Modify(attachment);
            await attachments.SaveChanges();
        }

        Assert.That((await Stored(attachmentId)).ContentType, Is.EqualTo("text/plain"));
    }
}
