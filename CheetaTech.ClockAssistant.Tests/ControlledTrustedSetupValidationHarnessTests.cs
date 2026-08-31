using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

/// <summary>
/// TEST-ONLY controlled composition harness.
///
/// This class deliberately does not use MauiProgram or application DI.
/// It manually composes the already-validated trusted Setup path with
/// fake/local provider and storage dependencies.
///
/// No HTTP or real provider operation is possible from these tests.
/// </summary>
public sealed class ControlledTrustedSetupValidationHarnessTests
{
    [Fact]
    public async Task Harness_Accepted_Candidate_Traverses_Full_Trusted_Path_And_Reaches_Ready()
    {
        var provider =
            new FakeValidationOnlyProvider(
                accepted: true);

        var resolver =
            new FakeProviderResolver(
                provider);

        var credentialStore =
            new FakeCredentialStore();

        var configurationStore =
            new FakeConfigurationStore();

        var credentialUpdate =
            new CredentialUpdateService(
                resolver,
                credentialStore);

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            new TrustedSetupPersistenceOrchestrator(
                new ConfigurationCompletenessEvaluator(),
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var persistence =
            new TrustedSetupPersistenceService(
                orchestrator);

        var candidate =
            CompleteCandidate();

        var result =
            await persistence
                .PrepareTrustedPersistenceAsync(
                    candidate);

        Assert.True(result.Success);

        Assert.Equal(
            SetupPersistenceStage.ReadyConfirmed,
            result.Stage);

        Assert.True(result.PersistenceAttempted);
        Assert.True(result.ProviderValidationPerformed);
        Assert.True(result.CredentialsSaved);
        Assert.True(result.ConfigurationSaved);

        Assert.Equal(
            1,
            resolver.ResolveCallCount);

        Assert.Same(
            candidate.Configuration,
            resolver.LastConfiguration);

        Assert.Equal(
            1,
            provider.ValidationCallCount);

        Assert.Equal(
            candidate.Username,
            provider.LastValidationUsername);

        Assert.Equal(
            candidate.Password,
            provider.LastValidationPassword);

        Assert.Equal(
            0,
            provider.TestConnectionCallCount);

        Assert.Equal(
            0,
            provider.ClockInCallCount);

        Assert.Equal(
            0,
            provider.ClockOutCallCount);

        Assert.Equal(
            candidate.Username,
            credentialStore.Current?.Username);

        Assert.Same(
            candidate.Configuration,
            configurationStore.Current);
    }

    [Fact]
    public async Task Harness_Rejected_Candidate_Does_Not_Replace_KnownGood_Trusted_State()
    {
        var previousCredentials =
            new StoredCredentials(
                "known-good-user",
                "known-good-password");

        var previousConfiguration =
            PreviousConfiguration();

        var provider =
            new FakeValidationOnlyProvider(
                accepted: false);

        var resolver =
            new FakeProviderResolver(
                provider);

        var credentialStore =
            new FakeCredentialStore(
                previousCredentials);

        var configurationStore =
            new FakeConfigurationStore(
                previousConfiguration);

        var credentialUpdate =
            new CredentialUpdateService(
                resolver,
                credentialStore);

        var readiness =
            new SetupReadinessService(
                configurationStore,
                new ConfigurationCompletenessEvaluator(),
                credentialStore);

        var orchestrator =
            new TrustedSetupPersistenceOrchestrator(
                new ConfigurationCompletenessEvaluator(),
                credentialUpdate,
                credentialStore,
                configurationStore,
                readiness);

        var persistence =
            new TrustedSetupPersistenceService(
                orchestrator);

        var result =
            await persistence
                .PrepareTrustedPersistenceAsync(
                    CompleteCandidate());

        Assert.False(result.Success);

        Assert.Equal(
            SetupPersistenceStage.Failed,
            result.Stage);

        Assert.True(result.PersistenceAttempted);
        Assert.True(result.ProviderValidationPerformed);
        Assert.False(result.CredentialsSaved);
        Assert.False(result.ConfigurationSaved);

        Assert.Equal(
            1,
            provider.ValidationCallCount);

        Assert.Equal(
            0,
            provider.TestConnectionCallCount);

        Assert.Equal(
            0,
            provider.ClockInCallCount);

        Assert.Equal(
            0,
            provider.ClockOutCallCount);

        Assert.Same(
            previousCredentials,
            credentialStore.Current);

        Assert.Same(
            previousConfiguration,
            configurationStore.Current);

        Assert.Equal(
            0,
            configurationStore.SaveCount);
    }

    private static SetupCandidate CompleteCandidate()
    {
        return new SetupCandidate(
            Configuration:
                new ClockAssistantConfiguration
                {
                    ProviderType = "UKG",
                    ProviderUrl =
                        "https://controlled.invalid/clock",
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
                },
            Username:
                "candidate-user",
            Password:
                "candidate-password");
    }

    private static ClockAssistantConfiguration PreviousConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl =
                "https://known-good.invalid/clock",
            WorkDays =
                new[]
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

    private sealed class FakeProviderResolver
        : ITimeClockProviderResolver
    {
        private readonly ITimeClockProvider _provider;

        public FakeProviderResolver(
            ITimeClockProvider provider)
        {
            _provider = provider;
        }

        public int ResolveCallCount { get; private set; }

        public ClockAssistantConfiguration? LastConfiguration { get; private set; }

        public StoredCredentials? LastCredentials { get; private set; }

        public ITimeClockProvider Resolve(
            ClockAssistantConfiguration configuration,
            StoredCredentials? credentials = null)
        {
            ResolveCallCount++;
            LastConfiguration = configuration;
            LastCredentials = credentials;

            return _provider;
        }
    }

    private sealed class FakeValidationOnlyProvider
        : ITimeClockProvider
    {
        private readonly bool _accepted;

        public FakeValidationOnlyProvider(
            bool accepted)
        {
            _accepted = accepted;
        }

        public int TestConnectionCallCount { get; private set; }

        public int ValidationCallCount { get; private set; }

        public int ClockInCallCount { get; private set; }

        public int ClockOutCallCount { get; private set; }

        public string? LastValidationUsername { get; private set; }

        public string? LastValidationPassword { get; private set; }

        public Task<ProviderResult> TestConnectionAsync()
        {
            TestConnectionCallCount++;

            throw new InvalidOperationException(
                "Controlled harness must not call TestConnectionAsync.");
        }

        public Task<ProviderResult> ValidateCredentialsAsync(
            string username,
            string password)
        {
            ValidationCallCount++;
            LastValidationUsername = username;
            LastValidationPassword = password;

            return Task.FromResult(
                new ProviderResult
                {
                    Success = _accepted,
                    Action = "ValidateCredentials",
                    TechnicalStatus =
                        _accepted
                            ? "CredentialsAccepted"
                            : "CredentialsRejected",
                    ProviderMessage =
                        _accepted
                            ? "Fake provider accepted credentials."
                            : "Fake provider rejected credentials.",
                    ErrorMessage =
                        _accepted
                            ? null
                            : "Fake provider rejected credentials.",
                    Timestamp =
                        DateTimeOffset.UtcNow
                });
        }

        public Task<ProviderResult> ClockInAsync()
        {
            ClockInCallCount++;

            throw new InvalidOperationException(
                "Controlled harness must never call ClockInAsync.");
        }

        public Task<ProviderResult> ClockOutAsync()
        {
            ClockOutCallCount++;

            throw new InvalidOperationException(
                "Controlled harness must never call ClockOutAsync.");
        }
    }

    private sealed class FakeCredentialStore
        : ICredentialStore
    {
        public FakeCredentialStore(
            StoredCredentials? initial = null)
        {
            Current = initial;
        }

        public StoredCredentials? Current { get; private set; }

        public int SaveCount { get; private set; }

        public int DeleteCount { get; private set; }

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

            DeleteCount++;
            Current = null;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfigurationStore
        : IClockAssistantConfigurationStore
    {
        public FakeConfigurationStore(
            ClockAssistantConfiguration? initial = null)
        {
            Current = initial;
        }

        public ClockAssistantConfiguration? Current { get; private set; }

        public int SaveCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Task SaveAsync(
            ClockAssistantConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCount++;
            Current = configuration;

            return Task.CompletedTask;
        }

        public Task<ClockAssistantConfiguration?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Current);
        }

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DeleteCount++;
            Current = null;

            return Task.CompletedTask;
        }
    }
}