using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class ClockOutReminderIndependenceTests
{
    [Theory]
    [InlineData(AttendanceActionState.NotDue)]
    [InlineData(AttendanceActionState.Failed)]
    [InlineData(AttendanceActionState.Unknown)]
    public void Evaluate_ClockOutReminderWindowReached_ClockInNotSucceeded_StillNotifiesClockOut(
        AttendanceActionState clockInState)
    {
        var evaluator =
            new AttendanceStateEvaluator();

        var configuration =
            Configuration();

        var record =
            new DailyAttendanceRecord
            {
                AttendanceDate =
                    new DateOnly(2026, 9, 10),
                ClockInState =
                    clockInState,
                ClockOutState =
                    AttendanceActionState.NotDue
            };

        var evaluation =
            evaluator.Evaluate(
                configuration,
                new DateTimeOffset(
                    2026, 9, 10, 19, 15, 0, TimeSpan.Zero),
                record);

        Assert.True(
            evaluation.ClockOutNotificationEligible);

        Assert.False(
            evaluation.ClockInNotificationEligible);

        // Reminder independence must NOT make the provider action Due.
        Assert.Equal(
            AttendanceActionState.NotDue,
            evaluation.ClockOutState);
    }

    [Fact]
    public void Evaluate_ClockOutAlreadySucceeded_DoesNotNotifyAgain()
    {
        var evaluator =
            new AttendanceStateEvaluator();

        var record =
            new DailyAttendanceRecord
            {
                AttendanceDate =
                    new DateOnly(2026, 9, 10),
                ClockInState =
                    AttendanceActionState.Failed,
                ClockOutState =
                    AttendanceActionState.Succeeded
            };

        var evaluation =
            evaluator.Evaluate(
                Configuration(),
                new DateTimeOffset(
                    2026, 9, 10, 19, 15, 0, TimeSpan.Zero),
                record);

        Assert.False(
            evaluation.ClockOutNotificationEligible);
    }

    private static ClockAssistantConfiguration Configuration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.invalid/clock",
            WorkDays =
                new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
            ClockInTime =
                new TimeOnly(7, 0),
            ClockOutTime =
                new TimeOnly(15, 30),
            TimeZoneId =
                "America/Toronto",
            NotificationLeadTime =
                TimeSpan.FromMinutes(15)
        };
    }
}