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

    /// <summary>
    /// Last Clock In provider punch-boundary report.
    /// false = punch POST did not begin; true = punch POST was entered or
    /// unknown/conservative; null = no prior provider attempt recorded.
    /// </summary>
    public bool? ClockInProviderRequestSent { get; init; }

    /// <summary>
    /// Last Clock Out provider punch-boundary report.
    /// false = punch POST did not begin; true = punch POST was entered or
    /// unknown/conservative; null = no prior provider attempt recorded.
    /// </summary>
    public bool? ClockOutProviderRequestSent { get; init; }
}
