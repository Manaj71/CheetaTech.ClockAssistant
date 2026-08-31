using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Providers.UKG;

/// <summary>
/// UKG-specific provider construction.
///
/// ProviderUrl comes exclusively from ClockAssistantConfiguration supplied by
/// the caller. No production tenant URL is embedded here.
/// </summary>
public sealed class UkgTimeClockProviderFactory
    : ITimeClockProviderFactory
{
    public const string ProviderTypeName = "UKG";

    private readonly HttpClient _httpClient;

    public UkgTimeClockProviderFactory(
        HttpClient httpClient)
    {
        _httpClient =
            httpClient
            ?? throw new ArgumentNullException(
                nameof(httpClient));
    }

    public string ProviderType =>
        ProviderTypeName;

    public ITimeClockProvider Create(
        ClockAssistantConfiguration configuration,
        StoredCredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.Equals(
                configuration.ProviderType?.Trim(),
                ProviderTypeName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "UKG factory can only create the UKG provider.",
                nameof(configuration));
        }

        if (!Uri.TryCreate(
                configuration.ProviderUrl?.Trim(),
                UriKind.Absolute,
                out var providerUri) ||
            (providerUri.Scheme != Uri.UriSchemeHttps &&
             providerUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException(
                "ProviderUrl must be an absolute HTTP or HTTPS URI.",
                nameof(configuration));
        }

        UkgCredentials? ukgCredentials = null;

        if (credentials is not null)
        {
            ukgCredentials =
                new UkgCredentials(
                    credentials.Username,
                    credentials.Password);
        }

        return new UkgReadyProvider(
            _httpClient,
            new UkgProviderSettings
            {
                ClockUrl = providerUri
            },
            ukgCredentials);
    }
}