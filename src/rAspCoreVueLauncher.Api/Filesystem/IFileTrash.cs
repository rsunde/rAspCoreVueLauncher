namespace rAspCoreVueLauncher.Api.Filesystem;

/// Sends entries to the OS trash/recycle bin. IsSupported is false on platforms
/// with no trash implementation, so the service can refuse a soft-delete rather
/// than silently deleting permanently.
public interface IFileTrash
{
    bool IsSupported { get; }
    void TrashFile(string path);
    void TrashDirectory(string path);
}
