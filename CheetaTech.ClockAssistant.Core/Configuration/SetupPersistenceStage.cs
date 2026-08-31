namespace CheetaTech.ClockAssistant.Core.Configuration;

public enum SetupPersistenceStage
{
    NotStarted = 0,
    LocalValidationFailed = 1,
    AwaitingProviderCredentialValidation = 2,
    ReadyForTrustedCommit = 3,
    CredentialsSaved = 4,
    ConfigurationSaved = 5,
    ReadyConfirmed = 6,
    Failed = 7
}
