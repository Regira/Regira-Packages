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
    /// <summary>
    /// Refuses an identifier that addresses a blob outside the container, through <c>../</c>, say (default). The SDK
    /// resolves the dot segments of a blob's address, so without the check <c>../other/file.pdf</c> names a blob in the
    /// container <c>other</c> of the same account.
    /// </summary>
    public bool Contained { get; set; } = true;
}