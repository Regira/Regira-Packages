namespace Regira.IO.Storage.SSH;

public class SftpConfig
{
    public string Host { get; set; } = null!;
    public int Port { get; set; } = 22;
    public string UserName { get; set; } = null!;
    public string? Password { get; set; }
    public string? ContainerName { get; set; } = "/";
    public bool Contained { get; set; } = true;
}