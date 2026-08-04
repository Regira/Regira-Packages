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
using System.Diagnostics;
using Regira.Entities.Attachments.Mapping.Abstractions;
using Regira.Entities.Mapping.Models;
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
        var sw = new Stopwatch();
        sw.Start();

        try
        {
            var mapper = HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
            var item = mapper.Map<TEntity>(model);
            // the route is authoritative — the body can neither target another row nor reparent it
            item.Id = id;
            item.ObjectId = objectId;

            var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
            var original = await FetchItem(id);
            if (original == null)
            {
                return NotFound();
            }
            if (original.ObjectId != objectId)
            {
                return BadRequest($"Bad {nameof(original.ObjectId)}");
            }

            await service.Save(item);
            var affected = await service.SaveChanges();

            var savedItem = await FetchItem(id);
            var savedModel = mapper.Map<TDto>(savedItem!);

            sw.Stop();

            return this.SaveResult(savedModel, affected, false, sw.ElapsedMilliseconds);
        }
        catch (EntityInputException<TEntity> ex)
        {
            foreach (var error in ex.InputErrors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }

            return BadRequest(ModelState);
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
        var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
        var item = await service.Details(id);

        if (item == null)
        {
            return NotFound();
        }

        return this.File(item.Attachment!, inline);
    }
    /// <summary>
    /// Downloads by the client-facing <c>FileName</c>, which may carry a virtual folder
    /// (<c>folder1/folder2/report.pdf</c>) — hence the catch-all: a single-segment token would never route a
    /// foldered name, leaving those files reachable only by id.
    /// </summary>
    [HttpGet("{objectId}/files/{*fileName}")]
    public virtual async Task<IActionResult> GetFile([FromRoute] int objectId, [FromRoute] string fileName, bool inline = true)
    {
        // ASP.NET Core does not decode %2F in a path segment (it would change the route's shape), and
        // LinkGenerator emits exactly that form for the Uri on the DTO — so an encoded link arrives as
        // "archive%2F2026%2Fscan.txt" and must be decoded here. A literal path arrives already split and is
        // untouched. Normalizing after decoding means both spellings hit the same stored value.
        var decodedFileName = (fileName.Contains("%2F", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(fileName)
            : fileName).ToVirtualPath();
        var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
        var items = await service.List(new { objectId = new[] { objectId }, fileName = decodedFileName }, new PagingInfo { PageSize = 1 });

        if (!items.Any())
        {
            return NotFound();
        }

        return await GetFile(items.First().Id);
    }
    // Upload
    [HttpPost("{objectId}/files")]
    public virtual async Task<ActionResult<SaveResult<TDto>>> Add([FromRoute] int objectId, IFormFile file, [FromForm] TInputDto model)
    {
        var sw = new Stopwatch();
        sw.Start();

        var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
        var mapper = HttpContext.RequestServices.GetRequiredService<IEntityMapper>();

        var item = mapper.Map<TEntity>(model);
        item.ObjectId = objectId;
        item.Attachment = file.ToNamedFile().ToAttachment();

        await service.Save(item);
        var affected = await service.SaveChanges();
        var savedModel = mapper.Map<TDto>(item);

        sw.Stop();

        return this.SaveResult(savedModel, affected, true, sw.ElapsedMilliseconds);
    }
    [HttpPut("{objectId}/files/{id}")]
    public virtual async Task<ActionResult<SaveResult<TDto>>> Modify([FromRoute] int objectId, [FromRoute] int id, IFormFile file)
    {
        var sw = new Stopwatch();
        sw.Start();

        var service = HttpContext.RequestServices.GetRequiredService<IEntityService<TEntity, int>>();
        var mapper = HttpContext.RequestServices.GetRequiredService<IEntityMapper>();

        var item = (await service.List(new { id }, new PagingInfo { PageSize = 1 })).SingleOrDefault();
        if (item == null)
        {
            return NotFound();
        }
        if (item.ObjectId != objectId)
        {
            return BadRequest($"Bad {nameof(item.ObjectId)}");
        }

        item.ObjectId = objectId;
        item.Attachment = file.ToNamedFile().ToAttachment();

        await service.Save(item);
        var affected = await service.SaveChanges();
        var savedModel = mapper.Map<TDto>(item);

        sw.Stop();

        return this.SaveResult(savedModel, affected, false, sw.ElapsedMilliseconds);
    }


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