namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record AttendanceReminderPlan(
    AttendanceActionType ActionType,
    DateOnly AttendanceDate,
    DateTimeOffset TriggerAtUtc,
    bool IsImmediate);