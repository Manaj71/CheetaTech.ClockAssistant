using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceHomeExceptionMessageTests
{
    private static readonly DateOnly Monday =
        new(2026, 9, 7);

    [Fact]
    public void ForHome_NormalScheduledDay_ReturnsNull()
    {
        var message = AttendanceHomeExceptionMessage.ForHome(
            CreateConfiguration(),
            Monday,
            AttendanceActionState.Due);

        Assert.Null(message);
    }

    [Fact]
    public void ForHome_HolidayBeforeClockIn_PausesRegularRemindersCopy()
    {
        var message = AttendanceHomeExceptionMessage.ForHome(
            CreateConfiguration(
                new ScheduleDayException
                {
                    Date = Monday,
                    Type = ScheduleExceptionType.StatutoryHoliday
                }),
            Monday,
            AttendanceActionState.Due);

        Assert.NotNull(message);
        Assert.Contains("Statutory Holiday", message);
        Assert.Contains("Regular scheduled reminders are paused", message);
        Assert.DoesNotContain("UKG", message);
        Assert.DoesNotContain("extra shift", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weekend", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForHome_VacationAfterSuccessfulClockIn_DoesNotClaimClockOutPaused()
    {
        var message = AttendanceHomeExceptionMessage.ForHome(
            CreateConfiguration(
                new ScheduleDayException
                {
                    Date = Monday,
                    Type = ScheduleExceptionType.Vacation
                }),
            Monday,
            AttendanceActionState.Succeeded);

        Assert.NotNull(message);
        Assert.Contains("Vacation", message);
        Assert.DoesNotContain("reminders are paused", message);
        Assert.DoesNotContain("Clock Out", message);
    }

    [Fact]
    public void ForHome_NonWorkdayException_StillShowsType()
    {
        var sunday = new DateOnly(2026, 9, 6);
        var message = AttendanceHomeExceptionMessage.ForHome(
            CreateConfiguration(
                new ScheduleDayException
                {
                    Date = sunday,
                    Type = ScheduleExceptionType.Other
                }),
            sunday,
            AttendanceActionState.NotDue);

        Assert.NotNull(message);
        Assert.Contains("Other", message);
        Assert.Contains("Regular scheduled reminders are paused", message);
    }

    [Fact]
    public void ForHome_UsesConfiguredAttendanceDate_NotUtcCalendarDate()
    {
        // Exception on local Monday; UTC calendar date is Tuesday.
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = Monday,
                Type = ScheduleExceptionType.SickDay
            });

        var utcNow = new DateTimeOffset(2026, 9, 8, 1, 0, 0, TimeSpan.Zero);
        var evaluation = new AttendanceStateEvaluator().Evaluate(
            configuration,
            utcNow);

        Assert.Equal(Monday, evaluation.AttendanceDate);

        var message = AttendanceHomeExceptionMessage.ForHome(
            configuration,
            evaluation.AttendanceDate,
            evaluation.ClockInState);

        Assert.NotNull(message);
        Assert.Contains("Sick Day", message);
    }

    [Theory]
    [InlineData(AttendanceActionState.Failed)]
    [InlineData(AttendanceActionState.Unknown)]
    public void ForHome_UncertainOrFailedClockIn_DoesNotClaimClockOutReminderActive(
        AttendanceActionState clockInState)
    {
        var message = AttendanceHomeExceptionMessage.ForHome(
            CreateConfiguration(
                new ScheduleDayException
                {
                    Date = Monday,
                    Type = ScheduleExceptionType.SickDay
                }),
            Monday,
            clockInState);

        Assert.NotNull(message);
        Assert.Contains("Regular scheduled reminders are paused", message);
        Assert.DoesNotContain("Clock Out reminder", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("still apply", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForHome_InProgress_DoesNotClaimRemindersPaused()
    {
        var message = AttendanceHomeExceptionMessage.ForHome(
            CreateConfiguration(
                new ScheduleDayException
                {
                    Date = Monday,
                    Type = ScheduleExceptionType.Vacation
                }),
            Monday,
            AttendanceActionState.InProgress);

        Assert.NotNull(message);
        Assert.Contains("Vacation", message);
        Assert.DoesNotContain("reminders are paused", message);
    }

    private static ClockAssistantConfiguration CreateConfiguration(
        params ScheduleDayException[] exceptions)
        => new()
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
            NotificationLeadTime = TimeSpan.FromMinutes(15)
        };
}
