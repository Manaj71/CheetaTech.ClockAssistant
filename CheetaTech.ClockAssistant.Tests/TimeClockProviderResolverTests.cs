using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class TimeClockProviderResolverTests
{
    [Fact]
    public void Resolve_Selects_Matching_Factory_Case_Insensitively()
    {
        var expectedProvider =
            new FakeProvider();

        var ukgFactory =
            new FakeFactory(
                "UKG",
                expectedProvider);

        var otherFactory =
            new FakeFactory(
                "OTHER",
                new FakeProvider());

        var resolver =
            new TimeClockProviderResolver(
                new ITimeClockProviderFactory[]
                {
                    otherFactory,
                    ukgFactory
                });

        var configuration =
            Configuration(
                providerType: "  ukg  ");

        var resolved =
            resolver.Resolve(
                configuration);

        Assert.Same(
            expectedProvider,
            resolved);

        Assert.Equal(
            1,
            ukgFactory.CreateCallCount);

        Assert.Same(
            configuration,
            ukgFactory.LastConfiguration);
    }

    [Fact]
    public void Resolve_Unsupported_ProviderType_Fails_Locally()
    {
        var resolver =
            new TimeClockProviderResolver(
                new ITimeClockProviderFactory[]
                {
                    new FakeFactory(
                        "UKG",
                        new FakeProvider())
                });

        var exception =
            Assert.Throws<NotSupportedException>(
                () => resolver.Resolve(
                    Configuration(
                        providerType: "UNKNOWN")));

        Assert.Contains(
            "UNKNOWN",
            exception.Message);
    }

    [Fact]
    public void Resolve_Duplicate_ProviderType_Registrations_Fail_Locally()
    {
        var resolver =
            new TimeClockProviderResolver(
                new ITimeClockProviderFactory[]
                {
                    new FakeFactory(
                        "UKG",
                        new FakeProvider()),
                    new FakeFactory(
                        "ukg",
                        new FakeProvider())
                });

        Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(
                Configuration(
                    providerType: "UKG")));
    }

    private static ClockAssistantConfiguration Configuration(
        string providerType)
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = providerType,
            ProviderUrl =
                "https://provider.test/ta/Tenant.clock"
        };
    }

    private sealed class FakeFactory
        : ITimeClockProviderFactory
    {
        private readonly ITimeClockProvider _provider;

        public FakeFactory(
            string providerType,
            ITimeClockProvider provider)
        {
            ProviderType = providerType;
            _provider = provider;
        }

        public string ProviderType { get; }

        public int CreateCallCount { get; private set; }

        public ClockAssistantConfiguration? LastConfiguration { get; private set; }

        public ITimeClockProvider Create(
            ClockAssistantConfiguration configuration,
            StoredCredentials? credentials = null)
        {
            CreateCallCount++;
            LastConfiguration = configuration;

            return _provider;
        }
    }

    private sealed class FakeProvider
        : ITimeClockProvider
    {
        public Task<ProviderResult> TestConnectionAsync() =>
            throw new InvalidOperationException(
                "Resolver tests must not execute provider operations.");

        public Task<ProviderResult> ValidateCredentialsAsync(
            string username,
            string password) =>
            throw new InvalidOperationException(
                "Resolver tests must not execute provider operations.");

        public Task<ProviderResult> ClockInAsync() =>
            throw new InvalidOperationException(
                "Resolver tests must not execute provider operations.");

        public Task<ProviderResult> ClockOutAsync() =>
            throw new InvalidOperationException(
                "Resolver tests must not execute provider operations.");
    }
}