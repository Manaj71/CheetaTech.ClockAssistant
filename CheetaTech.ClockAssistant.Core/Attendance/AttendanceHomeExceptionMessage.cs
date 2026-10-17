using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Attendance;

/// <summary>
/// Home presentation copy for schedule exceptions (reminders only).
/// Does not change execution or notification eligibility.
/// </summary>
public static class AttendanceHomeExceptionMessage
{
    public static string? ForHome(
        ClockAssistantConfiguration configuration,
        DateOnly attendanceDate,
        AttendanceActionState clockInState)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var exception = FindException(
            configuration.ScheduleExceptions,
            attendanceDate);

        if (exception is null)
        {
            return null;
        }

        var typeLabel = GetTypeLabel(exception.Type);

        // After confirmed Clock In (or live InProgress), do not claim Clock Out
        // reminders are paused — Out reminder independence still applies.
        if (clockInState == AttendanceActionState.Succeeded ||
            clockInState == AttendanceActionState.InProgress)
        {
            return $"Today is marked as {typeLabel} in ShiftPilot.";
        }

        return
            $"Today is marked as {typeLabel}. Regular scheduled reminders are paused.";
    }

    private static ScheduleDayException? FindException(
        IReadOnlyList<ScheduleDayException>? exceptions,
        DateOnly attendanceDate)
    {
        if (exceptions is null || exceptions.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < exceptions.Count; i++)
        {
            if (exceptions[i].Date == attendanceDate)
            {
                return exceptions[i];
            }
        }

        return null;
    }

    private static string GetTypeLabel(
        ScheduleExceptionType type)
    {
        return type switch
        {
            ScheduleExceptionType.StatutoryHoliday => "Statutory Holiday",
            ScheduleExceptionType.Vacation => "Vacation",
            ScheduleExceptionType.SickDay => "Sick Day",
            ScheduleExceptionType.RegularDayOff => "Regular Day Off",
            ScheduleExceptionType.Other => "Other",
            _ => type.ToString()
        };
    }
}
