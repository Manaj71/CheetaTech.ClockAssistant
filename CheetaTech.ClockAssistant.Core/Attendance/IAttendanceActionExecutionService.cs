namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceActionExecutionService
{
    Task<AttendanceActionExecutionResult> ExecuteAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}