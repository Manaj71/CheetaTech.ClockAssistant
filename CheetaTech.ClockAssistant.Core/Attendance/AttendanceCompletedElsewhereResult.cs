namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record AttendanceCompletedElsewhereResult(
    AttendanceActionType ActionType,
    AttendanceCompletedElsewhereStatus Status,
    DateOnly? AttendanceDate);
