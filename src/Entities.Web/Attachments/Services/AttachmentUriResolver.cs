using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Web.Attachments.Services;

public class AttachmentUriResolver<TEntityAttachment>(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor, ILogger? logger = null)
    : AttachmentUriResolver<TEntityAttachment, int, int, int, Attachment>(linkGenerator, httpContextAccessor, logger)
    where TEntityAttachment : IEntityAttachment<int, int, int, Attachment>;


public class AttachmentUriResolver<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor, ILogger? logger = null)
    : IAttachmentUriResolver<TEntityAttachment>
    where TEntityAttachment : IEntity<TEntityAttachmentKey>, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    /// <summary>
    /// MVC derives a controller's route value from its class name minus the <c>Controller</c> suffix. Both the
    /// entity type name and its conventional plural are tried, so <c>ProductAttachmentController</c> and
    /// <c>ProductAttachmentsController</c> both resolve.
    /// </summary>
    private static readonly string[] ControllerNames = [typeof(TEntityAttachment).Name, $"{typeof(TEntityAttachment).Name}s"];

    private static int _unresolvedLogged;

    public virtual string? Resolve(TEntityAttachment source)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;// outside an active request (seeding, background work) — expected, not a misconfiguration
        }

        var scheme = httpContext.Request.Scheme;
        var host = httpContext.Request.Host;
        var path = httpContext.Request.PathBase;

        object values = !string.IsNullOrWhiteSpace(source.Attachment?.FileName)
            ? new { objectId = source.ObjectId, filename = source.Attachment!.FileName, inline = true }
            : new { id = source.Id, inline = true };

        foreach (var controller in ControllerNames)
        {
            var uri = linkGenerator.GetUriByAction(
                action: "GetFile",
                controller: controller,
                values: values,
                scheme: scheme,
                host: host,
                pathBase: path);
            if (uri != null)
            {
                return uri;
            }
        }

        LogUnresolved();
        return null;
    }

    /// <summary>
    /// A null Uri is not an error (the DTO stays valid), so resolution failure is reported once per attachment type
    /// rather than per row — without it the misconfiguration is invisible.
    /// </summary>
    private void LogUnresolved()
    {
        if (logger == null || Interlocked.Exchange(ref _unresolvedLogged, 1) == 1)
        {
            return;
        }

        logger.LogWarning(
            "Attachment Uri stays null for {AttachmentType}: no mapped controller named \"{Controller}Controller\" or \"{ControllerPlural}Controller\" exposes a \"GetFile\" action. Name the attachment controller after the entity type and map it, or omit UseAttachmentUris() and compose download links from the attachment route.",
            typeof(TEntityAttachment).Name, ControllerNames[0], ControllerNames[1]);
    }
}
