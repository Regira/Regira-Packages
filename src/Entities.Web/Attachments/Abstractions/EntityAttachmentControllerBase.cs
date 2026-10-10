using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Controllers;
using Regira.Entities.Web.Models;
using Regira.Web.IO;
using Regira.Entities.Attachments.Mapping.Abstractions;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using static Regira.Web.Extensions.ControllerExtensions;

namespace Regira.Entities.Web.Attachments.Abstractions;

public abstract class EntityAttachmentControllerBase<TEntity> : EntityAttachmentControllerBase<TEntity, EntityAttachmentDto, EntityAttachmentInputDto>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>, IEntity<int>;
[EntityConstraintConflict]
public abstract class EntityAttachmentControllerBase<TEntity, TDto, TInputDto> : ControllerBase
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>, IEntity<int>
    where TInputDto : class, IEntityAttachmentInput
{
    // Details
    [HttpGet("attachments/{id}")]
    public virtual async Task<ActionResult<DetailsResult<TDto>>> Details([FromRoute] int id)
        => await this.Details<TEntity, int, TDto>(id) ?? NotFound();
    // List
    [HttpGet("attachments")]
    public virtual Task<ActionResult<ListResult<TDto>>> List([FromQuery] EntityAttachmentSearchObject so, [FromQuery] PagingInfo? pagingInfo = null)
        => this.List<TEntity, int, EntityAttachmentSearchObject, TDto>(so, pagingInfo);
    [HttpGet("{objectId}/attachments")]
    public virtual Task<ActionResult<ListResult<TDto>>> List([FromRoute] int objectId, [FromQuery] EntityAttachmentSearchObject so, [FromQuery] PagingInfo? pagingInfo = null)
    {
        so.ObjectId = [objectId];
        return List(so, pagingInfo);
    }

    // Save (Update)
    [HttpPut("{objectId}/attachments/{id}")]
    public virtual async Task<ActionResult<SaveResult<TDto>>?> Update([FromRoute] int objectId, [FromRoute] int id, [FromBody] TInputDto model)
    {
        try
        {
            // the route is authoritative — the body can neither target another row nor reparent it
            // MVC has validated the body by then, as for SaveCommand
            var result = await Sender.Send(new UpdateAttachmentCommand<TEntity, TDto, TInputDto>(objectId, id, model, ValidateInput: false));
            return result == null ? NotFound() : Ok(result);
        }
        catch (EntityInputException<TEntity> ex)
        {
            return ex.ToBadRequest(HttpContext);
        }
    }

    // Delete
    [HttpDelete("attachments/{id}")]
    public virtual async Task<ActionResult<DeleteResult<TDto>>?> Delete([FromRoute] int id)
        => await this.Delete<TEntity, int, TDto>(id) ?? NotFound();

    // Download
    [HttpGet("files/{id}")]
    public virtual async Task<IActionResult> GetFile([FromRoute] int id, bool inline = true)
    {
        var attachment = await Sender.Send(new AttachmentFileQuery<TEntity>(id));
        return attachment == null ? NotFound() : this.File(attachment, inline);
    }
    /// <summary>
    /// Downloads by the client-facing <c>FileName</c>, which may carry a virtual folder
    /// (<c>folder1/folder2/report.pdf</c>) — hence the catch-all: a single-segment token would never route a
    /// foldered name, leaving those files reachable only by id. Served through <see cref="GetFile(int, bool)"/>, so an
    /// override of that one covers this download too.
    /// </summary>
    [HttpGet("{objectId}/files/{*fileName}")]
    public virtual async Task<IActionResult> GetFile([FromRoute] int objectId, [FromRoute] string fileName, bool inline = true)
    {
        var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
        var link = (await service.List(new { objectId = new[] { objectId }, fileName = AttachmentRouteValues.DecodeFileName(fileName) }, new PagingInfo { PageSize = 1 }))
            .FirstOrDefault();
        return link == null ? NotFound() : await GetFile(link.Id, inline);
    }
    // Upload
    [HttpPost("{objectId}/files")]
    public virtual async Task<ActionResult<SaveResult<TDto>>> Add([FromRoute] int objectId, IFormFile file, [FromForm] TInputDto model)
    {
        try
        {
            // the route creates a link: an Id in the form cannot turn the upload into a write to another one
            return Ok(await Sender.Send(new UploadAttachmentCommand<TEntity, TDto, TInputDto>(objectId, model, file.ToNamedFile(), ValidateInput: false)));
        }
        catch (EntityInputException<TEntity> ex)
        {
            return ex.ToBadRequest(HttpContext);
        }
    }
    [HttpPut("{objectId}/files/{id}")]
    public virtual async Task<ActionResult<SaveResult<TDto>>> Modify([FromRoute] int objectId, [FromRoute] int id, IFormFile file)
    {
        try
        {
            var result = await Sender.Send(new ReplaceAttachmentFileCommand<TEntity, TDto>(objectId, id, file.ToNamedFile()));
            return result == null ? NotFound() : Ok(result);
        }
        catch (EntityInputException<TEntity> ex)
        {
            return ex.ToBadRequest(HttpContext);
        }
    }

    private IEntitySender Sender => HttpContext.RequestServices.GetEntitySender();

    /// <summary>
    /// Fetches item with related Attachment, but without file contents
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    protected async Task<TEntity?> FetchItem(int id)
    {
        var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
        return (await service.List(new { id }, new PagingInfo { PageSize = 1 })).SingleOrDefault();
    }
}