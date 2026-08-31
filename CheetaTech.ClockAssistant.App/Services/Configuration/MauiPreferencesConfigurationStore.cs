using System.Text.Json;
using CheetaTech.ClockAssistant.Core.Configuration;
using Microsoft.Maui.Storage;

namespace CheetaTech.ClockAssistant.App.Services.Configuration;

public sealed class MauiPreferencesConfigurationStore
    : IClockAssistantConfigurationStore
{
    private const string ConfigurationKey =
        "ClockAssistant.Configuration.v1";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task SaveAsync(
        ClockAssistantConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        var envelope = new ClockAssistantConfigurationEnvelope
        {
            Configuration = configuration
        };

        var json = JsonSerializer.Serialize(
            envelope,
            SerializerOptions);

        Preferences.Default.Set(
            ConfigurationKey,
            json);

        return Task.CompletedTask;
    }

    public Task<ClockAssistantConfiguration?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = Preferences.Default.Get(
            ConfigurationKey,
            defaultValue: string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Task.FromResult<ClockAssistantConfiguration?>(
                null);
        }

        var envelope =
            JsonSerializer.Deserialize<ClockAssistantConfigurationEnvelope>(
                json,
                SerializerOptions);

        if (envelope is null)
        {
            return Task.FromResult<ClockAssistantConfiguration?>(
                null);
        }

        if (envelope.SchemaVersion !=
            ClockAssistantConfigurationEnvelope.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported ClockAssistant configuration schema version: " +
                $"{envelope.SchemaVersion}.");
        }

        return Task.FromResult<ClockAssistantConfiguration?>(
            envelope.Configuration);
    }

    public Task DeleteAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Preferences.Default.Remove(ConfigurationKey);

        return Task.CompletedTask;
    }
}
