using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceStateEvaluatorTests
{
    private static readonly AttendanceStateEvaluator Evaluator = new();

    [Fact]
    public void Evaluate_ScheduledDayBeforeClockInLead_IsScheduledAndNotDue()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        var result = Evaluator.Evaluate(configuration, utcNow);

        Assert.Equal(new DateOnly(2026, 9, 7), result.AttendanceDate);
        Assert.Equal(AttendanceDayState.Scheduled, result.DayState);
        Assert.Equal(AttendanceActionState.NotDue, result.ClockInState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_ClockInLeadReached_MarksClockInDueAndNotificationEligible()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 10, 45, 0, TimeSpan.Zero);

        var result = Evaluator.Evaluate(configuration, utcNow);

        Assert.Equal(AttendanceDayState.ClockInDue, result.DayState);
        Assert.Equal(AttendanceActionState.Due, result.ClockInState);
        Assert.True(result.ClockInNotificationEligible);
    }

    [Fact]
    public void Evaluate_ClockInSucceeded_BlocksDuplicateClockInNotification()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = new DateOnly(2026, 9, 7),
            ClockInState = AttendanceActionState.Succeeded
        };

        var result = Evaluator.Evaluate(configuration, utcNow, record);

        Assert.Equal(AttendanceDayState.ClockedIn, result.DayState);
        Assert.Equal(AttendanceActionState.Succeeded, result.ClockInState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.Equal(AttendanceActionState.NotDue, result.ClockOutState);
    }

    [Fact]
    public void Evaluate_ClockOutLeadReachedAfterClockInSucceeded_MarksClockOutDue()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 19, 16, 0, TimeSpan.Zero);
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = new DateOnly(2026, 9, 7),
            ClockInState = AttendanceActionState.Succeeded
        };

        var result = Evaluator.Evaluate(configuration, utcNow, record);

        Assert.Equal(AttendanceDayState.ClockOutDue, result.DayState);
        Assert.Equal(AttendanceActionState.Due, result.ClockOutState);
        Assert.True(result.ClockOutNotificationEligible);
        Assert.False(result.ClockInNotificationEligible);
    }

    [Fact]
    public void Evaluate_ClockOutTimeReachedWithoutClockInSuccess_NotifiesButDoesNotAllowClockOut()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero);

        var result = Evaluator.Evaluate(configuration, utcNow);

        Assert.NotEqual(AttendanceActionState.Due, result.ClockOutState);
        Assert.True(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_BothActionsSucceeded_MarksDayCompletedAndBlocksDuplicates()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero);
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = new DateOnly(2026, 9, 7),
            ClockInState = AttendanceActionState.Succeeded,
            ClockOutState = AttendanceActionState.Succeeded
        };

        var result = Evaluator.Evaluate(configuration, utcNow, record);

        Assert.Equal(AttendanceDayState.Completed, result.DayState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_NonWorkday_IsNotScheduled()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 6, 15, 0, 0, TimeSpan.Zero);

        var result = Evaluator.Evaluate(configuration, utcNow);

        Assert.Equal(new DateOnly(2026, 9, 6), result.AttendanceDate);
        Assert.Equal(AttendanceDayState.NotScheduled, result.DayState);
        Assert.Equal(AttendanceActionState.NotDue, result.ClockInState);
        Assert.Equal(AttendanceActionState.NotDue, result.ClockOutState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_InProgress_BlocksDuplicateNotification()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 7, 11, 0, 0, TimeSpan.Zero);
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = new DateOnly(2026, 9, 7),
            ClockInState = AttendanceActionState.InProgress
        };

        var result = Evaluator.Evaluate(configuration, utcNow, record);

        Assert.Equal(AttendanceDayState.Active, result.DayState);
        Assert.Equal(AttendanceActionState.InProgress, result.ClockInState);
        Assert.False(result.ClockInNotificationEligible);
    }

    [Fact]
    public void Evaluate_UsesConfiguredTimeZoneForEffectiveAttendanceDate()
    {
        var configuration = CreateConfiguration();
        var utcNow = new DateTimeOffset(2026, 9, 8, 1, 0, 0, TimeSpan.Zero);

        var result = Evaluator.Evaluate(configuration, utcNow);

        Assert.Equal(new DateOnly(2026, 9, 7), result.AttendanceDate);
    }

    private static ClockAssistantConfiguration CreateConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.invalid/",
            WorkDays =
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            ],
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };
    }
}