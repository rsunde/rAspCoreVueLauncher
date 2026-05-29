using System.Runtime.Versioning;

namespace rAspCoreVueLauncher.Api.Filesystem;

[SupportedOSPlatform("linux")]
public sealed class LinuxFileTrash : IFileTrash
{
    public bool IsSupported => true;

    public void TrashFile(string path) => Trash(path);
    public void TrashDirectory(string path) => Trash(path);

    private static string EncodeTrashPath(string fullPath) =>
        string.Join('/', fullPath.Split('/').Select(Uri.EscapeDataString));

    private static void Trash(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var trashRoot = Path.Combine(home, ".local", "share", "Trash");
        var filesDir = Path.Combine(trashRoot, "files");
        var infoDir = Path.Combine(trashRoot, "info");
        Directory.CreateDirectory(filesDir);
        Directory.CreateDirectory(infoDir);

        var baseName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        var name = baseName;
        var n = 1;
        while (File.Exists(Path.Combine(filesDir, name))
               || Directory.Exists(Path.Combine(filesDir, name))
               || File.Exists(Path.Combine(infoDir, name + ".trashinfo")))
        {
            name = $"{baseName}.{n++}";
        }

        // DeletionDate is local time, no offset, per the spec.
        var info =
            "[Trash Info]\n" +
            $"Path={EncodeTrashPath(Path.GetFullPath(path))}\n" +
            $"DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n";
        var infoPath = Path.Combine(infoDir, name + ".trashinfo");
        File.WriteAllText(infoPath, info);
        try
        {
            if (Directory.Exists(path))
                Directory.Move(path, Path.Combine(filesDir, name));
            else
                File.Move(path, Path.Combine(filesDir, name));
        }
        catch
        {
            try { File.Delete(infoPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }
}
