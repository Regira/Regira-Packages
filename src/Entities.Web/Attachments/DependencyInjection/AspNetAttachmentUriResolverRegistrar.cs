using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Attachments.Services;

namespace Regira.Entities.Web.Attachments.DependencyInjection;

/// <summary>
/// ASP.NET Core implementation of <see cref="IAttachmentUriResolverRegistrar"/> that registers the
/// <see cref="AttachmentUriResolver{TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment}"/>
/// (using <see cref="LinkGenerator"/> + <see cref="IHttpContextAccessor"/>) for attachment DTO Uri resolution.
/// </summary>
public class AspNetAttachmentUriResolverRegistrar : IAttachmentUriResolverRegistrar
{
    public void Register<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(IServiceCollection services)
        where TEntityAttachment : class, IEntity<TEntityAttachmentKey>, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
        => services.AddTransient<IAttachmentUriResolver<TEntityAttachment>>(p =>
            new AttachmentUriResolver<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(
                p.GetRequiredService<LinkGenerator>(),
                p.GetRequiredService<IHttpContextAccessor>(),
                p.GetService<ILoggerFactory>()?.CreateLogger<AttachmentUriResolver<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>>()
            ));
}

public static class AttachmentUriServiceCollectionExtensions
{
    /// <summary>
    /// Enables ASP.NET Core based attachment Uri resolution. When set, attachment DTOs mapped via
    /// <c>HasAttachments(...)</c> get their <c>Uri</c> populated using <see cref="LinkGenerator"/> and the current
    /// <see cref="IHttpContextAccessor"/>. Call this before registering entities (e.g. at the top of the
    /// <c>UseEntities</c> options block). Requires <c>AddHttpContextAccessor()</c> on the host.
    /// <para>
    /// The <c>Uri</c> links to the <c>GetFile</c> action on a controller named after <c>TEntityAttachment</c> —
    /// a <c>ProductAttachment</c> resolves against <c>ProductAttachmentController</c> or
    /// <c>ProductAttachmentsController</c>, either deriving from
    /// <c>EntityAttachmentControllerBase&lt;ProductAttachment&gt;</c>. Any other class name is invisible to the
    /// link generator.
    /// </para>
    /// <para>
    /// The <c>Uri</c> is <see langword="null"/> — never an error — when: this option is omitted (the default
    /// <c>NullAttachmentUriResolver</c> applies); it is set on a different <c>UseEntities</c> options instance
    /// than the one the entity was registered on; no controller under either name above is mapped, or its
    /// download route was replaced by a custom endpoint; or there is no active request (seeding, background
    /// work). Only the controller-lookup failure is diagnosable at runtime — it logs a warning naming the
    /// controller names it tried, once per attachment type.
    /// </para>
    /// </summary>
    public static EntityServiceCollectionOptions UseAttachmentUris(this EntityServiceCollectionOptions options)
    {
        options.AttachmentUriRegistrar = new AspNetAttachmentUriResolverRegistrar();
        return options;
    }
}
