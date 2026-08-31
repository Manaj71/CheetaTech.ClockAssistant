using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Provider-independent trusted Setup persistence orchestrator.
///
/// The orchestrator does not know provider mechanics. Credential validation and
/// credential saving remain owned by ICredentialUpdateService.
///
/// Commit sequence:
/// 1. validate candidate locally;
/// 2. snapshot previous trusted state;
/// 3. validate/save candidate credentials;
/// 4. save non-secret configuration;
/// 5. confirm SetupReadinessState.Ready.
///
/// If a failure occurs after credentials have changed, the previous configuration
/// and credential snapshots are restored on a best-effort basis.
///
/// This type contains no MAUI, SecureStorage, Preferences, HttpClient, or
/// provider-specific dependency.
/// </summary>
public sealed class TrustedSetupPersistenceOrchestrator
    : ITrustedSetupPersistenceOrchestrator
{
    private readonly IConfigurationCompletenessEvaluator _completenessEvaluator;
    private readonly ICredentialUpdateService _credentialUpdateService;
    private readonly ICredentialStore _credentialStore;
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly ISetupReadinessService _setupReadinessService;

    public TrustedSetupPersistenceOrchestrator(
        IConfigurationCompletenessEvaluator completenessEvaluator,
        ICredentialUpdateService credentialUpdateService,
        ICredentialStore credentialStore,
        IClockAssistantConfigurationStore configurationStore,
        ISetupReadinessService setupReadinessService)
    {
        _completenessEvaluator =
            completenessEvaluator
            ?? throw new ArgumentNullException(
                nameof(completenessEvaluator));

        _credentialUpdateService =
            credentialUpdateService
            ?? throw new ArgumentNullException(
                nameof(credentialUpdateService));

        _credentialStore =
            credentialStore
            ?? throw new ArgumentNullException(
                nameof(credentialStore));

        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(
                nameof(configurationStore));

        _setupReadinessService =
            setupReadinessService
            ?? throw new ArgumentNullException(
                nameof(setupReadinessService));
    }

    public async Task<SetupPersistenceResult> CommitAsync(
        SetupCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        var issues =
            ValidateLocally(candidate);

        if (issues.Count > 0)
        {
            return new SetupPersistenceResult(
                Success: false,
                Stage:
                    SetupPersistenceStage.LocalValidationFailed,
                PersistenceAttempted: false,
                ProviderValidationPerformed: false,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues: issues,
                Message:
                    "Setup candidate failed local validation. " +
                    "Trusted persistence was not attempted.");
        }

        ClockAssistantConfiguration? previousConfiguration;
        StoredCredentials? previousCredentials;

        try
        {
            previousConfiguration =
                await _configurationStore
                    .GetAsync(cancellationToken)
                    .ConfigureAwait(false);

            previousCredentials =
                await _credentialStore
                    .GetCredentialsAsync(cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new SetupPersistenceResult(
                Success: false,
                Stage: SetupPersistenceStage.Failed,
                PersistenceAttempted: false,
                ProviderValidationPerformed: false,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues: new[]
                {
                    "TrustedStateSnapshotReadFailed"
                },
                Message:
                    "Trusted Setup commit could not snapshot the existing " +
                    $"trusted state: {ex.Message}");
        }

        var candidateCredentials =
            new StoredCredentials(
                candidate.Username,
                candidate.Password);

        CredentialUpdateResult credentialUpdate;

        try
        {
            credentialUpdate =
                await _credentialUpdateService
                    .ValidateAndSaveAsync(
                        candidate.Configuration!,
                        candidateCredentials,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var rollbackIssues =
                await RestorePreviousCredentialsAsync(
                    previousCredentials,
                    CancellationToken.None)
                .ConfigureAwait(false);

            var credentialFailureIssues =
                new List<string>
                {
                    "CredentialUpdateException"
                };

            credentialFailureIssues.AddRange(
                rollbackIssues);

            return new SetupPersistenceResult(
                Success: false,
                Stage: SetupPersistenceStage.Failed,
                PersistenceAttempted: true,
                ProviderValidationPerformed: true,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues: credentialFailureIssues,
                Message:
                    "Credential validation/update threw before configuration " +
                    "commit. Previous trusted state was restored where possible: " +
                    ex.Message);
        }

        if (!credentialUpdate.Success ||
            !credentialUpdate.CredentialsSaved)
        {
            var credentialFailureIssues =
                new List<string>
                {
                    credentialUpdate.TechnicalStatus
                };

            if (credentialUpdate.CredentialsSaved)
            {
                var rollbackIssues =
                    await RestorePreviousCredentialsAsync(
                        previousCredentials,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                credentialFailureIssues.Add(
                    "InconsistentCredentialUpdateResult");

                credentialFailureIssues.AddRange(
                    rollbackIssues);
            }

            return new SetupPersistenceResult(
                Success: false,
                Stage: SetupPersistenceStage.Failed,
                PersistenceAttempted: true,
                ProviderValidationPerformed: true,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues: credentialFailureIssues,
                Message:
                    credentialUpdate.Message
                    ?? "Candidate credentials were not accepted.");
        }

        try
        {
            await _configurationStore
                .SaveAsync(
                    candidate.Configuration!,
                    cancellationToken)
                .ConfigureAwait(false);

            var readiness =
                await _setupReadinessService
                    .EvaluateAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (readiness.State !=
                SetupReadinessState.Ready)
            {
                var rollbackIssues =
                    await RestorePreviousStateAsync(
                        previousConfiguration,
                        previousCredentials,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                var issuesAfterReadinessFailure =
                    new List<string>
                    {
                        "SetupReadinessNotReady"
                    };

                issuesAfterReadinessFailure.AddRange(
                    rollbackIssues);

                return new SetupPersistenceResult(
                    Success: false,
                    Stage: SetupPersistenceStage.Failed,
                    PersistenceAttempted: true,
                    ProviderValidationPerformed: true,
                    ConfigurationSaved: true,
                    CredentialsSaved: true,
                    Issues: issuesAfterReadinessFailure,
                    Message:
                        "Trusted Setup commit did not reach Ready state. " +
                        "Previous trusted state was restored where possible.");
            }

            return new SetupPersistenceResult(
                Success: true,
                Stage:
                    SetupPersistenceStage.ReadyConfirmed,
                PersistenceAttempted: true,
                ProviderValidationPerformed: true,
                ConfigurationSaved: true,
                CredentialsSaved: true,
                Issues: Array.Empty<string>(),
                Message:
                    "Trusted Setup candidate committed and readiness confirmed.");
        }
        catch (Exception ex)
        {
            var rollbackIssues =
                await RestorePreviousStateAsync(
                    previousConfiguration,
                    previousCredentials,
                    CancellationToken.None)
                .ConfigureAwait(false);

            var failureIssues =
                new List<string>
                {
                    "TrustedSetupCommitException"
                };

            failureIssues.AddRange(
                rollbackIssues);

            return new SetupPersistenceResult(
                Success: false,
                Stage: SetupPersistenceStage.Failed,
                PersistenceAttempted: true,
                ProviderValidationPerformed: true,
                ConfigurationSaved: false,
                CredentialsSaved: true,
                Issues: failureIssues,
                Message:
                    "Trusted Setup commit failed after credential update. " +
                    $"Previous trusted state was restored where possible: {ex.Message}");
        }
    }

    private List<string> ValidateLocally(
        SetupCandidate candidate)
    {
        var issues = new List<string>();

        var completeness =
            _completenessEvaluator.Evaluate(
                candidate.Configuration);

        if (!completeness.IsComplete)
        {
            issues.AddRange(
                completeness.MissingOrInvalidFields);
        }

        if (string.IsNullOrWhiteSpace(
                candidate.Username))
        {
            issues.Add(
                nameof(candidate.Username));
        }

        if (string.IsNullOrWhiteSpace(
                candidate.Password))
        {
            issues.Add(
                nameof(candidate.Password));
        }

        return issues;
    }

    private async Task<IReadOnlyCollection<string>> RestorePreviousCredentialsAsync(
        StoredCredentials? previousCredentials,
        CancellationToken cancellationToken)
    {
        var rollbackIssues =
            new List<string>();

        try
        {
            if (previousCredentials is null)
            {
                await _credentialStore
                    .DeleteCredentialsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _credentialStore
                    .SaveCredentialsAsync(
                        previousCredentials,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            rollbackIssues.Add(
                "CredentialRollbackFailed");
        }

        return rollbackIssues;
    }
    private async Task<IReadOnlyCollection<string>> RestorePreviousStateAsync(
        ClockAssistantConfiguration? previousConfiguration,
        StoredCredentials? previousCredentials,
        CancellationToken cancellationToken)
    {
        var rollbackIssues =
            new List<string>();

        try
        {
            if (previousCredentials is null)
            {
                await _credentialStore
                    .DeleteCredentialsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _credentialStore
                    .SaveCredentialsAsync(
                        previousCredentials,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            rollbackIssues.Add(
                "CredentialRollbackFailed");
        }

        try
        {
            if (previousConfiguration is null)
            {
                await _configurationStore
                    .DeleteAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _configurationStore
                    .SaveAsync(
                        previousConfiguration,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            rollbackIssues.Add(
                "ConfigurationRollbackFailed");
        }

        return rollbackIssues;
    }
}
