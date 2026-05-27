namespace rAspCoreVueLauncher.Shared.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

public record CurrentUser(string Id, string Email, IReadOnlyList<string> Roles);
