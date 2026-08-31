namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Provider-independent adapter that exposes the trusted Setup commit engine
/// through the ISetupPersistenceService boundary.
///
/// IMPORTANT:
/// This type must not be registered in production Setup flow until the
/// controlled provider credential-validation gate is explicitly opened.
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