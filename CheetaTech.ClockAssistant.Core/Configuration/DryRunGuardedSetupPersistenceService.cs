namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Phase 4 safety implementation.
///
/// A locally valid Setup candidate is not yet trusted because provider
/// credential validation is intentionally disabled until the controlled
/// application validation gate.
///
/// This service therefore never writes the candidate into trusted storage.
/// </summary>
public sealed class DryRunGuardedSetupPersistenceService
    : ISetupPersistenceService
{
    private readonly IConfigurationCompletenessEvaluator _completenessEvaluator;

    public DryRunGuardedSetupPersistenceService(
        IConfigurationCompletenessEvaluator completenessEvaluator)
    {
        _completenessEvaluator =
            completenessEvaluator
            ?? throw new ArgumentNullException(
                nameof(completenessEvaluator));
    }

    public Task<SetupPersistenceResult> PrepareTrustedPersistenceAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<string>();

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
            issues.Add(nameof(candidate.Username));
        }

        if (string.IsNullOrWhiteSpace(candidate.Password))
        {
            issues.Add(nameof(candidate.Password));
        }

        if (issues.Count > 0)
        {
            return Task.FromResult(
                new SetupPersistenceResult(
                    Success: false,
                    Stage:
                        SetupPersistenceStage.LocalValidationFailed,
                    PersistenceAttempted: false,
                    ProviderValidationPerformed: false,
                    ConfigurationSaved: false,
                    CredentialsSaved: false,
                    Issues: issues,
                    Message:
                        "Setup candidate is not locally complete. " +
                        "Trusted persistence was not attempted."));
        }

        return Task.FromResult(
            new SetupPersistenceResult(
                Success: false,
                Stage:
                    SetupPersistenceStage.AwaitingProviderCredentialValidation,
                PersistenceAttempted: false,
                ProviderValidationPerformed: false,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues: Array.Empty<string>(),
                Message:
                    "Setup candidate passed local checks but is not yet trusted. " +
                    "Provider credential validation is required at the controlled " +
                    "application validation gate before trusted persistence."));
    }
}
