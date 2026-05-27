using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public static class HardwareEndpoints
{
    public static IEndpointRouteBuilder MapHardwareEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware").WithTags("Hardware");

        group.MapGet("/info", async (IHardwareService hardware, CancellationToken ct) =>
            Results.Ok(await hardware.GetInfoAsync(ct)))
            .WithName("GetHardwareInfo")
            .Produces<HardwareInfo>();

        group.MapGet("/sensors", async (IHardwareService hardware, CancellationToken ct) =>
            Results.Ok(await hardware.GetSensorsAsync(ct)))
            .WithName("GetHardwareSensors")
            .Produces<HardwareSensors>();

        return app;
    }
}
