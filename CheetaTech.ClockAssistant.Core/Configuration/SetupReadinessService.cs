using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed class SetupReadinessService
    : ISetupReadinessService
{
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly IConfigurationCompletenessEvaluator _completenessEvaluator;
    private readonly ICredentialStore _credentialStore;

    public SetupReadinessService(
        IClockAssistantConfigurationStore configurationStore,
        IConfigurationCompletenessEvaluator completenessEvaluator,
        ICredentialStore credentialStore)
    {
        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(
                nameof(configurationStore));

        _completenessEvaluator =
            completenessEvaluator
            ?? throw new ArgumentNullException(
                nameof(completenessEvaluator));

        _credentialStore =
            credentialStore
            ?? throw new ArgumentNullException(
                nameof(credentialStore));
    }

    public async Task<SetupReadinessResult> EvaluateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configuration = await _configurationStore
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

        var completeness =
            _completenessEvaluator.Evaluate(configuration);

        cancellationToken.ThrowIfCancellationRequested();

        var credentials = await _credentialStore
            .GetCredentialsAsync(cancellationToken)
            .ConfigureAwait(false);

        var configurationAvailable =
            configuration is not null;

        var credentialsAvailable =
            credentials is not null;

        var ready =
            configurationAvailable
            && completeness.IsComplete
            && credentialsAvailable;

        return new SetupReadinessResult(
            State: ready
                ? SetupReadinessState.Ready
                : SetupReadinessState.SetupRequired,
            ConfigurationAvailable: configurationAvailable,
            ConfigurationComplete: completeness.IsComplete,
            CredentialsAvailable: credentialsAvailable,
            ConfigurationIssues: completeness.MissingOrInvalidFields);
    }
}
