using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceSnoozePlannerTests
{
    [Fact]
    public void ClockIn_AtLeadStart_SnoozesToClockInTime()
    {
        var result = new AttendanceSnoozePlanner().Plan(
            Configuration(),
            AttendanceActionType.ClockIn,
            new DateTimeOffset(2026, 9, 8, 10, 45, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 8, 11, 0, 0, TimeSpan.Zero),
            result.TriggerAtUtc);
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            result.RemainingUntilScheduledAction);
    }

    [Fact]
    public void ClockIn_LaterTap_UsesOnlyTimeStillRemaining()
    {
        var result = new AttendanceSnoozePlanner().Plan(
            Configuration(),
            AttendanceActionType.ClockIn,
            new DateTimeOffset(2026, 9, 8, 10, 55, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Equal(
            TimeSpan.FromMinutes(5),
            result.RemainingUntilScheduledAction);
    }

    [Fact]
    public void ClockOut_SnoozesToConfiguredClockOutTime()
    {
        var result = new AttendanceSnoozePlanner().Plan(
            Configuration(),
            AttendanceActionType.ClockOut,
            new DateTimeOffset(2026, 9, 8, 19, 20, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 8, 19, 30, 0, TimeSpan.Zero),
            result.TriggerAtUtc);
    }

    [Fact]
    public void ScheduledTimeReached_SnoozeIsBlocked()
    {
        var result = new AttendanceSnoozePlanner().Plan(
            Configuration(),
            AttendanceActionType.ClockIn,
            new DateTimeOffset(2026, 9, 8, 11, 0, 0, TimeSpan.Zero));

        Assert.Null(result);
    }

    private static ClockAssistantConfiguration Configuration()
        => new()
        {
            WorkDays = new[] {
                DayOfWeek.Monday, DayOfWeek.Tuesday,
                DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday
            },
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15)
        };
}