using Entities.TestApi.Infrastructure.Departments;

namespace Entities.TestApi.Infrastructure.Persons;

public record PersonDto
{
    public int Id { get; set; }

    public string? GivenName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    public int? SupervisorId { get; set; }
    public PersonDto? Supervisor { get; set; }
    public ICollection<PersonDto>? Subordinates { get; set; }
    public ICollection<DepartmentDto>? Departments { get; set; }

    public bool? HasAttachment { get; set; }
}