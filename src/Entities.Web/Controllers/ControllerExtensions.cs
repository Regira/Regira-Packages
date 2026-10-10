using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.DAL.Paging;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;
using System.Text.Json;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// The generated actions' implementation, also for a hand-written action: each helper sends the matching request of
/// <c>Regira.Entities.Mediator</c> through the <see cref="IEntitySender"/> and answers with its result — <c>null</c> when
/// the entity does not exist, so the caller picks the 404. The write helpers answer a refused write with 400 and a
/// constraint or concurrency conflict with 409, with or without the exception filter.
/// </summary>
public static class ControllerExtensions
{
    // Details
    public static OkObjectResult DetailsResult<TDto>(this ControllerBase _, TDto item, long? duration = null) =>
        new(new DetailsResult<TDto> { Item = item, Duration = duration });

    public static Task<ActionResult<DetailsResult<TDto>>?> Details<TEntity, TDto>(this ControllerBase ctrl, int id, ArchivedFilter? archived = null)
        where TEntity : class, IEntity<int>
        => ctrl.Details<TEntity, int, TDto>(id, archived);
    /// <summary>
    /// Details for a single row. Archived rows are hidden (404) unless <paramref name="archived"/> opts in
    /// (<c>Included</c> or <c>Only</c>) — the read contract stays unchanged for every other caller, and only
    /// the built-in <c>IArchivable</c> global filter reads it.
    /// </summary>
    public static async Task<ActionResult<DetailsResult<TDto>>?> Details<TEntity, TKey, TDto>(this ControllerBase ctrl, TKey id, ArchivedFilter? archived = null)
        where TEntity : class, IEntity<TKey>
        => Ok(await ctrl.Sender().Send(new DetailsQuery<TEntity, TKey, TDto>(id, archived)));

    // List
    public static OkObjectResult ListResult<TDto>(this ControllerBase _, IList<TDto> items, long? duration = null) =>
        new(new ListResult<TDto> { Items = items, Duration = duration });
    // simple
    public static async Task<ActionResult<ListResult<TDto>>> List<TEntity, TKey, TSearchObject, TDto>(this ControllerBase ctrl, TSearchObject? so = null, PagingInfo? pagingInfo = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>
        => Ok(await ctrl.Sender().Send(new ListQuery<TEntity, TKey, TSearchObject, TDto>(so, pagingInfo)))!;
    // complex
    public static async Task<ActionResult<ListResult<TDto>>> List<TEntity, TKey, TSo, TSortBy, TIncludes, TDto>(this ControllerBase ctrl,
        TSo[] so, PagingInfo pagingInfo, TIncludes[] includes, TSortBy[] sortBy)
        where TEntity : class, IEntity<TKey>
        where TSo : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        => Ok(await ctrl.Sender().Send(new ListQuery<TEntity, TKey, TSo, TSortBy, TIncludes, TDto>(so, pagingInfo, includes, sortBy)))!;

    // Search
    public static OkObjectResult SearchResult<TDto>(this ControllerBase _, IList<TDto> items, long count, long? duration = null) =>
        new(new SearchResult<TDto> { Items = items, Count = count, Duration = duration });
    // simple
    public static Task<ActionResult<SearchResult<TDto>>> Search<TEntity, TKey, TDto>(this ControllerBase ctrl, SearchObject<TKey>? so = null, PagingInfo? pagingInfo = null)
        where TEntity : class, IEntity<TKey>
        => ctrl.Search<TEntity, TKey, SearchObject<TKey>, TDto>(so, pagingInfo);
    // simple (custom search object)
    public static async Task<ActionResult<SearchResult<TDto>>> Search<TEntity, TKey, TSearchObject, TDto>(this ControllerBase ctrl, TSearchObject? so = null, PagingInfo? pagingInfo = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>
        => Ok(await ctrl.Sender().Send(new SearchQuery<TEntity, TKey, TSearchObject, TDto>(so, pagingInfo)))!;
    // complex
    public static async Task<ActionResult<SearchResult<TDto>>> Search<TEntity, TKey, TSo, TSortBy, TIncludes, TDto>(this ControllerBase ctrl,
        TSo[] so, PagingInfo pagingInfo, TIncludes[] includes, TSortBy[] sortBy)
        where TEntity : class, IEntity<TKey>
        where TSo : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        => Ok(await ctrl.Sender().Send(new SearchQuery<TEntity, TKey, TSo, TSortBy, TIncludes, TDto>(so, pagingInfo, includes, sortBy)))!;

    // Save
    public static OkObjectResult SaveResult<TDto>(this ControllerBase _, TDto item, int affected, bool isNew, long? duration = null) =>
        new(new SaveResult<TDto> { Item = item, Affected = affected, IsNew = isNew, Duration = duration });
    /// <summary>
    /// Creates or updates from <paramref name="model"/>. The model's DataAnnotations are not checked again: MVC has
    /// answered an invalid request body by then, or the app suppressed that answer on purpose.
    /// </summary>
    public static Task<ActionResult<SaveResult<TDto>>?> Save<TEntity, TKey, TDto, TInputDto>(this ControllerBase ctrl, TInputDto model, TKey? id = default)
        where TEntity : class, IEntity<TKey>
        => ctrl.Write<TEntity, SaveResult<TDto>>(new SaveCommand<TEntity, TKey, TDto, TInputDto>(model, id, ValidateInput: false));
    /// <summary>
    /// Applies a JSON Merge Patch (RFC 7386) from the request body to an existing entity.<br />
    /// Reads the body directly (independent of the configured MVC input formatter).
    /// </summary>
    public static async Task<ActionResult<SaveResult<TDto>>?> Patch<TEntity, TKey, TDto, TInputDto>(this ControllerBase ctrl, TKey id)
        where TEntity : class, IEntity<TKey>
        where TInputDto : class
        where TDto : class
    {
        JsonDocument patchDoc;
        try
        {
            patchDoc = await JsonDocument.ParseAsync(ctrl.Request.Body);
        }
        catch (JsonException)
        {
            return ctrl.BadRequest();
        }
        using (patchDoc)
        {
            return await ctrl.Patch<TEntity, TKey, TDto, TInputDto>(id, patchDoc.RootElement);
        }
    }
    /// <summary>
    /// Applies a JSON Merge Patch (RFC 7386) to an existing entity.<br />
    /// The current entity is serialized as merge base, patched, then deserialized as <typeparamref name="TInputDto"/>,
    /// so only properties present on the input model can be modified.<br />
    /// Assumes <typeparamref name="TInputDto"/> property names match those of <typeparamref name="TEntity"/>.
    /// The merged input is validated as a request body is, and refused with the 400 of a validator's refusal.
    /// </summary>
    public static Task<ActionResult<SaveResult<TDto>>?> Patch<TEntity, TKey, TDto, TInputDto>(this ControllerBase ctrl, TKey id, JsonElement patch)
        where TEntity : class, IEntity<TKey>
        where TInputDto : class
        where TDto : class
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<ActionResult<SaveResult<TDto>>?>(ctrl.BadRequest());
        }

        var serializerOptions = ctrl.HttpContext.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
        return ctrl.Write<TEntity, SaveResult<TDto>>(new PatchCommand<TEntity, TKey, TDto, TInputDto>(id, patch, serializerOptions));
    }

    // Delete
    /// <summary>
    /// Wraps a deleted item as a <see cref="DeleteResult{T}"/>. <c>affected</c> is the rows written — the
    /// real <c>SaveChanges</c> count, which a soft delete makes non-zero even though the row survives —
    /// and <c>duration</c> is elapsed milliseconds for the response envelope only.
    /// <para>
    /// ⚠️ <c>affected</c> is the third parameter and <c>duration</c> the fourth: a bare
    /// <c>this.DeleteResult(dto, someInt)</c> assigns the row count, not the elapsed time. A <c>long</c>
    /// stopwatch reading (the usual duration source) fails to compile there, so pass the duration by name —
    /// <c>this.DeleteResult(dto, affected, duration: ms)</c> — when in doubt.
    /// </para>
    /// </summary>
    public static OkObjectResult DeleteResult<TDto>(this ControllerBase _, TDto item, int affected, long? duration = null) =>
        new(new DeleteResult<TDto> { Item = item, Affected = affected, Duration = duration });
    /// <summary>
    /// Deletes a single row. For an <c>IArchivable</c> entity this is a soft delete (the row survives with
    /// <c>IsArchived = true</c>), so the lookup is archived-inclusive and a repeated delete is idempotent
    /// rather than a 404. Archived-inclusive widens nothing but the archived flag — the lookup is the
    /// regular filtered query, so tenant/owner row security (global filters <em>and</em> the app's own EF
    /// query filters) still constrains it on both target frameworks.
    /// <c>Affected</c> reports the real number of rows written.
    /// </summary>
    public static Task<ActionResult<DeleteResult<TDto>>?> Delete<TEntity, TKey, TDto>(this ControllerBase ctrl, TKey id)
        where TEntity : class, IEntity<TKey>
        => ctrl.Write<TEntity, DeleteResult<TDto>>(new DeleteCommand<TEntity, TKey, TDto>(id));

    public static TService GetRequiredEntityService<TService>(this ControllerBase ctrl)
        where TService : notnull
        => ctrl.HttpContext.RequestServices.GetRequiredEntityService<TService>();

    private static IEntitySender Sender(this ControllerBase ctrl) => ctrl.HttpContext.RequestServices.GetEntitySender();

    private static ActionResult<TResult>? Ok<TResult>(TResult? result)
        where TResult : class
    {
        if (result == null)
        {
            return null;
        }
        return new OkObjectResult(result);
    }

    /// <summary>
    /// Sends a write and maps what the write pipeline refuses — a 400 like a validator's, a 409 for a constraint or a
    /// concurrency conflict — whether or not the exception filter is registered.
    /// </summary>
    private static async Task<ActionResult<TResult>?> Write<TEntity, TResult>(this ControllerBase ctrl, IEntityRequest<TResult> request)
        where TResult : class
    {
        try
        {
            return Ok(await ctrl.Sender().Send(request));
        }
        catch (EntityInputException<TEntity> ex)
        {
            return ex.ToBadRequest(ctrl.HttpContext);
        }
        catch (EntityConstraintException)
        {
            return ctrl.Conflict(EntityConstraintProblem.Create());
        }
        catch (EntityConcurrencyException)
        {
            return ctrl.Conflict(EntityConcurrencyProblem.Create());
        }
    }
}
