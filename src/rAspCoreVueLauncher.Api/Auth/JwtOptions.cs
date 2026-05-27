namespace rAspCoreVueLauncher.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "rAspCoreVueLauncher";
    public string Audience { get; init; } = "rAspCoreVueLauncher.Client";
    public string SigningKey { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 60;
}
