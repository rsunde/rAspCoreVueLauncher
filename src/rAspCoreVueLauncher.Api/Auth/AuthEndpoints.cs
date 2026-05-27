using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using rAspCoreVueLauncher.Api.Data;
using rAspCoreVueLauncher.Shared.Auth;

namespace rAspCoreVueLauncher.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            UserManager<AppUser> users,
            SignInManager<AppUser> signIn,
            JwtTokenService tokens) =>
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null) return Results.Unauthorized();

            var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!check.Succeeded) return Results.Unauthorized();

            var roles = await users.GetRolesAsync(user);
            return Results.Ok(tokens.Issue(user, roles));
        })
        .WithName("Login")
        .AllowAnonymous();

        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ?? string.Empty;
            var email = user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email) ?? string.Empty;
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return Results.Ok(new CurrentUser(id, email, roles));
        })
        .WithName("Me")
        .RequireAuthorization();

        return app;
    }
}
