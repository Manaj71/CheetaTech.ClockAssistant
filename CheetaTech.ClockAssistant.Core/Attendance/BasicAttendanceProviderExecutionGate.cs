namespace CheetaTech.ClockAssistant.Core.Attendance;

/// <summary>
/// Basic-mode provider execution gate.
///
/// AttendanceActionExecutionService performs the authoritative workday,
/// eligibility, and duplicate checks before calling this gate. Reaching this
/// gate means the requested Clock In or Clock Out action is still eligible for
/// provider execution.
/// </summary>
public sealed class BasicAttendanceProviderExecutionGate
    : IAttendanceProviderExecutionGate
{
    public bool IsExecutionAllowed(
        AttendanceActionType actionType,
        DateOnly attendanceDate,
        DateTimeOffset utcNow)
    {
        _ = attendanceDate;
        _ = utcNow;

        return actionType is
            AttendanceActionType.ClockIn
            or AttendanceActionType.ClockOut;
    }
}