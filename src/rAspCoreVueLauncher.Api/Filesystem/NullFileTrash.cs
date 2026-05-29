using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Filesystem;

/// Fallback for platforms without trash support (e.g. macOS placeholder).
public sealed class NullFileTrash : IFileTrash
{
    public bool IsSupported => false;

    public void TrashFile(string path) => throw Unsupported();
    public void TrashDirectory(string path) => throw Unsupported();

    private static FilesystemException Unsupported() => new(
        FilesystemError.TrashUnsupported,
        "Moving to trash is not supported on this platform. Use permanent delete.");
}
