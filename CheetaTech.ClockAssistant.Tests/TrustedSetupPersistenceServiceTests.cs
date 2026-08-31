using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class TrustedSetupPersistenceServiceTests
{
    [Fact]
    public async Task PrepareTrustedPersistenceAsync_Delegates_Exact_Candidate_To_Orchestrator()
    {
        var expected =
            new SetupPersistenceResult(
                Success: true,
                Stage:
                    SetupPersistenceStage.ReadyConfirmed,
                PersistenceAttempted: true,
                ProviderValidationPerformed: true,
                ConfigurationSaved: true,
                CredentialsSaved: true,
                Issues:
                    Array.Empty<string>(),
                Message:
                    "Fake trusted commit completed.");

        var orchestrator =
            new FakeTrustedSetupPersistenceOrchestrator(
                expected);

        var service =
            new TrustedSetupPersistenceService(
                orchestrator);

        var candidate =
            CompleteCandidate();

        var result =
            await service.PrepareTrustedPersistenceAsync(
                candidate);

        Assert.Same(
            expected,
            result);

        Assert.Equal(
            1,
            orchestrator.CallCount);

        Assert.Same(
            candidate,
            orchestrator.LastCandidate);
    }

    [Fact]
    public async Task PrepareTrustedPersistenceAsync_Preserves_Failure_Result_Without_Reinterpretation()
    {
        var expected =
            new SetupPersistenceResult(
                Success: false,
                Stage:
                    SetupPersistenceStage.Failed,
                PersistenceAttempted: true,
                ProviderValidationPerformed: true,
                ConfigurationSaved: false,
                CredentialsSaved: false,
                Issues:
                    new[]
                    {
                        "CredentialValidationFailed"
                    },
                Message:
                    "Fake trusted commit rejected candidate.");

        var orchestrator =
            new FakeTrustedSetupPersistenceOrchestrator(
                expected);

        var service =
            new TrustedSetupPersistenceService(
                orchestrator);

        var result =
            await service.PrepareTrustedPersistenceAsync(
                CompleteCandidate());

        Assert.Same(
            expected,
            result);

        Assert.Equal(
            "CredentialValidationFailed",
            Assert.Single(result.Issues));
    }

    [Fact]
    public async Task PrepareTrustedPersistenceAsync_Forwards_CancellationToken()
    {
        using var cts =
            new CancellationTokenSource();

        var orchestrator =
            new FakeTrustedSetupPersistenceOrchestrator(
                new SetupPersistenceResult(
                    Success: false,
                    Stage:
                        SetupPersistenceStage.Failed,
                    PersistenceAttempted: false,
                    ProviderValidationPerformed: false,
                    ConfigurationSaved: false,
                    CredentialsSaved: false,
                    Issues:
                        Array.Empty<string>(),
                    Message:
                        "Not used."));

        var service =
            new TrustedSetupPersistenceService(
                orchestrator);

        await service.PrepareTrustedPersistenceAsync(
            CompleteCandidate(),
            cts.Token);

        Assert.Equal(
            cts.Token,
            orchestrator.LastCancellationToken);
    }

    [Fact]
    public async Task PrepareTrustedPersistenceAsync_NullCandidate_Throws_Before_Orchestrator_Call()
    {
        var orchestrator =
            new FakeTrustedSetupPersistenceOrchestrator(
                new SetupPersistenceResult(
                    Success: false,
                    Stage:
                        SetupPersistenceStage.Failed,
                    PersistenceAttempted: false,
                    ProviderValidationPerformed: false,
                    ConfigurationSaved: false,
                    CredentialsSaved: false,
                    Issues:
                        Array.Empty<string>(),
                    Message:
                        "Not used."));

        var service =
            new TrustedSetupPersistenceService(
                orchestrator);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () =>
                service.PrepareTrustedPersistenceAsync(
                    null!));

        Assert.Equal(
            0,
            orchestrator.CallCount);
    }

    private static SetupCandidate CompleteCandidate()
    {
        return new SetupCandidate(
            new ClockAssistantConfiguration
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
            },
            "user1",
            "password1");
    }

    private sealed class FakeTrustedSetupPersistenceOrchestrator
        : ITrustedSetupPersistenceOrchestrator
    {
        private readonly SetupPersistenceResult _result;

        public FakeTrustedSetupPersistenceOrchestrator(
            SetupPersistenceResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public SetupCandidate? LastCandidate { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<SetupPersistenceResult> CommitAsync(
            SetupCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCandidate = candidate;
            LastCancellationToken = cancellationToken;

            return Task.FromResult(
                _result);
        }
    }
}