namespace CheetaTech.ClockAssistant.Core.Configuration;

public interface ISetupReadinessService
{
    Task<SetupReadinessResult> EvaluateAsync(
        CancellationToken cancellationToken = default);
}
