namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceReminderScheduler
{
    Task<AttendanceReminderScheduleResult> ScheduleOneTimeAsync(
        DateTimeOffset triggerAtUtc,
        CancellationToken cancellationToken = default);

    Task<AttendanceReminderCancelResult> CancelScheduledReminderAsync(
        CancellationToken cancellationToken = default);

    Task<bool> OpenExactAlarmSettingsAsync(
        CancellationToken cancellationToken = default);
}
