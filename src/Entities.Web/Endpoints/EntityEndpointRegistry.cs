using System.Text;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// What <c>MapEntityEndpoints()</c> and <c>MapEntity&lt;TEntity&gt;()</c> mapped, for the startup checks — the DTO
/// checks, a missing <c>UseAttachmentUris()</c>, an entity served by a controller too.
/// </summary>
internal sealed class EntityEndpointRegistry
{
    private readonly List<MappedEntity> _entities = [];
    private readonly HashSet<Type> _namedDownloads = [];

    public IReadOnlyList<MappedEntity> Entities
    {
        get
        {
            lock (_entities)
            {
                return [.. _entities];
            }
        }
    }

    public void Add(MappedEntity entity)
    {
        lock (_entities)
        {
            _entities.Add(entity);
        }
    }

    /// <summary>
    /// <c>true</c> the first time an attachment link's downloads are mapped. Endpoint names are global, so a link mapped
    /// twice — its owner on a second route through <c>MapEntity&lt;TEntity&gt;()</c> — has only its first downloads named,
    /// and the attachment <c>Uri</c> links to those.
    /// </summary>
    public bool ClaimDownloadNames(Type attachmentType)
    {
        lock (_namedDownloads)
        {
            return _namedDownloads.Add(attachmentType);
        }
    }
}

/// <summary>One mapped entity; <c>Attachments</c> is the link served under its route, with its DTO pair, or <c>null</c>.</summary>
internal sealed record MappedEntity(Type EntityType, string Route, Type DtoType, Type InputDtoType, MappedAttachments? Attachments);

internal sealed record MappedAttachments(Type AttachmentType, Type DtoType, Type InputDtoType);

internal static class EntityEndpointNames
{
    /// <summary>The download by id of an attachment link type, which <c>AttachmentUriResolver</c> links to.</summary>
    public static string GetFile(Type attachmentType) => $"{attachmentType.Name}.GetFile";
    /// <summary>The download by owner and file name of an attachment link type.</summary>
    public static string GetFileByName(Type attachmentType) => $"{attachmentType.Name}.GetFileByName";

    /// <summary>
    /// The conventional route of an entity: the kebab-case plural of its type name — <c>Product</c> → <c>products</c>,
    /// <c>InterventionType</c> → <c>intervention-types</c>, <c>Category</c> → <c>categories</c>, <c>Address</c> → <c>addresses</c>.
    /// An irregular noun needs <c>EntityEndpointOptions.Route</c>.
    /// </summary>
    public static string Route(Type entityType)
    {
        var name = entityType.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        var kebab = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            // a word starts at an upper-case letter after a lower-case one or a digit, or at the last capital of an acronym
            var startsWord = char.IsUpper(c) && i > 0
                && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) || i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]));
            if (startsWord)
            {
                kebab.Append('-');
            }
            kebab.Append(char.ToLowerInvariant(c));
        }
        return Pluralize(kebab.ToString());
    }

    private static string Pluralize(string word)
    {
        if (word.Length > 1 && word.EndsWith('y') && !"aeiou".Contains(word[^2]))
        {
            return word[..^1] + "ies";
        }
        if (word.EndsWith('s') || word.EndsWith('x') || word.EndsWith('z') || word.EndsWith("ch") || word.EndsWith("sh"))
        {
            return word + "es";
        }
        return word + "s";
    }
}
