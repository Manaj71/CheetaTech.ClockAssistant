namespace CheetaTech.ClockAssistant.Core.Providers;

public sealed class ProviderResult
{
    public bool Success { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? ProviderMessage { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string? TechnicalStatus { get; init; }

    public string? ErrorMessage { get; init; }
}
