# Filesystem File Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a desktop file manager — a Vue UI plus ASP.NET Core sidecar API — for full read/write/move/copy/delete of the logged-in user's local filesystem, modeled one-for-one on the existing Hardware module.

**Architecture:** A new `Filesystem` feature module parallels `Hardware/` across `Shared` (records + service interface + typed errors), `Api` (minimal-API endpoints, `FilesystemService` over `System.IO`, OS-selected `IFileTrash`, security middleware), and `Web` (TS types, non-polling Pinia store, `FileManagerPanel.vue`). Access is gated by an always-on host-header guard plus a startup token minted by the Tauri shell — not by path scoping (full-filesystem access is the goal).

**Tech Stack:** .NET 10 minimal APIs, `System.IO`, `Microsoft.VisualBasic.FileIO` (Windows recycle bin), MSTest + NSubstitute + FluentAssertions + `WebApplicationFactory`, Vue 3 `<script setup>` + Pinia + axios + Tailwind, Tauri v2 (Rust) sidecar + `invoke` command.

**Source-of-truth design:** `docs/superpowers/specs/2026-05-29-filesystem-file-manager-design.md`

**Note on auth header:** The design's data-flow mentions an "auth token header." Per the launcher-only role (auth/EF Core scaffolding is being removed) the *only* security header is `X-Launcher-Token`. Do **not** add Bearer auth. `src/api/client.ts` currently sets no auth header — keep it that way.

**Phasing:** After Phase 4 the file manager fully works in local dev (no token). Phase 5 adds the production security layer (host guard + token + Tauri wiring). Implement phases in order; each task is TDD where a test is meaningful.

---

## File Structure

**Shared — `rAspCoreVueLauncher.Shared.Filesystem`** (`src/rAspCoreVueLauncher.Shared/Filesystem/`)
- `FilesystemRecords.cs` — `FileEntry`, `DirectoryListing`, `DownloadInfo`, and the request records (grouped, mirroring how `HardwareSensors.cs` groups related records).
- `FilesystemError.cs` — `FilesystemError` enum + `FilesystemException`.
- `IFilesystemService.cs` — service contract.

**Api — `rAspCoreVueLauncher.Api.Filesystem`** (`src/rAspCoreVueLauncher.Api/Filesystem/`)
- `FilesystemService.cs` — `System.IO` implementation, translates failures to `FilesystemException`.
- `IFileTrash.cs` — trash contract.
- `WindowsFileTrash.cs`, `LinuxFileTrash.cs`, `NullFileTrash.cs` — platform implementations.
- `FilesystemEndpoints.cs` — `MapFilesystemEndpoints` extension.
- `LauncherSecurity.cs` — `UseLauncherHostGuard` + `UseFilesystemToken` middleware extensions.

**Api — modified:** `src/rAspCoreVueLauncher.Api/Program.cs` (DI + endpoint + middleware wiring).

**Tests — `rAspCoreVueLauncher.Api.Tests`** (`tests/rAspCoreVueLauncher.Api.Tests/`)
- `Infrastructure/TestAppFactory.cs` — *modified* to substitute `IFilesystemService` and set a token.
- `FilesystemEndpointTests.cs` — endpoint happy paths + error mapping + security.
- `FilesystemServiceTests.cs` — delete-routing unit tests against real `FilesystemService` with a substituted `IFileTrash`.

**Web** (`src/rAspCoreVueLauncher.Web/`)
- `src/types/filesystem.ts` — TS mirror of the shared records.
- `src/stores/filesystem.ts` — Pinia composition store, **no polling**.
- `src/components/ConfirmDialog.vue` — reusable confirm modal (none exists today).
- `src/components/FileManagerPanel.vue` — breadcrumb + entry list + actions.
- `src/views/HomeView.vue` — *modified* to mount the panel.
- `src/api/client.ts` — *modified* to add a token setter.
- `src/launcherToken.ts` — bootstraps the token from the Tauri `fs_token` command.

**Tauri — modified:** `src/rAspCoreVueLauncher.Web/src-tauri/src/lib.rs`, `.../Cargo.toml`.

---

# Phase 1 — Shared contracts

### Task 1: Shared records

**Files:**
- Create: `src/rAspCoreVueLauncher.Shared/Filesystem/FilesystemRecords.cs`

- [ ] **Step 1: Write the records**

```csharp
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
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/rAspCoreVueLauncher.Shared/rAspCoreVueLauncher.Shared.csproj`
Expected: Build succeeded. (`FileAttributes` lives in `System.IO`, implicitly usable.)

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Shared/Filesystem/FilesystemRecords.cs
git commit -m "feat: add shared Filesystem record types"
```

---

### Task 2: Shared typed errors

**Files:**
- Create: `src/rAspCoreVueLauncher.Shared/Filesystem/FilesystemError.cs`

- [ ] **Step 1: Write the error type**

```csharp
namespace rAspCoreVueLauncher.Shared.Filesystem;

/// Typed failure conditions the service surfaces; the endpoint layer maps each
/// to an HTTP status code. Keeps raw exceptions / stack traces out of responses.
public enum FilesystemError
{
    NotFound,         // -> 404
    AccessDenied,     // -> 403
    Conflict,         // -> 409 (target exists on move/copy/mkdir)
    TooLarge,         // -> 413 (read exceeds size cap)
    TrashUnsupported, // -> 409 (trash requested on a platform without trash support)
}

public sealed class FilesystemException(FilesystemError error, string message)
    : Exception(message)
{
    public FilesystemError Error { get; } = error;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Shared/rAspCoreVueLauncher.Shared.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Shared/Filesystem/FilesystemError.cs
git commit -m "feat: add FilesystemError enum + FilesystemException"
```

---

### Task 3: Service interface

**Files:**
- Create: `src/rAspCoreVueLauncher.Shared/Filesystem/IFilesystemService.cs`

- [ ] **Step 1: Write the interface**

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Shared/rAspCoreVueLauncher.Shared.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Shared/Filesystem/IFilesystemService.cs
git commit -m "feat: add IFilesystemService contract"
```

---

# Phase 2 — Backend service, trash, endpoints

### Task 4: `IFileTrash` contract + `NullFileTrash`

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/IFileTrash.cs`
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/NullFileTrash.cs`

- [ ] **Step 1: Write the contract**

`IFileTrash.cs`:
```csharp
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
```

- [ ] **Step 2: Write the null implementation**

`NullFileTrash.cs`:
```csharp
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
```

- [ ] **Step 3: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/IFileTrash.cs src/rAspCoreVueLauncher.Api/Filesystem/NullFileTrash.cs
git commit -m "feat: add IFileTrash contract + NullFileTrash"
```

---

### Task 5: `WindowsFileTrash`

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/WindowsFileTrash.cs`

- [ ] **Step 1: Write the implementation**

`Microsoft.VisualBasic.FileIO.FileSystem` ships with the .NET runtime (no extra PackageReference). The recycle-bin APIs are Windows-only, so annotate the class with `[SupportedOSPlatform("windows")]` to satisfy the CA1416 analyzer — it is only ever registered on Windows (Task 13).

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded (no CA1416 warning-as-error).

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/WindowsFileTrash.cs
git commit -m "feat: add WindowsFileTrash (recycle bin via Microsoft.VisualBasic.FileIO)"
```

---

### Task 6: `LinuxFileTrash`

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/LinuxFileTrash.cs`

- [ ] **Step 1: Write the implementation**

Implements the XDG Trash spec: move the entry into `~/.local/share/Trash/files/` and write a sibling `*.trashinfo` describing the original path and deletion time. Name collisions get a numeric suffix.

```csharp
using System.Runtime.Versioning;

namespace rAspCoreVueLauncher.Api.Filesystem;

[SupportedOSPlatform("linux")]
public sealed class LinuxFileTrash : IFileTrash
{
    public bool IsSupported => true;

    public void TrashFile(string path) => Trash(path);
    public void TrashDirectory(string path) => Trash(path);

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
            $"Path={Uri.EscapeDataString(Path.GetFullPath(path))}\n" +
            $"DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n";
        File.WriteAllText(Path.Combine(infoDir, name + ".trashinfo"), info);

        if (Directory.Exists(path))
            Directory.Move(path, Path.Combine(filesDir, name));
        else
            File.Move(path, Path.Combine(filesDir, name));
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/LinuxFileTrash.cs
git commit -m "feat: add LinuxFileTrash (XDG trash spec)"
```

---

### Task 7: `FilesystemService` — list / roots / read / download

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/FilesystemService.cs`

- [ ] **Step 1: Write the service skeleton with read-side methods**

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded. (OS selection happens in `Program.cs`, not the service, so no `RuntimeInformation` import is needed here.)

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/FilesystemService.cs
git commit -m "feat: FilesystemService list/roots/read/download"
```

---

### Task 8: `FilesystemService` — write / mkdir / move / copy / delete

**Files:**
- Modify: `src/rAspCoreVueLauncher.Api/Filesystem/FilesystemService.cs`

- [ ] **Step 1: Replace the five `NotImplementedException` stubs with real implementations**

Replace the block beginning `// Write-side methods added in Task 8.` through the end of the class body with:

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/FilesystemService.cs
git commit -m "feat: FilesystemService write/mkdir/move/copy/delete with trash routing"
```

---

### Task 9: Endpoints

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/FilesystemEndpoints.cs`

- [ ] **Step 1: Write the endpoint group**

Mirrors `HardwareEndpoints` (`MapGroup("/api/...").WithTags(...)`, lambda DI, `Results.*`, `.WithName`/`.Produces`). A single helper maps `FilesystemException` → status with a `{ error, code }` body.

```csharp
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Filesystem;

public static class FilesystemEndpoints
{
    public static IEndpointRouteBuilder MapFilesystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/filesystem").WithTags("Filesystem");

        group.MapGet("/list", (IFilesystemService fs, string? path) =>
                Run(() => Results.Ok(fs.List(path))))
            .WithName("ListDirectory").Produces<DirectoryListing>();

        group.MapGet("/roots", (IFilesystemService fs) =>
                Run(() => Results.Ok(fs.Roots())))
            .WithName("ListRoots").Produces<IReadOnlyList<FileEntry>>();

        group.MapGet("/read", async (IFilesystemService fs, string path, CancellationToken ct) =>
                await RunAsync(async () => Results.Text(await fs.ReadTextAsync(path, ct))))
            .WithName("ReadFile").Produces<string>();

        group.MapGet("/download", (IFilesystemService fs, string path) =>
                Run(() =>
                {
                    var info = fs.PrepareDownload(path);
                    return Results.File(info.FullPath, "application/octet-stream", info.FileName);
                }))
            .WithName("DownloadFile");

        group.MapPost("/write", async (IFilesystemService fs, WriteFileRequest req, CancellationToken ct) =>
                await RunAsync(async () => { await fs.WriteAsync(req, ct); return Results.Ok(); }))
            .WithName("WriteFile");

        group.MapPost("/mkdir", (IFilesystemService fs, MkdirRequest req) =>
                Run(() => { fs.CreateDirectory(req); return Results.Created(req.Path, null); }))
            .WithName("CreateDirectory");

        group.MapPost("/move", (IFilesystemService fs, MoveRequest req) =>
                Run(() => { fs.Move(req); return Results.Ok(); }))
            .WithName("MoveEntry");

        group.MapPost("/copy", async (IFilesystemService fs, CopyRequest req, CancellationToken ct) =>
                await RunAsync(async () => { await fs.CopyAsync(req, ct); return Results.Ok(); }))
            .WithName("CopyEntry");

        group.MapPost("/delete", (IFilesystemService fs, DeleteRequest req) =>
                Run(() => { fs.Delete(req); return Results.Ok(); }))
            .WithName("DeleteEntry");

        return app;
    }

    private static IResult Run(Func<IResult> action)
    {
        try { return action(); }
        catch (FilesystemException ex) { return Map(ex); }
    }

    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (FilesystemException ex) { return Map(ex); }
    }

    private static IResult Map(FilesystemException ex)
    {
        var status = ex.Error switch
        {
            FilesystemError.NotFound => StatusCodes.Status404NotFound,
            FilesystemError.AccessDenied => StatusCodes.Status403Forbidden,
            FilesystemError.Conflict => StatusCodes.Status409Conflict,
            FilesystemError.TooLarge => StatusCodes.Status413PayloadTooLarge,
            FilesystemError.TrashUnsupported => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Results.Json(new { error = ex.Message, code = ex.Error.ToString() }, statusCode: status);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/FilesystemEndpoints.cs
git commit -m "feat: add Filesystem minimal-API endpoints with typed error mapping"
```

---

### Task 10: Wire DI + endpoints into `Program.cs`

**Files:**
- Modify: `src/rAspCoreVueLauncher.Api/Program.cs`

- [ ] **Step 1: Add the using**

After line 3 (`using rAspCoreVueLauncher.Api.Hardware;`) add:

```csharp
using rAspCoreVueLauncher.Api.Filesystem;
```

- [ ] **Step 2: Register services (OS-aware trash, mirroring IBatteryReader)**

Immediately after the `builder.Services.AddSingleton<IHardwareService, HardwareService>();` line (currently line 30), add:

```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    builder.Services.AddSingleton<IFileTrash, WindowsFileTrash>();
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    builder.Services.AddSingleton<IFileTrash, LinuxFileTrash>();
else
    builder.Services.AddSingleton<IFileTrash, NullFileTrash>();
builder.Services.AddSingleton<IFilesystemService, FilesystemService>();
```

- [ ] **Step 3: Map endpoints**

Immediately after `app.MapHardwareEndpoints();` (currently line 48) add:

```csharp
app.MapFilesystemEndpoints();
```

- [ ] **Step 4: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Program.cs
git commit -m "feat: register Filesystem service + OS-aware IFileTrash + endpoints"
```

---

# Phase 3 — Backend tests

### Task 11: Extend `TestAppFactory` to substitute `IFilesystemService` + set token

**Files:**
- Modify: `tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs`

- [ ] **Step 1: Read the current file**

Run: open `tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs` to see the existing `HardwareSubstitute` / `BatteryReaderSubstitute` pattern and its usings.

- [ ] **Step 2: Add a `FilesystemSubstitute` property + a `FsToken` setting**

Add a public property alongside the existing substitutes:

```csharp
public IFilesystemService? FilesystemSubstitute { get; init; }
public string? FsToken { get; init; }
```

Add the `using rAspCoreVueLauncher.Shared.Filesystem;` import at the top.

Inside `ConfigureWebHost`, before `builder.ConfigureServices(...)`, add (so the token middleware sees a configured value):

```csharp
if (FsToken is not null)
    builder.UseSetting("fs-token", FsToken);
```

Inside the existing `builder.ConfigureServices(services => { ... })` block, after the hardware/battery substitution logic, add the same removal-then-add pattern:

```csharp
if (FilesystemSubstitute is not null)
{
    var fsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFilesystemService));
    if (fsDescriptor is not null) services.Remove(fsDescriptor);
    services.AddSingleton(FilesystemSubstitute);
}
```

- [ ] **Step 3: Build the test project**

Run: `dotnet build tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs
git commit -m "test: TestAppFactory supports IFilesystemService substitute + fs-token"
```

---

### Task 12: Endpoint happy-path + error-mapping tests

**Files:**
- Create: `tests/rAspCoreVueLauncher.Api.Tests/FilesystemEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

Mirrors `HardwareEndpointTests` (NSubstitute, FluentAssertions, `TestAppFactory`).

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Tests.Infrastructure;
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public class FilesystemEndpointTests
{
    private static (TestAppFactory factory, HttpClient client, IFilesystemService fs) Build()
    {
        var fs = Substitute.For<IFilesystemService>();
        var factory = new TestAppFactory { FilesystemSubstitute = fs };
        return (factory, factory.CreateClient(), fs);
    }

    [TestMethod]
    public async Task List_ReturnsDirectoryListing()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        var listing = new DirectoryListing("/home/x", "/home",
            new[] { new FileEntry("a.txt", "/home/x/a.txt", false, 3, DateTimeOffset.UnixEpoch, FileAttributes.Normal) });
        fs.List(Arg.Any<string?>()).Returns(listing);

        var response = await client.GetAsync("/api/filesystem/list?path=/home/x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DirectoryListing>();
        body!.Path.Should().Be("/home/x");
        body.Entries.Should().HaveCount(1);
        fs.Received(1).List("/home/x");
    }

    [TestMethod]
    public async Task Roots_ReturnsEntries()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        fs.Roots().Returns(new[] { new FileEntry("C:\\", "C:\\", true, 0, default, FileAttributes.Directory) });

        var response = await client.GetAsync("/api/filesystem/roots");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<FileEntry>>();
        body!.Should().ContainSingle(e => e.Name == "C:\\");
    }

    [TestMethod]
    public async Task Read_ReturnsTextContent()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        fs.ReadTextAsync("/f.txt", Arg.Any<CancellationToken>()).Returns("hello");

        var response = await client.GetAsync("/api/filesystem/read?path=/f.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("hello");
    }

    [TestMethod]
    public async Task Write_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/write",
            new WriteFileRequest("/f.txt", "data", true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await fs.Received(1).WriteAsync(
            Arg.Is<WriteFileRequest>(r => r.Path == "/f.txt" && r.Content == "data"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Mkdir_ReturnsCreated()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/mkdir", new MkdirRequest("/d"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fs.Received(1).CreateDirectory(Arg.Is<MkdirRequest>(r => r.Path == "/d"));
    }

    [TestMethod]
    public async Task Move_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/move",
            new MoveRequest("/a", "/b", false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fs.Received(1).Move(Arg.Is<MoveRequest>(r => r.Source == "/a" && r.Destination == "/b"));
    }

    [TestMethod]
    public async Task Copy_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/copy",
            new CopyRequest("/a", "/b", false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await fs.Received(1).CopyAsync(Arg.Is<CopyRequest>(r => r.Source == "/a"), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Delete_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/delete",
            new DeleteRequest("/a", false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fs.Received(1).Delete(Arg.Is<DeleteRequest>(r => r.Path == "/a" && !r.Permanent));
    }

    [DataTestMethod]
    [DataRow(FilesystemError.NotFound, HttpStatusCode.NotFound)]
    [DataRow(FilesystemError.AccessDenied, HttpStatusCode.Forbidden)]
    [DataRow(FilesystemError.Conflict, HttpStatusCode.Conflict)]
    [DataRow(FilesystemError.TooLarge, HttpStatusCode.RequestEntityTooLarge)]
    [DataRow(FilesystemError.TrashUnsupported, HttpStatusCode.Conflict)]
    public async Task List_MapsFilesystemErrorToStatus(FilesystemError error, HttpStatusCode expected)
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        fs.List(Arg.Any<string?>()).Throws(new FilesystemException(error, "boom"));

        var response = await client.GetAsync("/api/filesystem/list?path=/x");

        response.StatusCode.Should().Be(expected);
    }
}
```

Add the missing using for `Throws`: `using NSubstitute.ExceptionExtensions;` at the top.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj --filter FilesystemEndpointTests`
Expected: These compile and run. They should **pass** already if Tasks 9–11 are correct. If any fail, fix the endpoint/factory — do not adjust the test to match a bug. (This task has no separate implementation step because the endpoints already exist; it is the verification gate for Phase 2.)

- [ ] **Step 3: Commit**

```bash
git add tests/rAspCoreVueLauncher.Api.Tests/FilesystemEndpointTests.cs
git commit -m "test: Filesystem endpoint happy paths + error-status mapping"
```

---

### Task 13: `FilesystemService` delete-routing unit tests

**Files:**
- Create: `tests/rAspCoreVueLauncher.Api.Tests/FilesystemServiceTests.cs`

- [ ] **Step 1: Write the tests against the real service with a substituted `IFileTrash`**

```csharp
using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Filesystem;
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public class FilesystemServiceTests
{
    private static string NewTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "x");
        return path;
    }

    [TestMethod]
    public void Delete_SoftDelete_RoutesToTrash()
    {
        var trash = Substitute.For<IFileTrash>();
        trash.IsSupported.Returns(true);
        var service = new FilesystemService(trash);
        var path = NewTempFile();

        service.Delete(new DeleteRequest(path, Permanent: false));

        trash.Received(1).TrashFile(path);
        // The real file still exists because the substitute did not actually move it.
        File.Exists(path).Should().BeTrue();
        File.Delete(path);
    }

    [TestMethod]
    public void Delete_Permanent_BypassesTrash()
    {
        var trash = Substitute.For<IFileTrash>();
        trash.IsSupported.Returns(true);
        var service = new FilesystemService(trash);
        var path = NewTempFile();

        service.Delete(new DeleteRequest(path, Permanent: true));

        trash.DidNotReceive().TrashFile(Arg.Any<string>());
        File.Exists(path).Should().BeFalse();
    }

    [TestMethod]
    public void Delete_SoftDelete_OnUnsupportedTrash_Throws()
    {
        var trash = Substitute.For<IFileTrash>();
        trash.IsSupported.Returns(false);
        var service = new FilesystemService(trash);
        var path = NewTempFile();

        var act = () => service.Delete(new DeleteRequest(path, Permanent: false));

        act.Should().Throw<FilesystemException>()
            .Which.Error.Should().Be(FilesystemError.TrashUnsupported);
        File.Exists(path).Should().BeTrue();
        File.Delete(path);
    }

    [TestMethod]
    public void Delete_MissingPath_ThrowsNotFound()
    {
        var service = new FilesystemService(Substitute.For<IFileTrash>());
        var act = () => service.Delete(new DeleteRequest(
            Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName()), false));

        act.Should().Throw<FilesystemException>()
            .Which.Error.Should().Be(FilesystemError.NotFound);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj --filter FilesystemServiceTests`
Expected: PASS (all four).

- [ ] **Step 3: Commit**

```bash
git add tests/rAspCoreVueLauncher.Api.Tests/FilesystemServiceTests.cs
git commit -m "test: FilesystemService delete routing (trash vs permanent)"
```

---

# Phase 4 — Frontend (works in local dev without a token)

### Task 14: TS types

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/types/filesystem.ts`

- [ ] **Step 1: Write the interfaces (mirror `types/hardware.ts` style — `export interface`)**

```typescript
export interface FileEntry {
  name: string
  path: string
  isDirectory: boolean
  size: number
  modified: string
  attributes: number
}

export interface DirectoryListing {
  path: string
  parent: string | null
  entries: FileEntry[]
}

export interface WriteFileRequest {
  path: string
  content: string
  overwrite: boolean
}

export interface MkdirRequest {
  path: string
}

export interface MoveRequest {
  source: string
  destination: string
  overwrite: boolean
}

export interface CopyRequest {
  source: string
  destination: string
  overwrite: boolean
}

export interface DeleteRequest {
  path: string
  permanent: boolean
}
```

- [ ] **Step 2: Type-check**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build` (or `npx vue-tsc --noEmit` if defined)
Expected: No type errors from this file.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/types/filesystem.ts
git commit -m "feat(web): add filesystem TS types"
```

---

### Task 15: Pinia store (no polling)

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/stores/filesystem.ts`

- [ ] **Step 1: Write the store**

Composition-API store mirroring `stores/hardware.ts` minus the polling. On-demand fetch only.

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/api/client'
import type {
  DirectoryListing,
  FileEntry,
  WriteFileRequest,
  MkdirRequest,
  MoveRequest,
  CopyRequest,
  DeleteRequest,
} from '@/types/filesystem'

export const useFilesystemStore = defineStore('filesystem', () => {
  const currentPath = ref<string>('')
  const parent = ref<string | null>(null)
  const entries = ref<FileEntry[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  function describeError(e: unknown): string {
    if (typeof e === 'object' && e !== null && 'response' in e) {
      const resp = (e as { response?: { data?: { error?: string } } }).response
      if (resp?.data?.error) return resp.data.error
    }
    return e instanceof Error ? e.message : 'Unknown error'
  }

  async function list(path?: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get<DirectoryListing>('/api/filesystem/list', {
        params: path ? { path } : undefined,
      })
      currentPath.value = data.path
      parent.value = data.parent
      entries.value = data.entries
    } catch (e) {
      error.value = describeError(e)
    } finally {
      loading.value = false
    }
  }

  async function read(path: string): Promise<string> {
    const { data } = await api.get<string>('/api/filesystem/read', { params: { path } })
    return data
  }

  function downloadUrl(path: string): string {
    const base = api.defaults.baseURL ?? ''
    return `${base.replace(/\/$/, '')}/api/filesystem/download?path=${encodeURIComponent(path)}`
  }

  async function write(req: WriteFileRequest) {
    await api.post('/api/filesystem/write', req)
    await list(currentPath.value)
  }

  async function mkdir(req: MkdirRequest) {
    await api.post('/api/filesystem/mkdir', req)
    await list(currentPath.value)
  }

  async function move(req: MoveRequest) {
    await api.post('/api/filesystem/move', req)
    await list(currentPath.value)
  }

  async function copy(req: CopyRequest) {
    await api.post('/api/filesystem/copy', req)
    await list(currentPath.value)
  }

  async function remove(req: DeleteRequest) {
    await api.post('/api/filesystem/delete', req)
    await list(currentPath.value)
  }

  return {
    currentPath, parent, entries, loading, error,
    list, read, downloadUrl, write, mkdir, move, copy, remove,
  }
})
```

- [ ] **Step 2: Type-check**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build`
Expected: No type errors from this file.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/stores/filesystem.ts
git commit -m "feat(web): add non-polling filesystem Pinia store"
```

---

### Task 16: Confirm dialog component

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/components/ConfirmDialog.vue`

No modal exists today. Build a small reusable one using the `ui/button` primitive and Tailwind. Avoid native `window.confirm()` (the design requires an in-app dialog, and the delete dialog must offer trash-vs-permanent).

- [ ] **Step 1: Write the component**

```vue
<script setup lang="ts">
import { Button } from '@/components/ui/button'

defineProps<{
  open: boolean
  title: string
  message: string
  confirmLabel?: string
  danger?: boolean
}>()

const emit = defineEmits<{ confirm: []; cancel: [] }>()
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
    @click.self="emit('cancel')"
  >
    <div class="w-full max-w-md rounded-lg border bg-card p-6 text-card-foreground shadow-lg">
      <h2 class="text-lg font-semibold">{{ title }}</h2>
      <p class="mt-2 text-sm text-muted-foreground">{{ message }}</p>
      <slot />
      <div class="mt-6 flex justify-end gap-2">
        <Button variant="outline" @click="emit('cancel')">Cancel</Button>
        <Button :variant="danger ? 'destructive' : 'default'" @click="emit('confirm')">
          {{ confirmLabel ?? 'Confirm' }}
        </Button>
      </div>
    </div>
  </div>
</template>
```

> If the `ui/button` `buttonVariants` set does not include `outline`/`destructive`, use `variant="secondary"` and add Tailwind `bg-red-600 text-white` classes to the confirm button instead. Verify available variants in `src/components/ui/button/Button.vue` before finalizing.

- [ ] **Step 2: Type-check**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/components/ConfirmDialog.vue
git commit -m "feat(web): add reusable ConfirmDialog component"
```

---

### Task 17: `FileManagerPanel.vue`

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/components/FileManagerPanel.vue`

- [ ] **Step 1: Write the panel**

Breadcrumb bar + entry list, `ui/card` + `ui/button` + Tailwind, confirm dialog for destructive ops, delete dialog offering trash (default) vs permanent.

```vue
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useFilesystemStore } from '@/stores/filesystem'
import type { FileEntry } from '@/types/filesystem'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

const fs = useFilesystemStore()

onMounted(() => fs.list())

const crumbs = computed(() => {
  const p = fs.currentPath
  if (!p) return [] as { label: string; path: string }[]
  const sep = p.includes('\\') ? '\\' : '/'
  const parts = p.split(sep).filter(Boolean)
  const acc: { label: string; path: string }[] = []
  let cur = sep === '/' ? '' : ''
  for (const part of parts) {
    cur = cur ? `${cur}${sep}${part}` : (sep === '/' ? `/${part}` : `${part}${sep}`)
    acc.push({ label: part, path: cur })
  }
  return acc
})

function open(entry: FileEntry) {
  if (entry.isDirectory) fs.list(entry.path)
}

function goUp() {
  if (fs.parent) fs.list(fs.parent)
}

function fmtSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// --- delete dialog state ---
const deleteTarget = ref<FileEntry | null>(null)
const deletePermanent = ref(false)

function askDelete(entry: FileEntry) {
  deleteTarget.value = entry
  deletePermanent.value = false
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  await fs.remove({ path: deleteTarget.value.path, permanent: deletePermanent.value })
  deleteTarget.value = null
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle>Files</CardTitle>
    </CardHeader>
    <CardContent>
      <div class="mb-3 flex flex-wrap items-center gap-1 text-sm">
        <Button variant="ghost" size="sm" @click="goUp" :disabled="!fs.parent">↑ Up</Button>
        <button class="hover:underline" @click="fs.list()">~</button>
        <template v-for="c in crumbs" :key="c.path">
          <span class="text-muted-foreground">/</span>
          <button class="hover:underline" @click="fs.list(c.path)">{{ c.label }}</button>
        </template>
      </div>

      <p v-if="fs.error" class="mb-2 text-sm text-red-600">{{ fs.error }}</p>
      <p v-if="fs.loading" class="text-sm text-muted-foreground">Loading…</p>

      <ul class="divide-y">
        <li
          v-for="entry in fs.entries"
          :key="entry.path"
          class="flex items-center justify-between py-2"
        >
          <button class="flex items-center gap-2 text-left hover:underline" @click="open(entry)">
            <span>{{ entry.isDirectory ? '📁' : '📄' }}</span>
            <span>{{ entry.name }}</span>
          </button>
          <div class="flex items-center gap-3">
            <span class="font-mono text-xs tabular-nums text-muted-foreground">
              {{ entry.isDirectory ? '' : fmtSize(entry.size) }}
            </span>
            <a
              v-if="!entry.isDirectory"
              :href="fs.downloadUrl(entry.path)"
              class="text-xs hover:underline"
            >Download</a>
            <Button variant="ghost" size="sm" @click="askDelete(entry)">Delete</Button>
          </div>
        </li>
      </ul>
    </CardContent>
  </Card>

  <ConfirmDialog
    :open="deleteTarget !== null"
    title="Delete entry"
    :message="`Delete '${deleteTarget?.name}'?`"
    confirm-label="Delete"
    danger
    @cancel="deleteTarget = null"
    @confirm="confirmDelete"
  >
    <label class="mt-4 flex items-center gap-2 text-sm">
      <input type="checkbox" v-model="deletePermanent" />
      Delete permanently (skip trash / recycle bin)
    </label>
  </ConfirmDialog>
</template>
```

> If `ui/button` does not support a `size` prop, drop `size="sm"`. Verify in `Button.vue`.

- [ ] **Step 2: Type-check**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/components/FileManagerPanel.vue
git commit -m "feat(web): add FileManagerPanel (breadcrumb + list + delete dialog)"
```

---

### Task 18: Mount the panel in `HomeView.vue`

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src/views/HomeView.vue`

- [ ] **Step 1: Read the current file**

Open `src/rAspCoreVueLauncher.Web/src/views/HomeView.vue` to see the existing `<script setup>` imports and where `<SensorsPanel :sensors="hardware.sensors" />` is rendered.

- [ ] **Step 2: Add the import**

In the `<script setup lang="ts">` block, alongside the existing component imports, add:

```typescript
import FileManagerPanel from '@/components/FileManagerPanel.vue'
```

- [ ] **Step 3: Render the panel**

In the template, immediately after the existing `<SensorsPanel :sensors="hardware.sensors" />` line, add:

```vue
<FileManagerPanel />
```

(The panel manages its own store + initial `list()` on mount, so no extra script wiring is needed.)

- [ ] **Step 4: Type-check + manual smoke**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build`
Expected: No type errors.

Manual: with the API running (`dotnet run --project src/rAspCoreVueLauncher.Api`) and `npm run dev`, load the page — the panel lists the home directory; clicking folders navigates; Up/breadcrumb works; Delete opens the dialog.

- [ ] **Step 5: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/HomeView.vue
git commit -m "feat(web): mount FileManagerPanel on home view"
```

---

# Phase 5 — Security hardening (host guard + token + Tauri wiring)

### Task 19: Security middleware

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Filesystem/LauncherSecurity.cs`

- [ ] **Step 1: Write the middleware extensions**

Two pieces: an always-on host-header guard (defeats DNS-rebinding — a rebind attack arrives with an attacker-controlled `Host`, never a loopback name), and a token check scoped to `/api/filesystem`. The token is read from configuration key `fs-token` (the Tauri shell passes `--fs-token <token>`; ASP.NET maps that to this key). When no token is configured (local dev), the token check is a no-op so `dotnet watch` + Vite still work.

```csharp
namespace rAspCoreVueLauncher.Api.Filesystem;

public static class LauncherSecurity
{
    private static readonly HashSet<string> LoopbackHosts =
        new(StringComparer.OrdinalIgnoreCase) { "127.0.0.1", "localhost", "[::1]", "::1" };

    /// Rejects any request whose Host header is not a loopback hostname.
    public static IApplicationBuilder UseLauncherHostGuard(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var host = ctx.Request.Host.Host; // host without port
            if (!LoopbackHosts.Contains(host))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { error = "Host not allowed", code = "HostRejected" });
                return;
            }
            await next();
        });

    /// Requires a matching X-Launcher-Token header on /api/filesystem/* when a
    /// token is configured. No configured token => check disabled (dev mode).
    public static IApplicationBuilder UseFilesystemToken(this IApplicationBuilder app, string? token) =>
        app.Use(async (ctx, next) =>
        {
            if (!string.IsNullOrEmpty(token)
                && ctx.Request.Path.StartsWithSegments("/api/filesystem"))
            {
                var provided = ctx.Request.Headers["X-Launcher-Token"].ToString();
                if (!string.Equals(provided, token, StringComparison.Ordinal))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Missing or invalid launcher token", code = "TokenRejected" });
                    return;
                }
            }
            await next();
        });
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Filesystem/LauncherSecurity.cs
git commit -m "feat: add launcher host-guard + filesystem token middleware"
```

---

### Task 20: Wire middleware into `Program.cs`

**Files:**
- Modify: `src/rAspCoreVueLauncher.Api/Program.cs`

- [ ] **Step 1: Insert the middleware**

After `app.UseCors(VueDevCors);` (currently line 40) and before the `if (app.Environment.IsDevelopment())` block, add:

```csharp
app.UseLauncherHostGuard();
app.UseFilesystemToken(builder.Configuration["fs-token"]);
```

(`using rAspCoreVueLauncher.Api.Filesystem;` was already added in Task 10.)

- [ ] **Step 2: Build**

Run: `dotnet build src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Run the full backend test suite (regression — host guard now applies to all tests)**

Run: `dotnet test tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj`
Expected: PASS. `WebApplicationFactory`'s default client sends `Host: localhost`, which the guard allows, so existing hardware + filesystem tests still pass.

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Api/Program.cs
git commit -m "feat: enable host-guard + filesystem token middleware"
```

---

### Task 21: Security tests

**Files:**
- Create: `tests/rAspCoreVueLauncher.Api.Tests/FilesystemSecurityTests.cs`

- [ ] **Step 1: Write the tests**

```csharp
using System.Net;
using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Tests.Infrastructure;
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public class FilesystemSecurityTests
{
    private static IFilesystemService StubFs()
    {
        var fs = Substitute.For<IFilesystemService>();
        fs.List(Arg.Any<string?>())
          .Returns(new DirectoryListing("/x", null, Array.Empty<FileEntry>()));
        return fs;
    }

    [TestMethod]
    public async Task Filesystem_WithoutToken_WhenTokenConfigured_Is401()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs(), FsToken = "secret" };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Filesystem_WithValidToken_Is200()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs(), FsToken = "secret" };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Launcher-Token", "secret");

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task NonLoopbackHost_Is403()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs() };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "evil.example.com";

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task NoTokenConfigured_FilesystemAccessibleInDev()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs() }; // no FsToken
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj --filter FilesystemSecurityTests`
Expected: PASS (all four).

- [ ] **Step 3: Commit**

```bash
git add tests/rAspCoreVueLauncher.Api.Tests/FilesystemSecurityTests.cs
git commit -m "test: filesystem token + host-guard security"
```

---

### Task 22: Tauri — mint token, pass to sidecar, expose via command

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src-tauri/Cargo.toml`
- Modify: `src/rAspCoreVueLauncher.Web/src-tauri/src/lib.rs`

- [ ] **Step 1: Add a UUID dependency for token generation**

In `Cargo.toml` under `[dependencies]`, add:

```toml
uuid = { version = "1", features = ["v4"] }
```

- [ ] **Step 2: Mint the token, store it, pass it to the sidecar, expose a command**

Edit `lib.rs`:

(a) Add a managed-state struct near `ApiSidecar` (after line 6):

```rust
// Random per-launch token gating /api/filesystem/* on the sidecar.
struct FsToken(String);

#[tauri::command]
fn fs_token(state: tauri::State<FsToken>) -> String {
    state.0.clone()
}
```

(b) In `run()`, generate the token and register it as managed state. Replace the `.manage(ApiSidecar(Mutex::new(None)))` line (line 12) with:

```rust
        .manage(ApiSidecar(Mutex::new(None)))
        .manage(FsToken(uuid::Uuid::new_v4().to_string()))
        .invoke_handler(tauri::generate_handler![fs_token])
```

(c) Pass the token to the sidecar. Replace the `.args(["--urls", "http://127.0.0.1:5148"])` line (line 30) with:

```rust
                    .args([
                        "--urls",
                        "http://127.0.0.1:5148",
                        "--fs-token",
                        &app.state::<FsToken>().0,
                    ])
```

- [ ] **Step 3: Build the Tauri Rust crate**

Run (from `src/rAspCoreVueLauncher.Web/src-tauri`): `cargo check`
Expected: Compiles. (No bundling needed for this check.)

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src-tauri/Cargo.toml src/rAspCoreVueLauncher.Web/src-tauri/src/lib.rs
git commit -m "feat(tauri): mint fs token, pass --fs-token to sidecar, expose fs_token command"
```

---

### Task 23: Frontend — read token from Tauri, set axios header

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src/api/client.ts`
- Create: `src/rAspCoreVueLauncher.Web/src/launcherToken.ts`
- Modify: `src/rAspCoreVueLauncher.Web/src/main.ts`

- [ ] **Step 1: Add a token setter to the axios client**

Append to `src/api/client.ts`:

```typescript
export function setLauncherToken(token: string): void {
  api.defaults.headers.common['X-Launcher-Token'] = token
}
```

- [ ] **Step 2: Write the bootstrap helper**

`src/launcherToken.ts`. In a Tauri WebView, `window.__TAURI_INTERNALS__` exists and `invoke('fs_token')` returns the token. In a plain browser dev session it is absent, so fall back to `VITE_FS_TOKEN` (empty by default — the dev API has no token configured, so no header is required).

```typescript
import { setLauncherToken } from '@/api/client'

export async function initLauncherToken(): Promise<void> {
  const isTauri = typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window
  if (isTauri) {
    const { invoke } = await import('@tauri-apps/api/core')
    const token = await invoke<string>('fs_token')
    setLauncherToken(token)
    return
  }
  const devToken = import.meta.env.VITE_FS_TOKEN
  if (devToken) setLauncherToken(devToken)
}
```

> Verify `@tauri-apps/api` is in `src/rAspCoreVueLauncher.Web/package.json` dependencies. If absent, run `npm install @tauri-apps/api` (standard in Tauri v2 scaffolds) and commit the manifest changes with this task.

- [ ] **Step 3: Call it during app startup**

Open `src/main.ts`. Before `app.mount('#app')`, initialize the token (fire-and-forget is fine — the panel's first `list()` runs after mount, but to be safe await it). Add:

```typescript
import { initLauncherToken } from '@/launcherToken'
```

and replace the final `app.mount('#app')` with:

```typescript
initLauncherToken().finally(() => app.mount('#app'))
```

(If `main.ts` structures the app differently, adapt: ensure `initLauncherToken()` resolves before the first filesystem request. Read the file first.)

- [ ] **Step 4: Type-check / build**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build`
Expected: No type errors.

- [ ] **Step 5: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/api/client.ts src/rAspCoreVueLauncher.Web/src/launcherToken.ts src/rAspCoreVueLauncher.Web/src/main.ts src/rAspCoreVueLauncher.Web/package.json
git commit -m "feat(web): fetch launcher token from Tauri + set X-Launcher-Token header"
```

---

### Task 24: Full verification + design-doc status bump

**Files:**
- Modify: `docs/superpowers/specs/2026-05-29-filesystem-file-manager-design.md`

- [ ] **Step 1: Run the entire backend test suite**

Run: `dotnet test tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj`
Expected: All tests PASS (hardware + filesystem + service + security).

- [ ] **Step 2: Build the whole solution + frontend**

Run: `dotnet build` (solution root) and, from `src/rAspCoreVueLauncher.Web`, `npm run build`.
Expected: Both succeed.

- [ ] **Step 3: Manual end-to-end smoke (release-style token path)**

Run the API with a token and exercise the guard:
```bash
dotnet run --project src/rAspCoreVueLauncher.Api -- --fs-token testtoken123
```
Then verify with two requests (PowerShell):
```powershell
# Missing token -> 401
Invoke-WebRequest -Uri "http://127.0.0.1:5148/api/filesystem/roots" -SkipHttpErrorCheck | Select-Object StatusCode
# Valid token -> 200
Invoke-WebRequest -Uri "http://127.0.0.1:5148/api/filesystem/roots" -Headers @{ "X-Launcher-Token" = "testtoken123" } | Select-Object StatusCode
```
Expected: 401 then 200.

- [ ] **Step 4: Update the design status**

Change the header line in the design doc from:
```
**Status:** Approved design (pre-implementation)
```
to:
```
**Status:** Implemented (see docs/superpowers/plans/2026-05-29-filesystem-file-manager.md)
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-05-29-filesystem-file-manager-design.md
git commit -m "docs: mark filesystem file-manager design as implemented"
```

---

## Notes & decisions locked by this plan

- **Token config key:** `fs-token`. Tauri passes `--fs-token <uuid>`; ASP.NET's command-line config provider maps it to `Configuration["fs-token"]`. Dev fallback for the frontend is `VITE_FS_TOKEN` (empty by default).
- **Host guard scope:** global (all routes), allowing only loopback hostnames regardless of port — keeps hardware endpoints working in dev while defeating DNS-rebinding.
- **Token guard scope:** `/api/filesystem/*` only; no-op when no token is configured.
- **No path scoping:** arbitrary absolute paths are permitted by design — the token + host guard gate access, not path normalization.
- **Read cap:** 5 MB → `413`. Larger files use `/download` (raw stream, no cap).
- **`/list` empty path:** defaults to the user profile directory.
- **No Bearer/auth header:** consistent with the launcher-only role; only `X-Launcher-Token` is added.
