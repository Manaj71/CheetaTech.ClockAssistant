namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceProviderExecutionGate
{
    bool IsExecutionAllowed(
        AttendanceActionType actionType,
        DateOnly attendanceDate,
        DateTimeOffset utcNow);
}