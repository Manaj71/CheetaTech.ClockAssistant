namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record AttendanceSnoozePlan(
    AttendanceActionType ActionType,
    DateTimeOffset TriggerAtUtc,
    TimeSpan RemainingUntilScheduledAction);