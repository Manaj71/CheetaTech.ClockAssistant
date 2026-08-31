using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;

namespace CheetaTech.ClockAssistant.Core.Security;

public sealed class CredentialUpdateService : ICredentialUpdateService
{
    private readonly ITimeClockProviderResolver _providerResolver;
    private readonly ICredentialStore _credentialStore;

    public CredentialUpdateService(
        ITimeClockProviderResolver providerResolver,
        ICredentialStore credentialStore)
    {
        _providerResolver = providerResolver
            ?? throw new ArgumentNullException(nameof(providerResolver));

        _credentialStore = credentialStore
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
                Message: "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Password))
        {
            return new CredentialUpdateResult(
                Success: false,
                CredentialsSaved: false,
                TechnicalStatus: "InvalidConfiguration",
                Message: "Password is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        ITimeClockProvider provider;

        try
        {
            provider =
                _providerResolver.Resolve(
                    configuration);
        }
        catch (ArgumentException ex)
        {
            return ProviderResolutionFailure(ex);
        }
        catch (NotSupportedException ex)
        {
            return ProviderResolutionFailure(ex);
        }
        catch (InvalidOperationException ex)
        {
            return ProviderResolutionFailure(ex);
        }

        var validation = await provider
            .ValidateCredentialsAsync(
                candidate.Username,
                candidate.Password)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!validation.Success)
        {
            return new CredentialUpdateResult(
                Success: false,
                CredentialsSaved: false,
                TechnicalStatus: validation.TechnicalStatus ?? "CredentialValidationFailed",
                Message:
                    validation.ProviderMessage
                    ?? validation.ErrorMessage
                    ?? "Provider did not accept the candidate credentials.");
        }

        await _credentialStore
            .SaveCredentialsAsync(
                candidate,
                cancellationToken)
            .ConfigureAwait(false);

        return new CredentialUpdateResult(
            Success: true,
            CredentialsSaved: true,
            TechnicalStatus: "CredentialsValidatedAndSaved",
            Message: "Credentials were validated and saved.");
    }

    private static CredentialUpdateResult ProviderResolutionFailure(
        Exception exception)
    {
        return new CredentialUpdateResult(
            Success: false,
            CredentialsSaved: false,
            TechnicalStatus: "ProviderResolutionFailed",
            Message: exception.Message);
    }
}

