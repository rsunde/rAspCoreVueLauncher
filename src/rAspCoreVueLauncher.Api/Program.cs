using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using rAspCoreVueLauncher.Api.Auth;
using rAspCoreVueLauncher.Api.Data;
using rAspCoreVueLauncher.Api.Hardware;
using rAspCoreVueLauncher.Shared.Hardware;

var builder = WebApplication.CreateBuilder(args);

const string VueDevCors = "VueDev";
builder.Services.AddCors(o => o.AddPolicy(VueDevCors, p => p
    .WithOrigins("http://localhost:5173", "http://localhost:4173", "tauri://localhost", "https://tauri.localhost")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));

builder.Services
    .AddIdentityCore<AppUser>(o =>
    {
        o.Password.RequireNonAlphanumeric = true;
        o.Password.RequiredLength = 8;
        o.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IMobileSensorCache, MobileSensorCache>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    builder.Services.AddSingleton<IBatteryReader, WindowsBatteryReader>();
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    builder.Services.AddSingleton<IBatteryReader, LinuxBatteryReader>();
else
    builder.Services.AddSingleton<IBatteryReader, NullBatteryReader>();
builder.Services.AddSingleton<IHardwareService, HardwareService>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors(VueDevCors);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("rAspCoreVueLauncher API"));
}

app.MapHardwareEndpoints();
app.MapAuthEndpoints();

app.MapGet("/", () => Results.Redirect("/scalar/v1"));

await DatabaseSeeder.EnsureSeededAsync(app.Services);

app.Run();

public partial class Program;
