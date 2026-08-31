using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Core.Providers;

/// <summary>
/// Resolves the configured provider from trusted or candidate configuration.
/// </summary>
public interface ITimeClockProviderResolver
{
    ITimeClockProvider Resolve(
        ClockAssistantConfiguration configuration,
        StoredCredentials? credentials = null);
}