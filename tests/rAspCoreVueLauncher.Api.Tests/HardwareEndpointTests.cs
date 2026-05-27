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

    [TestMethod]
    public async Task PostMobileSensors_StoresReading_AndIsReturnedByGetSensors()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var reading = new MobileSensorReading(
            ClientId: "test-phone",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            Device: null,
            Motion: new MotionSensors(
                Accelerometer: new Vector3(0.1, 0.2, 9.8),
                Gyroscope: null,
                Magnetometer: null,
                Gravity: null,
                LinearAcceleration: null,
                RotationVector: null,
                UserAcceleration: null,
                StepCount: null,
                Cadence: null),
            Orientation: null,
            Environment: null,
            Location: null,
            Health: null,
            Biometric: null,
            Connectivity: null,
            UserInterface: null);

        var post = await client.PostAsJsonAsync("/api/hardware/sensors/mobile", reading);
        post.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
        sensors!.Mobile.Should().NotBeNull();
        sensors.Mobile!.ClientId.Should().Be("test-phone");
        sensors.Mobile.Motion!.Accelerometer.Should().Be(new Vector3(0.1, 0.2, 9.8));
    }

    [TestMethod]
    public async Task PostMobileSensors_MissingClientId_ReturnsValidationProblem()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var bad = new MobileSensorReading(
            ClientId: "   ",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            Device: null,
            Motion: null,
            Orientation: null,
            Environment: null,
            Location: null,
            Health: null,
            Biometric: null,
            Connectivity: null,
            UserInterface: null);

        var response = await client.PostAsJsonAsync("/api/hardware/sensors/mobile", bad);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("clientId");
    }

    [TestMethod]
    public async Task PostMobileSensors_LatestReadingWinsOverOlder()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var older = new MobileSensorReading(
            ClientId: "phone-a",
            CapturedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            Device: null,
            Motion: new MotionSensors(
                Accelerometer: new Vector3(1.0, 1.0, 1.0),
                Gyroscope: null, Magnetometer: null, Gravity: null,
                LinearAcceleration: null, RotationVector: null,
                UserAcceleration: null, StepCount: null, Cadence: null),
            Orientation: null, Environment: null, Location: null,
            Health: null, Biometric: null, Connectivity: null, UserInterface: null);

        var newer = new MobileSensorReading(
            ClientId: "phone-b",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            Device: null,
            Motion: new MotionSensors(
                Accelerometer: new Vector3(2.0, 2.0, 2.0),
                Gyroscope: null, Magnetometer: null, Gravity: null,
                LinearAcceleration: null, RotationVector: null,
                UserAcceleration: null, StepCount: null, Cadence: null),
            Orientation: null, Environment: null, Location: null,
            Health: null, Biometric: null, Connectivity: null, UserInterface: null);

        (await client.PostAsJsonAsync("/api/hardware/sensors/mobile", older))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await client.PostAsJsonAsync("/api/hardware/sensors/mobile", newer))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
        sensors!.Mobile.Should().NotBeNull();
        sensors.Mobile!.ClientId.Should().Be("phone-b");
        sensors.Mobile.Motion!.Accelerometer.Should().Be(new Vector3(2.0, 2.0, 2.0));
    }
}
