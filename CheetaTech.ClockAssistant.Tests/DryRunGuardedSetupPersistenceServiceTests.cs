using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class DryRunGuardedSetupPersistenceServiceTests
{
    [Fact]
    public async Task PrepareTrustedPersistenceAsync_CompleteCandidate_DoesNotPersist()
    {
        var service = CreateService();

        var result =
            await service.PrepareTrustedPersistenceAsync(
                CompleteCandidate());

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.AwaitingProviderCredentialValidation,
            result.Stage);
        Assert.False(result.PersistenceAttempted);
        Assert.False(result.ProviderValidationPerformed);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task PrepareTrustedPersistenceAsync_IncompleteConfiguration_IsRejectedLocally()
    {
        var service = CreateService();

        var result =
            await service.PrepareTrustedPersistenceAsync(
                new SetupCandidate(
                    new ClockAssistantConfiguration(),
                    "user1",
                    "password1"));

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.LocalValidationFailed,
            result.Stage);
        Assert.False(result.PersistenceAttempted);
        Assert.False(result.ProviderValidationPerformed);
        Assert.False(result.ConfigurationSaved);
        Assert.False(result.CredentialsSaved);
        Assert.Contains("ProviderType", result.Issues);
        Assert.Contains("ProviderUrl", result.Issues);
    }

    [Fact]
    public async Task PrepareTrustedPersistenceAsync_MissingUsername_IsRejectedLocally()
    {
        var service = CreateService();

        var result =
            await service.PrepareTrustedPersistenceAsync(
                new SetupCandidate(
                    CompleteConfiguration(),
                    " ",
                    "password1"));

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.LocalValidationFailed,
            result.Stage);
        Assert.False(result.PersistenceAttempted);
        Assert.Contains("Username", result.Issues);
    }

    [Fact]
    public async Task PrepareTrustedPersistenceAsync_MissingPassword_IsRejectedLocally()
    {
        var service = CreateService();

        var result =
            await service.PrepareTrustedPersistenceAsync(
                new SetupCandidate(
                    CompleteConfiguration(),
                    "user1",
                    string.Empty));

        Assert.False(result.Success);
        Assert.Equal(
            SetupPersistenceStage.LocalValidationFailed,
            result.Stage);
        Assert.False(result.PersistenceAttempted);
        Assert.Contains("Password", result.Issues);
    }

    private static DryRunGuardedSetupPersistenceService CreateService()
    {
        return new DryRunGuardedSetupPersistenceService(
            new ConfigurationCompletenessEvaluator());
    }

    private static SetupCandidate CompleteCandidate()
    {
        return new SetupCandidate(
            CompleteConfiguration(),
            "user1",
            "password1");
    }

    private static ClockAssistantConfiguration CompleteConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.test/clock",
            WorkDays = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            },
            ClockInTime = new TimeOnly(7, 53),
            ClockOutTime = new TimeOnly(15, 0),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime =
                TimeSpan.FromMinutes(15),
            ExecutionMode =
                ClockExecutionMode.BasicConfirmation
        };
    }
}
