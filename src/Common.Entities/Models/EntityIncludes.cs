namespace Regira.Entities.Models;

/// <summary>
/// Always define value All as last value
/// </summary>
[Flags]
public enum EntityIncludes
{
    Default = 0,
    All = 1
}