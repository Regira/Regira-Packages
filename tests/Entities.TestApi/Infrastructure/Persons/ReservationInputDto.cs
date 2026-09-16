using System.ComponentModel.DataAnnotations;

namespace Entities.TestApi.Infrastructure.Persons;

public record ReservationInputDto
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string? Room { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
