namespace Entities.TestApi.Infrastructure.Persons;

public record ReservationDto
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string? Room { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
