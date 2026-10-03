namespace Regira.IO.Storage.SSH;

public class SftpConfig
{
    public string Host { get; set; } = null!;
    public int Port { get; set; } = 22;
    public string UserName { get; set; } = null!;
    public string? Password { get; set; }
    /// <summary>
    /// The server's SHA-256 host key fingerprint, as <c>ssh-keygen -lf</c> prints it (<c>SHA256:…</c>; the prefix is
    /// optional). The connection is refused when the server presents another key. Left empty, any host key is
    /// accepted, which leaves the connection open to an impersonating server.
    /// </summary>
    public string? HostKeyFingerprint { get; set; }
    public string? ContainerName { get; set; } = "/";
    public bool Contained { get; set; } = true;
}