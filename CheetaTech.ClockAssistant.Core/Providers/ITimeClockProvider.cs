namespace CheetaTech.ClockAssistant.Core.Providers;

public interface ITimeClockProvider
{
    Task<ProviderResult> TestConnectionAsync();

    Task<ProviderResult> ValidateCredentialsAsync(
        string username,
        string password);

    Task<ProviderResult> ClockInAsync();

    Task<ProviderResult> ClockOutAsync();
}

