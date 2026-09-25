namespace CheetaTech.ClockAssistant.Core.Providers;

public sealed class ProviderResult
{
    public bool Success { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? ProviderMessage { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string? TechnicalStatus { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Explicit punch-submission boundary reported by the provider.
    /// false = punch POST did not begin; true = punch POST was entered;
    /// null = provider did not report the boundary (treat as may-have-been-sent).
    /// </summary>
    public bool? ProviderRequestSent { get; init; }
}
