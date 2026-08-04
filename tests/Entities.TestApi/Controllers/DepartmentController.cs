using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Web.Controllers.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Controllers;

[ApiController]
[Route("departments")]
public class DepartmentController : EntityControllerBase<Department>;