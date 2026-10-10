namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// One endpoint of an entity's mapped set (<c>MapEntityEndpoints()</c>), to exclude it or open it anonymously
/// (<see cref="EntityEndpointOptions"/>). The attachment endpoints map under their owner's route.
/// </summary>
public enum EntityEndpoint
{
    /// <summary><c>GET {id}</c></summary>
    Details,
    /// <summary><c>GET</c>, and <c>POST list</c> on a complex entity</summary>
    List,
    /// <summary><c>GET search</c>, and <c>POST search</c> on a complex entity</summary>
    Search,
    /// <summary><c>POST save</c></summary>
    Save,
    /// <summary><c>POST</c></summary>
    Create,
    /// <summary><c>PUT {id}</c></summary>
    Modify,
    /// <summary><c>PATCH {id}</c></summary>
    Patch,
    /// <summary><c>DELETE {id}</c></summary>
    Delete,
    /// <summary><c>GET attachments/{id}</c></summary>
    AttachmentDetails,
    /// <summary><c>GET attachments</c> and <c>GET {objectId}/attachments</c></summary>
    AttachmentList,
    /// <summary><c>PUT {objectId}/attachments/{id}</c></summary>
    AttachmentUpdate,
    /// <summary><c>DELETE attachments/{id}</c></summary>
    AttachmentDelete,
    /// <summary><c>GET files/{id}</c> and <c>GET {objectId}/files/{*fileName}</c></summary>
    Download,
    /// <summary><c>POST {objectId}/files</c></summary>
    Upload,
    /// <summary><c>PUT {objectId}/files/{id}</c></summary>
    ReplaceFile
}

/// <summary>
/// Endpoint metadata on every mapped entity endpoint: the entity whose route it serves — the owner, for an attachment
/// endpoint — and which endpoint it is. An authorization handler reads it from <c>HttpContext.GetEndpoint()</c> to gate
/// writes per entity.
/// </summary>
public sealed record EntityEndpointMetadata(Type EntityType, EntityEndpoint Endpoint)
{
    /// <summary>Whether the endpoint writes; the <c>POST list</c> and <c>POST search</c> overloads read.</summary>
    public bool IsWrite => Endpoint is not (EntityEndpoint.Details or EntityEndpoint.List or EntityEndpoint.Search
        or EntityEndpoint.AttachmentDetails or EntityEndpoint.AttachmentList or EntityEndpoint.Download);
}
