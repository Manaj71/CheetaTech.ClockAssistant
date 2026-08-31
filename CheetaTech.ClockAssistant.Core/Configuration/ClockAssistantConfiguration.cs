namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed record ClockAssistantConfiguration
{
    public string ProviderType { get; init; } = string.Empty;

    public string ProviderUrl { get; init; } = string.Empty;

    public IReadOnlyCollection<DayOfWeek> WorkDays { get; init; }
        = Array.Empty<DayOfWeek>();

    public TimeOnly? ClockInTime { get; init; }

    public TimeOnly? ClockOutTime { get; init; }

    public string TimeZoneId { get; init; } = string.Empty;

    public TimeSpan NotificationLeadTime { get; init; }

    /// <summary>
    /// Requested operating mode from configuration.
    /// This value does not grant entitlement to Advanced automatic execution.
    /// An entitlement/capability check is still required before any automatic punch.
    /// </summary>
    public ClockExecutionMode ExecutionMode { get; init; }
        = ClockExecutionMode.BasicConfirmation;
}


