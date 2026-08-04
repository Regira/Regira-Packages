using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Controllers;
using Regira.Entities.Web.Models;
using Regira.Web.Extensions;
using Regira.Web.IO;

namespace Regira.Entities.Web.Attachments.Abstractions;

[ApiController]
[Route("attachments")]
[EntityConstraintConflict]
public abstract class AttachmentControllerBase(IEntityService<Attachment, int> service, IEntityMapper mapper) : ControllerBase
{
    [HttpGet("{id}")]
    public virtual async Task<IActionResult> GetFile([FromRoute] int id, bool inline = true)
    {
        var item = await service.Details(id);

        if (item == null)
        {
            return NotFound();
        }

        return this.File(item, inline);
    }
    [HttpPost]
    public virtual async Task<ActionResult<SaveResult<AttachmentDto>>> Save(IFormFile file)
    {
        var item = file.ToNamedFile().ToAttachment();
        await service.Save(item);
        var affected = await service.SaveChanges();
        var savedModel = mapper.Map<AttachmentDto>(item);
        return this.SaveResult(savedModel, affected, true);
    }
    [HttpPut("{id}")]
    public virtual async Task<ActionResult<SaveResult<AttachmentDto>>> Save([FromRoute] int id, IFormFile file)
    {
        var item = file.ToNamedFile().ToAttachment();
        item.Id = id;
        await service.Save(item);
        var affected = await service.SaveChanges();
        var savedModel = mapper.Map<AttachmentDto>(item);
        return this.SaveResult(savedModel, affected, false);
    }
}