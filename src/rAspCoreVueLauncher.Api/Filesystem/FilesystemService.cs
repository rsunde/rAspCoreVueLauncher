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

    public async Task WriteAsync(WriteFileRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Overwrite && File.Exists(request.Path))
            throw new FilesystemException(FilesystemError.Conflict, $"File already exists: {request.Path}");
        try
        {
            await File.WriteAllTextAsync(request.Path, request.Content, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new FilesystemException(FilesystemError.NotFound, ex.Message);
        }
    }

    public void CreateDirectory(MkdirRequest request)
    {
        if (Directory.Exists(request.Path) || File.Exists(request.Path))
            throw new FilesystemException(FilesystemError.Conflict, $"Path already exists: {request.Path}");
        try
        {
            Directory.CreateDirectory(request.Path);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
    }

    public void Move(MoveRequest request)
    {
        if (!File.Exists(request.Source) && !Directory.Exists(request.Source))
            throw new FilesystemException(FilesystemError.NotFound, $"Source not found: {request.Source}");
        if (!request.Overwrite && (File.Exists(request.Destination) || Directory.Exists(request.Destination)))
            throw new FilesystemException(FilesystemError.Conflict, $"Destination already exists: {request.Destination}");
        try
        {
            if (Directory.Exists(request.Source))
            {
                if (request.Overwrite && Directory.Exists(request.Destination))
                    Directory.Delete(request.Destination, recursive: true);
                Directory.Move(request.Source, request.Destination);
            }
            else
            {
                File.Move(request.Source, request.Destination, request.Overwrite);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
    }

    public async Task CopyAsync(CopyRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.Source) && !Directory.Exists(request.Source))
            throw new FilesystemException(FilesystemError.NotFound, $"Source not found: {request.Source}");
        if (!request.Overwrite && (File.Exists(request.Destination) || Directory.Exists(request.Destination)))
            throw new FilesystemException(FilesystemError.Conflict, $"Destination already exists: {request.Destination}");
        try
        {
            if (Directory.Exists(request.Source))
                CopyDirectory(request.Source, request.Destination, request.Overwrite);
            else
                File.Copy(request.Source, request.Destination, request.Overwrite);
            await Task.CompletedTask;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
    }

    public void Delete(DeleteRequest request)
    {
        var isDir = Directory.Exists(request.Path);
        if (!isDir && !File.Exists(request.Path))
            throw new FilesystemException(FilesystemError.NotFound, $"Path not found: {request.Path}");
        try
        {
            if (request.Permanent)
            {
                if (isDir) Directory.Delete(request.Path, recursive: true);
                else File.Delete(request.Path);
            }
            else
            {
                if (!trash.IsSupported)
                    throw new FilesystemException(FilesystemError.TrashUnsupported,
                        "Moving to trash is not supported on this platform. Use permanent delete.");
                if (isDir) trash.TrashDirectory(request.Path);
                else trash.TrashFile(request.Path);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FilesystemException(FilesystemError.AccessDenied, ex.Message);
        }
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite);
        foreach (var sub in Directory.EnumerateDirectories(source))
            CopyDirectory(sub, Path.Combine(destination, Path.GetFileName(sub)), overwrite);
    }
}
