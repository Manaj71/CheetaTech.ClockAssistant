namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public interface IAttendanceReminderStartupCoordinator
{
    Task<AttendanceReminderScheduleResult?> ScheduleNextAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<AttendanceReminderScheduleResult?> ScheduleNextAfterActionAsync(
        CheetaTech.ClockAssistant.Core.Attendance.AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);}