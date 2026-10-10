using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regira.DAL.Paging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Attachments;
using Regira.Entities.Web.Models;
using Regira.Entities.Web.Validation;
using Regira.Web.Extensions;
using Regira.Web.IO;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// Maps one registered entity's endpoints — the controllers' route table, each endpoint a lambda that binds the request
/// and sends it through <see cref="IEntitySender"/>. The entity's types come from its <c>For&lt;&gt;()</c> registration, so
/// each set is mapped once at startup through a generic method closed over them; no request reflects.
/// </summary>
internal sealed class EntityEndpointMapper
{
    private static readonly MethodInfo MapSimpleMethod = typeof(EntityEndpointMapper).GetMethod(nameof(MapSimple), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo MapComplexMethod = typeof(EntityEndpointMapper).GetMethod(nameof(MapComplex), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo MapAttachmentsMethod = typeof(EntityEndpointMapper).GetMethod(nameof(MapAttachments), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceProvider _services;
    private readonly IReadOnlyList<EntityMappingRegistration> _mappings;
    private readonly IReadOnlyDictionary<Type, EntityEndpointOptions> _options;
    private readonly EntityEndpointRegistry? _registry;

    private EntityEndpointMapper(IServiceProvider services)
    {
        _services = services;
        Logger = services.GetService<ILoggerFactory>()?.CreateLogger("Regira.Entities.Web.Endpoints");
        Registrations = (services.GetService<EntityRegistrationLog>()?.Entities ?? [])
            .DistinctBy(r => r.EntityType)
            .ToList();
        _mappings = services.GetServices<EntityMappingRegistration>().ToList();
        _options = services.GetServices<EntityEndpointRegistration>()
            .GroupBy(r => r.EntityType)
            .ToDictionary(g => g.Key, g => g.First().Options);
        _registry = services.GetService<EntityEndpointRegistry>();
        (ControllerEntities, ControllerAttachments) = DiscoverControllers(services);
    }

    public static EntityEndpointMapper For(IServiceProvider services) => new(services);

    public ILogger? Logger { get; }
    public IReadOnlyList<EntityRegistrationLog.EntityRegistration> Registrations { get; }
    /// <summary>The entities an <c>EntityControllerBase</c> subclass MVC discovered serves.</summary>
    public IReadOnlySet<Type> ControllerEntities { get; }
    /// <summary>The attachment links an <c>EntityAttachmentControllerBase</c> subclass MVC discovered serves.</summary>
    public IReadOnlySet<Type> ControllerAttachments { get; }

    public EntityEndpointOptions OptionsFor(Type entityType)
        => _options.TryGetValue(entityType, out var options) ? options : new EntityEndpointOptions();

    public RouteGroupBuilder Map(IEndpointRouteBuilder parent, EntityRegistrationLog.EntityRegistration registration, string? route = null)
    {
        var entityType = registration.EntityType;
        var options = OptionsFor(entityType);
        var (dtoType, inputDtoType) = DtosOf(entityType, options);
        route ??= options.Route ?? EntityEndpointNames.Route(entityType);

        var group = parent.MapGroup(route).WithTags(entityType.Name);
        if (registration.IsComplex)
        {
            if (registration.SortByType == null || registration.IncludesType == null)
            {
                throw new InvalidOperationException($"{entityType.Name}: its complex registration does not record its sort and includes types.");
            }
            Invoke(MapComplexMethod.MakeGenericMethod(entityType, registration.KeyType, registration.SearchObjectType,
                registration.SortByType, registration.IncludesType, dtoType, inputDtoType), group, options);
        }
        else
        {
            // a plain int For<>() records SearchObject<int>, while its controller base binds the SearchObject record: binding
            // that one too, the endpoints send the ListQuery and SearchQuery a controller sends, so one handler replaces either
            var searchObjectType = registration.SearchObjectType == typeof(SearchObject<int>) ? typeof(SearchObject) : registration.SearchObjectType;
            Invoke(MapSimpleMethod.MakeGenericMethod(entityType, registration.KeyType, searchObjectType, dtoType, inputDtoType), group, options);
        }

        var attachments = AttachmentsOf(entityType);
        if (attachments != null)
        {
            // a second mapping of the same link must not repeat the download names: a duplicate name fails every request
            var nameDownloads = _registry?.ClaimDownloadNames(attachments.AttachmentType) ?? true;
            Invoke(MapAttachmentsMethod.MakeGenericMethod(entityType, attachments.AttachmentType, attachments.DtoType, attachments.InputDtoType),
                group, options, nameDownloads);
        }

        _registry?.Add(new MappedEntity(entityType, route, dtoType, inputDtoType, attachments));
        return group;
    }

    private (Type Dto, Type InputDto) DtosOf(Type entityType, EntityEndpointOptions options)
    {
        if (options is { DtoType: not null, InputDtoType: not null })
        {
            return (options.DtoType, options.InputDtoType);
        }
        var mapping = _mappings.LastOrDefault(m => m.EntityType == entityType);
        if (mapping != null)
        {
            return (mapping.DtoType, mapping.InputDtoType);
        }
        // no fallback to the entity itself: that would silently put every column on the wire of an app whose DTOs live
        // on its controllers
        throw new InvalidOperationException(
            $"{entityType.Name}: its endpoints are mapped, but no DTOs are declared for it. Declare them with e.UseMapping<TDto, TInputDto>(), " +
            $"serve the entity as itself with e.Endpoints(o => o.UseDtos<{entityType.Name}, {entityType.Name}>()), " +
            "or keep it off the mapped surface with e.Endpoints(o => o.Disable()).");
    }

    // the int-keyed link shape the attachment controllers serve too, when HasAttachments() registered its service
    private MappedAttachments? AttachmentsOf(Type ownerType)
    {
        var hasAttachments = ownerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasAttachments<,,,,>));
        if (hasAttachments == null)
        {
            return null;
        }
        var arguments = hasAttachments.GetGenericArguments();
        var linkType = arguments[0];
        if (arguments[1] != typeof(int) || arguments[2] != typeof(int) || arguments[3] != typeof(int) || arguments[4] != typeof(Attachment))
        {
            Logger?.LogWarning("{Owner}: its attachments are not mapped. Mapped attachment endpoints serve the int-keyed link shape (IHasAttachments<{Link}>) only.",
                ownerType.Name, linkType.Name);
            return null;
        }
        var isService = _services.GetService<IServiceProviderIsService>();
        if (isService?.IsService(typeof(IEntityService<,>).MakeGenericType(linkType, typeof(int))) != true)
        {
            return null;
        }
        if (ControllerAttachments.Contains(linkType))
        {
            Logger?.LogInformation("{Link}: an attachment controller serves it, so its endpoints are left to that controller.", linkType.Name);
            return null;
        }
        var mapping = _mappings.LastOrDefault(m => m.EntityType == linkType);
        return new MappedAttachments(linkType, mapping?.DtoType ?? typeof(EntityAttachmentDto), mapping?.InputDtoType ?? typeof(EntityAttachmentInputDto));
    }

    private static (IReadOnlySet<Type> Entities, IReadOnlySet<Type> Attachments) DiscoverControllers(IServiceProvider services)
    {
        var partManager = services.GetService<ApplicationPartManager>();
        if (partManager == null)
        {
            return (new HashSet<Type>(), new HashSet<Type>());
        }
        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);
        var controllers = feature.Controllers.Select(c => c.AsType()).ToList();
        return (
            controllers.Select(c => ControllerDtoShapeSource.Shape(c)?.EntityType).OfType<Type>().ToHashSet(),
            controllers.SelectMany(ControllerRegistrationValidator.GetAttachmentEntities).ToHashSet());
    }

    private static void Invoke(MethodInfo method, params object[] arguments)
    {
        try
        {
            method.Invoke(null, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    // ── the endpoint sets ────────────────────────────────────────────────────────────────────────────────────────────

    private static void MapSimple<TEntity, TKey, TSearchObject, TDto, TInputDto>(RouteGroupBuilder group, EntityEndpointOptions options)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TDto : class
        where TInputDto : class
    {
        MapDetails<TEntity, TKey, TDto>(group, options);
        if (options.Maps(EntityEndpoint.List))
        {
            group.MapGet("", async (HttpContext http) =>
                {
                    var (so, paging) = BindQuery<TEntity, TSearchObject>(http);
                    return Ok(await Sender(http).Send(new ListQuery<TEntity, TKey, TSearchObject, TDto>(so, paging)));
                })
                .Describe<TEntity>(EntityEndpoint.List, options)
                .Produces<ListResult<TDto>>()
                .ProducesValidationProblem();
        }
        if (options.Maps(EntityEndpoint.Search))
        {
            group.MapGet("search", async (HttpContext http) =>
                {
                    var (so, paging) = BindQuery<TEntity, TSearchObject>(http);
                    return Ok(await Sender(http).Send(new SearchQuery<TEntity, TKey, TSearchObject, TDto>(so, paging)));
                })
                .Describe<TEntity>(EntityEndpoint.Search, options)
                .Produces<SearchResult<TDto>>()
                .ProducesValidationProblem();
        }
        MapWrites<TEntity, TKey, TDto, TInputDto>(group, options);
    }

    private static void MapComplex<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>(RouteGroupBuilder group, EntityEndpointOptions options)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        where TDto : class
        where TInputDto : class
    {
        MapDetails<TEntity, TKey, TDto>(group, options);
        if (options.Maps(EntityEndpoint.List))
        {
            group.MapGet("", async (HttpContext http) =>
                {
                    var (so, paging, includes, sortBy) = BindComplexQuery<TEntity, TSearchObject, TSortBy, TIncludes>(http, bindSearchObject: true);
                    return Ok(await Sender(http).Send(new ListQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>([so], paging, includes, sortBy)));
                })
                .Describe<TEntity>(EntityEndpoint.List, options)
                .Produces<ListResult<TDto>>()
                .ProducesValidationProblem();
            group.MapPost("list", async (TSearchObject?[] searchObjects, HttpContext http) =>
                {
                    var (_, paging, includes, sortBy) = BindComplexQuery<TEntity, TSearchObject, TSortBy, TIncludes>(http, bindSearchObject: false);
                    return Ok(await Sender(http).Send(new ListQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>(searchObjects, paging, includes, sortBy)));
                })
                .Describe<TEntity>(EntityEndpoint.List, options)
                .Produces<ListResult<TDto>>()
                .ProducesValidationProblem();
        }
        if (options.Maps(EntityEndpoint.Search))
        {
            group.MapGet("search", async (HttpContext http) =>
                {
                    var (so, paging, includes, sortBy) = BindComplexQuery<TEntity, TSearchObject, TSortBy, TIncludes>(http, bindSearchObject: true);
                    return Ok(await Sender(http).Send(new SearchQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>([so], paging, includes, sortBy)));
                })
                .Describe<TEntity>(EntityEndpoint.Search, options)
                .Produces<SearchResult<TDto>>()
                .ProducesValidationProblem();
            group.MapPost("search", async (TSearchObject?[] searchObjects, HttpContext http) =>
                {
                    var (_, paging, includes, sortBy) = BindComplexQuery<TEntity, TSearchObject, TSortBy, TIncludes>(http, bindSearchObject: false);
                    return Ok(await Sender(http).Send(new SearchQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>(searchObjects, paging, includes, sortBy)));
                })
                .Describe<TEntity>(EntityEndpoint.Search, options)
                .Produces<SearchResult<TDto>>()
                .ProducesValidationProblem();
        }
        MapWrites<TEntity, TKey, TDto, TInputDto>(group, options);
    }

    private static void MapDetails<TEntity, TKey, TDto>(RouteGroupBuilder group, EntityEndpointOptions options)
        where TEntity : class, IEntity<TKey>
    {
        if (!options.Maps(EntityEndpoint.Details))
        {
            return;
        }
        group.MapGet("{id}", async (TKey id, HttpContext http) =>
            {
                var errors = new List<EntityInputError>();
                var archived = EntityQueryBinder.BindValue<ArchivedFilter?>(http.Request.Query, "archived", errors);
                ThrowIfInvalid<TEntity>(errors);
                return Ok(await Sender(http).Send(new DetailsQuery<TEntity, TKey, TDto>(id, archived)));
            })
            .Describe<TEntity>(EntityEndpoint.Details, options)
            .Produces<DetailsResult<TDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites<TEntity, TKey, TDto, TInputDto>(RouteGroupBuilder group, EntityEndpointOptions options)
        where TEntity : class, IEntity<TKey>
        where TDto : class
        where TInputDto : class
    {
        // no MVC validated the body here, so each save checks its input's DataAnnotations (ValidateInput defaults to true),
        // as the attachment upload and update below do
        if (options.Maps(EntityEndpoint.Save))
        {
            group.MapPost("save", async (TInputDto input, HttpContext http)
                    => Ok(await Sender(http).Send(new SaveCommand<TEntity, TKey, TDto, TInputDto>(input))))
                .Describe<TEntity>(EntityEndpoint.Save, options)
                .ProducesWrite<SaveResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.Create))
        {
            group.MapPost("", async (TInputDto input, HttpContext http)
                    => Ok(await Sender(http).Send(new SaveCommand<TEntity, TKey, TDto, TInputDto>(input))))
                .Describe<TEntity>(EntityEndpoint.Create, options)
                .ProducesWrite<SaveResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.Modify))
        {
            group.MapPut("{id}", async (TKey id, TInputDto input, HttpContext http)
                    => Ok(await Sender(http).Send(new SaveCommand<TEntity, TKey, TDto, TInputDto>(input, id))))
                .Describe<TEntity>(EntityEndpoint.Modify, options)
                .ProducesWrite<SaveResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.Patch))
        {
            group.MapPatch("{id}", async (TKey id, HttpContext http) =>
                {
                    // read directly, as the controllers do: a merge patch is no TInputDto, and null clears a field
                    JsonDocument patch;
                    try
                    {
                        patch = await JsonDocument.ParseAsync(http.Request.Body);
                    }
                    catch (JsonException)
                    {
                        return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
                    }
                    using (patch)
                    {
                        if (patch.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
                        }
                        var serializerOptions = http.RequestServices.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
                        return Ok(await Sender(http).Send(new PatchCommand<TEntity, TKey, TDto, TInputDto>(id, patch.RootElement, serializerOptions)));
                    }
                })
                .Describe<TEntity>(EntityEndpoint.Patch, options)
                .Accepts<TInputDto>("application/merge-patch+json", "application/json")
                .ProducesWrite<SaveResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.Delete))
        {
            group.MapDelete("{id}", async (TKey id, HttpContext http)
                    => Ok(await Sender(http).Send(new DeleteCommand<TEntity, TKey, TDto>(id))))
                .Describe<TEntity>(EntityEndpoint.Delete, options)
                .ProducesWrite<DeleteResult<TDto>>();
        }
    }

    // the routes of EntityAttachmentControllerBase, under the owner's route
    private static void MapAttachments<TOwner, TLink, TDto, TInputDto>(RouteGroupBuilder group, EntityEndpointOptions options, bool nameDownloads)
        where TLink : class, IEntityAttachment<int, int, int, Attachment>
        where TDto : class
        where TInputDto : class
    {
        if (options.Maps(EntityEndpoint.AttachmentDetails))
        {
            group.MapGet("attachments/{id}", async (int id, HttpContext http)
                    => Ok(await Sender(http).Send(new DetailsQuery<TLink, int, TDto>(id))))
                .Describe<TOwner>(EntityEndpoint.AttachmentDetails, options)
                .Produces<DetailsResult<TDto>>()
                .ProducesProblem(StatusCodes.Status404NotFound);
        }
        if (options.Maps(EntityEndpoint.AttachmentList))
        {
            group.MapGet("attachments", async (HttpContext http) =>
                {
                    var (so, paging) = BindQuery<TLink, EntityAttachmentSearchObject>(http);
                    return Ok(await Sender(http).Send(new ListQuery<TLink, int, EntityAttachmentSearchObject, TDto>(so, paging)));
                })
                .Describe<TOwner>(EntityEndpoint.AttachmentList, options)
                .Produces<ListResult<TDto>>()
                .ProducesValidationProblem();
            group.MapGet("{objectId}/attachments", async (int objectId, HttpContext http) =>
                {
                    var (so, paging) = BindQuery<TLink, EntityAttachmentSearchObject>(http);
                    so.ObjectId = [objectId];
                    return Ok(await Sender(http).Send(new ListQuery<TLink, int, EntityAttachmentSearchObject, TDto>(so, paging)));
                })
                .Describe<TOwner>(EntityEndpoint.AttachmentList, options)
                .Produces<ListResult<TDto>>()
                .ProducesValidationProblem();
        }
        if (options.Maps(EntityEndpoint.AttachmentUpdate))
        {
            group.MapPut("{objectId}/attachments/{id}", async (int objectId, int id, TInputDto input, HttpContext http)
                    => Ok(await Sender(http).Send(new UpdateAttachmentCommand<TLink, TDto, TInputDto>(objectId, id, input))))
                .Describe<TOwner>(EntityEndpoint.AttachmentUpdate, options)
                .ProducesWrite<SaveResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.AttachmentDelete))
        {
            group.MapDelete("attachments/{id}", async (int id, HttpContext http)
                    => Ok(await Sender(http).Send(new DeleteCommand<TLink, int, TDto>(id))))
                .Describe<TOwner>(EntityEndpoint.AttachmentDelete, options)
                .ProducesWrite<DeleteResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.Download))
        {
            var byId = group.MapGet("files/{id}", async (int id, HttpContext http, bool inline = true) =>
                {
                    var attachment = await Sender(http).Send(new AttachmentFileQuery<TLink>(id));
                    return attachment == null ? Results.Problem(statusCode: StatusCodes.Status404NotFound) : attachment.ToFileResult(http, inline);
                })
                .Describe<TOwner>(EntityEndpoint.Download, options);
            var byName = group.MapGet("{objectId}/files/{*fileName}", async (int objectId, string fileName, HttpContext http, bool inline = true) =>
                {
                    var attachment = await Sender(http).Send(new AttachmentFileByNameQuery<TLink>(objectId, AttachmentRouteValues.DecodeFileName(fileName)));
                    return attachment == null ? Results.Problem(statusCode: StatusCodes.Status404NotFound) : attachment.ToFileResult(http, inline);
                })
                .Describe<TOwner>(EntityEndpoint.Download, options);
            // the names AttachmentUriResolver links to
            if (nameDownloads)
            {
                byId.WithName(EntityEndpointNames.GetFile(typeof(TLink)));
                byName.WithName(EntityEndpointNames.GetFileByName(typeof(TLink)));
            }
        }
        // a client posting a form without an antiforgery token is the expected caller of an API
        if (options.Maps(EntityEndpoint.Upload))
        {
            // a form with no model fields binds no model here, where MVC binds an empty one
            group.MapPost("{objectId}/files", async (int objectId, IFormFile file, [FromForm] TInputDto? model, HttpContext http)
                    => Ok(await Sender(http).Send(new UploadAttachmentCommand<TLink, TDto, TInputDto>(objectId, model ?? Activator.CreateInstance<TInputDto>(), file.ToNamedFile()))))
                .DisableAntiforgery()
                .Describe<TOwner>(EntityEndpoint.Upload, options)
                .ProducesWrite<SaveResult<TDto>>();
        }
        if (options.Maps(EntityEndpoint.ReplaceFile))
        {
            group.MapPut("{objectId}/files/{id}", async (int objectId, int id, IFormFile file, HttpContext http)
                    => Ok(await Sender(http).Send(new ReplaceAttachmentFileCommand<TLink, TDto>(objectId, id, file.ToNamedFile()))))
                .DisableAntiforgery()
                .Describe<TOwner>(EntityEndpoint.ReplaceFile, options)
                .ProducesWrite<SaveResult<TDto>>();
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static IEntitySender Sender(HttpContext http) => http.RequestServices.GetEntitySender();

    private static IResult Ok<TResult>(TResult? result)
        where TResult : class
        => result == null ? Results.Problem(statusCode: StatusCodes.Status404NotFound) : Results.Ok(result);

    private static (TSearchObject SearchObject, PagingInfo Paging) BindQuery<TEntity, TSearchObject>(HttpContext http)
        where TSearchObject : class, new()
    {
        var errors = new List<EntityInputError>();
        var so = EntityQueryBinder.Bind<TSearchObject>(http.Request.Query, errors);
        var paging = EntityQueryBinder.Bind<PagingInfo>(http.Request.Query, errors);
        ThrowIfInvalid<TEntity>(errors);
        return (so, paging);
    }

    private static (TSearchObject SearchObject, PagingInfo Paging, TIncludes[] Includes, TSortBy[] SortBy) BindComplexQuery<TEntity, TSearchObject, TSortBy, TIncludes>(
        HttpContext http, bool bindSearchObject)
        where TSearchObject : class, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        var query = http.Request.Query;
        var errors = new List<EntityInputError>();
        var so = bindSearchObject ? EntityQueryBinder.Bind<TSearchObject>(query, errors) : new TSearchObject();
        var paging = EntityQueryBinder.Bind<PagingInfo>(query, errors);
        var includes = EntityQueryBinder.BindList<TIncludes>(query, "includes", errors);
        var sortBy = EntityQueryBinder.BindList<TSortBy>(query, "sortBy", errors);
        ThrowIfInvalid<TEntity>(errors);
        return (so, paging, includes, sortBy);
    }

    // the 400 of model binding, as the exception filter answers a validator's: errors keyed by the parameter's property
    private static void ThrowIfInvalid<TEntity>(List<EntityInputError> errors)
    {
        if (errors.Count > 0)
        {
            throw new EntityInputException<TEntity>("The query string is not valid.") { Errors = errors };
        }
    }
}

internal static class EntityEndpointConventionExtensions
{
    public static RouteHandlerBuilder Describe<TEntity>(this RouteHandlerBuilder builder, EntityEndpoint endpoint, EntityEndpointOptions options)
    {
        builder.WithMetadata(new EntityEndpointMetadata(typeof(TEntity), endpoint));
        if (options.Anonymous.Contains(endpoint))
        {
            builder.AllowAnonymous();
        }
        return builder;
    }

    public static RouteHandlerBuilder ProducesWrite<TResult>(this RouteHandlerBuilder builder)
        => builder
            .Produces<TResult>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
