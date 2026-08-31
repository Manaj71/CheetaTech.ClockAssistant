using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class DryRunSetupLifecycleServiceTests
{
    [Fact]
    public async Task EvaluateCandidateAsync_CompleteCandidate_Reaches_DryRun_Persistence_Gate()
    {
        var persistence =
            new FakeSetupPersistenceService(
                new SetupPersistenceResult(
                    Success: false,
                    Stage:
                        SetupPersistenceStage.AwaitingProviderCredentialValidation,
                    PersistenceAttempted: false,
                    ProviderValidationPerformed: false,
                    ConfigurationSaved: false,
                    CredentialsSaved: false,
                    Issues:
                        Array.Empty<string>(),
                    Message:
                        "Setup candidate passed local checks but is not yet trusted."));

        var service =
            CreateService(
                persistence);

        var candidate =
            new SetupCandidate(
                CompleteConfiguration(),
                "user1",
                "password1");

        var result =
            await service.EvaluateCandidateAsync(
                candidate);

        Assert.False(result.Success);
        Assert.True(result.ConfigurationValid);
        Assert.True(result.CredentialsLocallyValid);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);
        Assert.Empty(result.Issues);

        Assert.Equal(
            1,
            persistence.CallCount);

        Assert.Same(
            candidate,
            persistence.LastCandidate);
    }

    [Fact]
    public async Task EvaluateCandidateAsync_MissingUsername_FailsLocally_Without_Persistence_Call()
    {
        var persistence =
            new FakeSetupPersistenceService();

        var service =
            CreateService(
                persistence);

        var result =
            await service.EvaluateCandidateAsync(
                new SetupCandidate(
                    CompleteConfiguration(),
                    " ",
                    "password1"));

        Assert.False(result.Success);
        Assert.True(result.ConfigurationValid);
        Assert.False(result.CredentialsLocallyValid);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);
        Assert.Contains(
            "Username",
            result.Issues);

        Assert.Equal(
            0,
            persistence.CallCount);
    }

    [Fact]
    public async Task EvaluateCandidateAsync_MissingPassword_FailsLocally_Without_Persistence_Call()
    {
        var persistence =
            new FakeSetupPersistenceService();

        var service =
            CreateService(
                persistence);

        var result =
            await service.EvaluateCandidateAsync(
                new SetupCandidate(
                    CompleteConfiguration(),
                    "user1",
                    string.Empty));

        Assert.False(result.Success);
        Assert.True(result.ConfigurationValid);
        Assert.False(result.CredentialsLocallyValid);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);
        Assert.Contains(
            "Password",
            result.Issues);

        Assert.Equal(
            0,
            persistence.CallCount);
    }

    [Fact]
    public async Task EvaluateCandidateAsync_IncompleteConfiguration_FailsLocally_Without_Persistence_Call()
    {
        var persistence =
            new FakeSetupPersistenceService();

        var service =
            CreateService(
                persistence);

        var result =
            await service.EvaluateCandidateAsync(
                new SetupCandidate(
                    new ClockAssistantConfiguration(),
                    "user1",
                    "password1"));

        Assert.False(result.Success);
        Assert.False(result.ConfigurationValid);
        Assert.True(result.CredentialsLocallyValid);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);

        Assert.Contains(
            "ProviderType",
            result.Issues);

        Assert.Contains(
            "ProviderUrl",
            result.Issues);

        Assert.Contains(
            "WorkDays",
            result.Issues);

        Assert.Contains(
            "ClockInTime",
            result.Issues);

        Assert.Contains(
            "ClockOutTime",
            result.Issues);

        Assert.Contains(
            "TimeZoneId",
            result.Issues);

        Assert.Equal(
            0,
            persistence.CallCount);
    }

    [Fact]
    public async Task EvaluateCandidateAsync_NullConfiguration_FailsLocally_Without_Persistence_Call()
    {
        var persistence =
            new FakeSetupPersistenceService();

        var service =
            CreateService(
                persistence);

        var result =
            await service.EvaluateCandidateAsync(
                new SetupCandidate(
                    null,
                    "user1",
                    "password1"));

        Assert.False(result.Success);
        Assert.False(result.ConfigurationValid);
        Assert.True(result.CredentialsLocallyValid);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);
        Assert.Contains(
            "Configuration",
            result.Issues);

        Assert.Equal(
            0,
            persistence.CallCount);
    }

    [Fact]
    public async Task EvaluateCandidateAsync_Maps_Persistence_Result_Without_Provider_Knowledge()
    {
        var persistence =
            new FakeSetupPersistenceService(
                new SetupPersistenceResult(
                    Success: false,
                    Stage:
                        SetupPersistenceStage.AwaitingProviderCredentialValidation,
                    PersistenceAttempted: false,
                    ProviderValidationPerformed: false,
                    ConfigurationSaved: false,
                    CredentialsSaved: false,
                    Issues:
                        new[]
                        {
                            "ControlledGateClosed"
                        },
                    Message:
                        "Controlled provider-validation gate remains closed."));

        var service =
            CreateService(
                persistence);

        var result =
            await service.EvaluateCandidateAsync(
                new SetupCandidate(
                    CompleteConfiguration(),
                    "user1",
                    "password1"));

        Assert.False(result.Success);
        Assert.True(result.ConfigurationValid);
        Assert.True(result.CredentialsLocallyValid);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);

        Assert.Contains(
            "ControlledGateClosed",
            result.Issues);

        Assert.Equal(
            "Controlled provider-validation gate remains closed.",
            result.Message);
    }

    private static DryRunSetupLifecycleService CreateService(
        ISetupPersistenceService persistenceService)
    {
        return new DryRunSetupLifecycleService(
            new ConfigurationCompletenessEvaluator(),
            persistenceService);
    }

    private static ClockAssistantConfiguration CompleteConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl =
                "https://example.test/clock",
            WorkDays =
                new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
            ClockInTime =
                new TimeOnly(7, 53),
            ClockOutTime =
                new TimeOnly(15, 0),
            TimeZoneId =
                "America/Toronto",
            NotificationLeadTime =
                TimeSpan.FromMinutes(15),
            ExecutionMode =
                ClockExecutionMode.BasicConfirmation
        };
    }

    private sealed class FakeSetupPersistenceService
        : ISetupPersistenceService
    {
        private readonly SetupPersistenceResult _result;

        public FakeSetupPersistenceService(
            SetupPersistenceResult? result = null)
        {
            _result =
                result
                ?? new SetupPersistenceResult(
                    Success: false,
                    Stage:
                        SetupPersistenceStage.AwaitingProviderCredentialValidation,
                    PersistenceAttempted: false,
                    ProviderValidationPerformed: false,
                    ConfigurationSaved: false,
                    CredentialsSaved: false,
                    Issues:
                        Array.Empty<string>(),
                    Message:
                        "DryRun persistence gate.");
        }

        public int CallCount { get; private set; }

        public SetupCandidate? LastCandidate { get; private set; }

        public Task<SetupPersistenceResult> PrepareTrustedPersistenceAsync(
            SetupCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            LastCandidate = candidate;

            return Task.FromResult(
                _result);
        }
    }
}