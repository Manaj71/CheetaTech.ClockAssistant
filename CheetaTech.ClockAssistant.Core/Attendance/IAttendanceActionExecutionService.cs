namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceActionExecutionService
{
    Task<AttendanceActionExecutionResult> ExecuteAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<AttendanceActionExecutionResult> ExecuteAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        AttendanceActionExecutionMode executionMode,
        CancellationToken cancellationToken = default);
}