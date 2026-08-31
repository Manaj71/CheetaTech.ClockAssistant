using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Security;

public interface ICredentialUpdateService
{
    Task<CredentialUpdateResult> ValidateAndSaveAsync(
        ClockAssistantConfiguration configuration,
        StoredCredentials candidate,
        CancellationToken cancellationToken = default);
}
