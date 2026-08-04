namespace Regira.IO.Storage.FileSystem;

/// <summary>
/// Options for a credential-protected network share (UNC path), used by <see cref="NetworkShareCommunicator"/>.
/// <see cref="FileSystemOptions.RootFolder"/> must be a UNC path (e.g. <c>\\server\share\folder</c>).
/// </summary>
public class NetworkFileSystemOptions : FileSystemOptions
{
    public string UserName { get; set; } = string.Empty;
    public string? Password { get; set; }
    /// <summary>
    /// Optional domain — alternatively embed it in <see cref="UserName"/> (<c>DOMAIN\user</c>).
    /// </summary>
    public string? Domain { get; set; }
}
