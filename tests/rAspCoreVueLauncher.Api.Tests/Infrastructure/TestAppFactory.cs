using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using rAspCoreVueLauncher.Api.Data;
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Tests.Infrastructure;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public IHardwareService? HardwareSubstitute { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Swap SQLite file DB for an in-memory unique SQLite per test factory.
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source=file:test-{Guid.NewGuid():N}?mode=memory&cache=shared"));

            if (HardwareSubstitute is not null)
            {
                var hwDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHardwareService));
                if (hwDescriptor is not null) services.Remove(hwDescriptor);
                services.AddSingleton(HardwareSubstitute);
            }
        });
    }
}
