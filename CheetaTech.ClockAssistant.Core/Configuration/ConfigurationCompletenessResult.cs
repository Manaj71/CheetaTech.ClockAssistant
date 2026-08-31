namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed record ConfigurationCompletenessResult(
    bool IsComplete,
    IReadOnlyCollection<string> MissingOrInvalidFields);
