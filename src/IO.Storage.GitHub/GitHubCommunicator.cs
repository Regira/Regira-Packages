using System.Net.Http.Headers;
using System.Reflection;

namespace Regira.IO.Storage.GitHub;

public class GitHubCommunicator : IDisposable
{
    // every instance shares one connection pool, recycled so a change to the API's DNS reaches a long-running process;
    // no cookies, so nothing one instance receives rides on another's requests
    private static readonly SocketsHttpHandler SharedHandler = new() { PooledConnectionLifetime = TimeSpan.FromMinutes(2), UseCookies = false };

    public string Root { get; }
    internal Uri RootUri { get; }
    /// <summary>The git blobs API of the repository, where a file's content is read by its sha.</summary>
    internal string BlobsRoot { get; }
    internal string Branch { get; }
    internal string? CommitMessage { get; }
    internal bool Contained { get; }

    private readonly string _userAgent;
    private readonly string? _key;

    public GitHubCommunicator(GitHubOptions options)
    {
        Root = string.IsNullOrEmpty(options.ContentPath)
            ? options.Uri.TrimEnd('/') + "/contents/"
            : options.Uri.TrimEnd('/') + "/contents/" + options.ContentPath!.Trim('/') + "/";
        RootUri = new Uri(Root);
        BlobsRoot = options.Uri.TrimEnd('/') + "/git/blobs/";

        Branch = options.Branch;
        CommitMessage = options.CommitMessage;
        Contained = options.Contained;

        _userAgent = options.UserAgent ?? Assembly.GetExecutingAssembly().GetName().Name!;
        // an invalid user agent fails here rather than on the first request
        new HttpRequestMessage().Headers.UserAgent.ParseAdd(_userAgent);
        _key = options.Key;
    }
    internal Task<HttpResponseMessage> GetAsync(string uri)
        => SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));
    internal Task<HttpResponseMessage> PutAsync(string uri, HttpContent content)
        => SendAsync(new HttpRequestMessage(HttpMethod.Put, uri) { Content = content });
    internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        request.Headers.UserAgent.ParseAdd(_userAgent);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        if (!string.IsNullOrEmpty(_key))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        }
        return new HttpClient(SharedHandler, disposeHandler: false).SendAsync(request);
    }

    public void Dispose()
    {
        // every request takes its client from the shared pool, so there is nothing of its own to release
        GC.SuppressFinalize(this);
    }
}
