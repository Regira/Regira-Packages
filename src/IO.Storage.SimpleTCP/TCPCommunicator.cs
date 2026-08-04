using SimpleTCP;

namespace Regira.IO.Storage.SimpleTCP;

public class TCPCommunicator(TCPConfig config) : IDisposable
{
    private readonly SimpleTcpClient _client = new();
    private bool _isOpen;

    protected internal SimpleTcpClient Open()
    {
        if (!_isOpen)
        {
            _client.Connect(config.Host, config.Port);
            _isOpen = true;
        }
        return _client;
    }
    protected internal SimpleTcpClient Close()
    {
        _isOpen = false;
        return _client.Disconnect();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}