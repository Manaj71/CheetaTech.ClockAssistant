namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record DailyAttendanceRecord
{
    public DateOnly AttendanceDate { get; init; }

    public AttendanceActionState ClockInState { get; init; }
        = AttendanceActionState.NotDue;

    public AttendanceActionState ClockOutState { get; init; }
        = AttendanceActionState.NotDue;
}
