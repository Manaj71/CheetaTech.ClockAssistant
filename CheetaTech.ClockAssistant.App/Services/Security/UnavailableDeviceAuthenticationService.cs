namespace CheetaTech.ClockAssistant.App.Services.Security;

/// <summary>
/// Fail-closed fallback for platforms whose device-auth adapter
/// has not yet been implemented.
/// </summary>
public sealed class UnavailableDeviceAuthenticationService
    : IDeviceAuthenticationService
{
    public Task<DeviceAuthenticationStatus> AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            DeviceAuthenticationStatus.Unavailable);
    }
}