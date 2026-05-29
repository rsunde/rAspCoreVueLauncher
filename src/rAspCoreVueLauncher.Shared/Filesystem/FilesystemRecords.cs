namespace rAspCoreVueLauncher.Shared.Filesystem;

public record FileEntry(
    string Name,
    string Path,
    bool IsDirectory,
    long Size,
    DateTimeOffset Modified,
    FileAttributes Attributes);

public record DirectoryListing(
    string Path,
    string? Parent,
    IReadOnlyList<FileEntry> Entries);

/// Physical path + suggested download filename, resolved by the service.
public record DownloadInfo(string FullPath, string FileName);

public record WriteFileRequest(string Path, string Content, bool Overwrite);
public record MkdirRequest(string Path);
public record MoveRequest(string Source, string Destination, bool Overwrite);
public record CopyRequest(string Source, string Destination, bool Overwrite);
public record DeleteRequest(string Path, bool Permanent);
