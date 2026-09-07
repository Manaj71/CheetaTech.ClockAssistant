using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class DeferredCredentialUpdateServiceTests
{
    [Fact]
    public async Task ValidateAndSaveAsync_ValidCandidate_SavesWithoutProviderValidation()
    {
        var store = new FakeCredentialStore();
        var service = new DeferredCredentialUpdateService(store);

        var result = await service.ValidateAndSaveAsync(
            CompleteConfiguration(),
            new StoredCredentials(
                "candidate-user",
                "candidate-password"));

        Assert.True(result.Success);
        Assert.True(result.CredentialsSaved);
        Assert.False(result.ProviderValidationPerformed);
        Assert.Equal(
            "CredentialsStoredUnverified",
            result.TechnicalStatus);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(
            "candidate-user",
            store.Current?.Username);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_EmptyUsername_DoesNotSave()
    {
        var store = new FakeCredentialStore();
        var service = new DeferredCredentialUpdateService(store);

        var result = await service.ValidateAndSaveAsync(
            CompleteConfiguration(),
            new StoredCredentials(
                string.Empty,
                "candidate-password"));

        Assert.False(result.Success);
        Assert.False(result.CredentialsSaved);
        Assert.False(result.ProviderValidationPerformed);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_EmptyPassword_DoesNotSave()
    {
        var store = new FakeCredentialStore();
        var service = new DeferredCredentialUpdateService(store);

        var result = await service.ValidateAndSaveAsync(
            CompleteConfiguration(),
            new StoredCredentials(
                "candidate-user",
                string.Empty));

        Assert.False(result.Success);
        Assert.False(result.CredentialsSaved);
        Assert.False(result.ProviderValidationPerformed);
        Assert.Equal(0, store.SaveCount);
    }

    private static ClockAssistantConfiguration CompleteConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://provider.invalid/clock",
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
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public StoredCredentials? Current { get; private set; }
        public int SaveCount { get; private set; }

        public Task SaveCredentialsAsync(
            StoredCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = credentials;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<StoredCredentials?> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Current);
        }

        public Task DeleteCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = null;
            return Task.CompletedTask;
        }
    }
}
