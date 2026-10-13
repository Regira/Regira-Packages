using Azure.Storage.Blobs;

namespace Regira.IO.Storage.Azure;

public class AzureCommunicator(AzureOptions config)
{
    public bool IsOpened { get; set; }

    // built on first use; its constructor makes no request, so the members that only read the container's address need no Open
    internal BlobContainerClient Container
    {
        get => _container ??= new BlobContainerClient(_connectionString, _containerName);
        set => _container = value;
    }
    private BlobContainerClient? _container;
    internal bool Contained => config.Contained;

    private readonly string _containerName = config.ContainerName ?? throw new ArgumentNullException(nameof(config.ContainerName));
    private readonly string _connectionString = config.ConnectionString ?? throw new ArgumentNullException(nameof(config.ConnectionString));
    private readonly SemaphoreSlim _openLock = new(1, 1);


    public async Task Open()
    {
        if (IsOpened)
        {
            return;
        }

        await _openLock.WaitAsync();
        try
        {
            if (IsOpened)
            {
                return;
            }

            if (!await Container.ExistsAsync())
            {
                if (!config.CreateContainerIfNotExists)
                {
                    throw new InvalidOperationException($"Blob container '{_containerName}' does not exist. Create it, or enable {nameof(AzureOptions.CreateContainerIfNotExists)}.");
                }
                await Container.CreateAsync();
            }

            IsOpened = true;
        }
        finally
        {
            _openLock.Release();
        }
    }
}