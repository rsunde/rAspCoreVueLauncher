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
