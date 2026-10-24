using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IO.Testing.Helpers;

/// <summary>
/// An HTTP endpoint on the loopback address that records the request line and the headers of every request, so a test
/// reads which address a client asked for. It answers <c>404 Not Found</c>, or a <c>GET</c> with
/// <c>200 OK</c> and the JSON body it was given, with a <c>Set-Cookie</c> header when it was given one.
/// Given a function instead, it answers each request with the <c>200 OK</c> the function returns for it, or <c>404 Not Found</c> for <c>null</c>.
/// </summary>
public sealed class RecordingHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<RecordedRequest, (string ContentType, byte[] Body)?> _answer;
    private readonly string? _setCookie;
    private readonly Task _accepting;

    public RecordingHttpServer(string? getBody = null, string? setCookie = null)
        : this(r => r.Method == "GET" && getBody != null ? ("application/json", Encoding.UTF8.GetBytes(getBody)) : null, setCookie)
    {
    }
    public RecordingHttpServer(Func<RecordedRequest, (string ContentType, byte[] Body)?> answer, string? setCookie = null)
    {
        _answer = answer;
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

            var request = new RecordedRequest(requestLine[0], requestLine[1], headers);
            Requests.Enqueue(request);
            var answer = _answer(request);
            var (status, content) = answer != null ? ("200 OK", answer.Value.Body) : ("404 Not Found", []);
            var contentType = content.Length > 0 ? $"Content-Type: {answer!.Value.ContentType}\r\n" : "";
            var cookie = _setCookie != null ? $"Set-Cookie: {_setCookie}\r\n" : "";
            var responseHead = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{contentType}{cookie}Content-Length: {content.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseHead);
            await stream.WriteAsync(content);
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
