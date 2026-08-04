namespace Regira.IO.Storage.FileSystem;

/// <summary>
/// <see cref="BinaryFileService"/> for a credential-protected network share (UNC path) —
/// the communicator authenticates lazily before the first file operation. Windows only.
/// A dedicated type so both services stay single-constructor and unambiguous for DI containers.
/// </summary>
public class NetworkFileService(NetworkShareCommunicator communicator) : BinaryFileService(communicator);
