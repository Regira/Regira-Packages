namespace Regira.Entities.Mediator;

/// <summary>The operation an entity request performs. <see cref="Save"/>, <see cref="Patch"/> and <see cref="Delete"/> write.</summary>
public enum EntityOperation
{
    Details,
    List,
    Search,
    Save,
    Patch,
    Delete
}
