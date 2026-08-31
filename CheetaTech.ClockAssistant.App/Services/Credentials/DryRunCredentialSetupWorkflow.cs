namespace CheetaTech.ClockAssistant.App.Services.Credentials;

public sealed class DryRunCredentialSetupWorkflow
    : ICredentialSetupWorkflow
{
    public Task<CredentialSetupWorkflowResult> SubmitAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username))
        {
            return Task.FromResult(
                new CredentialSetupWorkflowResult(
                    Success: false,
                    CredentialsSent: false,
                    CredentialsSaved: false,
                    Message:
                        "DryRun: username is required. " +
                        "No provider request was sent."));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(
                new CredentialSetupWorkflowResult(
                    Success: false,
                    CredentialsSent: false,
                    CredentialsSaved: false,
                    Message:
                        "DryRun: password is required. " +
                        "No provider request was sent."));
        }

        return Task.FromResult(
            new CredentialSetupWorkflowResult(
                Success: true,
                CredentialsSent: false,
                CredentialsSaved: false,
                Message:
                    "DryRun passed local input checks. " +
                    "Credentials were not validated, sent, or saved."));
    }
}
