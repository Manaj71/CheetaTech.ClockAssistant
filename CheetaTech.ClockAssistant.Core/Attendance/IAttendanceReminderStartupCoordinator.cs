namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceReminderStartupCoordinator
{
    Task<AttendanceReminderScheduleResult?> ScheduleNextAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<AttendanceReminderScheduleResult?> ScheduleNextAfterActionAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configuration-driven reschedule: replace when a plan exists, cancel
    /// only when planning succeeds with no eligible reminder. Never cancel
    /// solely because planning or scheduling failed.
    /// </summary>
    Task<AttendanceReminderRescheduleResult> RescheduleAfterConfigurationChangeAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
