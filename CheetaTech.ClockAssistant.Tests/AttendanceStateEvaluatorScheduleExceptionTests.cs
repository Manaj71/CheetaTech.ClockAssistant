using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceStateEvaluatorScheduleExceptionTests
{
    private static readonly AttendanceStateEvaluator Evaluator = new();

    private static readonly DateOnly Workday =
        new(2026, 9, 7); // Monday America/Toronto

    private static readonly DateTimeOffset ClockInDueUtc =
        new(2026, 9, 7, 10, 50, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockOutDueUtc =
        new(2026, 9, 7, 19, 20, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_NoExceptions_NormalWorkdayClockInReminderUnchanged()
    {
        var result = Evaluator.Evaluate(
            CreateConfiguration(),
            ClockInDueUtc);

        Assert.Equal(Workday, result.AttendanceDate);
        Assert.Equal(AttendanceDayState.ClockInDue, result.DayState);
        Assert.Equal(AttendanceActionState.Due, result.ClockInState);
        Assert.True(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_ExceptionOnWorkday_BeforeClockIn_SuppressesBothNotifications_DoesNotForceNotScheduled()
    {
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Workday,
                Type = ScheduleExceptionType.StatutoryHoliday
            });

        var result = Evaluator.Evaluate(
            configuration,
            ClockInDueUtc);

        Assert.Equal(Workday, result.AttendanceDate);
        Assert.NotEqual(AttendanceDayState.NotScheduled, result.DayState);
        Assert.Equal(AttendanceDayState.ClockInDue, result.DayState);
        Assert.Equal(AttendanceActionState.Due, result.ClockInState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_ExceptionOnWorkday_ClockInSucceeded_PreservesClockOutNotification()
    {
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Workday,
                Type = ScheduleExceptionType.Vacation
            });

        var record = new DailyAttendanceRecord
        {
            AttendanceDate = Workday,
            ClockInState = AttendanceActionState.Succeeded
        };

        var result = Evaluator.Evaluate(
            configuration,
            ClockOutDueUtc,
            record);

        Assert.Equal(AttendanceDayState.ClockOutDue, result.DayState);
        Assert.Equal(AttendanceActionState.Due, result.ClockOutState);
        Assert.True(result.ClockOutNotificationEligible);
        Assert.False(result.ClockInNotificationEligible);
    }

    [Theory]
    [InlineData(AttendanceActionState.Failed)]
    [InlineData(AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionState.NotDue)]
    public void Evaluate_Exception_UnstartedClockIn_DoesNotPreserveClockOutNotification(
        AttendanceActionState clockInState)
    {
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Workday,
                Type = ScheduleExceptionType.SickDay
            });

        var record = new DailyAttendanceRecord
        {
            AttendanceDate = Workday,
            ClockInState = clockInState,
            ClockInProviderRequestSent =
                clockInState == AttendanceActionState.Failed
                    ? false
                    : null
        };

        var result = Evaluator.Evaluate(
            configuration,
            ClockOutDueUtc,
            record);

        Assert.NotEqual(AttendanceDayState.NotScheduled, result.DayState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_Exception_LiveInProgress_DoesNotApplyFullSuppress()
    {
        // Without full suppress, Out-window independence can still notify Out
        // while Clock In remains InProgress (existing reminder independence).
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Workday,
                Type = ScheduleExceptionType.Other
            });

        var record = new DailyAttendanceRecord
        {
            AttendanceDate = Workday,
            ClockInState = AttendanceActionState.InProgress
        };

        var result = Evaluator.Evaluate(
            configuration,
            ClockOutDueUtc,
            record);

        Assert.Equal(AttendanceActionState.InProgress, result.ClockInState);
        Assert.False(result.ClockInNotificationEligible);
        Assert.True(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_ExceptionOnDifferentDate_DoesNotAffectToday()
    {
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Workday.AddDays(1),
                Type = ScheduleExceptionType.RegularDayOff
            });

        var result = Evaluator.Evaluate(
            configuration,
            ClockInDueUtc);

        Assert.True(result.ClockInNotificationEligible);
        Assert.Equal(AttendanceDayState.ClockInDue, result.DayState);
    }

    [Fact]
    public void Evaluate_UsesConfiguredLocalAttendanceDate_NotUtcCalendarDate()
    {
        // 2026-09-08 01:00 UTC = 2026-09-07 21:00 America/Toronto (EDT).
        var localMonday = new DateOnly(2026, 9, 7);
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = localMonday,
                Type = ScheduleExceptionType.StatutoryHoliday
            });

        var utcNow = new DateTimeOffset(2026, 9, 8, 1, 0, 0, TimeSpan.Zero);
        var result = Evaluator.Evaluate(configuration, utcNow);

        Assert.Equal(localMonday, result.AttendanceDate);
        Assert.NotEqual(new DateOnly(2026, 9, 8), result.AttendanceDate);
        Assert.False(result.ClockInNotificationEligible);
        Assert.False(result.ClockOutNotificationEligible);
    }

    [Fact]
    public void Evaluate_ExceptionDoesNotChangeWorkDays()
    {
        var workDays = new[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Workday,
                Type = ScheduleExceptionType.Vacation
            }) with
        {
            WorkDays = workDays
        };

        _ = Evaluator.Evaluate(configuration, ClockInDueUtc);

        Assert.Equal(workDays, configuration.WorkDays);
    }

    private static ClockAssistantConfiguration CreateConfiguration(
        params ScheduleDayException[] exceptions)
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
            ScheduleExceptions = exceptions,
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };
    }
}
