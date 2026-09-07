using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Security;

/// <summary>
/// Production Setup credential persistence policy while standalone provider
/// credential pre-validation is deferred.
///
/// This service performs local required-field checks and stores credentials using
/// ICredentialStore. It has no provider, resolver, HttpClient, or provider-specific
/// dependency and therefore cannot perform a provider request.
///
/// A successful result means "stored securely", NOT "provider verified".
/// </summary>
public sealed class DeferredCredentialUpdateService : ICredentialUpdateService
{
    private readonly ICredentialStore _credentialStore;

    public DeferredCredentialUpdateService(
        ICredentialStore credentialStore)
    {
        _credentialStore =
            credentialStore
            ?? throw new ArgumentNullException(nameof(credentialStore));
    }

    public async Task<CredentialUpdateResult> ValidateAndSaveAsync(
        ClockAssistantConfiguration configuration,
        StoredCredentials candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(candidate);

        if (string.IsNullOrWhiteSpace(candidate.Username))
        {
            return new CredentialUpdateResult(
                Success: false,
                CredentialsSaved: false,
                TechnicalStatus: "InvalidConfiguration",
                Message: "Username is required.")
            {
                ProviderValidationPerformed = false
            };
        }

        if (string.IsNullOrWhiteSpace(candidate.Password))
        {
            return new CredentialUpdateResult(
                Success: false,
                CredentialsSaved: false,
                TechnicalStatus: "InvalidConfiguration",
                Message: "Password is required.")
            {
                ProviderValidationPerformed = false
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _credentialStore
            .SaveCredentialsAsync(
                candidate,
                cancellationToken)
            .ConfigureAwait(false);

        return new CredentialUpdateResult(
            Success: true,
            CredentialsSaved: true,
            TechnicalStatus: "CredentialsStoredUnverified",
            Message:
                "Credentials were stored securely. " +
                "Provider credential validation is deferred.")
        {
            ProviderValidationPerformed = false
        };
    }
}
