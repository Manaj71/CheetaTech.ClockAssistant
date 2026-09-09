using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.App.Services.Diagnostics;

public interface IAttendanceActionAuditLog
{
    string LogFilePath { get; }

    Task AppendReceivedAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task AppendResultAsync(
        AttendanceActionExecutionResult result,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task AppendExceptionAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        string stage,
        string exceptionType,
        CancellationToken cancellationToken = default);
}