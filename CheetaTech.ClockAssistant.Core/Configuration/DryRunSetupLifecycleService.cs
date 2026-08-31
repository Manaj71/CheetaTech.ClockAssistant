namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed class DryRunSetupLifecycleService
    : ISetupLifecycleService
{
    private readonly IConfigurationCompletenessEvaluator _completenessEvaluator;
    private readonly ISetupPersistenceService _setupPersistenceService;

    public DryRunSetupLifecycleService(
        IConfigurationCompletenessEvaluator completenessEvaluator,
        ISetupPersistenceService setupPersistenceService)
    {
        _completenessEvaluator =
            completenessEvaluator
            ?? throw new ArgumentNullException(
                nameof(completenessEvaluator));

        _setupPersistenceService =
            setupPersistenceService
            ?? throw new ArgumentNullException(
                nameof(setupPersistenceService));
    }

    public async Task<SetupLifecycleResult> EvaluateCandidateAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var issues =
            new List<string>();

        var completeness =
            _completenessEvaluator.Evaluate(
                candidate.Configuration);

        if (!completeness.IsComplete)
        {
            issues.AddRange(
                completeness.MissingOrInvalidFields);
        }

        if (string.IsNullOrWhiteSpace(candidate.Username))
        {
            issues.Add(
                nameof(candidate.Username));
        }

        if (string.IsNullOrWhiteSpace(candidate.Password))
        {
            issues.Add(
                nameof(candidate.Password));
        }

        var configurationValid =
            completeness.IsComplete;

        var credentialsLocallyValid =
            !string.IsNullOrWhiteSpace(candidate.Username)
            && !string.IsNullOrWhiteSpace(candidate.Password);

        if (!configurationValid ||
            !credentialsLocallyValid)
        {
            return new SetupLifecycleResult(
                Success: false,
                ConfigurationValid: configurationValid,
                CredentialsLocallyValid: credentialsLocallyValid,
                ProviderRequestSent: false,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues: issues,
                Message:
                    "Setup candidate has missing or invalid fields. " +
                    "Nothing was sent or saved.");
        }

        var persistence =
            await _setupPersistenceService
                .PrepareTrustedPersistenceAsync(
                    candidate,
                    cancellationToken)
                .ConfigureAwait(false);

        return new SetupLifecycleResult(
            Success: persistence.Success,
            ConfigurationValid: true,
            CredentialsLocallyValid: true,
            ProviderRequestSent:
                persistence.ProviderValidationPerformed,
            ConfigurationSaved:
                persistence.ConfigurationSaved,
            CredentialsSaved:
                persistence.CredentialsSaved,
            Issues:
                persistence.Issues,
            Message:
                persistence.Message);
    }
}