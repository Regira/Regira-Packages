using System.Net.Http.Headers;
using System.Reflection;

namespace Regira.IO.Storage.GitHub;

public class GitHubCommunicator : IDisposable
{
    public string Root { get; }
    internal HttpClient Client { get; }
    internal string Branch { get; }
    internal string? CommitMessage { get; }

    public GitHubCommunicator(GitHubOptions options)
    {
        Root = string.IsNullOrEmpty(options.ContentPath)
            ? options.Uri.TrimEnd('/') + "/contents/"
            : options.Uri.TrimEnd('/') + "/contents/" + options.ContentPath!.Trim('/') + "/";

        Branch = options.Branch;
        CommitMessage = options.CommitMessage;

        Client = new HttpClient { BaseAddress = new Uri(Root) };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent ?? Assembly.GetExecutingAssembly().GetName().Name);
        Client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        if (!string.IsNullOrEmpty(options.Key))
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Key);
        }
    }

    public void Dispose()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}
