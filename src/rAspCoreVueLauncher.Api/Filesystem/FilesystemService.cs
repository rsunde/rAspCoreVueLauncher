using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Filesystem;

public sealed class FilesystemService(IFileTrash trash) : IFilesystemService
{
    private const long MaxReadBytes = 5L * 1024 * 1024; // 5 MB read cap

    public DirectoryListing List(string? path)
    {
        var target = string.IsNullOrWhiteSpace(path)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : path;

        var dir = new DirectoryInfo(target);
        if (!dir.Exists)
            throw new FilesystemException(FilesystemError.NotFound, $"Directory not found: {target}");

        try
        {
            var entries = dir.EnumerateFileSystemInfos()
                .Select(ToEntry)
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new DirectoryListing(dir.FullName, dir.Parent?.FullName, entries);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
    }

    public IReadOnlyList<FileEntry> Roots()
    {
        var result = new List<FileEntry>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                if (d.DriveType is DriveType.Ram or DriveType.Unknown or DriveType.NoRootDirectory) continue;
                var root = d.RootDirectory;
                result.Add(new FileEntry(
                    Name: d.Name,
                    Path: root.FullName,
                    IsDirectory: true,
                    Size: 0,
                    Modified: default,
                    Attributes: FileAttributes.Directory));
            }
            catch { /* some pseudo-fs entries on Linux throw; skip them */ }
        }
        return result;
    }

    public async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FilesystemException(FilesystemError.NotFound, $"File not found: {path}");
        if (info.Length > MaxReadBytes)
            throw new FilesystemException(FilesystemError.TooLarge,
                $"File exceeds the {MaxReadBytes / (1024 * 1024)} MB read cap.");
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
    }

    public DownloadInfo PrepareDownload(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FilesystemException(FilesystemError.NotFound, $"File not found: {path}");
        return new DownloadInfo(info.FullName, info.Name);
    }

    private static FileEntry ToEntry(FileSystemInfo fsi)
    {
        var isDir = fsi is DirectoryInfo;
        return new FileEntry(
            Name: fsi.Name,
            Path: fsi.FullName,
            IsDirectory: isDir,
            Size: isDir ? 0 : ((FileInfo)fsi).Length,
            Modified: fsi.LastWriteTimeUtc,
            Attributes: fsi.Attributes);
    }

    // Write-side methods added in Task 8.
    public Task WriteAsync(WriteFileRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public void CreateDirectory(MkdirRequest request) => throw new NotImplementedException();
    public void Move(MoveRequest request) => throw new NotImplementedException();
    public Task CopyAsync(CopyRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public void Delete(DeleteRequest request) => throw new NotImplementedException();
}
