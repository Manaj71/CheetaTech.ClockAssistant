namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed record SetupPersistenceResult(
    bool Success,
    SetupPersistenceStage Stage,
    bool PersistenceAttempted,
    bool ProviderValidationPerformed,
    bool ConfigurationSaved,
    bool CredentialsSaved,
    IReadOnlyCollection<string> Issues,
    string Message);
