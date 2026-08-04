using Microsoft.AspNetCore.Mvc;
using Regira.DAL.Paging;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Web.Controllers;

namespace Entities.TestApi.Controllers;

[ApiController]
[Route("attachments")]
public class TypedAttachmentController(ITypedAttachmentService service) : ControllerBase
{
    [HttpGet("typed")]
    public async Task<IActionResult> List([FromQuery] EntityAttachmentSearchObject? so, [FromQuery] PagingInfo? pagingInfo)
    {
        var items = await service.List(so, pagingInfo);
        return this.ListResult(items);
    }
}