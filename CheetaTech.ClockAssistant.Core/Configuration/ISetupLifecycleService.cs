namespace CheetaTech.ClockAssistant.Core.Configuration;

public interface ISetupLifecycleService
{
    Task<SetupLifecycleResult> EvaluateCandidateAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default);
}
