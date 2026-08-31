using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Core.Providers;

/// <summary>
/// Provider-independent resolver. Provider-specific knowledge remains inside
/// registered ITimeClockProviderFactory implementations.
/// </summary>
public sealed class TimeClockProviderResolver
    : ITimeClockProviderResolver
{
    private readonly IReadOnlyCollection<ITimeClockProviderFactory> _factories;

    public TimeClockProviderResolver(
        IEnumerable<ITimeClockProviderFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);

        _factories =
            factories.ToArray();
    }

    public ITimeClockProvider Resolve(
        ClockAssistantConfiguration configuration,
        StoredCredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var providerType =
            configuration.ProviderType?.Trim();

        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new ArgumentException(
                "ProviderType is required before resolving a time-clock provider.",
                nameof(configuration));
        }

        var matches =
            _factories
                .Where(factory =>
                    string.Equals(
                        factory.ProviderType?.Trim(),
                        providerType,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (matches.Length == 0)
        {
            throw new NotSupportedException(
                $"Provider type '{providerType}' is not registered.");
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Provider type '{providerType}' has multiple registered factories.");
        }

        return matches[0]
            .Create(
                configuration,
                credentials);
    }
}