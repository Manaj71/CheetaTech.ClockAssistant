namespace CheetaTech.ClockAssistant.Core.Attendance;

public enum AttendanceActionState
{
    NotDue = 0,
    Due = 1,
    InProgress = 2,
    Succeeded = 3,
    Failed = 4,
    Skipped = 5,
    Unknown = 6
}
