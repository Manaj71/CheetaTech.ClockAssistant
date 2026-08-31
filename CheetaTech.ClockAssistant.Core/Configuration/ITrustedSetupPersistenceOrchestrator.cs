namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Coordinates the trusted Setup commit sequence after local candidate construction.
///
/// Production wiring must not use this orchestration until the controlled provider
/// credential-validation gate is explicitly opened. Unit tests may use mocked/local
/// implementations of the existing dependencies.
/// </summary>
public interface ITrustedSetupPersistenceOrchestrator
{
    Task<SetupPersistenceResult> CommitAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default);
}
