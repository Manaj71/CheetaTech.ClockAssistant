namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed record SetupLifecycleResult(
    bool Success,
    bool ConfigurationValid,
    bool CredentialsLocallyValid,
    bool ProviderRequestSent,
    bool ConfigurationSaved,
    bool CredentialsSaved,
    IReadOnlyCollection<string> Issues,
    string Message);
