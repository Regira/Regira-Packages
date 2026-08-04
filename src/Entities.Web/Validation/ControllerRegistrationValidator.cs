#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Regira.Entities.Attachments;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Attachments.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;

namespace Regira.Entities.Web.Validation;

/// <summary>
/// Verifies at startup that every controller deriving from an <c>EntityControllerBase&lt;...&gt;</c> variant
/// has the exact closed <c>IEntityService&lt;...&gt;</c> registrations its endpoints resolve at request time —
/// an arity mismatch between <c>For&lt;&gt;()</c> and the controller's generic arguments then fails
/// <c>dotnet run</c> with the explanatory message instead of a request-time 500.
/// Registered by <c>UseEntities()</c> (late-bound, Development-only by default) or explicitly via
/// <c>ValidateEntityControllers()</c>.
/// </summary>
public sealed class ControllerRegistrationValidator(ApplicationPartManager? partManager = null) : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        if (partManager == null)
        {
            yield break; // MVC is not configured — nothing to validate
        }

        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);

        var anyEntityControllers = false;
        foreach (var controller in feature.Controllers)
        {
            foreach (var requiredService in GetRequiredEntityServices(controller.AsType()))
            {
                anyEntityControllers = true;
                if (!EntityServiceDiagnostics.IsRegistered(context.Services, requiredService))
                {
                    yield return new EntityValidationIssue(EntityValidationSeverity.Error,
                        $"{controller.Name}: {EntityServiceDiagnostics.DescribeMissingService(requiredService, context.Services)}");
                }
            }
        }

        if (anyEntityControllers && !context.Services.Any(d => d.ServiceType == typeof(IEntityMapper)))
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Error,
                "Entity controllers map responses through IEntityMapper, but no mapper is registered. " +
                "Configure one via UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper().");
        }

        // Mapping an attachment controller is the statement that clients download these files over HTTP, so a
        // null Uri resolver is a wiring slip, not a choice: every attachment DTO ships Uri = null and an <img>
        // or download link built from it silently requests nothing.
        var attachmentsWithoutUris = feature.Controllers
            .SelectMany(c => GetAttachmentEntities(c.AsType()))
            .Distinct()
            .Where(entity => IsNullUriResolver(context, entity))
            .Select(entity => entity.Name)
            .ToArray();

        if (attachmentsWithoutUris.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"Attachment DTOs will have a null Uri for: {string.Join(", ", attachmentsWithoutUris)}. " +
                "Their controllers are mapped but UseAttachmentUris() was not called on the same UseEntities options instance, " +
                "so the null resolver is in place. Add options.UseAttachmentUris() (plus AddHttpContextAccessor()), " +
                "or have clients build download links from the {objectId}/files/{fileName} route themselves.");
        }
    }

    /// <summary>The <c>TEntity</c> of every <c>EntityAttachmentControllerBase&lt;...&gt;</c> a controller derives from.</summary>
    internal static IEnumerable<Type> GetAttachmentEntities(Type controllerType)
    {
        for (var type = controllerType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (!type.IsGenericType)
            {
                continue;
            }
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(EntityAttachmentControllerBase<>) || definition == typeof(EntityAttachmentControllerBase<,,>))
            {
                yield return type.GetGenericArguments()[0];
                yield break;
            }
        }
    }

    private static bool IsNullUriResolver(EntityValidationContext context, Type attachmentEntity)
    {
        var resolverType = typeof(IAttachmentUriResolver<>).MakeGenericType(attachmentEntity);
        object? resolver;
        try
        {
            resolver = context.Provider.GetService(resolverType);
        }
        catch (Exception)
        {
            // Resolving the real resolver pulls in LinkGenerator/IHttpContextAccessor; if that fails the app has
            // a wiring problem the service checks above already surface. A validator must never fail startup on
            // its own diagnostics.
            return false;
        }

        // Absent is not a finding either: without attachment DTO mapping there is no Uri to resolve. Neither is
        // a custom resolver, whose natural spelling (`class MyResolver : AttachmentUriResolver<ProductAttachment>`)
        // is a non-generic type — hence the IsGenericType guard before GetGenericTypeDefinition().
        return resolver?.GetType() is { IsGenericType: true } resolverImplementation
               && resolverImplementation.GetGenericTypeDefinition() == typeof(NullAttachmentUriResolver<>);
    }

    /// <summary>
    /// The closed IEntityService registrations a controller's endpoints resolve: the simple base needs
    /// <c>IEntityService&lt;TEntity, TKey&gt;</c>; the complex base additionally needs the 5-arity service
    /// for its List/Search endpoints.
    /// </summary>
    internal static IEnumerable<Type> GetRequiredEntityServices(Type controllerType)
    {
        for (var type = controllerType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (!type.IsGenericType)
            {
                continue;
            }
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (definition == typeof(EntityControllerBase<,,,,>))
            {
                // TEntity, TKey, TSearchObject, TDto, TInputDto
                yield return typeof(IEntityService<,>).MakeGenericType(args[0], args[1]);
                yield break;
            }
            if (definition == typeof(EntityControllerBase<,,,,,,>))
            {
                // TEntity, TKey, TSo, TSortBy, TIncludes, TDto, TInputDto
                yield return typeof(IEntityService<,>).MakeGenericType(args[0], args[1]);
                yield return typeof(IEntityService<,,,,>).MakeGenericType(args[0], args[1], args[2], args[3], args[4]);
                yield break;
            }
        }
    }
}
#endif
