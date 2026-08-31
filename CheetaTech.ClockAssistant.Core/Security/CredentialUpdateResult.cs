namespace CheetaTech.ClockAssistant.Core.Security;

public sealed record CredentialUpdateResult(
    bool Success,
    bool CredentialsSaved,
    string TechnicalStatus,
    string? Message);
