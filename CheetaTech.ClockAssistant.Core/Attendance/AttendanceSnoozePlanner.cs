using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceSnoozePlanner
{
    public AttendanceSnoozePlan? Plan(
        ClockAssistantConfiguration configuration,
        AttendanceActionType actionType,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.TimeZoneId))
        {
            throw new ArgumentException(
                "A configured time zone is required.",
                nameof(configuration));
        }

        var scheduledTime = actionType switch
        {
            AttendanceActionType.ClockIn => configuration.ClockInTime,
            AttendanceActionType.ClockOut => configuration.ClockOutTime,
            _ => null
        };

        if (scheduledTime is null)
        {
            return null;
        }

        var timeZone = ResolveTimeZone(configuration.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var attendanceDate = DateOnly.FromDateTime(localNow.DateTime);

        var scheduledLocal = attendanceDate.ToDateTime(
            scheduledTime.Value,
            DateTimeKind.Unspecified);

        if (scheduledLocal <= localNow.DateTime ||
            timeZone.IsInvalidTime(scheduledLocal))
        {
            return null;
        }

        var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(
            scheduledLocal,
            timeZone);

        var triggerAtUtc = new DateTimeOffset(
            scheduledUtc,
            TimeSpan.Zero);

        var remaining = triggerAtUtc - utcNow;

        return remaining > TimeSpan.Zero
            ? new AttendanceSnoozePlan(
                actionType,
                triggerAtUtc,
                remaining)
            : null;
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
                return TimeZoneInfo.FindSystemTimeZoneById(
                    windowsTimeZoneId);
            }

            throw;
        }
    }
}