using System.Text.Json;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class ScheduleExceptionConfigurationTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void NewConfiguration_DefaultsScheduleExceptionsToEmpty()
    {
        var configuration = new ClockAssistantConfiguration();

        Assert.NotNull(configuration.ScheduleExceptions);
        Assert.Empty(configuration.ScheduleExceptions);
    }

    [Fact]
    public void LegacyJsonWithoutScheduleExceptions_DeserializesEmptyList()
    {
        const string legacyJson =
            """
            {
              "providerType": "UKG",
              "providerUrl": "https://example.invalid/",
              "workDays": [1, 2, 3, 4, 5],
              "clockInTime": "07:00:00",
              "clockOutTime": "15:30:00",
              "timeZoneId": "America/Toronto",
              "notificationLeadTime": "00:15:00",
              "highVisibilityReminders": true,
              "executionMode": 0
            }
            """;

        var configuration =
            JsonSerializer.Deserialize<ClockAssistantConfiguration>(
                legacyJson,
                SerializerOptions);

        Assert.NotNull(configuration);
        Assert.Equal("UKG", configuration.ProviderType);
        Assert.Equal("https://example.invalid/", configuration.ProviderUrl);
        Assert.Equal(5, configuration.WorkDays.Count);
        Assert.Equal(new TimeOnly(7, 0), configuration.ClockInTime);
        Assert.Equal(new TimeOnly(15, 30), configuration.ClockOutTime);
        Assert.Equal("America/Toronto", configuration.TimeZoneId);
        Assert.Equal(TimeSpan.FromMinutes(15), configuration.NotificationLeadTime);
        Assert.NotNull(configuration.ScheduleExceptions);
        Assert.Empty(configuration.ScheduleExceptions);
    }

    [Fact]
    public void LegacySchemaVersion1Envelope_WithoutExceptions_LoadsEmptyList()
    {
        // Envelope shape used by MauiPreferencesConfigurationStore (schemaVersion 1).
        const string legacyEnvelopeJson =
            """
            {
              "schemaVersion": 1,
              "configuration": {
                "providerType": "UKG",
                "providerUrl": "https://example.invalid/",
                "workDays": [1, 2, 3, 4, 5],
                "clockInTime": "07:00:00",
                "clockOutTime": "15:30:00",
                "timeZoneId": "America/Toronto",
                "notificationLeadTime": "00:15:00",
                "highVisibilityReminders": true,
                "executionMode": 0
              }
            }
            """;

        using var document = JsonDocument.Parse(legacyEnvelopeJson);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());

        var configuration =
            JsonSerializer.Deserialize<ClockAssistantConfiguration>(
                document.RootElement.GetProperty("configuration").GetRawText(),
                SerializerOptions);

        Assert.NotNull(configuration);
        Assert.Empty(configuration.ScheduleExceptions);
        Assert.Contains(DayOfWeek.Monday, configuration.WorkDays);
    }

    [Fact]
    public void ScheduleExceptions_RoundTrip_PreservesDateTypeAndExistingFields()
    {
        var source = new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.invalid/",
            WorkDays =
            [
                DayOfWeek.Monday,
                DayOfWeek.Wednesday
            ],
            ScheduleExceptions =
            [
                new ScheduleDayException
                {
                    Date = new DateOnly(2026, 10, 13),
                    Type = ScheduleExceptionType.StatutoryHoliday
                },
                new ScheduleDayException
                {
                    Date = new DateOnly(2026, 10, 14),
                    Type = ScheduleExceptionType.Vacation
                }
            ],
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            HighVisibilityReminders = true,
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };

        var json = JsonSerializer.Serialize(source, SerializerOptions);
        var restored =
            JsonSerializer.Deserialize<ClockAssistantConfiguration>(
                json,
                SerializerOptions);

        Assert.NotNull(restored);
        Assert.Equal(source.ProviderType, restored.ProviderType);
        Assert.Equal(source.ProviderUrl, restored.ProviderUrl);
        Assert.Equal(source.WorkDays, restored.WorkDays);
        Assert.Equal(source.ClockInTime, restored.ClockInTime);
        Assert.Equal(source.ClockOutTime, restored.ClockOutTime);
        Assert.Equal(source.TimeZoneId, restored.TimeZoneId);
        Assert.Equal(source.NotificationLeadTime, restored.NotificationLeadTime);
        Assert.Equal(2, restored.ScheduleExceptions.Count);
        Assert.Equal(
            new DateOnly(2026, 10, 13),
            restored.ScheduleExceptions[0].Date);
        Assert.Equal(
            ScheduleExceptionType.StatutoryHoliday,
            restored.ScheduleExceptions[0].Type);
        Assert.Equal(
            new DateOnly(2026, 10, 14),
            restored.ScheduleExceptions[1].Date);
        Assert.Equal(
            ScheduleExceptionType.Vacation,
            restored.ScheduleExceptions[1].Type);
    }
}
