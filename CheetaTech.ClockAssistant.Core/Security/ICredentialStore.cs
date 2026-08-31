namespace CheetaTech.ClockAssistant.Core.Security;

public interface ICredentialStore
{
    Task SaveCredentialsAsync(
        StoredCredentials credentials,
        CancellationToken cancellationToken = default);

    Task<StoredCredentials?> GetCredentialsAsync(
        CancellationToken cancellationToken = default);

    Task DeleteCredentialsAsync(
        CancellationToken cancellationToken = default);
}
