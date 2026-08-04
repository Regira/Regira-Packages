using Entities.TestApi.Infrastructure.Departments;
using Regira.Entities.Mapping.Models;
using System.ComponentModel.DataAnnotations;

namespace Entities.TestApi.Infrastructure.Courses;

public record CourseDto
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public string? Title { get; set; }
    public int Credits { get; set; }

    public DepartmentDto? Department { get; set; }
    public ICollection<CourseAttachmentDto>? Attachments { get; set; }
}
public record CourseInputDto
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    [StringLength(64, MinimumLength = 3)]
    public string? Title { get; set; }
    [Range(0, 5)]
    public int Credits { get; set; }

    public DepartmentInputDto? Department { get; set; }
    public ICollection<CourseAttachmentInputDto>? Attachments { get; set; }
}

public record CourseAttachmentDto : EntityAttachmentDto
{
    public string? Description { get; set; }
}
public record CourseAttachmentInputDto : EntityAttachmentInputDto
{
    public string? Description { get; set; }
}