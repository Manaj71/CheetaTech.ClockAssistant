using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class ControlledDateAttendanceProviderExecutionGateTests
{
    private static readonly DateOnly ControlledDate =
        new(2026, 9, 9);

    [Fact]
    public void IsExecutionAllowed_ClockIn_OnControlledDate_ReturnsTrue()
    {
        var gate =
            new ControlledDateAttendanceProviderExecutionGate(
                ControlledDate);

        var allowed =
            gate.IsExecutionAllowed(
                AttendanceActionType.ClockIn,
                ControlledDate,
                new DateTimeOffset(
                    2026, 9, 9, 10, 55, 0, TimeSpan.Zero));

        Assert.True(allowed);
    }

    [Fact]
    public void IsExecutionAllowed_ClockOut_OnControlledDate_ReturnsTrue()
    {
        var gate =
            new ControlledDateAttendanceProviderExecutionGate(
                ControlledDate);

        var allowed =
            gate.IsExecutionAllowed(
                AttendanceActionType.ClockOut,
                ControlledDate,
                new DateTimeOffset(
                    2026, 9, 9, 19, 25, 0, TimeSpan.Zero));

        Assert.True(allowed);
    }

    [Fact]
    public void IsExecutionAllowed_OtherDate_ReturnsFalse()
    {
        var gate =
            new ControlledDateAttendanceProviderExecutionGate(
                ControlledDate);

        var allowed =
            gate.IsExecutionAllowed(
                AttendanceActionType.ClockIn,
                new DateOnly(2026, 9, 8),
                new DateTimeOffset(
                    2026, 9, 8, 10, 55, 0, TimeSpan.Zero));

        Assert.False(allowed);
    }
}