namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Provider-independent adapter that exposes the trusted Setup commit engine
/// through the ISetupPersistenceService boundary.
///
/// Production Setup may register this adapter when the selected
/// ICredentialUpdateService policy is explicit. The current production Setup
/// policy stores credentials securely while provider pre-validation is deferred.
/// A successful commit must not be described as provider-verified unless the
/// credential-update result reports ProviderValidationPerformed=true.
/// </summary>
public sealed class TrustedSetupPersistenceService
    : ISetupPersistenceService
{
    private readonly ITrustedSetupPersistenceOrchestrator _orchestrator;

    public TrustedSetupPersistenceService(
        ITrustedSetupPersistenceOrchestrator orchestrator)
    {
        _orchestrator =
            orchestrator
            ?? throw new ArgumentNullException(
                nameof(orchestrator));
    }

    public Task<SetupPersistenceResult> PrepareTrustedPersistenceAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return _orchestrator.CommitAsync(
            candidate,
            cancellationToken);
    }
}
