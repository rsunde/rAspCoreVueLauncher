using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using rAspCoreVueLauncher.Shared.Seed;

namespace rAspCoreVueLauncher.Api.Data;

public static class DatabaseSeeder
{
    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Template repo: use EnsureCreated for zero-friction first run.
        // Swap to MigrateAsync once you start tracking schema with `dotnet ef migrations add`.
        await db.Database.EnsureCreatedAsync();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(SeedData.DefaultUserEmail) is null)
        {
            var user = new AppUser { UserName = SeedData.DefaultUserEmail, Email = SeedData.DefaultUserEmail, EmailConfirmed = true };
            var result = await users.CreateAsync(user, SeedData.DefaultUserPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed default user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
