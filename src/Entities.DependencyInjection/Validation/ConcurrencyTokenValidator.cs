using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.EFcore.Conventions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.Models.Abstractions;
using System.Reflection;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports a concurrency token that is never checked, never moves, or cannot make the round trip to the client. The
/// write path compares every concurrency token the model declares with the value the client sent, so the check
/// exists only when the model declares the token, holds only when every write moves it, and reaches the client only
/// through the DTOs:
/// <list type="bullet">
/// <item><b>Error</b> — an <see cref="IHasConcurrencyToken"/> entity whose model does not treat <c>ConcurrencyToken</c>
/// as a concurrency token, because the wiring that declares it never reached the context (a non-generic
/// <c>UseEntities()</c>, or <c>WireDbContext(...)</c> without <c>DbContextWiring.ConcurrencyTokens</c>). Nothing is
/// compared, so every write is last-write-wins. An explicit <c>.IsConcurrencyToken(false)</c> on a context that has
/// the wiring is a deliberate opt-out and is not reported.</item>
/// <item><b>Warning</b> — an <see cref="IHasConcurrencyToken"/> entity while no <see cref="HasConcurrencyTokenDbPrimer"/>
/// (or subclass) is among the primers the save interceptor runs. Nothing mints a new token on save, so two clients
/// holding the same value both pass.</item>
/// <item><b>Warning</b> — the read DTO or the input DTO has no property for a token. The client never receives it,
/// or can never send it back, so every write through the entity controller is last-write-wins: 200 OK, no
/// conflict, no log.</item>
/// <item><b>Warning</b> — the input DTO initializes its token property, or an entity that is its own input DTO
/// initializes its token. A client that leaves the token out sends that value instead of none, so its write answers
/// 409 where an absent token skips the check.</item>
/// <item><b>Error</b> — the entity initializes its token (<c>= Guid.NewGuid()</c>) and a separate input DTO has no
/// property for it. The mapper builds a fresh entity for every request, so every PUT and PATCH carries a token the
/// row never held and answers 409.</item>
/// </list>
/// <para>
/// Detected statically, from the model DI builds, the last <c>UseMapping&lt;TDto, TInputDto&gt;()</c> registration and
/// the primers the interceptor would run. What counts as a token and as "supplied" is shared with the write path, and
/// the primers come from the interceptor's own discovery, so neither can disagree with what runs. An entity that is
/// its own input DTO carries its token by construction, so only its initializer is judged. Blind spots by
/// construction: a <c>Related()</c> child (mapped inside its parent's DTO, whose shape this validator does not see)
/// and a <c>DbContext</c> constructed outside DI.
/// </para>
/// </summary>
internal sealed class ConcurrencyTokenValidator : IEntityRegistrationValidator
{
    private const string SeeAlso = "See entities.patterns → Optimistic concurrency.";

    private sealed record InspectedContext(Type ContextType, IModel Model, bool ConventionWired);

    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var registered = context.Registrations.Entities
            .Select(r => r.EntityType)
            .Distinct()
            .OrderBy(t => t.Name)
            .ToArray();
        if (registered.Length == 0)
        {
            yield break;
        }
        var markers = registered.Where(t => typeof(IHasConcurrencyToken).IsAssignableFrom(t)).ToArray();
        var mappings = context.Services
            .Where(d => d.ServiceType == typeof(EntityMappingRegistration))
            .Select(d => d.ImplementationInstance)
            .OfType<EntityMappingRegistration>()
            .ToArray();
        // Last-wins: UseMapping appends a registration per call and DI resolves the last one (see
        // AttachmentsInputDtoValidator). An entity without one — or mapped onto itself — is its own input DTO.
        var effective = registered
            .Select(entityType => (EntityType: entityType, Mapping: mappings.LastOrDefault(m => m.EntityType == entityType)))
            .ToArray();
        var mapped = effective
            .Where(e => e.Mapping != null && e.Mapping.InputDtoType != e.EntityType)
            .Select(e => e.Mapping!)
            .ToArray();
        var selfBound = effective
            .Where(e => e.Mapping == null || e.Mapping.InputDtoType == e.EntityType)
            .Select(e => e.EntityType)
            .ToArray();

        using var scope = context.Provider.CreateScope();
        var contexts = new List<InspectedContext>();
        var failures = new List<string>();
        foreach (var contextType in ValidationContextTypes.Inspectable(context))
        {
            var (inspected, failure) = Inspect(scope.ServiceProvider, contextType);
            if (inspected != null)
            {
                contexts.Add(inspected);
            }
            else
            {
                failures.Add($"Could not inspect {contextType.Name} for concurrency tokens: {failure}");
            }
        }
        // an app with no marker and no separate DTOs only has initializers to lose here — not worth a line of its own
        if (markers.Length > 0 || mapped.Length > 0)
        {
            foreach (var failure in failures)
            {
                yield return new EntityValidationIssue(EntityValidationSeverity.Info, failure);
            }
        }

        if (markers.Length > 0)
        {
            var (mints, failure) = MintsConcurrencyTokens(scope.ServiceProvider, context.Services);
            if (failure != null)
            {
                yield return new EntityValidationIssue(EntityValidationSeverity.Info,
                    $"Could not resolve the registered primers to check that IHasConcurrencyToken is minted: {failure}");
            }
            else if (!mints)
            {
                yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                    $"{string.Join(", ", markers.Select(t => t.Name))} implement{(markers.Length == 1 ? "s" : "")} IHasConcurrencyToken, " +
                    "but no registered primer mints the concurrency token: HasConcurrencyTokenDbPrimer is not registered (nor a subclass of it), " +
                    "so two clients holding the same value both pass the check. " +
                    $"ACTION: register the entities with UseEntities<TContext>(o => o.UseDefaults()), or add the primer with o.AddPrimer<HasConcurrencyTokenDbPrimer>(). {SeeAlso}");
            }
        }

        foreach (var marker in markers)
        {
            // every context that maps the marker, not whichever one comes first: the same entity can be a token in one
            // context and last-write-wins in another, and the silent one is what this check exists to find
            foreach (var owner in contexts.Where(c => c.Model.FindEntityType(marker) != null))
            {
                var property = owner.Model.FindEntityType(marker)!.FindProperty(nameof(IHasConcurrencyToken.ConcurrencyToken));
                if (property?.IsConcurrencyToken == true)
                {
                    continue;
                }

                // a mapped property the wiring left alone is an explicit .IsConcurrencyToken(false): a deliberate opt-out.
                // A property the model does not have at all is not a choice about concurrency — the convention had
                // nothing to attach to, and saying nothing there is how the marker goes quiet.
                if (property != null && owner.ConventionWired)
                {
                    continue;
                }

                var contextName = owner.ContextType.Name;
                var cause = property == null
                    ? $"{contextName}'s model has no ConcurrencyToken property to declare — [NotMapped], an Ignore(...) call, or an explicit interface implementation keeps it out of the model, and a convention cannot reach what is not mapped. " +
                      "ACTION: map the property (drop [NotMapped]/Ignore(...), or implement the member implicitly rather than explicitly)"
                    : "the wiring that declares it never reached this context (a non-generic UseEntities(), or WireDbContext(...) without DbContextWiring.ConcurrencyTokens). " +
                      $"ACTION: register the context with UseEntities<{contextName}>(o => o.UseDefaults()), or add DbContextWiring.ConcurrencyTokens to WireDbContext(...)";

                yield return new EntityValidationIssue(EntityValidationSeverity.Error,
                    $"{marker.Name} implements IHasConcurrencyToken, but {contextName}'s model does not treat ConcurrencyToken as a concurrency token: {cause}. " +
                    $"Nothing is compared, so every write is last-write-wins — 200 OK, no conflict, no log. {SeeAlso}");
            }
        }

        foreach (var mapping in mapped)
        {
            var entityType = FindEntityType(contexts, mapping.EntityType);
            if (entityType == null)
            {
                continue;
            }
            foreach (var token in entityType.GetClientConcurrencyTokens())
            {
                var issue = InspectDtos(mapping, token);
                if (issue != null)
                {
                    yield return issue;
                }
            }
        }

        foreach (var entityClrType in selfBound)
        {
            var entityType = FindEntityType(contexts, entityClrType);
            if (entityType == null)
            {
                continue;
            }
            foreach (var token in entityType.GetClientConcurrencyTokens())
            {
                if (!HoldsATokenWhenNew(entityClrType, token, e => ConcurrencyTokenExtensions.ReadClrValue(token, e)))
                {
                    continue;
                }

                var entity = entityClrType.Name;
                yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                    $"{entity}.{token.Name} is a concurrency token with an initializer, and {entity} is its own input DTO. " +
                    $"A request that leaves the token out — or code that modifies a new {entity} without setting it — carries the initializer's value instead of none, " +
                    "so it answers 409 where an absent token skips the check. " +
                    $"ACTION: remove the initializer from {entity}.{token.Name} and mint the token in a primer instead. {SeeAlso}");
            }
        }
    }

    private static IEntityType? FindEntityType(IEnumerable<InspectedContext> contexts, Type entityType)
        => contexts.Select(c => c.Model.FindEntityType(entityType)).FirstOrDefault(t => t != null);

    private static EntityValidationIssue? InspectDtos(EntityMappingRegistration mapping, IProperty token)
    {
        var entity = mapping.EntityType.Name;
        var name = token.Name;
        var inputDto = mapping.InputDtoType.Name;
        var inputProperty = FindProperty(mapping.InputDtoType, name);
        var readProperty = FindProperty(mapping.DtoType, name);
        var declaration = $"`public {TypeName(token.ClrType)} {name} {{ get; set; }}`";

        if (inputProperty == null && HoldsATokenWhenNew(mapping.EntityType, token, e => ConcurrencyTokenExtensions.ReadClrValue(token, e)))
        {
            return new EntityValidationIssue(EntityValidationSeverity.Error,
                $"{entity}.{name} is a concurrency token with an initializer, and {inputDto} has no {name} property. " +
                $"The mapper builds a fresh {entity} for every request, so every PUT and PATCH carries a token the row never held and answers 409. " +
                $"ACTION: remove the initializer from {entity}.{name} (mint the token in a primer instead) and add {declaration} to {inputDto}" +
                (readProperty == null && mapping.DtoType != mapping.InputDtoType ? $" and {mapping.DtoType.Name}" : "") + $". {SeeAlso}");
        }

        if (inputProperty != null && HoldsATokenWhenNew(mapping.InputDtoType, token, dto => inputProperty.GetValue(dto)))
        {
            return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{inputDto}.{name} has an initializer, and {name} is {entity}'s concurrency token. " +
                "A client that leaves the token out sends that value instead of none, so its write answers 409 where an absent token skips the check. " +
                $"ACTION: remove the initializer from {inputDto}.{name}. {SeeAlso}");
        }

        var missingOn = new[] { inputProperty == null ? mapping.InputDtoType : null, readProperty == null ? mapping.DtoType : null }
            .OfType<Type>()
            .Distinct()
            .Select(t => t.Name)
            .ToArray();
        if (missingOn.Length > 0)
        {
            var dtos = string.Join(" and ", missingOn);
            return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{entity}.{name} is a concurrency token, but {dtos} {(missingOn.Length == 1 ? "has" : "have")} no {name} property. " +
                "The client never gets the version it read back to the server, so every write through the entity controller is last-write-wins: 200 OK, no conflict, no log. " +
                $"ACTION: add {declaration} to {dtos}, without an initializer. {SeeAlso}");
        }

        return null;
    }

    /// <summary>
    /// Whether the save interceptor would run <see cref="HasConcurrencyTokenDbPrimer"/> or a subclass. The primers come
    /// from the interceptor's own discovery, so an instance or factory registration counts exactly as it does when a
    /// save runs — a descriptor's declared type alone would miss both.
    /// </summary>
    private static (bool Mints, string? Error) MintsConcurrencyTokens(IServiceProvider provider, IServiceCollection services)
    {
        try
        {
            return (PrimerDiscovery.GetPrimers(provider, services).Any(p => p is HasConcurrencyTokenDbPrimer), null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Whether a freshly constructed <paramref name="type"/> already carries a token — the value the mapper leaves
    /// in place when nothing overwrites it. A type without a usable parameterless constructor is not judged.
    /// </summary>
    private static bool HoldsATokenWhenNew(Type type, IProperty token, Func<object, object?> read)
    {
        object? value;
        try
        {
            var instance = Activator.CreateInstance(type, nonPublic: true);
            if (instance == null)
            {
                return false;
            }
            value = read(instance);
        }
        catch
        {
            return false;
        }

        if (value == null)
        {
            return false;
        }
        // a DTO may carry the token in another shape (a base64 string for a rowversion) — judge that by its own default
        var tokenType = Nullable.GetUnderlyingType(token.ClrType) ?? token.ClrType;
        return tokenType.IsInstanceOfType(value)
            ? ConcurrencyTokenExtensions.IsSupplied(token, value)
            : !(value is string { Length: 0 } or Array { Length: 0 }
                || (value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType()))));
    }

    private static PropertyInfo? FindProperty(Type type, string name)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.GetIndexParameters().Length == 0);

    private static string TypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return TypeName(underlying) + "?";
        }
        return type == typeof(byte[]) ? "byte[]"
            : type == typeof(int) ? "int"
            : type == typeof(uint) ? "uint"
            : type == typeof(long) ? "long"
            : type == typeof(ulong) ? "ulong"
            : type == typeof(string) ? "string"
            : type.Name;
    }

    private static (InspectedContext? Context, string? Error) Inspect(IServiceProvider provider, Type contextType)
    {
        try
        {
            // Building a model needs no connection; resolving a context still can throw (a missing provider,
            // a throwing factory) — a diagnostic must never take the host down for its own inspection.
            var dbContext = (DbContext)provider.GetRequiredService(contextType);
            var wired = dbContext.GetService<IDbContextOptions>().FindExtension<ConcurrencyTokenOptionsExtension>() != null;
            return (new InspectedContext(contextType, dbContext.Model, wired), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
