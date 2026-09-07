namespace CheetaTech.ClockAssistant.Core.Attendance;

public enum AttendanceDayState
{
    NotScheduled = 0,
    Scheduled = 1,
    AwaitingActivation = 2,
    Active = 3,
    ClockInDue = 4,
    ClockedIn = 5,
    ClockOutDue = 6,
    ClockedOut = 7,
    Completed = 8,
    Skipped = 9,
    Exception = 10,
    Error = 11
}
