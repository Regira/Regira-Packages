using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Models;
using Testing.Library.Contoso;

namespace Entities.TestApi.Controllers;

/// <summary>
/// Stands in for a hand-written domain action — <c>POST {id}/approve</c> and its kind — which writes through
/// <c>IEntityService</c> and so raises the same exceptions the generated actions do, without going through
/// <c>ControllerExtensions.Save</c>'s catch blocks. It exists to prove those exceptions still reach the client
/// as 400 and 409, which is the whole point of registering the filter application-wide.
/// </summary>
[ApiController]
[Route("domain-actions")]
public class DomainActionController : ControllerBase
{
    [HttpPost("input")]
    public IActionResult Input() =>
        throw new EntityInputException<Course>("A rule rejected the write")
        {
            InputErrors = { ["Title"] = "Only a draft course can be renamed." }
        };

    // Parameterized by a RELATED entity: what a prepper guarding Department throws during a Course write.
    // No closed-generic catch on Course sees this one.
    [HttpPost("input-related")]
    public IActionResult InputRelated() =>
        throw new EntityInputException<Department>("A rule rejected the write")
        {
            InputErrors = { ["DepartmentId"] = "Unknown department." }
        };

    [HttpPost("input-without-field-errors")]
    public IActionResult InputWithoutFieldErrors() =>
        throw new EntityInputException<Course>("Credits must be positive.");

    [HttpPost("constraint")]
    public IActionResult Constraint() =>
        throw new EntityConstraintException("UNIQUE constraint failed: Courses.Title");
}
