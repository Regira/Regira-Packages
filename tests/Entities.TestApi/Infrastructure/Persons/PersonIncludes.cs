namespace Entities.TestApi.Infrastructure.Persons;

[Flags]
public enum PersonIncludes
{
    None,
    Supervisor = 1 << 0,
    Subordinates = 1 << 1,
    Departments = 1 << 2,
    All = Supervisor | Subordinates | Departments
}