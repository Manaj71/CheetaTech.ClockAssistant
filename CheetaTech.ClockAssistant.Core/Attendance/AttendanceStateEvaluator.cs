using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceStateEvaluator
{
    public AttendanceStateEvaluation Evaluate(
        ClockAssistantConfiguration configuration,
        DateTimeOffset utcNow,
        DailyAttendanceRecord? record = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.TimeZoneId))
        {
            throw new ArgumentException(
                "A configured time zone is required.",
                nameof(configuration));
        }

        if (configuration.ClockInTime is null)
        {
            throw new ArgumentException(
                "A configured Clock In time is required.",
                nameof(configuration));
        }

        if (configuration.ClockOutTime is null)
        {
            throw new ArgumentException(
                "A configured Clock Out time is required.",
                nameof(configuration));
        }

        if (configuration.NotificationLeadTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Notification lead time cannot be negative.");
        }

        var timeZone = ResolveTimeZone(configuration.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var attendanceDate = DateOnly.FromDateTime(localNow.DateTime);

        if (!configuration.WorkDays.Contains(localNow.DayOfWeek))
        {
            return new AttendanceStateEvaluation(
                attendanceDate,
                AttendanceDayState.NotScheduled,
                AttendanceActionState.NotDue,
                AttendanceActionState.NotDue,
                ClockInNotificationEligible: false,
                ClockOutNotificationEligible: false);
        }

        var currentRecord = record is not null &&
                            record.AttendanceDate == attendanceDate
            ? record
            : new DailyAttendanceRecord
            {
                AttendanceDate = attendanceDate
            };

        var clockInDue = IsNotificationWindowReached(
            attendanceDate,
            configuration.ClockInTime.Value,
            configuration.NotificationLeadTime,
            localNow);

        var clockInState = EvaluateActionState(
            currentRecord.ClockInState,
            clockInDue);

        var clockOutDue = clockInState == AttendanceActionState.Succeeded &&
                          IsNotificationWindowReached(
                              attendanceDate,
                              configuration.ClockOutTime.Value,
                              configuration.NotificationLeadTime,
                              localNow);

        var clockOutState = EvaluateActionState(
            currentRecord.ClockOutState,
            clockOutDue);

        var dayState = EvaluateDayState(
            clockInState,
            clockOutState);

        return new AttendanceStateEvaluation(
            attendanceDate,
            dayState,
            clockInState,
            clockOutState,
            ClockInNotificationEligible: clockInState == AttendanceActionState.Due,
            ClockOutNotificationEligible: clockOutState == AttendanceActionState.Due);
    }

    private static AttendanceActionState EvaluateActionState(
        AttendanceActionState existingState,
        bool due)
    {
        return existingState switch
        {
            AttendanceActionState.Succeeded => AttendanceActionState.Succeeded,
            AttendanceActionState.InProgress => AttendanceActionState.InProgress,
            AttendanceActionState.Failed => AttendanceActionState.Failed,
            AttendanceActionState.Skipped => AttendanceActionState.Skipped,
            AttendanceActionState.Unknown => AttendanceActionState.Unknown,
            _ => due
                ? AttendanceActionState.Due
                : AttendanceActionState.NotDue
        };
    }

    private static AttendanceDayState EvaluateDayState(
        AttendanceActionState clockInState,
        AttendanceActionState clockOutState)
    {
        if (clockInState == AttendanceActionState.Succeeded &&
            clockOutState == AttendanceActionState.Succeeded)
        {
            return AttendanceDayState.Completed;
        }

        if (clockOutState == AttendanceActionState.Succeeded &&
            clockInState != AttendanceActionState.Succeeded)
        {
            return AttendanceDayState.Error;
        }

        if (clockInState == AttendanceActionState.Failed ||
            clockInState == AttendanceActionState.Unknown ||
            clockOutState == AttendanceActionState.Failed ||
            clockOutState == AttendanceActionState.Unknown)
        {
            return AttendanceDayState.Error;
        }

        if (clockInState == AttendanceActionState.InProgress ||
            clockOutState == AttendanceActionState.InProgress)
        {
            return AttendanceDayState.Active;
        }

        if (clockInState == AttendanceActionState.Succeeded &&
            clockOutState == AttendanceActionState.Due)
        {
            return AttendanceDayState.ClockOutDue;
        }

        if (clockInState == AttendanceActionState.Succeeded)
        {
            return AttendanceDayState.ClockedIn;
        }

        if (clockInState == AttendanceActionState.Due)
        {
            return AttendanceDayState.ClockInDue;
        }

        if (clockInState == AttendanceActionState.Skipped ||
            clockOutState == AttendanceActionState.Skipped)
        {
            return AttendanceDayState.Skipped;
        }

        return AttendanceDayState.Scheduled;
    }

    private static bool IsNotificationWindowReached(
        DateOnly attendanceDate,
        TimeOnly scheduledTime,
        TimeSpan notificationLeadTime,
        DateTimeOffset localNow)
    {
        var scheduledLocal = attendanceDate.ToDateTime(
            scheduledTime,
            DateTimeKind.Unspecified);

        var notificationStart = scheduledLocal - notificationLeadTime;

        return localNow.DateTime >= notificationStart;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(
                    timeZoneId,
                    out var windowsTimeZoneId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsTimeZoneId);
            }

            throw;
        }
    }
}
