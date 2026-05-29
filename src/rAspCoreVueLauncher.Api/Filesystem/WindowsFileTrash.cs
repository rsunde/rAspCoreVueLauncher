using System.Runtime.Versioning;
using Microsoft.VisualBasic.FileIO;

namespace rAspCoreVueLauncher.Api.Filesystem;

[SupportedOSPlatform("windows")]
public sealed class WindowsFileTrash : IFileTrash
{
    public bool IsSupported => true;

    public void TrashFile(string path) =>
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

    public void TrashDirectory(string path) =>
        FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
}
