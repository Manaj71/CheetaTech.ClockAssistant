namespace CheetaTech.ClockAssistant.App.Services.Security;

public enum DeviceAuthenticationStatus
{
    Succeeded,
    NotConfigured,
    Unavailable,
    CanceledOrFailed
}

public interface IDeviceAuthenticationService
{
    Task<DeviceAuthenticationStatus> AuthenticateAsync(
        CancellationToken cancellationToken = default);
}