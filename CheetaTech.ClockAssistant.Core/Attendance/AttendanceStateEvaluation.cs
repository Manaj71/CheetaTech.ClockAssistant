namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record AttendanceStateEvaluation(
    DateOnly AttendanceDate,
    AttendanceDayState DayState,
    AttendanceActionState ClockInState,
    AttendanceActionState ClockOutState,
    bool ClockInNotificationEligible,
    bool ClockOutNotificationEligible);
