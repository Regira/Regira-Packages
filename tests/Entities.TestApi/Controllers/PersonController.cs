using Entities.TestApi.Infrastructure.Persons;
using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Web.Attachments.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Controllers;

[ApiController]
[Route("persons")]
public class PersonController : EntityControllerBase<Person, int, PersonSearchObject, PersonSortBy, PersonIncludes, PersonDto, PersonInputDto>;

[ApiController]
[Route("persons")]
public class PersonAttachmentController : EntityAttachmentControllerBase<PersonAttachment>;