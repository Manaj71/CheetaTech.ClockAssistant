namespace CheetaTech.ClockAssistant.Core.Configuration;

public interface ISetupPersistenceService
{
    Task<SetupPersistenceResult> PrepareTrustedPersistenceAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default);
}
