namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record AttendanceNotificationDecision(
    DateOnly AttendanceDate,
    AttendanceActionType? ActionType,
    AttendanceStateEvaluation Evaluation)
{
    public bool NotificationEligible => ActionType is not null;
}