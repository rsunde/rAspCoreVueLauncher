using System.Runtime.InteropServices;
using Scalar.AspNetCore;
using rAspCoreVueLauncher.Api.Hardware;
using rAspCoreVueLauncher.Api.Json;
using rAspCoreVueLauncher.Shared.Hardware;

var builder = WebApplication.CreateBuilder(args);

const string VueDevCors = "VueDev";
builder.Services.AddCors(o => o.AddPolicy(VueDevCors, p => p
    // Accept any localhost origin so each cloned Vue app can pick its own port
    // (5173, 5174, ...) without the API needing to know about it. Tauri origins
    // are listed explicitly since they aren't `http://localhost`.
    .SetIsOriginAllowed(origin =>
        origin is "tauri://localhost" or "https://tauri.localhost"
        || (Uri.TryCreate(origin, UriKind.Absolute, out var u)
            && u.Host == "localhost"
            && (u.Scheme == "http" || u.Scheme == "https")))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddSingleton<IMobileSensorCache, MobileSensorCache>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    builder.Services.AddSingleton<IBatteryReader, WindowsBatteryReader>();
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    builder.Services.AddSingleton<IBatteryReader, LinuxBatteryReader>();
else
    builder.Services.AddSingleton<IBatteryReader, NullBatteryReader>();
builder.Services.AddSingleton<IHardwareService, HardwareService>();

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new LenientDateTimeOffsetConverter()));

builder.Services.AddOpenApi(o =>
    o.AddOperationTransformer<MobileSensorExampleTransformer>());

var app = builder.Build();

app.UseCors(VueDevCors);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("rAspCoreVueLauncher API"));
}

app.MapHardwareEndpoints();

app.MapGet("/", () => Results.Redirect("/scalar/v1"));

app.Run();

public partial class Program;
