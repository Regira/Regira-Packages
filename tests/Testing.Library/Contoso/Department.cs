using System.ComponentModel.DataAnnotations;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;

namespace Testing.Library.Contoso;

public class Department : IEntityWithSerial, IHasNormalizedTitle, IHasNormalizedContent, IHasCourses, IHasCreated
{
    public int Id { get; set; }
    public int? AdministratorId { get; set; }

    [StringLength(64, MinimumLength = 3)]
    public string? Title { get; set; }
    [MaxLength(64)]
    [Normalized(SourceProperty = nameof(Title))]
    public string? NormalizedTitle { get; set; }

    [DataType(DataType.Currency)]
    public decimal Budget { get; set; }

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;


    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public Person? Administrator { get; set; }
    public ICollection<Course>? Courses { get; set; }

    [MaxLength(256)]
    public string? NormalizedContent { get; set; }
}