namespace CheetaTech.ClockAssistant.Core.Configuration;

public interface IClockAssistantConfigurationStore
{
    Task SaveAsync(
        ClockAssistantConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<ClockAssistantConfiguration?> GetAsync(
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CancellationToken cancellationToken = default);
}
