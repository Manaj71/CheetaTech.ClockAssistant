namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record AttendanceActionExecutionResult(
    AttendanceActionType ActionType,
    AttendanceActionExecutionStatus Status,
    DateOnly? AttendanceDate,
    bool ProviderRequestSent,
    bool ProviderConfirmed);