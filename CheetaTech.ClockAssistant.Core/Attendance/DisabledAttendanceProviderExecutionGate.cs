namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class DisabledAttendanceProviderExecutionGate
    : IAttendanceProviderExecutionGate
{
    public bool IsExecutionAllowed()
        => false;
}