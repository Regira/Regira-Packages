using System.Text.Json;
using System.Text.Json.Serialization;
using Regira.DAL.Paging;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of <see cref="PatchCommand{TEntity,TKey,TDto,TInputDto}"/>.</summary>
public class PatchHandler<TEntity, TKey, TDto, TInputDto>(IServiceProvider services)
    : IEntityRequestHandler<PatchCommand<TEntity, TKey, TDto, TInputDto>, SaveResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TDto : class
    where TInputDto : class
{
    // enums read by name or by number: a sender outside HTTP writes a patch either way, and the merge base is written
    // with these options too, so the round trip never meets a representation it cannot read
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SaveResult<TDto>?> Handle(PatchCommand<TEntity, TKey, TDto, TInputDto> request, CancellationToken token = default)
    {
        if (request.Patch.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("A JSON merge patch is a JSON object.", nameof(request));
        }

        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        // List instead of Details to avoid loading related entities (Details fetches max includes).
        // Archived-inclusive: patching an archived row (e.g. to restore it) must reach it. The lookup runs
        // through the regular query pipeline, so every other global filter (tenant/owner row security) still
        // applies — and so does every EF query filter the app configured itself, on both target frameworks
        // (see QueryExtensions.FilterArchivable).
        var existing = (await service.List(new { id = request.Id, Archived = ArchivedFilter.Included }, new PagingInfo { PageSize = 1 }, token)).SingleOrDefault();
        if (existing == null)
        {
            return null;
        }

        var serializerOptions = request.SerializerOptions ?? DefaultSerializerOptions;
        // serializing TEntity and deserializing as TInputDto keeps TInputDto as the write boundary
        var baseJson = JsonSerializer.Serialize(existing, serializerOptions);
        var mergedJson = ApplyJsonMergePatch(baseJson, request.Patch, serializerOptions);
        var mergedInput = JsonSerializer.Deserialize<TInputDto>(mergedJson, serializerOptions)!;

        Services.ThrowIfInvalidInput<TEntity>(mergedInput);
        return await Services.Save<TEntity, TKey, TDto, TInputDto>(mergedInput, request.Id, token);
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
}
