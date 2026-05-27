using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Tests.Infrastructure;
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public sealed class HardwareEndpointTests
{
    [TestMethod]
    public async Task GetInfo_ReturnsRealPlatformShape()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync<HardwareInfo>("/api/hardware/info");

        info.Should().NotBeNull();
        info!.OsPlatform.Should().BeOneOf("Windows", "Linux", "macOS", "FreeBSD");
        info.MachineName.Should().NotBeNullOrWhiteSpace();
        info.ProcessorCount.Should().BeGreaterThan(0);
        info.RuntimeVersion.Should().StartWith(".NET");
    }

    [TestMethod]
    public async Task GetInfo_UsesInjectedHardwareService()
    {
        var fake = Substitute.For<IHardwareService>();
        fake.GetInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new HardwareInfo("TestOS", "Test 1.0", "Arm64", "test-host", 99, 64_000, ".NET test"));

        await using var factory = new TestAppFactory { HardwareSubstitute = fake };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/hardware/info");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var info = await response.Content.ReadFromJsonAsync<HardwareInfo>();
        info!.MachineName.Should().Be("test-host");
        info.ProcessorCount.Should().Be(99);

        await fake.Received(1).GetInfoAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task GetSensors_ReturnsLivePayload()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");

        sensors.Should().NotBeNull();
        sensors!.Cpu.LogicalCores.Should().BeGreaterThan(0);
        sensors.Memory.TotalAvailableMb.Should().BeGreaterThan(0);
        sensors.Networks.Should().NotBeEmpty();
        sensors.ServerTimeUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }
}
