using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;
using System.ComponentModel.DataAnnotations;

namespace Testing.Library.Contoso;

public class Person : IEntityWithSerial, IHasAggregateKey, IHasNormalizedTitle, IHasAttachments, IHasAttachments<PersonAttachment>, IHasNormalizedContent
{
    public int Id { get; set; }
    public Guid? AggregateKey { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(32)]
    public string? GivenName { get; set; }
    [MaxLength(32)]
    [Normalized(SourceProperty = nameof(GivenName))]
    public string? NormalizedGivenName { get; set; }

    [Required]
    [MaxLength(64)]
    public string? LastName { get; set; }
    [MaxLength(64)]
    [Normalized(SourceProperty = nameof(LastName))]
    public string? NormalizedLastName { get; set; }

    public string Title => $"{GivenName} {LastName}".Trim();
    [MaxLength(64)]
    [Normalized(SourceProperties = [nameof(LastName), nameof(GivenName)])]
    public string? NormalizedTitle { get; set; }

    [MaxLength(1024)]
    public string? Description { get; set; }


    [MaxLength(32)]
    public string? Phone { get; set; }
    [MaxLength(32)]
    [Normalized(SourceProperty = nameof(Phone))]
    public string? NormalizedPhone { get; set; }
    [MaxLength(256)]
    [Normalized]
    public string? Email { get; set; }

    public int? SupervisorId { get; set; }
    public Person? Supervisor { get; set; }
    public ICollection<Person>? Subordinates { get; set; }
    public ICollection<Department>? Departments { get; set; }

    public bool? HasAttachment { get; set; }
    public ICollection<PersonAttachment>? Attachments { get; set; }
    ICollection<IEntityAttachment>? IHasAttachments.Attachments
    {
        get => Attachments?.Cast<IEntityAttachment>().ToArray();
        set => Attachments = value?.Cast<PersonAttachment>().ToArray();
    }

    [MaxLength(1024)]
    [Normalized(SourceProperties = [nameof(GivenName), nameof(LastName), nameof(Description), nameof(Phone), nameof(Email)])]
    public string? NormalizedContent { get; set; }
}

public class PersonAttachment : EntityAttachment
{
    public PersonAttachment()
    {
        ObjectType = nameof(Person);
    }
}