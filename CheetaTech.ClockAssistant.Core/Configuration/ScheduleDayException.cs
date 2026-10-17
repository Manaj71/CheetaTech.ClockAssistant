namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Per-date reminder exception for the configured local calendar date.
/// Does not grant or deny attendance punches.
/// </summary>
public sealed record ScheduleDayException
{
    public DateOnly Date { get; init; }

    public ScheduleExceptionType Type { get; init; }
}
