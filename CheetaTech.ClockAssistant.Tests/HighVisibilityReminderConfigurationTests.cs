using System.Text.Json;
using CheetaTech.ClockAssistant.Core.Configuration;
using Xunit;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class HighVisibilityReminderConfigurationTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void NewConfiguration_DefaultsHighVisibilityRemindersToOn()
    {
        var configuration = new ClockAssistantConfiguration();

        Assert.True(configuration.HighVisibilityReminders);
    }

    [Fact]
    public void LegacyJsonWithoutHighVisibilityProperty_DefaultsToOn()
    {
        const string legacyJson = "{}";

        var configuration =
            JsonSerializer.Deserialize<ClockAssistantConfiguration>(
                legacyJson,
                SerializerOptions);

        Assert.NotNull(configuration);
        Assert.True(configuration.HighVisibilityReminders);
    }

    [Fact]
    public void ExplicitOff_RoundTripsAsOff()
    {
        var source =
            new ClockAssistantConfiguration
            {
                HighVisibilityReminders = false
            };

        var json =
            JsonSerializer.Serialize(
                source,
                SerializerOptions);

        var restored =
            JsonSerializer.Deserialize<ClockAssistantConfiguration>(
                json,
                SerializerOptions);

        Assert.NotNull(restored);
        Assert.False(restored.HighVisibilityReminders);
    }
}