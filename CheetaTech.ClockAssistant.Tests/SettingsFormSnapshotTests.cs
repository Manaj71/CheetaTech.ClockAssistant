using System.Reflection;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class SettingsFormSnapshotTests
{
    [Fact]
    public void AreEquivalent_IdenticalValues_AreNotDirty()
    {
        var left = Capture();
        var right = Capture();

        Assert.True(SettingsFormSnapshot.AreEquivalent(left, right));
    }

    [Fact]
    public void AreEquivalent_ReorderedWorkDays_AreNotDirty()
    {
        var left = Capture(
            workDays:
            [
                DayOfWeek.Friday,
                DayOfWeek.Monday,
                DayOfWeek.Monday
            ]);

        var right = Capture(
            workDays:
            [
                DayOfWeek.Monday,
                DayOfWeek.Friday
            ]);

        Assert.True(SettingsFormSnapshot.AreEquivalent(left, right));
    }

    [Fact]
    public void AreEquivalent_ReorderedExceptions_AreNotDirty()
    {
        var left = Capture(
            scheduleExceptions:
            [
                Exception(new DateOnly(2026, 10, 20), ScheduleExceptionType.Vacation),
                Exception(new DateOnly(2026, 10, 13), ScheduleExceptionType.SickDay)
            ]);

        var right = Capture(
            scheduleExceptions:
            [
                Exception(new DateOnly(2026, 10, 13), ScheduleExceptionType.SickDay),
                Exception(new DateOnly(2026, 10, 20), ScheduleExceptionType.Vacation)
            ]);

        Assert.True(SettingsFormSnapshot.AreEquivalent(left, right));
    }

    [Fact]
    public void AreEquivalent_TrimmedTextAndLeadMinutes_AreNotDirty()
    {
        var left = Capture(
            providerUrl: " https://example.com ",
            username: " ada ",
            timeZoneId: " America/Toronto ",
            notificationLeadMinutesText: " 15 ");

        var right = Capture(
            providerUrl: "https://example.com",
            username: "ada",
            timeZoneId: "America/Toronto",
            notificationLeadMinutesText: "015");

        Assert.True(SettingsFormSnapshot.AreEquivalent(left, right));
    }

    [Fact]
    public void AreEquivalent_EmptyPasswordFlag_IsNotDirty()
    {
        var left = Capture(hasReplacementPassword: false);
        var right = Capture(hasReplacementPassword: false);

        Assert.True(SettingsFormSnapshot.AreEquivalent(left, right));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("url")]
    [InlineData("username")]
    [InlineData("password")]
    [InlineData("workDays")]
    [InlineData("clockIn")]
    [InlineData("clockOut")]
    [InlineData("timeZone")]
    [InlineData("leadMinutes")]
    [InlineData("highVisibility")]
    [InlineData("executionMode")]
    [InlineData("addedException")]
    [InlineData("updatedException")]
    [InlineData("removedException")]
    public void AreEquivalent_EachPersistableChange_IsDirty(string change)
    {
        var existing =
            new[]
            {
                Exception(new DateOnly(2026, 10, 13), ScheduleExceptionType.Vacation)
            };

        var baseline = Capture(scheduleExceptions: existing);

        var current = change switch
        {
            "provider" => Capture(providerType: "Other", scheduleExceptions: existing),
            "url" => Capture(providerUrl: "https://other.example", scheduleExceptions: existing),
            "username" => Capture(username: "other-user", scheduleExceptions: existing),
            "password" => Capture(hasReplacementPassword: true, scheduleExceptions: existing),
            "workDays" => Capture(workDays: [DayOfWeek.Monday], scheduleExceptions: existing),
            "clockIn" => Capture(clockInTime: new TimeSpan(8, 30, 0), scheduleExceptions: existing),
            "clockOut" => Capture(clockOutTime: new TimeSpan(16, 45, 0), scheduleExceptions: existing),
            "timeZone" => Capture(timeZoneId: "America/Vancouver", scheduleExceptions: existing),
            "leadMinutes" => Capture(notificationLeadMinutesText: "abc", scheduleExceptions: existing),
            "highVisibility" => Capture(highVisibilityReminders: false, scheduleExceptions: existing),
            "executionMode" => Capture(executionMode: "OtherMode", scheduleExceptions: existing),
            "addedException" => Capture(
                scheduleExceptions:
                [
                    Exception(new DateOnly(2026, 10, 13), ScheduleExceptionType.Vacation),
                    Exception(new DateOnly(2026, 10, 14), ScheduleExceptionType.Other)
                ]),
            "updatedException" => Capture(
                scheduleExceptions:
                [
                    Exception(new DateOnly(2026, 10, 13), ScheduleExceptionType.SickDay)
                ]),
            "removedException" => Capture(scheduleExceptions: []),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };

        Assert.False(SettingsFormSnapshot.AreEquivalent(baseline, current));
    }

    [Fact]
    public void AreEquivalent_NullArguments_Throw()
    {
        var snapshot = Capture();

        Assert.Throws<ArgumentNullException>(
            () => SettingsFormSnapshot.AreEquivalent(snapshot, null!));
        Assert.Throws<ArgumentNullException>(
            () => SettingsFormSnapshot.AreEquivalent(null!, snapshot));
    }

    [Fact]
    public void Snapshot_DoesNotStorePasswordText()
    {
        const BindingFlags flags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic;

        var names =
            typeof(SettingsFormSnapshot)
                .GetFields(flags)
                .Select(field => field.Name)
                .Concat(
                    typeof(SettingsFormSnapshot)
                        .GetProperties(flags)
                        .Select(property => property.Name));

        Assert.DoesNotContain(
            names,
            name =>
                name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                && !name.Contains(
                    "HasReplacement",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static SettingsFormSnapshot Capture(
        string providerType = "UKG",
        string providerUrl = "https://example.com",
        string username = "ada",
        bool hasReplacementPassword = false,
        IReadOnlyList<DayOfWeek>? workDays = null,
        TimeSpan? clockInTime = null,
        TimeSpan? clockOutTime = null,
        string timeZoneId = "America/Toronto",
        string notificationLeadMinutesText = "15",
        bool highVisibilityReminders = true,
        string executionMode = "BasicConfirmation",
        IReadOnlyList<ScheduleDayException>? scheduleExceptions = null)
    {
        return SettingsFormSnapshot.Capture(
            providerType,
            providerUrl,
            username,
            hasReplacementPassword,
            workDays ??
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            ],
            clockInTime ?? new TimeSpan(9, 0, 0),
            clockOutTime ?? new TimeSpan(17, 0, 0),
            timeZoneId,
            notificationLeadMinutesText,
            highVisibilityReminders,
            executionMode,
            scheduleExceptions ?? []);
    }

    private static ScheduleDayException Exception(
        DateOnly date,
        ScheduleExceptionType type)
    {
        return new ScheduleDayException
        {
            Date = date,
            Type = type
        };
    }
}
