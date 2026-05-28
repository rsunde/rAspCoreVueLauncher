using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rAspCoreVueLauncher.Api.Json;

// Accepts:
//   - ISO 8601 strings with a timezone offset (the canonical form)
//   - ISO 8601 strings without an offset (Scalar's default example) -> assumed UTC
//   - JSON numbers as Unix epoch (ms if >= 10^12, otherwise seconds)
public sealed class LenientDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const DateTimeStyles ParseStyles =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                // Skip Utf8JsonReader.TryGetDateTimeOffset — it parses offsetless
                // strings using local time instead of honouring AssumeUniversal.
                var raw = reader.GetString();
                if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, ParseStyles, out var parsed))
                    return parsed;
                throw new JsonException($"Unable to parse '{raw}' as DateTimeOffset.");

            case JsonTokenType.Number:
                var n = reader.GetInt64();
                return n >= 1_000_000_000_000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(n)
                    : DateTimeOffset.FromUnixTimeSeconds(n);

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} when parsing DateTimeOffset.");
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
