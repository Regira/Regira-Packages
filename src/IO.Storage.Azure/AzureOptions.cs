namespace Regira.IO.Storage.Azure;

public class AzureOptions
{
    public string? ContainerName { get; set; }
    public string? ConnectionString { get; set; }
    /// <summary>
    /// Creates the container when it does not exist yet (default).
    /// Set to false to fail fast on a missing container (e.g. to catch misconfigured names).
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;
}