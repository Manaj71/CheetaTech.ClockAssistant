namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public interface IAttendanceReminderStartupCoordinator
{
    Task<AttendanceReminderScheduleResult?> ScheduleNextAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}