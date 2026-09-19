namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed record DailyAttendanceRecord
{
    public DateOnly AttendanceDate { get; init; }

    public AttendanceActionState ClockInState { get; init; }
        = AttendanceActionState.NotDue;

    public AttendanceActionCompletionSource ClockInCompletionSource { get; init; }
        = AttendanceActionCompletionSource.ProviderConfirmed;

    public AttendanceActionState ClockOutState { get; init; }
        = AttendanceActionState.NotDue;

    public AttendanceActionCompletionSource ClockOutCompletionSource { get; init; }
        = AttendanceActionCompletionSource.ProviderConfirmed;
}
