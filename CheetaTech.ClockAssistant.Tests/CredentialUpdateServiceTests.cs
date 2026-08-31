using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class CredentialUpdateServiceTests
{
    [Fact]
    public async Task ValidateAndSaveAsync_Uses_Candidate_Configuration_And_Saves_Only_After_Provider_Accepts()
    {
        var provider =
            new FakeTimeClockProvider(
                CredentialAccepted());

        var resolver =
            new FakeProviderResolver(
                provider);

        var store =
            new FakeCredentialStore(
                new StoredCredentials(
                    "old-user",
                    "old-password"));

        var service =
            new CredentialUpdateService(
                resolver,
                store);

        var configuration =
            CompleteConfiguration(
                providerUrl:
                    "https://candidate.example.test/clock");

        var candidate =
            new StoredCredentials(
                "new-user",
                "new-password");

        var result =
            await service.ValidateAndSaveAsync(
                configuration,
                candidate);

        Assert.True(result.Success);
        Assert.True(result.CredentialsSaved);
        Assert.Equal(
            "CredentialsValidatedAndSaved",
            result.TechnicalStatus);

        Assert.Equal(
            1,
            resolver.ResolveCallCount);

        Assert.Same(
            configuration,
            resolver.LastConfiguration);

        Assert.Equal(
            1,
            provider.ValidationCallCount);

        Assert.Equal(
            "new-user",
            provider.LastValidationUsername);

        Assert.Equal(
            "new-password",
            provider.LastValidationPassword);

        Assert.Equal(
            1,
            store.SaveCallCount);

        Assert.Equal(
            candidate,
            store.Current);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_Does_Not_Replace_KnownGood_When_Validation_Fails()
    {
        var original =
            new StoredCredentials(
                "known-good-user",
                "known-good-password");

        var provider =
            new FakeTimeClockProvider(
                CredentialRejected());

        var resolver =
            new FakeProviderResolver(
                provider);

        var store =
            new FakeCredentialStore(
                original);

        var service =
            new CredentialUpdateService(
                resolver,
                store);

        var result =
            await service.ValidateAndSaveAsync(
                CompleteConfiguration(),
                new StoredCredentials(
                    "candidate-user",
                    "candidate-bad-password"));

        Assert.False(result.Success);
        Assert.False(result.CredentialsSaved);
        Assert.Equal(
            "CredentialsRejected",
            result.TechnicalStatus);

        Assert.Equal(
            1,
            resolver.ResolveCallCount);

        Assert.Equal(
            1,
            provider.ValidationCallCount);

        Assert.Equal(
            0,
            store.SaveCallCount);

        Assert.Equal(
            original,
            store.Current);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_Does_Not_Resolve_Provider_For_Empty_Username()
    {
        var provider =
            new FakeTimeClockProvider(
                CredentialAccepted());

        var resolver =
            new FakeProviderResolver(
                provider);

        var store =
            new FakeCredentialStore(
                new StoredCredentials(
                    "known-good-user",
                    "known-good-password"));

        var service =
            new CredentialUpdateService(
                resolver,
                store);

        var result =
            await service.ValidateAndSaveAsync(
                CompleteConfiguration(),
                new StoredCredentials(
                    string.Empty,
                    "candidate-password"));

        Assert.False(result.Success);
        Assert.False(result.CredentialsSaved);
        Assert.Equal(
            "InvalidConfiguration",
            result.TechnicalStatus);

        Assert.Equal(
            0,
            resolver.ResolveCallCount);

        Assert.Equal(
            0,
            provider.ValidationCallCount);

        Assert.Equal(
            0,
            store.SaveCallCount);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_Does_Not_Resolve_Provider_For_Empty_Password()
    {
        var provider =
            new FakeTimeClockProvider(
                CredentialAccepted());

        var resolver =
            new FakeProviderResolver(
                provider);

        var store =
            new FakeCredentialStore(
                new StoredCredentials(
                    "known-good-user",
                    "known-good-password"));

        var service =
            new CredentialUpdateService(
                resolver,
                store);

        var result =
            await service.ValidateAndSaveAsync(
                CompleteConfiguration(),
                new StoredCredentials(
                    "candidate-user",
                    string.Empty));

        Assert.False(result.Success);
        Assert.False(result.CredentialsSaved);
        Assert.Equal(
            "InvalidConfiguration",
            result.TechnicalStatus);

        Assert.Equal(
            0,
            resolver.ResolveCallCount);

        Assert.Equal(
            0,
            provider.ValidationCallCount);

        Assert.Equal(
            0,
            store.SaveCallCount);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_ProviderResolutionFailure_Is_Controlled_And_Does_Not_Save()
    {
        var resolver =
            new FakeProviderResolver(
                exception:
                    new NotSupportedException(
                        "Provider type 'UNKNOWN' is not registered."));

        var original =
            new StoredCredentials(
                "known-good-user",
                "known-good-password");

        var store =
            new FakeCredentialStore(
                original);

        var service =
            new CredentialUpdateService(
                resolver,
                store);

        var configuration =
            CompleteConfiguration() with
            {
                ProviderType = "UNKNOWN"
            };

        var result =
            await service.ValidateAndSaveAsync(
                configuration,
                new StoredCredentials(
                    "candidate-user",
                    "candidate-password"));

        Assert.False(result.Success);
        Assert.False(result.CredentialsSaved);
        Assert.Equal(
            "ProviderResolutionFailed",
            result.TechnicalStatus);

        Assert.Equal(
            1,
            resolver.ResolveCallCount);

        Assert.Equal(
            original,
            store.Current);

        Assert.Equal(
            0,
            store.SaveCallCount);
    }

    private static ClockAssistantConfiguration CompleteConfiguration(
        string providerUrl =
            "https://provider.test/ta/Tenant.clock")
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = providerUrl,
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

    private static ProviderResult CredentialAccepted()
    {
        return new ProviderResult
        {
            Success = true,
            Action = "ValidateCredentials",
            TechnicalStatus = "CredentialsAccepted",
            ProviderMessage = "Provider accepted credentials.",
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static ProviderResult CredentialRejected()
    {
        return new ProviderResult
        {
            Success = false,
            Action = "ValidateCredentials",
            TechnicalStatus = "CredentialsRejected",
            ProviderMessage = "Provider rejected credentials.",
            Timestamp = DateTimeOffset.UtcNow,
            ErrorMessage = "Provider rejected credentials."
        };
    }

    private sealed class FakeProviderResolver
        : ITimeClockProviderResolver
    {
        private readonly ITimeClockProvider? _provider;
        private readonly Exception? _exception;

        public FakeProviderResolver(
            ITimeClockProvider provider)
        {
            _provider = provider;
        }

        public FakeProviderResolver(
            Exception exception)
        {
            _exception = exception;
        }

        public int ResolveCallCount { get; private set; }

        public ClockAssistantConfiguration? LastConfiguration { get; private set; }

        public ITimeClockProvider Resolve(
            ClockAssistantConfiguration configuration,
            StoredCredentials? credentials = null)
        {
            ResolveCallCount++;
            LastConfiguration = configuration;

            if (_exception is not null)
            {
                throw _exception;
            }

            return _provider
                ?? throw new InvalidOperationException(
                    "Fake provider was not configured.");
        }
    }

    private sealed class FakeTimeClockProvider
        : ITimeClockProvider
    {
        private readonly ProviderResult _validationResult;

        public FakeTimeClockProvider(
            ProviderResult validationResult)
        {
            _validationResult = validationResult;
        }

        public int ValidationCallCount { get; private set; }

        public string? LastValidationUsername { get; private set; }

        public string? LastValidationPassword { get; private set; }

        public Task<ProviderResult> TestConnectionAsync()
        {
            throw new InvalidOperationException(
                "TestConnectionAsync must not be called by credential update tests.");
        }

        public Task<ProviderResult> ValidateCredentialsAsync(
            string username,
            string password)
        {
            ValidationCallCount++;
            LastValidationUsername = username;
            LastValidationPassword = password;

            return Task.FromResult(
                _validationResult);
        }

        public Task<ProviderResult> ClockInAsync()
        {
            throw new InvalidOperationException(
                "ClockInAsync must not be called by credential update tests.");
        }

        public Task<ProviderResult> ClockOutAsync()
        {
            throw new InvalidOperationException(
                "ClockOutAsync must not be called by credential update tests.");
        }
    }

    private sealed class FakeCredentialStore
        : ICredentialStore
    {
        public FakeCredentialStore(
            StoredCredentials? initial)
        {
            Current = initial;
        }

        public StoredCredentials? Current { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task SaveCredentialsAsync(
            StoredCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCallCount++;
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
}