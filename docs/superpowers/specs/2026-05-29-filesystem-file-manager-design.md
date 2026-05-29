# Filesystem File Manager — Design

**Date:** 2026-05-29
**Status:** Implemented (see docs/superpowers/plans/2026-05-29-filesystem-file-manager.md)

## Goal

Add a desktop file manager to the launcher: a Vue UI for browsing and managing
the local filesystem with full read/write/move/copy/delete, backed by the
ASP.NET Core sidecar API. The feature is modeled as a new "Filesystem" capability
that mirrors the existing "Hardware" sensors module one-for-one across the
`Api`, `Shared`, and `Web` projects.

## Scope

- **Target platform:** Desktop-first (Tauri + ASP.NET sidecar). The sidecar runs
  as the logged-in user, so file operations inherit that user's permissions —
  full read/write/delete of anything the user can touch (not admin/root; no
  elevation).
- **Mobile:** Out of scope for this design. Mobile (Capacitor) sandboxes apps and
  has no .NET sidecar, so a true full-filesystem manager is not achievable there.
  A scoped mobile variant may be designed separately later.
- **Operations:** Browse + read, create + write, move/rename/copy, delete.

## Architecture

The filesystem is exposed as a feature module that parallels `Hardware/`:

```
Api/Filesystem/      FilesystemEndpoints.cs, FilesystemService.cs,
                     IFilesystemService.cs, IFileTrash + platform impls
Shared/Filesystem/   FileEntry, DirectoryListing, request records
Web/src/             types/filesystem.ts, stores/useFilesystemStore,
                     components/FileManagerPanel.vue
```

Data flow: `FileManagerPanel.vue` → `useFilesystemStore` (Pinia) → axios
(`api.ts`, base URL from `VITE_API_BASE_URL`, with auth token header) →
`/api/filesystem/*` endpoints → `FilesystemService` (`System.IO`) →
`IFileTrash` for deletes.

## Backend — `rAspCoreVueLauncher.Api.Filesystem`

### Endpoints (`FilesystemEndpoints.cs`)

Static extension `MapFilesystemEndpoints(this IEndpointRouteBuilder)`, using
`MapGroup("/api/filesystem").WithTags("Filesystem")`, registered in `Program.cs`
alongside `MapHardwareEndpoints()`. DI via inline lambda parameters.

| Method & route                 | Name              | Returns / body                              |
|--------------------------------|-------------------|---------------------------------------------|
| `GET  /list?path=`             | `ListDirectory`   | `DirectoryListing`                          |
| `GET  /roots`                  | `ListRoots`       | `IReadOnlyList<FileEntry>` (drives/volumes) |
| `GET  /read?path=`             | `ReadFile`        | text content (size-capped at 5 MB → 413)    |
| `GET  /download?path=`         | `DownloadFile`    | raw file stream (`Results.File`)            |
| `POST /write`                  | `WriteFile`       | `WriteFileRequest` → 200                    |
| `POST /mkdir`                  | `CreateDirectory` | `MkdirRequest` → 201                        |
| `POST /move`                   | `MoveEntry`       | `MoveRequest` → 200                         |
| `POST /copy`                   | `CopyEntry`       | `CopyRequest` → 200                         |
| `POST /delete`                 | `DeleteEntry`     | `DeleteRequest` → 200                       |

`/roots` reuses the `DriveInfo.GetDrives()` logic already present in the codebase.

### Services / DI (all singletons, matching hardware)

- `IFilesystemService` → `FilesystemService` — wraps `System.IO`.
- `IFileTrash` → platform-selected by `RuntimeInformation.IsOSPlatform()`,
  exactly like `IBatteryReader`:
  - `WindowsFileTrash` — Recycle Bin (via `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile`/`DeleteDirectory` with `RecycleOption.SendToRecycleBin`, or shell API).
  - `LinuxFileTrash` — XDG trash spec (`~/.local/share/Trash`).
  - `NullFileTrash` — fallback (e.g. macOS placeholder); permanent delete only,
    so a trash request on an unsupported platform returns a clear error rather
    than silently deleting permanently.

### Delete behavior

`DeleteRequest(string Path, bool Permanent)`. Default (`Permanent = false`)
routes through `IFileTrash`. `Permanent = true` bypasses trash and deletes
immediately. If trash is requested on a platform whose `IFileTrash` is
`NullFileTrash`, return 409 with a clear message (do not silently hard-delete).

### Error handling

`FilesystemService` surfaces typed conditions; the endpoint layer maps them to
status codes with a small `{ error, code }` body — never a raw stack trace:

- missing path → **404**
- `UnauthorizedAccessException` → **403**
- existing target on move/mkdir/copy → **409**
- read exceeds size cap → **413**
- unsupported-platform trash → **409**

## Shared — `rAspCoreVueLauncher.Shared.Filesystem`

Record types, native System.Text.Json serialization, no JSON attributes,
nullable-annotated, lowercase-`r` namespace:

```csharp
public record FileEntry(
    string Name, string Path, bool IsDirectory,
    long Size, DateTimeOffset Modified, FileAttributes Attributes);

public record DirectoryListing(
    string Path, string? Parent, IReadOnlyList<FileEntry> Entries);

public record WriteFileRequest(string Path, string Content, bool Overwrite);
public record MkdirRequest(string Path);
public record MoveRequest(string Source, string Destination, bool Overwrite);
public record CopyRequest(string Source, string Destination, bool Overwrite);
public record DeleteRequest(string Path, bool Permanent);
```

## Frontend — `rAspCoreVueLauncher.Web`

- `types/filesystem.ts` — TS mirror of the shared records.
- `stores/useFilesystemStore` — Pinia composition store: state `currentPath`,
  `entries`, `loading`, `error`; actions `list(path)`, `read(path)`,
  `download(path)`, `write(...)`, `mkdir(...)`, `move(...)`, `copy(...)`,
  `remove(path, permanent)`. Uses the existing axios instance. **No polling**
  (file state is fetched on demand, unlike sensors).
- `components/FileManagerPanel.vue` — `<script setup lang="ts">`, breadcrumb path
  bar + entry list, `ui/card` / `ui/button` primitives + Tailwind. Destructive
  operations (delete, overwriting move/copy) require a confirm dialog. Delete
  dialog offers trash (default) vs permanent.

## Security hardening

The chosen approach exposes full-CRUD filesystem operations on the localhost
sidecar. Two layers protect it:

1. **Host-header guard** (always-on middleware): reject any request whose `Host`
   header is not the expected `127.0.0.1:<port>`. Defeats DNS-rebinding attacks
   from malicious web pages.
2. **Startup token**: the Tauri shell (`src-tauri/src/lib.rs`) generates a random
   token at launch, passes it to the sidecar (e.g. `--fs-token <token>`) and
   injects it into the WebView (window global / injected env). The Vue axios
   instance sends it as a request header (e.g. `X-Launcher-Token`). The API
   rejects `/api/filesystem/*` requests lacking the valid token. Blocks other
   local processes that don't know the token.

In development (standalone Kestrel, no Tauri shell), the token check is relaxed
or fed via an env var so `dotnet watch` + Vite dev still works. Document the dev
behavior in the implementation plan.

Note: path-traversal normalization is intentionally **not** a restriction here —
full filesystem access is the goal, so arbitrary absolute paths are permitted.
The token + host guard are what gate access, not path scoping.

## Testing

`tests/rAspCoreVueLauncher.Api.Tests/FilesystemEndpointsTests.cs`:
`[TestClass]` + `TestAppFactory` with a substitutable `IFilesystemService`
(NSubstitute), FluentAssertions. Coverage:

- list / read / write / mkdir / move / copy / delete happy paths.
- error mapping: not-found → 404, denied → 403, conflict → 409, oversize → 413.
- delete routes to `IFileTrash` when `Permanent = false`; bypasses when `true`
  (verified via `.Received()` on a substituted `IFileTrash`).
- security: request without the token is rejected; request with a non-loopback
  `Host` header is rejected.

## Out of scope (YAGNI)

- Mobile filesystem support.
- File search / indexing, thumbnails, archive (zip) handling, permissions
  editing, symbolic-link management — none are required for the initial manager.
- Multi-pane / tabbed UI — single breadcrumb + list view to start.
