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
        if (!string.IsNullOrWhiteSpace(_config.HostKeyFingerprint))
        {
            var expected = _config.HostKeyFingerprint;
            _client.HostKeyReceived += (_, e) => e.CanTrust = MatchesFingerprint(expected, e.FingerPrintSHA256);
        }
    }

    /// <summary>
    /// Compares a configured SHA-256 fingerprint, with or without its <c>SHA256:</c> prefix and padding, to the one the
    /// server presents (unpadded base64, as SSH.NET reports it). Base64 is case-sensitive, so the comparison is too.
    /// </summary>
    protected internal static bool MatchesFingerprint(string expected, string presented)
    {
        var fingerprint = expected.Trim();
        if (fingerprint.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            fingerprint = fingerprint["SHA256:".Length..];
        }
        return string.Equals(fingerprint.TrimEnd('='), presented.TrimEnd('='), StringComparison.Ordinal);
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