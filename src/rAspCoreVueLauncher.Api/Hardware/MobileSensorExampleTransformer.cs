using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace rAspCoreVueLauncher.Api.Hardware;

// Without an explicit example, Scalar's "Try it" UI generates a request body
// from the schema that does not deserialise (quoted numbers, objects in slots
// that expect primitives). This seeds a valid example so the docs UI works
// out of the box.
public sealed class MobileSensorExampleTransformer : IOpenApiOperationTransformer
{
    public const string EndpointName = "IngestMobileSensors";

    private const string ExampleJson = """
        {
          "clientId": "example-phone",
          "capturedAtUtc": "2026-05-28T10:00:00Z",
          "device": null,
          "motion": {
            "accelerometer": { "x": 0.1, "y": 0.2, "z": 9.8 },
            "gyroscope": null, "magnetometer": null, "gravity": null,
            "linearAcceleration": null, "rotationVector": null,
            "userAcceleration": null, "stepCount": null, "cadence": null
          },
          "orientation": null, "environment": null, "location": null,
          "health": null, "biometric": null, "connectivity": null,
          "userInterface": null
        }
        """;

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var name = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Routing.EndpointNameMetadata>()
            .FirstOrDefault()?.EndpointName;

        if (name == EndpointName
            && operation.RequestBody?.Content.TryGetValue("application/json", out var media) == true)
        {
            media.Example = JsonNode.Parse(ExampleJson);
        }

        return Task.CompletedTask;
    }
}
