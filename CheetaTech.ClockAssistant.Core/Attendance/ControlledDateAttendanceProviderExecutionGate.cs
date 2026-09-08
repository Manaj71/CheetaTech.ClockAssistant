namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class ControlledDateAttendanceProviderExecutionGate
    : IAttendanceProviderExecutionGate
{
    private readonly DateOnly _allowedAttendanceDate;

    public ControlledDateAttendanceProviderExecutionGate(
        DateOnly allowedAttendanceDate)
    {
        _allowedAttendanceDate =
            allowedAttendanceDate;
    }

    public bool IsExecutionAllowed(
        AttendanceActionType actionType,
        DateOnly attendanceDate,
        DateTimeOffset utcNow)
    {
        _ = utcNow;

        return attendanceDate ==
               _allowedAttendanceDate
            && actionType is
                AttendanceActionType.ClockIn
                or AttendanceActionType.ClockOut;
    }
}