using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.Utilities;
using Regira.Serializing.Abstractions;
using Regira.Web.Utilities;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Regira.IO.Storage.GitHub;

/// <summary>
/// Based on https://docs.github.com/en/rest/repos/contents?apiVersion=2022-11-28
/// </summary>
public class GitHubService(GitHubCommunicator communicator, ISerializer serializer) : IFileService
{
    public string Root => communicator.Root;

    // GitHub's contents API is eventually consistent: a GET right after a write can
    // still 404 (or serve the pre-write blob), which used to make GetFileSha return
    // null and break the next write (update -> 422 "sha wasn't supplied"; delete ->
    // silent no-op). Writes return the authoritative new SHA, so cache it per
    // (branch, path) and prefer it over the racy read-back.
    private readonly ConcurrentDictionary<string, string> _shaCache = new();

    public async Task<bool> Exists(string identifier)
    {
        var uri = GetAbsoluteUri(identifier);
        var response = await communicator.GetAsync(uri);
        return response.IsSuccessStatusCode;
    }
    public async Task<byte[]?> GetBytes(string identifier)
    {
        var uri = GetAbsoluteUri(identifier);
        var response = await communicator.GetAsync(uri);
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync();
        var item = serializer.Deserialize<GitHubItem>(json);
        if (item?.Content == null)
            return null;
        return Convert.FromBase64String(item.Content.Replace("\n", ""));
    }
    public async Task<Stream?> GetStream(string identifier)
    {
        var bytes = await GetBytes(identifier);
        return bytes != null ? new MemoryStream(bytes) : null;
    }
    /// <inheritdoc />
    /// <remarks>The contents API gives a file's size but no time, so <see cref="StoredFileInfo.LastModified"/> is <c>null</c>.</remarks>
    public async Task<StoredFileInfo?> GetInfo(string identifier)
    {
        var uri = GetAbsoluteUri(identifier);
        var response = await communicator.GetAsync(uri);
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync();
        // a folder answers with the list of its items
        if (json.TrimStart().StartsWith('['))
            return null;
        var item = serializer.Deserialize<GitHubItem>(json);
        return item?.Type == GitHubItemType.File ? new StoredFileInfo(item.Size, null) : null;
    }
    public async Task<IEnumerable<string>> List(FileSearchObject? so = null)
    {
        var listUri = GetAbsoluteUri(so?.FolderUri ?? string.Empty);
        var response = await communicator.GetAsync(listUri);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var items = serializer.Deserialize<GitHubItem[]>(content)!;

        var files = new List<string>();
        foreach (var item in items)
        {
            if (item.Type == GitHubItemType.Dir)
            {
                if (so == null || so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Directories)
                {
                    if (so?.Extensions == null)
                    {
                        var folder = item.Url.Split('?').First().TrimEnd('/') + "/";
                        files.Add(folder);
                    }
                }
                if (so?.Recursive == true)
                {
                    var dirSo = new FileSearchObject
                    {
                        FolderUri = $"{so.FolderUri}/{item.Name}".Trim(@"\/".ToCharArray()),
                        Type = so.Type,
                        Extensions = so.Extensions,
                        Recursive = true
                    };
                    var dirFiles = await List(dirSo);
                    files.AddRange(dirFiles);
                }
            }

            if (item.Type == GitHubItemType.File)
            {
                if (so == null || so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Files)
                {
                    if (so == null || (so.Extensions?.Count ?? 0) == 0 || so.Extensions?.Any(e => item.Name.EndsWith(e, StringComparison.InvariantCultureIgnoreCase)) == true)
                    {
                        var identifier = GetIdentifier(item.Url);
                        files.Add(identifier);
                    }
                }
            }
        }

        return files;
    }
#if NET10_0_OR_GREATER
    public async IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null)
    {
        foreach (var identifier in await List(so))
        {
            yield return identifier;
        }
    }
#endif

    /// <summary>The address every request for <paramref name="identifier"/> goes to: below <see cref="Root"/>, a <c>?ref=</c> query kept.</summary>
    /// <exception cref="UnauthorizedAccessException">Contained, the address leaves <see cref="Root"/>.</exception>
    public string GetAbsoluteUri(string identifier)
    {
        // a # is part of a file's name, where a URL would read it as the start of a fragment and request a shorter name
        var uri = FileNameUtility.GetUri(identifier, Root).Replace("#", "%23");
        if (communicator.Contained)
        {
            UrlPath.EnsureContained(new Uri(uri), communicator.RootUri);
        }
        return uri;
    }
    public string GetIdentifier(string uri)
    {
        return FileNameUtility.GetRelativeUri(uri, Root);
    }
    public string? GetRelativeFolder(string identifier)
    {
        return FileNameUtility.GetRelativeFolder(identifier, Root);
    }

    public async Task Move(string sourceIdentifier, string targetIdentifier)
    {
        var bytes = await GetBytes(sourceIdentifier) ?? throw new FileNotFoundException($"Source not found: {sourceIdentifier}");
        await Save(targetIdentifier, bytes);
        await Delete(sourceIdentifier);
    }
    public async Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
    {
        var (path, branch) = GetPathAndBranch(identifier);
        var key = CacheKey(branch, path);
        var content = Convert.ToBase64String(bytes);

        for (var attempt = 0; ; attempt++)
        {
            var sha = _shaCache.TryGetValue(key, out var cached) ? cached : await GetFileSha(identifier);

            var body = new Dictionary<string, object?>
            {
                ["message"] = communicator.CommitMessage ?? $"update {path}",
                ["content"] = content,
                ["branch"] = branch
            };
            if (sha != null)
                body["sha"] = sha;

            var json = serializer.Serialize(body);
            var response = await communicator.PutAsync(GetAbsoluteUri(path), new StringContent(json, Encoding.UTF8, "application/json"));

            // The sha we sent is wrong: GitHub answers 409 (a cached sha outlived an external write) or
            // 422 (a required sha was missing because the read-back raced the previous write). Drop the
            // cache entry and retry once with a freshly fetched sha before surfacing the failure.
            if (IsShaConflict(response.StatusCode) && attempt == 0)
            {
                _shaCache.TryRemove(key, out _);
                continue;
            }

            // Any other failure (or a second conflict) is terminal — evict first so a stale sha can't
            // poison every later write to this path, then surface the error.
            if (!response.IsSuccessStatusCode)
            {
                _shaCache.TryRemove(key, out _);
                response.EnsureSuccessStatusCode();
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var newSha = serializer.Deserialize<GitHubWriteResponse>(responseJson)?.Content?.Sha;
            if (newSha != null)
                Cache(key, newSha);
            else
                _shaCache.TryRemove(key, out _); // response carried no sha — drop rather than keep a stale one

            return identifier;
        }
    }
    public async Task<string> Save(string identifier, Stream stream, string? contentType = null)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return await Save(identifier, ms.ToArray(), contentType);
    }
    public async Task Delete(string identifier)
    {
        var (path, branch) = GetPathAndBranch(identifier);
        var key = CacheKey(branch, path);

        for (var attempt = 0; ; attempt++)
        {
            var sha = _shaCache.TryGetValue(key, out var cached) ? cached : await GetFileSha(identifier);
            if (sha == null)
            {
                _shaCache.TryRemove(key, out _);
                return; // nothing to delete — stays idempotent
            }

            var body = new Dictionary<string, string>
            {
                ["message"] = communicator.CommitMessage ?? $"delete {path}",
                ["sha"] = sha,
                ["branch"] = branch
            };

            var json = serializer.Serialize(body);
            using var request = new HttpRequestMessage(HttpMethod.Delete, GetAbsoluteUri(path))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var response = await communicator.SendAsync(request);

            // A stale cached sha makes GitHub reject the delete with 409 (sha mismatch) or 422 (missing
            // sha). Drop the cache and retry once with a freshly fetched sha — mirroring Save. On the retry
            // a genuinely absent file yields a null sha and we no-op, so Delete stays idempotent.
            if (IsShaConflict(response.StatusCode) && attempt == 0)
            {
                _shaCache.TryRemove(key, out _);
                continue;
            }

            // The file is already gone (deleted out from under our cache): treat as success, not an error,
            // so a double-delete or an externally-deleted file doesn't throw — preserving Delete's no-op contract.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _shaCache.TryRemove(key, out _);
                return;
            }

            response.EnsureSuccessStatusCode();
            _shaCache.TryRemove(key, out _);
            return;
        }
    }

    private async Task<string?> GetFileSha(string identifier)
    {
        var uri = GetAbsoluteUri(identifier);
        var response = await communicator.GetAsync(uri);
        if (!response.IsSuccessStatusCode)
            return null;
        var content = await response.Content.ReadAsStringAsync();
        return serializer.Deserialize<GitHubItem>(content)?.Sha;
    }
    private (string path, string branch) GetPathAndBranch(string identifier)
    {
        var parts = identifier.Replace('\\', '/').Split('?');
        var branch = communicator.Branch;
        if (parts.Length > 1)
        {
            var query = UriUtility.ToQueryDictionary(parts[1]);
            var refBranch = query.Contains("ref") ? query["ref"].FirstOrDefault() : null;
            if (!string.IsNullOrEmpty(refBranch))
                branch = refBranch;
        }
        return (parts[0], branch);
    }
    private static string CacheKey(string branch, string path) => $"{branch}\n{path}";

    // GitHub rejects a write whose sha no longer matches the current blob with 409 Conflict, and a write
    // that omits a required sha with 422 Unprocessable Entity. Both mean "our sha is wrong — refetch it".
    private static bool IsShaConflict(HttpStatusCode status) =>
        status is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity;

    // Upper bound so a long-lived service that writes many distinct paths can't grow the cache without
    // limit. Evicted entries only cost one extra read-back on their next write, so clearing is safe.
    private const int MaxCacheEntries = 10_000;
    private void Cache(string key, string sha)
    {
        if (_shaCache.Count >= MaxCacheEntries && !_shaCache.ContainsKey(key))
            _shaCache.Clear();
        _shaCache[key] = sha;
    }

    // Shape of the PUT/DELETE contents response, where the blob metadata (and its SHA)
    // lives under "content" rather than at the top level like a GET returns.
    private class GitHubWriteResponse
    {
        public GitHubItem? Content { get; set; }
    }
}
