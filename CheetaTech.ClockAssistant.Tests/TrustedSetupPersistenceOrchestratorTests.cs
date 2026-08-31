using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class TrustedSetupPersistenceOrchestratorTests
{
    [Fact]
    public async Task CommitAsync_CompleteCandidate_CommitsAndConfirmsReady()
    {
        var credentialStore =
            new FakeCredentialStore();

        var configurationStore =
            new FakeConfigurationStore();

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    AcceptedCredentialResult()
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.True(result.Success);
        Assert.Equal(
            SetupPersistenceStage.ReadyConfirmed,
            result.Stage);
        Assert.True(result.PersistenceAttempted);
        Assert.True(result.ProviderValidationPerformed);
        Assert.True(result.ConfigurationSaved);
        Assert.True(result.CredentialsSaved);

        Assert.Equal(
            "new-user",
            credentialStore.Current?.Username);

        Assert.Equal(
            "UKG",
            configurationStore.Current?.ProviderType);

        Assert.Equal(
            "https://example.test/clock",
            credentialUpdate.LastConfiguration?.ProviderUrl);

        Assert.Same(
            configurationStore.Current,
            credentialUpdate.LastConfiguration);
    }

    [Fact]
    public async Task CommitAsync_LocalValidationFailure_WritesNothing()
    {
        var credentialStore =
            new FakeCredentialStore();

        var configurationStore =
            new FakeConfigurationStore();

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    AcceptedCredentialResult()
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                new SetupCandidate(
                    new ClockAssistantConfiguration(),
                    string.Empty,
                    string.Empty));

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.LocalValidationFailed,
            result.Stage);
        Assert.False(result.PersistenceAttempted);
        Assert.Equal(
            0,
            credentialUpdate.CallCount);
        Assert.Equal(
            0,
            configurationStore.SaveCount);
    }

    [Fact]
    public async Task CommitAsync_CredentialRejected_PreservesPreviousTrustedState()
    {
        var oldCredentials =
            new StoredCredentials(
                "old-user",
                "old-password");

        var oldConfiguration =
            PreviousConfiguration();

        var credentialStore =
            new FakeCredentialStore
            {
                Current = oldCredentials
            };

        var configurationStore =
            new FakeConfigurationStore
            {
                Current = oldConfiguration
            };

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    new CredentialUpdateResult(
                        Success: false,
                        CredentialsSaved: false,
                        TechnicalStatus:
                            "CredentialValidationFailed",
                        Message:
                            "Rejected by fake validator.")
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Equal(
            "old-user",
            credentialStore.Current?.Username);
        Assert.Same(
            oldConfiguration,
            configurationStore.Current);
        Assert.Equal(
            0,
            configurationStore.SaveCount);
    }

    [Fact]
    public async Task CommitAsync_ConfigurationSaveFails_RestoresPreviousCredentialsAndConfiguration()
    {
        var oldCredentials =
            new StoredCredentials(
                "old-user",
                "old-password");

        var oldConfiguration =
            PreviousConfiguration();

        var credentialStore =
            new FakeCredentialStore
            {
                Current = oldCredentials
            };

        var configurationStore =
            new FakeConfigurationStore
            {
                Current = oldConfiguration,
                FailNextSave = true
            };

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    AcceptedCredentialResult()
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.Failed,
            result.Stage);

        Assert.Equal(
            "old-user",
            credentialStore.Current?.Username);

        Assert.Same(
            oldConfiguration,
            configurationStore.Current);

        Assert.True(
            credentialStore.SaveCount >= 2);
    }

    [Fact]
    public async Task CommitAsync_ReadinessNotReady_RestoresPreviousTrustedState()
    {
        var oldCredentials =
            new StoredCredentials(
                "old-user",
                "old-password");

        var oldConfiguration =
            PreviousConfiguration();

        var credentialStore =
            new FakeCredentialStore
            {
                Current = oldCredentials
            };

        var configurationStore =
            new FakeConfigurationStore
            {
                Current = oldConfiguration
            };

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    AcceptedCredentialResult()
            };

        var readiness =
            new AlwaysSetupRequiredReadinessService();

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.Failed,
            result.Stage);
        Assert.Contains(
            "SetupReadinessNotReady",
            result.Issues);

        Assert.Equal(
            "old-user",
            credentialStore.Current?.Username);

        Assert.Same(
            oldConfiguration,
            configurationStore.Current);
    }

    [Fact]
    public async Task CommitAsync_SnapshotReadFails_ReturnsControlledFailureWithoutCredentialUpdate()
    {
        var credentialStore =
            new FakeCredentialStore();

        var configurationStore =
            new FakeConfigurationStore
            {
                FailNextGet = true
            };

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    AcceptedCredentialResult()
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.Failed,
            result.Stage);
        Assert.False(result.PersistenceAttempted);
        Assert.False(result.ProviderValidationPerformed);
        Assert.Contains(
            "TrustedStateSnapshotReadFailed",
            result.Issues);
        Assert.Equal(
            0,
            credentialUpdate.CallCount);
    }

    [Fact]
    public async Task CommitAsync_CredentialUpdateThrows_RestoresPreviousTrustedState()
    {
        var oldCredentials =
            new StoredCredentials(
                "old-user",
                "old-password");

        var oldConfiguration =
            PreviousConfiguration();

        var credentialStore =
            new FakeCredentialStore
            {
                Current = oldCredentials
            };

        var configurationStore =
            new FakeConfigurationStore
            {
                Current = oldConfiguration
            };

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                ThrowAfterSavingCandidate = true
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Contains(
            "CredentialUpdateException",
            result.Issues);
        Assert.Equal(
            "old-user",
            credentialStore.Current?.Username);
        Assert.Same(
            oldConfiguration,
            configurationStore.Current);

        Assert.Equal(
            2,
            credentialStore.SaveCount);

        Assert.Equal(
            0,
            configurationStore.SaveCount);
    }

    [Fact]
    public async Task CommitAsync_FailedCredentialResultClaimsSaved_RestoresPreviousTrustedState()
    {
        var oldCredentials =
            new StoredCredentials(
                "old-user",
                "old-password");

        var oldConfiguration =
            PreviousConfiguration();

        var credentialStore =
            new FakeCredentialStore
            {
                Current = oldCredentials
            };

        var configurationStore =
            new FakeConfigurationStore
            {
                Current = oldConfiguration
            };

        var credentialUpdate =
            new FakeCredentialUpdateService(
                credentialStore)
            {
                Result =
                    new CredentialUpdateResult(
                        Success: false,
                        CredentialsSaved: true,
                        TechnicalStatus:
                            "SimulatedInconsistentCredentialResult",
                        Message:
                            "Simulated inconsistent fake result.")
            };

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            CreateOrchestrator(
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var result =
            await orchestrator.CommitAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Contains(
            "InconsistentCredentialUpdateResult",
            result.Issues);
        Assert.Equal(
            "old-user",
            credentialStore.Current?.Username);
        Assert.Same(
            oldConfiguration,
            configurationStore.Current);

        Assert.Equal(
            2,
            credentialStore.SaveCount);

        Assert.Equal(
            0,
            configurationStore.SaveCount);
    }
    private static TrustedSetupPersistenceOrchestrator CreateOrchestrator(
        ICredentialUpdateService credentialUpdateService,
        ICredentialStore credentialStore,
        IClockAssistantConfigurationStore configurationStore,
        ISetupReadinessService readinessService)
    {
        return new TrustedSetupPersistenceOrchestrator(
            new ConfigurationCompletenessEvaluator(),
            credentialUpdateService,
            credentialStore,
            configurationStore,
            readinessService);
    }

    private static SetupCandidate CompleteCandidate()
    {
        return new SetupCandidate(
            CompleteConfiguration(),
            "new-user",
            "new-password");
    }

    private static ClockAssistantConfiguration CompleteConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl =
                "https://example.test/clock",
            WorkDays = new[]
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

    private static ClockAssistantConfiguration PreviousConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl =
                "https://old.example.test/clock",
            WorkDays = new[]
            {
                DayOfWeek.Monday
            },
            ClockInTime =
                new TimeOnly(8, 0),
            ClockOutTime =
                new TimeOnly(16, 0),
            TimeZoneId =
                "America/Toronto",
            NotificationLeadTime =
                TimeSpan.FromMinutes(10),
            ExecutionMode =
                ClockExecutionMode.BasicConfirmation
        };
    }

    private static CredentialUpdateResult AcceptedCredentialResult()
    {
        return new CredentialUpdateResult(
            Success: true,
            CredentialsSaved: true,
            TechnicalStatus:
                "CredentialsValidatedAndSaved",
            Message:
                "Accepted by fake validator.");
    }

    private sealed class FakeCredentialUpdateService
        : ICredentialUpdateService
    {
        private readonly ICredentialStore _credentialStore;

        public FakeCredentialUpdateService(
            ICredentialStore credentialStore)
        {
            _credentialStore =
                credentialStore;
        }

        public CredentialUpdateResult Result { get; set; } =
            AcceptedCredentialResult();

        public int CallCount { get; private set; }

        public bool ThrowAfterSavingCandidate { get; set; }

        public ClockAssistantConfiguration? LastConfiguration { get; private set; }

        public async Task<CredentialUpdateResult> ValidateAndSaveAsync(
            ClockAssistantConfiguration configuration,
            StoredCredentials candidate,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastConfiguration = configuration;

            if (ThrowAfterSavingCandidate)
            {
                await _credentialStore
                    .SaveCredentialsAsync(
                        candidate,
                        cancellationToken);

                throw new InvalidOperationException(
                    "Simulated credential-update exception after candidate save.");
            }

            if (Result.CredentialsSaved)
            {
                await _credentialStore
                    .SaveCredentialsAsync(
                        candidate,
                        cancellationToken);
            }

            return Result;
        }
    }

    private sealed class FakeCredentialStore
        : ICredentialStore
    {
        public StoredCredentials? Current { get; set; }

        public int SaveCount { get; private set; }

        public Task SaveCredentialsAsync(
            StoredCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCount++;
            Current = credentials;

            return Task.CompletedTask;
        }

        public Task<StoredCredentials?> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Current);
        }

        public Task DeleteCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Current = null;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfigurationStore
        : IClockAssistantConfigurationStore
    {
        public ClockAssistantConfiguration? Current { get; set; }

        public bool FailNextSave { get; set; }

        public bool FailNextGet { get; set; }

        public int SaveCount { get; private set; }

        public Task SaveAsync(
            ClockAssistantConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCount++;

            if (FailNextSave)
            {
                FailNextSave = false;

                throw new InvalidOperationException(
                    "Simulated configuration save failure.");
            }

            Current = configuration;

            return Task.CompletedTask;
        }

        public Task<ClockAssistantConfiguration?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FailNextGet)
            {
                FailNextGet = false;

                throw new InvalidOperationException(
                    "Simulated configuration snapshot read failure.");
            }

            return Task.FromResult(
                Current);
        }

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Current = null;

            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysSetupRequiredReadinessService
        : ISetupReadinessService
    {
        public Task<SetupReadinessResult> EvaluateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new SetupReadinessResult(
                    State:
                        SetupReadinessState.SetupRequired,
                    ConfigurationAvailable: true,
                    ConfigurationComplete: true,
                    CredentialsAvailable: true,
                    ConfigurationIssues:
                        Array.Empty<string>()));
        }
    }
}
