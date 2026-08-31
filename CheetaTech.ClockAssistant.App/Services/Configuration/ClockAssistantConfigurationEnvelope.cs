using System.Text.Json.Serialization;

namespace CheetaTech.ClockAssistant.App.Services.Configuration;

internal sealed record ClockAssistantConfigurationEnvelope
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("configuration")]
    public required CheetaTech.ClockAssistant.Core.Configuration.ClockAssistantConfiguration
        Configuration { get; init; }
}
