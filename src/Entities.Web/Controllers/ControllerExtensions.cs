using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;
using Microsoft.Extensions.Options;
using Regira.Utilities;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Regira.Entities.Web.Controllers;

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
    {
        var sw = new Stopwatch();
        sw.Start();

        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        var item = await service.Details(id, archived);
        if (item == null)
        {
            return null;
        }

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var model = mapper.Map<TDto>(item);
        sw.Stop();
        return ctrl.DetailsResult(model, sw.ElapsedMilliseconds);
    }

    // List
    public static OkObjectResult ListResult<TDto>(this ControllerBase _, IList<TDto> items, long? duration = null) =>
        new(new ListResult<TDto> { Items = items, Duration = duration });
    // simple
    public static async Task<ActionResult<ListResult<TDto>>> List<TEntity, TKey, TSearchObject, TDto>(this ControllerBase ctrl, TSearchObject? so = null, PagingInfo? pagingInfo = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>
    {
        var sw = new Stopwatch();
        sw.Start();

        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        pagingInfo = ctrl.WithPagingDefaults<TEntity>(pagingInfo);
        var items = await service.List(so, pagingInfo);

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var models = mapper.Map<List<TDto>>(items);

        sw.Stop();
        return ctrl.ListResult(models, sw.ElapsedMilliseconds);
    }
    // complex
    public static async Task<ActionResult<ListResult<TDto>>> List<TEntity, TKey, TSo, TSortBy, TIncludes, TDto>(this ControllerBase ctrl,
        TSo[] so, PagingInfo pagingInfo, TIncludes[] includes, TSortBy[] sortBy)
        where TEntity : class, IEntity<TKey>
        where TSo : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        var sw = new Stopwatch();
        sw.Start();

        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey, TSo, TSortBy, TIncludes>>();
        var items = await service
            .List(so, sortBy, includes.ToBitmask(), ctrl.WithPagingDefaults<TEntity>(pagingInfo));

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var models = mapper.Map<List<TDto>>(items);

        sw.Stop();
        return ctrl.ListResult(models, sw.ElapsedMilliseconds);
    }

    // Search
    public static OkObjectResult SearchResult<TDto>(this ControllerBase _, IList<TDto> items, long count, long? duration = null) =>
        new(new SearchResult<TDto> { Items = items, Count = count, Duration = duration });
    // simple
    public static async Task<ActionResult<SearchResult<TDto>>> Search<TEntity, TKey, TDto>(this ControllerBase ctrl, SearchObject<TKey>? so = null, PagingInfo? pagingInfo = null)
        where TEntity : class, IEntity<TKey>
    {
        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();

        var sw = new Stopwatch();
        sw.Start();

        var count = await service.Count(so);

        IList<TEntity> items = count == 0
            ? Array.Empty<TEntity>()
            : await service.List(so, ctrl.WithPagingDefaults<TEntity>(pagingInfo));

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var models = mapper.Map<List<TDto>>(items);

        sw.Stop();
        return ctrl.SearchResult(models, count, sw.ElapsedMilliseconds);

    }
    // simple (custom search object)
    public static async Task<ActionResult<SearchResult<TDto>>> Search<TEntity, TKey, TSearchObject, TDto>(this ControllerBase ctrl, TSearchObject? so = null, PagingInfo? pagingInfo = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>
    {
        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();

        var sw = new Stopwatch();
        sw.Start();

        var count = await service.Count(so);

        IList<TEntity> items = count == 0
            ? Array.Empty<TEntity>()
            : await service.List(so, ctrl.WithPagingDefaults<TEntity>(pagingInfo));

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var models = mapper.Map<List<TDto>>(items);

        sw.Stop();
        return ctrl.SearchResult(models, count, sw.ElapsedMilliseconds);
    }
    // complex
    public static async Task<ActionResult<SearchResult<TDto>>> Search<TEntity, TKey, TSo, TSortBy, TIncludes, TDto>(this ControllerBase ctrl,
        TSo[] so, PagingInfo pagingInfo, TIncludes[] includes, TSortBy[] sortBy)
        where TEntity : class, IEntity<TKey>
        where TSo : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey, TSo, TSortBy, TIncludes>>();

        var sw = new Stopwatch();
        sw.Start();

        var count = await service.Count(so);

        IList<TEntity> items = count == 0
            ? Array.Empty<TEntity>()
            : await service.List(so, sortBy, includes.ToBitmask(), ctrl.WithPagingDefaults<TEntity>(pagingInfo));

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var models = mapper.Map<List<TDto>>(items);

        sw.Stop();
        return ctrl.SearchResult(models, count, sw.ElapsedMilliseconds);
    }

    // Save
    public static OkObjectResult SaveResult<TDto>(this ControllerBase _, TDto item, int affected, bool isNew, long? duration = null) =>
        new(new SaveResult<TDto> { Item = item, Affected = affected, IsNew = isNew, Duration = duration });
    public static async Task<ActionResult<SaveResult<TDto>>?> Save<TEntity, TKey, TDto, TInputDto>(this ControllerBase ctrl, TInputDto model, TKey? id = default)
        where TEntity : class, IEntity<TKey>
    {
        var sw = new Stopwatch();
        sw.Start();

        try
        {
            var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
            var item = mapper.Map<TEntity>(model!);
            if (!id?.Equals(default(TKey)) ?? false)
            {
                item.Id = id;
            }
            var isNew = item.IsNew();

            var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
            if (!isNew)
            {
                // archived-inclusive row lookup + preservation of a persisted IsArchived that TInputDto
                // cannot express (see EntitySaveHelper.ResolveExistingForWrite)
                var exists = await EntitySaveHelper.ResolveExistingForWrite<TEntity, TKey, TInputDto>(service, item);
                if (!exists)
                {
                    return null;
                }
            }

            await service.Save(item);
            var affected = await service.SaveChanges();

            var savedItem = await EntitySaveHelper.ResolveSavedItem(ctrl.HttpContext.RequestServices, service, item);
            var savedModel = mapper.Map<TDto>(savedItem!);

            sw.Stop();

            return ctrl.SaveResult(savedModel, affected, isNew, sw.ElapsedMilliseconds);
        }
        catch (EntityInputException<TEntity> ex)
        {
            foreach (var error in ex.InputErrors)
            {
                ctrl.ModelState.AddModelError(error.Key, error.Value);
            }

            return ctrl.BadRequest(ctrl.ModelState);
        }
        catch (EntityConstraintException)
        {
            return ctrl.Conflict(EntityConstraintProblem.Create());
        }
    }
    // Patch
    private static readonly JsonSerializerOptions DefaultPatchSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
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
    /// </summary>
    public static async Task<ActionResult<SaveResult<TDto>>?> Patch<TEntity, TKey, TDto, TInputDto>(this ControllerBase ctrl, TKey id, JsonElement patch)
        where TEntity : class, IEntity<TKey>
        where TInputDto : class
        where TDto : class
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            return ctrl.BadRequest();
        }

        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        // List instead of Details to avoid loading related entities (Details fetches max includes).
        // Archived-inclusive: patching an archived row (e.g. to restore it) must reach it. The lookup runs
        // through the regular query pipeline, so every other global filter (tenant/owner row security) still
        // applies — and so does every EF query filter the app configured itself, on both target frameworks
        // (see QueryExtensions.FilterArchivable).
        var existing = (await service.List(new { id, Archived = ArchivedFilter.Included }, new PagingInfo { PageSize = 1 })).SingleOrDefault();
        if (existing == null) return null;

        var serializerOptions = ctrl.HttpContext.RequestServices
            .GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions
            ?? DefaultPatchSerializerOptions;

        // serializing TEntity and deserializing as TInputDto keeps TInputDto as the write boundary
        var baseJson = JsonSerializer.Serialize(existing, serializerOptions);
        var mergedJson = ApplyJsonMergePatch(baseJson, patch, serializerOptions);
        var mergedInput = JsonSerializer.Deserialize<TInputDto>(mergedJson, serializerOptions)!;

        if (!ctrl.TryValidateModel(mergedInput))
            return ctrl.BadRequest(ctrl.ModelState);

        return await ctrl.Save<TEntity, TKey, TDto, TInputDto>(mergedInput, id);
    }

    private static string ApplyJsonMergePatch(string baseJson, JsonElement patch, JsonSerializerOptions serializerOptions)
    {
        using var baseDoc = JsonDocument.Parse(baseJson);
        var result = baseDoc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var prop in patch.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Null)
                result.Remove(prop.Name);
            else
                result[prop.Name] = prop.Value;
        }

        return JsonSerializer.Serialize(result, serializerOptions);
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
    public static async Task<ActionResult<DeleteResult<TDto>>?> Delete<TEntity, TKey, TDto>(this ControllerBase ctrl, TKey id)
        where TEntity : class, IEntity<TKey>
    {
        var sw = new Stopwatch();
        sw.Start();

        var service = ctrl.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        var item = (await service.List(new { id, Archived = ArchivedFilter.Included })).SingleOrDefault();
        if (item == null)
        {
            return null;
        }

        int affected;
        try
        {
            await service.Remove(item);
            affected = await service.SaveChanges();
        }
        catch (EntityConstraintException)
        {
            return ctrl.Conflict(EntityConstraintProblem.Create());
        }

        var mapper = ctrl.HttpContext.RequestServices.GetRequiredService<IEntityMapper>();
        var model = mapper.Map<TDto>(item);

        sw.Stop();

        return ctrl.DeleteResult(model, affected, sw.ElapsedMilliseconds);
    }

    public static TService GetRequiredEntityService<TService>(this ControllerBase ctrl)
        where TService : notnull
    {
        try
        {
            return ctrl.HttpContext.RequestServices.GetRequiredService<TService>();
        }
        catch (InvalidOperationException ex)
        {
            var services = ctrl.HttpContext.RequestServices.GetService<IServiceCollection>();
            throw new InvalidOperationException(EntityServiceDiagnostics.DescribeMissingService(typeof(TService), services), ex);
        }
    }

    /// <summary>
    /// Resolves the effective paging for a List/Search request from the configured <see cref="EntityListOptions"/>.
    /// Enforced at the HTTP boundary only — the FastEndpoints surface applies the same rule, while direct
    /// service calls keep full control. A per-entity
    /// <see cref="EntityListOptions{TEntity}"/>, when registered, fully replaces the global options.
    /// An omitted <c>PageSize</c> (<c>null</c>) falls back to <see cref="EntityListOptions.DefaultPageSize"/>;
    /// a non-positive <c>PageSize</c> (an explicit opt-out) falls back to <see cref="EntityListOptions.MaxPageSize"/>;
    /// a positive value is honoured. <c>MaxPageSize</c> is always the ceiling. <see cref="PagingInfo"/> is a
    /// record, so <c>with</c> preserves <c>Page</c>.
    /// </summary>
    private static PagingInfo? WithPagingDefaults<TEntity>(this ControllerBase ctrl, PagingInfo? pagingInfo)
        where TEntity : class
    {
        var services = ctrl.HttpContext.RequestServices;
        var opts = (EntityListOptions?)services.GetService<EntityListOptions<TEntity>>() ?? services.GetService<EntityListOptions>();
        return pagingInfo.ApplyPagingDefaults(opts);
    }
}