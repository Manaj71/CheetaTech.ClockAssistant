using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Core.Providers;

/// <summary>
/// Provider-specific construction contract.
///
/// Implementations belong in provider-specific projects. Core scheduling and
/// attendance logic must not change when another provider is added.
/// </summary>
public interface ITimeClockProviderFactory
{
    string ProviderType { get; }

    ITimeClockProvider Create(
        ClockAssistantConfiguration configuration,
        StoredCredentials? credentials = null);
}