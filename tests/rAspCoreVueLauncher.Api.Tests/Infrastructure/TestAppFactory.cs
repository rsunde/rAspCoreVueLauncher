using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using rAspCoreVueLauncher.Api.Hardware;
using rAspCoreVueLauncher.Shared.Filesystem;
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Tests.Infrastructure;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public IHardwareService? HardwareSubstitute { get; set; }
    public IBatteryReader? BatteryReaderSubstitute { get; set; }
    public IFilesystemService? FilesystemSubstitute { get; set; }
    public string? FsToken { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        if (FsToken is not null)
            builder.UseSetting("fs-token", FsToken);
        builder.ConfigureServices(services =>
        {
            if (HardwareSubstitute is not null)
            {
                var hwDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHardwareService));
                if (hwDescriptor is not null) services.Remove(hwDescriptor);
                services.AddSingleton(HardwareSubstitute);
            }

            if (BatteryReaderSubstitute is not null)
            {
                var batDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBatteryReader));
                if (batDescriptor is not null) services.Remove(batDescriptor);
                services.AddSingleton(BatteryReaderSubstitute);
            }

            if (FilesystemSubstitute is not null)
            {
                var fsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFilesystemService));
                if (fsDescriptor is not null) services.Remove(fsDescriptor);
                services.AddSingleton(FilesystemSubstitute);
            }
        });
    }
}
