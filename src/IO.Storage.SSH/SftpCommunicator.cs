using Renci.SshNet;

namespace Regira.IO.Storage.SSH;

public class SftpCommunicator : IDisposable
{
    private readonly SftpConfig _config;
    private readonly SftpClient _client;
    internal string ContainerName => $"/{_config.ContainerName?.TrimStart('/')}";
    internal bool Contained => _config.Contained;
    public SftpCommunicator(SftpConfig config)
    {
        _config = config;
        _client = new SftpClient(_config.Host, _config.Port, _config.UserName, _config.Password ?? string.Empty);
    }

    protected internal Task<SftpClient> Open()
    {
        if (!_client.IsConnected)
        {
            _client.Connect();
        }
        return Task.FromResult(_client);
    }
    protected internal Task Close()
    {
        _client.Disconnect();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_client.IsConnected)
        {
            _client.Disconnect();
        }
        _client.Dispose();
    }
}