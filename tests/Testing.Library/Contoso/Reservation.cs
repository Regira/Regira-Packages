using Regira.Entities.Attributes;
using Regira.Entities.Models.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Testing.Library.Contoso;

/// <summary>
/// A person's room reservation, owned through <see cref="Person.Reservations"/>. Its token is a required stamp:
/// a client updating one must send back the value it read, or the write is refused as input.
/// </summary>
public class Reservation : IEntityWithSerial, IHasConcurrencyToken
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    [MaxLength(64)]
    public string? Room { get; set; }

    [VersionStamp(Required = true)]
    public Guid ConcurrencyToken { get; set; }

    public Person? Person { get; set; }
}
