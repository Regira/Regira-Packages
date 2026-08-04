using System.ComponentModel.DataAnnotations;

namespace Entities.TestApi.Infrastructure.Departments;

public record DepartmentInputDto
{
    public int Id { get; set; }
    public int? AdministratorId { get; set; }

    [StringLength(64, MinimumLength = 3)]
    public string? Title { get; set; }
    [DataType(DataType.Currency)]
    public decimal Budget { get; set; }
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    public Guid ConcurrencyToken { get; set; }
}