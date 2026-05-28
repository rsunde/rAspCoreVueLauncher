using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Hardware;
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
        sensors!.MobileDevices.Should().HaveCount(1);
        var device = sensors.MobileDevices[0];
        device.ClientId.Should().Be("test-phone");
        device.Motion!.Accelerometer.Should().Be(new Vector3(0.1, 0.2, 9.8));
    }

    [TestMethod]
    [DataRow("\"2026-05-28T10:00:00\"", DisplayName = "ISO 8601 without offset")]
    [DataRow("\"2026-05-28T10:00:00Z\"", DisplayName = "ISO 8601 with Z offset")]
    [DataRow("1748426400", DisplayName = "Unix seconds")]
    [DataRow("1748426400000", DisplayName = "Unix milliseconds")]
    public async Task PostMobileSensors_AcceptsLenientCapturedAtFormats(string capturedAtJson)
    {
        // Scalar's "Try it" generator may emit a DateTime without an offset OR a
        // numeric epoch. The endpoint must accept either and treat as UTC.
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var body = $$"""
            {
              "clientId": "scalar-test",
              "capturedAtUtc": {{capturedAtJson}},
              "device": null, "motion": null, "orientation": null,
              "environment": null, "location": null, "health": null,
              "biometric": null, "connectivity": null, "userInterface": null
            }
            """;

        var post = await client.PostAsync(
            "/api/hardware/sensors/mobile",
            new StringContent(body, Encoding.UTF8, "application/json"));
        post.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
        var device = sensors!.MobileDevices.Should().ContainSingle().Subject;
        device.ClientId.Should().Be("scalar-test");
        device.CapturedAtUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public async Task IngestMobileSensors_OpenApiDoc_HasWorkingRequestExample()
    {
        // The whole point of attaching an example: Scalar uses it as the default
        // "Try it" body. If it disappears, Scalar regresses to a payload that
        // doesn't deserialise. Sanity-check both the doc and the example itself.
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var doc = await client.GetFromJsonAsync<JsonNode>("/openapi/v1.json");
        var media = doc!["paths"]!["/api/hardware/sensors/mobile"]!["post"]!
            ["requestBody"]!["content"]!["application/json"]!;
        var example = media["example"];
        example.Should().NotBeNull("Scalar relies on the example to seed a valid Try-it body");

        var post = await client.PostAsync(
            "/api/hardware/sensors/mobile",
            new StringContent(example!.ToJsonString(), Encoding.UTF8, "application/json"));
        post.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
    public async Task GetSensors_Battery_NullBatteryReader_ReturnsNullBattery()
    {
        // Explicitly use NullBatteryReader so the test is OS-independent
        await using var factory = new TestAppFactory { BatteryReaderSubstitute = new NullBatteryReader() };
        var client = factory.CreateClient();

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
        sensors!.Battery.Should().BeNull();
    }

    [TestMethod]
    public async Task GetSensors_Battery_WhenReaderReturnsBattery_IncludedInResponse()
    {
        var fake = Substitute.For<IBatteryReader>();
        fake.ReadAsync().Returns(new BatterySnapshot(72, false, null));

        await using var factory = new TestAppFactory { BatteryReaderSubstitute = fake };
        var client = factory.CreateClient();

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
        sensors!.Battery.Should().NotBeNull();
        sensors.Battery!.PercentRemaining.Should().Be(72);
        sensors.Battery.IsCharging.Should().BeFalse();
    }

    [TestMethod]
    public async Task PostMobileSensors_MultipleDevices_AllReturnedInGetSensors()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var phoneA = new MobileSensorReading(
            ClientId: "phone-a",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            Device: null,
            Motion: new MotionSensors(
                Accelerometer: new Vector3(1.0, 1.0, 1.0),
                Gyroscope: null, Magnetometer: null, Gravity: null,
                LinearAcceleration: null, RotationVector: null,
                UserAcceleration: null, StepCount: null, Cadence: null),
            Orientation: null, Environment: null, Location: null,
            Health: null, Biometric: null, Connectivity: null, UserInterface: null);

        var phoneB = new MobileSensorReading(
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

        (await client.PostAsJsonAsync("/api/hardware/sensors/mobile", phoneA)).StatusCode
            .Should().Be(HttpStatusCode.Accepted);
        (await client.PostAsJsonAsync("/api/hardware/sensors/mobile", phoneB)).StatusCode
            .Should().Be(HttpStatusCode.Accepted);

        var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
        sensors!.MobileDevices.Should().HaveCount(2);
        sensors.MobileDevices.Should().Contain(d => d.ClientId == "phone-a");
        sensors.MobileDevices.Should().Contain(d => d.ClientId == "phone-b");
    }
}
