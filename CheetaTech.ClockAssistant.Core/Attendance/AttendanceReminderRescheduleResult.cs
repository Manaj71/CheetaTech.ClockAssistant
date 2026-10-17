namespace CheetaTech.ClockAssistant.Core.Attendance;

public enum AttendanceReminderRescheduleResult
{
    Scheduled = 0,
    Cancelled = 1,
    ExactAlarmPermissionRequired = 2,
    ScheduleFailed = 3,
    CancelFailed = 4,
    PlanningFailed = 5,
    MissingConfiguration = 6
}
