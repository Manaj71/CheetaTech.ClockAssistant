using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class SetupReadinessServiceTests
{
    [Fact]
    public async Task EvaluateAsync_No_Configuration_And_No_Credentials_Requires_Setup()
    {
        var service = CreateService(
            configuration: null,
            credentials: null);

        var result = await service.EvaluateAsync();

        Assert.True(result.SetupRequired);
        Assert.False(result.Ready);
        Assert.False(result.ConfigurationAvailable);
        Assert.False(result.ConfigurationComplete);
        Assert.False(result.CredentialsAvailable);
    }

    [Fact]
    public async Task EvaluateAsync_Complete_Configuration_But_No_Credentials_Requires_Setup()
    {
        var service = CreateService(
            ValidConfiguration(),
            credentials: null);

        var result = await service.EvaluateAsync();

        Assert.True(result.SetupRequired);
        Assert.True(result.ConfigurationAvailable);
        Assert.True(result.ConfigurationComplete);
        Assert.False(result.CredentialsAvailable);
    }

    [Fact]
    public async Task EvaluateAsync_Credentials_But_No_Configuration_Requires_Setup()
    {
        var service = CreateService(
            configuration: null,
            credentials: ValidCredentials());

        var result = await service.EvaluateAsync();

        Assert.True(result.SetupRequired);
        Assert.False(result.ConfigurationAvailable);
        Assert.False(result.ConfigurationComplete);
        Assert.True(result.CredentialsAvailable);
    }

    [Fact]
    public async Task EvaluateAsync_Incomplete_Configuration_And_Credentials_Requires_Setup()
    {
        var configuration = ValidConfiguration() with
        {
            ProviderUrl = string.Empty
        };

        var service = CreateService(
            configuration,
            ValidCredentials());

        var result = await service.EvaluateAsync();

        Assert.True(result.SetupRequired);
        Assert.True(result.ConfigurationAvailable);
        Assert.False(result.ConfigurationComplete);
        Assert.True(result.CredentialsAvailable);
        Assert.Contains(
            nameof(configuration.ProviderUrl),
            result.ConfigurationIssues);
    }

    [Fact]
    public async Task EvaluateAsync_Complete_Configuration_And_Credentials_Is_Ready()
    {
        var service = CreateService(
            ValidConfiguration(),
            ValidCredentials());

        var result = await service.EvaluateAsync();

        Assert.False(result.SetupRequired);
        Assert.True(result.Ready);
        Assert.True(result.ConfigurationAvailable);
        Assert.True(result.ConfigurationComplete);
        Assert.True(result.CredentialsAvailable);
        Assert.Empty(result.ConfigurationIssues);
    }

    [Fact]
    public async Task EvaluateAsync_Does_Not_Call_Provider()
    {
        var service = CreateService(
            ValidConfiguration(),
            ValidCredentials());

        var result = await service.EvaluateAsync();

        Assert.True(result.Ready);
    }

    private static SetupReadinessService CreateService(
        ClockAssistantConfiguration? configuration,
        StoredCredentials? credentials)
    {
        return new SetupReadinessService(
            new FakeConfigurationStore(configuration),
            new ConfigurationCompletenessEvaluator(),
            new FakeCredentialStore(credentials));
    }

    private static ClockAssistantConfiguration ValidConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://provider.test/ta/Tenant.clock",
            WorkDays =
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            ],
            ClockInTime = new TimeOnly(7, 53),
            ClockOutTime = new TimeOnly(15, 0),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };
    }

    private static StoredCredentials ValidCredentials()
    {
        return new StoredCredentials(
            "fake-user",
            "fake-password");
    }

    private sealed class FakeConfigurationStore
        : IClockAssistantConfigurationStore
    {
        private ClockAssistantConfiguration? _current;

        public FakeConfigurationStore(
            ClockAssistantConfiguration? current)
        {
            _current = current;
        }

        public Task SaveAsync(
            ClockAssistantConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _current = configuration;
            return Task.CompletedTask;
        }

        public Task<ClockAssistantConfiguration?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_current);
        }

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _current = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialStore
        : ICredentialStore
    {
        private StoredCredentials? _current;

        public FakeCredentialStore(
            StoredCredentials? current)
        {
            _current = current;
        }

        public Task SaveCredentialsAsync(
            StoredCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _current = credentials;
            return Task.CompletedTask;
        }

        public Task<StoredCredentials?> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_current);
        }

        public Task DeleteCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _current = null;
            return Task.CompletedTask;
        }
    }
}
