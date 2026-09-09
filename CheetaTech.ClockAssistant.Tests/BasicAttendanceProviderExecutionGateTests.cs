using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class BasicAttendanceProviderExecutionGateTests
{
    [Theory]
    [InlineData(AttendanceActionType.ClockIn)]
    [InlineData(AttendanceActionType.ClockOut)]
    public void IsExecutionAllowed_BasicAttendanceAction_ReturnsTrue(
        AttendanceActionType actionType)
    {
        var gate =
            new BasicAttendanceProviderExecutionGate();

        var allowed =
            gate.IsExecutionAllowed(
                actionType,
                new DateOnly(2026, 9, 10),
                new DateTimeOffset(
                    2026, 9, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.True(allowed);
    }
}