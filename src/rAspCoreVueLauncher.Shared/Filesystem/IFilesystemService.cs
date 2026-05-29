namespace rAspCoreVueLauncher.Shared.Filesystem;

public interface IFilesystemService
{
    /// Lists a directory. Empty/null path falls back to the user profile dir.
    DirectoryListing List(string? path);

    /// Drives/volumes as FileEntry rows (IsDirectory = true).
    IReadOnlyList<FileEntry> Roots();

    /// Reads a text file; throws FilesystemError.TooLarge above the 5 MB cap.
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default);

    /// Resolves a file for raw download (existence/permission checked here).
    DownloadInfo PrepareDownload(string path);

    Task WriteAsync(WriteFileRequest request, CancellationToken cancellationToken = default);
    void CreateDirectory(MkdirRequest request);
    void Move(MoveRequest request);
    Task CopyAsync(CopyRequest request, CancellationToken cancellationToken = default);
    void Delete(DeleteRequest request);
}
