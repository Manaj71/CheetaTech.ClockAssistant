namespace CheetaTech.ClockAssistant.App.Services.Credentials;

public interface ICredentialSetupWorkflow
{
    Task<CredentialSetupWorkflowResult> SubmitAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
