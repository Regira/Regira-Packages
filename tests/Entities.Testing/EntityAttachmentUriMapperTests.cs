using Regira.Entities.Attachments;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing;

/// <summary>
/// Pins the DTO↔attachment pairing rules of <see cref="EntityAttachmentUriAfterMapper{TEntity,TEntityAttachment,TEntityAttachmentDto,TEntityAttachmentKey,TObjectKey,TAttachmentKey,TAttachment}"/>:
/// persisted DTOs pair by Id (string-normalized, so a non-int entity key still matches the DTO's int Id),
/// an unmatched real Id gets no Uri, and only default-Id (unsaved) rows pair by position.
/// </summary>
[TestFixture]
public class EntityAttachmentUriMapperTests
{
    private class LongAttachment : EntityAttachment<long, long, long, Attachment<long>>;
    private class LongOwner : IEntity<long>, IHasAttachments<LongAttachment, long, long, long, Attachment<long>>
    {
        public long Id { get; set; }
        public ICollection<LongAttachment>? Attachments { get; set; }
        public bool? HasAttachment { get; set; }
    }
    private class OwnerDto
    {
        public ICollection<EntityAttachmentDto>? Attachments { get; set; }
    }
    private class FakeResolver : IAttachmentUriResolver<LongAttachment>
    {
        public string? Resolve(LongAttachment source) => $"uri/{source.Id}";
    }

    private static EntityAttachmentUriAfterMapper<LongOwner, LongAttachment, EntityAttachmentDto, long, long, long, Attachment<long>> CreateMapper()
        => new(new FakeResolver());

    [Test]
    public void Pairs_By_Id_Across_Key_Types_And_Reordering()
    {
        // entity key is long, DTO Id is int — pairing must survive the type difference and any order
        var source = new LongOwner { Attachments = [new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }] };
        var target = new OwnerDto { Attachments = [new() { Id = 3 }, new() { Id = 1 }, new() { Id = 2 }] };

        CreateMapper().AfterMap(source, target);

        Assert.Multiple(() =>
        {
            foreach (var dto in target.Attachments!)
            {
                Assert.That(dto.Uri, Is.EqualTo($"uri/{dto.Id}"));
            }
        });
    }

    [Test]
    public void Unmatched_Real_Id_Gets_No_Uri()
    {
        var source = new LongOwner { Attachments = [new() { Id = 1 }, new() { Id = 2 }] };
        var target = new OwnerDto { Attachments = [new() { Id = 99 }, new() { Id = 2 }] };

        CreateMapper().AfterMap(source, target);

        Assert.Multiple(() =>
        {
            // a persisted DTO that matches nothing must not borrow another attachment's download Uri
            Assert.That(target.Attachments!.First().Uri, Is.Null);
            Assert.That(target.Attachments!.Last().Uri, Is.EqualTo("uri/2"));
        });
    }

    [Test]
    public void Default_Id_Pairs_By_Position()
    {
        var source = new LongOwner { Attachments = [new() { Id = 1 }, new() { Id = 0 }] };
        var target = new OwnerDto { Attachments = [new() { Id = 1 }, new() { Id = 0 }] };

        CreateMapper().AfterMap(source, target);

        Assert.That(target.Attachments!.Last().Uri, Is.EqualTo("uri/0"));
    }

    [Test]
    public void Mixed_Saved_And_Unsaved_With_Different_Orderings_Do_Not_Cross_Assign()
    {
        // unsaved DTOs pair with the LEFTOVER attachments (those no Id match consumed), not with the
        // attachment at the same list index — differing orderings must not hand out another row's Uri
        var source = new LongOwner { Attachments = [new() { Id = 0 }, new() { Id = 5 }] };
        var target = new OwnerDto { Attachments = [new() { Id = 5 }, new() { Id = 0 }] };

        CreateMapper().AfterMap(source, target);

        Assert.Multiple(() =>
        {
            Assert.That(target.Attachments!.First().Uri, Is.EqualTo("uri/5"));
            Assert.That(target.Attachments!.Last().Uri, Is.EqualTo("uri/0"));
        });
    }

    private class WrongItemDto;
    private class WrongOwnerDto
    {
        public ICollection<WrongItemDto>? Attachments { get; set; }
    }

    [Test]
    public void Non_Derived_Dto_Collection_Fails_Loudly()
    {
        // a silent skip would ship every response with Uri = null and nothing in the logs
        var source = new LongOwner { Attachments = [new() { Id = 1 }] };
        var target = new WrongOwnerDto { Attachments = [new WrongItemDto()] };

        Assert.Throws<InvalidOperationException>(() => CreateMapper().AfterMap(source, target));
    }
}
