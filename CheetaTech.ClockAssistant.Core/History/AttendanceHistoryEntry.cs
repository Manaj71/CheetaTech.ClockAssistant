using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Core.History;

public sealed record AttendanceHistoryEntry(
    DateOnly AttendanceDate,
    DateTimeOffset OccurredAtUtc,
    AttendanceActionType ActionType,
    AttendanceHistoryOutcome Outcome,
    bool ProviderConfirmed);
