namespace CheetaTech.ClockAssistant.Core.Security;

public sealed record CredentialUpdateResult(
    bool Success,
    bool CredentialsSaved,
    string TechnicalStatus,
    string? Message)
{
    /// <summary>
    /// True only when the credential-update operation actually attempted provider
    /// credential validation. Historical credential-update paths validate with the
    /// provider, so the compatibility default is true. Deferred Setup persistence
    /// explicitly sets this to false.
    /// </summary>
    public bool ProviderValidationPerformed { get; init; } = true;
}
