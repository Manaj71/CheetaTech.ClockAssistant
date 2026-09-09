namespace CheetaTech.ClockAssistant.Core.Attendance;

/// <summary>
/// Basic-mode provider execution gate.
///
/// AttendanceActionExecutionService performs the authoritative Phase 5
/// workday/due/duplicate checks before calling this gate. Reaching this gate
/// means the user has explicitly pressed the Clock In or Clock Out notification
/// action and the action is still eligible.
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