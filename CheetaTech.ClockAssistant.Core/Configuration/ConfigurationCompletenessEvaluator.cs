namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed class ConfigurationCompletenessEvaluator
    : IConfigurationCompletenessEvaluator
{
    public ConfigurationCompletenessResult Evaluate(
        ClockAssistantConfiguration? configuration)
    {
        var issues = new List<string>();

        if (configuration is null)
        {
            issues.Add("Configuration");
            return new ConfigurationCompletenessResult(
                IsComplete: false,
                MissingOrInvalidFields: issues);
        }

        if (string.IsNullOrWhiteSpace(configuration.ProviderType))
        {
            issues.Add(nameof(configuration.ProviderType));
        }

        if (!TryValidateProviderUrl(configuration.ProviderUrl))
        {
            issues.Add(nameof(configuration.ProviderUrl));
        }

        if (configuration.WorkDays.Count == 0)
        {
            issues.Add(nameof(configuration.WorkDays));
        }

        if (!configuration.ClockInTime.HasValue)
        {
            issues.Add(nameof(configuration.ClockInTime));
        }

        if (!configuration.ClockOutTime.HasValue)
        {
            issues.Add(nameof(configuration.ClockOutTime));
        }

        if (string.IsNullOrWhiteSpace(configuration.TimeZoneId))
        {
            issues.Add(nameof(configuration.TimeZoneId));
        }

        if (configuration.NotificationLeadTime < TimeSpan.Zero)
        {
            issues.Add(nameof(configuration.NotificationLeadTime));
        }

        return new ConfigurationCompletenessResult(
            IsComplete: issues.Count == 0,
            MissingOrInvalidFields: issues);
    }

    private static bool TryValidateProviderUrl(
        string providerUrl)
    {
        if (!Uri.TryCreate(
                providerUrl,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeHttp;
    }
}

