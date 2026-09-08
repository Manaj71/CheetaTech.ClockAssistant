namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceNotificationDecisionService
{
    Task<AttendanceNotificationDecision?> EvaluateAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}