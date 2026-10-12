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

    /// <summary>
    /// Marks an action completed elsewhere using the same exclusive gate as
    /// provider execution. Reloads and revalidates persisted state under that
    /// gate before saving. Does not call the provider.
    /// </summary>
    Task<AttendanceCompletedElsewhereResult> MarkCompletedElsewhereAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}