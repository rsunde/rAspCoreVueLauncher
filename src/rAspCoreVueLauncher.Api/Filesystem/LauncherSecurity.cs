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
