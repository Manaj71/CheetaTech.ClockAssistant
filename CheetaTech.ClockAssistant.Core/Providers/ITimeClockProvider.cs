namespace CheetaTech.ClockAssistant.Core.Providers;

public interface ITimeClockProvider
{
    Task<ProviderResult> TestConnectionAsync();

    Task<ProviderResult> ClockInAsync();

    Task<ProviderResult> ClockOutAsync();
}
