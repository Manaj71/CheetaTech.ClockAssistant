namespace CheetaTech.ClockAssistant.App.Services.Credentials;

public sealed record CredentialSetupWorkflowResult(
    bool Success,
    bool CredentialsSent,
    bool CredentialsSaved,
    string Message);
