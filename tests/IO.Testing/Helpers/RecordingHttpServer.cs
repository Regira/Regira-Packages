using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IO.Testing.Helpers;

/// <summary>
/// An HTTP endpoint on the loopback address that records the request line and the headers of every request, so a test
/// reads which address a client asked for. It answers <c>404 Not Found</c>, or a <c>GET</c> with
/// <c>200 OK</c> and the JSON body it was given, with a <c>Set-Cookie</c> header when it was given one.
/// </summary>
public sealed class RecordingHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly string? _getBody;
    private readonly string? _setCookie;
    private readonly Task _accepting;

    public RecordingHttpServer(string? getBody = null, string? setCookie = null)
    {
        _getBody = getBody;
        _setCookie = setCookie;
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _accepting = Accept();
    }

    public Uri BaseUri { get; }
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    private async Task Accept()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                // DisposeAsync cancels and then stops the listener: an accept that starts in between
                // fails with ObjectDisposedException or SocketException instead of OperationCanceledException.
                return;
            }
            _ = Answer(client);
        }
    }

    private async Task Answer(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            var head = await ReadHead(stream);
            var lines = head.Split("\r\n");
            var requestLine = lines[0].Split(' ');
            var headers = lines.Skip(1)
                .Select(l => l.Split(':', 2))
                .Where(p => p.Length == 2)
                .GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(p => p[1].Trim())), StringComparer.OrdinalIgnoreCase);

            if (headers.TryGetValue("Content-Length", out var length))
            {
                var remaining = int.Parse(length);
                var buffer = new byte[4096];
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)));
                    if (read == 0) break;
                    remaining -= read;
                }
            }

            Requests.Enqueue(new RecordedRequest(requestLine[0], requestLine[1], headers));
            var (status, body) = requestLine[0] == "GET" && _getBody != null ? ("200 OK", _getBody) : ("404 Not Found", "");
            var content = Encoding.UTF8.GetBytes(body);
            var contentType = content.Length > 0 ? "Content-Type: application/json\r\n" : "";
            var cookie = _setCookie != null ? $"Set-Cookie: {_setCookie}\r\n" : "";
            var responseHead = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{contentType}{cookie}Content-Length: {content.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseHead.Concat(content).ToArray());
        }
    }

    private static async Task<string> ReadHead(NetworkStream stream)
    {
        var head = new List<byte>();
        var buffer = new byte[1];
        while (await stream.ReadAsync(buffer) == 1)
        {
            head.Add(buffer[0]);
            if (head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n')
            {
                break;
            }
        }
        return Encoding.ASCII.GetString(head.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        await _accepting;
        _stop.Dispose();
    }

    /// <param name="Target">The request target as sent: the path and query, percent-escaped.</param>
    public record RecordedRequest(string Method, string Target, IReadOnlyDictionary<string, string> Headers)
    {
        public string? Authorization => Headers.GetValueOrDefault("Authorization");
    }
}
