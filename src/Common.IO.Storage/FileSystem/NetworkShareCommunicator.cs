using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Regira.IO.Storage.FileSystem;

/// <summary>
/// Authenticates against a credential-protected network share (UNC path) so <see cref="NetworkFileService"/>
/// can access it with regular file I/O. Windows only — on other platforms mount the share at OS level
/// (e.g. mount.cifs) and use plain <see cref="FileSystemOptions"/> instead.
/// <para>
/// Windows holds one connection per share per logon session, so communicators are ref-counted
/// process-wide by share + user: the first <see cref="Open"/> establishes the connection, only
/// disposing the last communicator releases it. If the connection is cancelled externally,
/// recover with <see cref="Reconnect"/> — <see cref="Close"/> only drops this communicator's
/// reference, so it cannot redial while others still hold one.
/// </para>
/// </summary>
public class NetworkShareCommunicator : IDisposable
{
    private static readonly string PlatformMessage =
        $"{nameof(NetworkShareCommunicator)} requires Windows (WNet API). On other platforms mount the share at OS level and use {nameof(FileSystemOptions)}.";

    // WNetCancelConnection2 tears down the share connection for the whole logon session, so a
    // process-wide ref-count is needed to keep one instance's Dispose from severing the others
    private static readonly object RegistryLock = new();
    private static readonly Dictionary<string, ShareConnection> Connections = new(StringComparer.OrdinalIgnoreCase);

    // One gate per share + user, rather than a single process-wide lock: WNetAddConnection2 blocks on
    // name resolution and SMB negotiation — tens of seconds against an unreachable server — and must
    // not be dialled under a lock shared with other shares, or one bad server stalls every communicator
    private sealed class ShareConnection
    {
        public int RefCount;
    }

    private readonly NetworkFileSystemOptions _options;
    private readonly string _shareRoot;
    private readonly string _userName;
    // guards this instance's connected state; always entered before the share gate, never the reverse
    private readonly object _stateLock = new();
    private bool _isConnected;

    // keyed on user too: a second communicator with different credentials must dial itself so
    // Windows can report the credential conflict instead of silently reusing another user's session
    private string ConnectionKey => $@"{_shareRoot}|{_userName}";

    internal NetworkFileSystemOptions Options => _options;
    public bool IsConnected
    {
        get
        {
            lock (_stateLock)
            {
                return _isConnected;
            }
        }
    }
    /// <summary>
    /// The share the connection is made to — <c>\\server\share</c>, derived from <see cref="FileSystemOptions.RootFolder"/>.
    /// </summary>
    public string ShareRoot => _shareRoot;

    public NetworkShareCommunicator(NetworkFileSystemOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            throw new ArgumentException($"{nameof(options.UserName)} is required", nameof(options));
        }
        _shareRoot = FileNameUtility.GetUncShareRoot(options.RootFolder)
                     ?? throw new ArgumentException($@"{nameof(options.RootFolder)} must be a UNC path (\\server\share\...), got '{options.RootFolder}'", nameof(options));
        _userName = string.IsNullOrEmpty(options.Domain)
            ? options.UserName
            : $@"{options.Domain}\{options.UserName}";
        _options = options;
    }

    /// <summary>
    /// Establishes the authenticated connection — idempotent, safe to call multiple times.
    /// </summary>
    public Task Open()
    {
        EnsureConnected();
        return Task.CompletedTask;
    }
    /// <summary>
    /// Releases this communicator's reference to the share. The connection itself stays up while
    /// other communicators still reference it.
    /// </summary>
    public Task Close()
    {
        Disconnect();
        return Task.CompletedTask;
    }
    /// <summary>
    /// Re-establishes the share connection even when other communicators still reference it — the
    /// recovery path after it was cancelled outside this process (e.g. <c>net use /delete</c>).
    /// </summary>
    public Task Reconnect()
    {
        lock (_stateLock)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(PlatformMessage);
            }
            var share = GetShareConnection();
            lock (share)
            {
                // clear whatever session state is left over before dialling again
                Mpr.WNetCancelConnection2(_shareRoot, 0, false);
                Connect();
                // RefCount tracks instances holding a reference, so only count this one if it wasn't already
                if (!_isConnected)
                {
                    share.RefCount++;
                }
            }
            _isConnected = true;
        }
        return Task.CompletedTask;
    }

    protected internal void EnsureConnected()
    {
        lock (_stateLock)
        {
            if (_isConnected)
            {
                return;
            }
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(PlatformMessage);
            }

            var share = GetShareConnection();
            lock (share)
            {
                if (share.RefCount == 0)
                {
                    Connect();
                }
                share.RefCount++;
            }
            _isConnected = true;
        }
    }
    private void Disconnect()
    {
        lock (_stateLock)
        {
            if (!_isConnected)
            {
                return;
            }
            var share = GetShareConnection();
            lock (share)
            {
                share.RefCount--;
                if (share.RefCount <= 0)
                {
                    share.RefCount = 0;
                    if (OperatingSystem.IsWindows())
                    {
                        // best effort — force: false leaves connections with open handles intact
                        Mpr.WNetCancelConnection2(_shareRoot, 0, false);
                    }
                }
            }
            _isConnected = false;
        }
    }

    private void Connect()
    {
        var resource = new Mpr.NetResource
        {
            Type = Mpr.ResourceTypeDisk,
            RemoteName = _shareRoot
        };
        var error = Mpr.WNetAddConnection2(ref resource, _options.Password, _userName, 0);
        if (error != 0)
        {
            throw new Win32Exception(error, $"Could not connect to '{_shareRoot}' as '{_userName}' (error {error})");
        }
    }
    // Gates are never removed: the set is bounded by the shares an application talks to, and keeping
    // the gate alive keeps the ref-count stable across a disconnect/reconnect cycle.
    private ShareConnection GetShareConnection()
    {
        lock (RegistryLock)
        {
            if (!Connections.TryGetValue(ConnectionKey, out var share))
            {
                share = new ShareConnection();
                Connections[ConnectionKey] = share;
            }
            return share;
        }
    }

    public void Dispose() => Disconnect();

    private static class Mpr
    {
        public const int ResourceTypeDisk = 1;

        // https://learn.microsoft.com/windows/win32/api/winnetwk/ns-winnetwk-netresourcew
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NetResource
        {
            public int Scope;
            public int Type;
            public int DisplayType;
            public int Usage;
            public string? LocalName;
            public string? RemoteName;
            public string? Comment;
            public string? Provider;
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? userName, int flags);
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetCancelConnection2(string name, int flags, bool force);
    }
}
