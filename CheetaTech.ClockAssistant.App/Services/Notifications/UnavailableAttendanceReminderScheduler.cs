namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public sealed class UnavailableAttendanceReminderScheduler
    : IAttendanceReminderScheduler
{
    public Task<AttendanceReminderScheduleResult> ScheduleOneTimeAsync(
        DateTimeOffset triggerAtUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AttendanceReminderScheduleResult.Failed);

    public Task<bool> OpenExactAlarmSettingsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}