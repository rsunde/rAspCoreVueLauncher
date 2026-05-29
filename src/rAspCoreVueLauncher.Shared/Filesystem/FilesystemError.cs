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
