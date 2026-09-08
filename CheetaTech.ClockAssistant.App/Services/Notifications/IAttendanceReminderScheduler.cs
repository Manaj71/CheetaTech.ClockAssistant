namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public enum AttendanceReminderScheduleResult
{
    Scheduled = 0,
    ExactAlarmPermissionRequired = 1,
    Failed = 2
}

public interface IAttendanceReminderScheduler
{
    Task<AttendanceReminderScheduleResult> ScheduleOneTimeAsync(
        DateTimeOffset triggerAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> OpenExactAlarmSettingsAsync(
        CancellationToken cancellationToken = default);
}