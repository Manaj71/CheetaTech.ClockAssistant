using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceActionExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ClockInDue_TransitionsThroughInProgressToSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);

        Assert.Collection(
            stateStore.SavedRecords,
            first => Assert.Equal(
                AttendanceActionState.InProgress,
                first.ClockInState),
            second => Assert.Equal(
                AttendanceActionState.Succeeded,
                second.ClockInState));
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateSucceededClockIn_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded
            });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCredentials_DoesNotCallProviderOrChangeState()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore,
            includeCredentials: false);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.MissingCredentials,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderRejects_StoresFailed()
    {
        var provider = FakeProvider.Rejected();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.ProviderRejected,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);

        Assert.Equal(
            AttendanceActionState.Failed,
            stateStore.SavedRecords[^1].ClockInState);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_StoresUnknown()
    {
        var provider = FakeProvider.Throwing();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.ProviderUnknown,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);

        Assert.Equal(
            AttendanceActionState.Unknown,
            stateStore.SavedRecords[^1].ClockInState);
    }

    [Fact]
    public async Task ExecuteAsync_ClockOutDue_CallsClockOutAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.NotDue
            });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(1, provider.ClockOutCallCount);

        Assert.Collection(
            stateStore.SavedRecords,
            first => Assert.Equal(
                AttendanceActionState.InProgress,
                first.ClockOutState),
            second => Assert.Equal(
                AttendanceActionState.Succeeded,
                second.ClockOutState));
    }

    [Fact]
    public async Task ExecuteAsync_ExecutionGateDisabled_BlocksBeforeCredentialsAndProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore,
            includeCredentials: false,
            executionAllowed: false);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.ExecutionDisabled,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }
    [Fact]
    public async Task ExecuteAsync_ManualClockInBeforeReminderWindow_CallsProviderAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);

        Assert.Collection(
            stateStore.SavedRecords,
            first => Assert.Equal(
                AttendanceActionState.InProgress,
                first.ClockInState),
            second => Assert.Equal(
                AttendanceActionState.Succeeded,
                second.ClockInState));
    }

    [Fact]
    public async Task ExecuteAsync_ManualClockInAfterConfiguredTime_CallsProviderAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInLateUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ManualClockOutBeforeReminderWindow_AfterClockInSucceeded_CallsProviderAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.NotDue
            });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(1, provider.ClockOutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ManualClockOutAfterConfiguredTime_AfterClockInSucceeded_CallsProviderAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.NotDue
            });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutLateUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(1, provider.ClockOutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ManualClockOutBeforeClockIn_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualDuplicateClockIn_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded
            });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInLateUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualDuplicateClockOut_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.Succeeded
            });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutLateUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Theory]
    [InlineData(AttendanceActionType.ClockIn, AttendanceActionState.Failed)]
    [InlineData(AttendanceActionType.ClockIn, AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionType.ClockIn, AttendanceActionState.Skipped)]
    [InlineData(AttendanceActionType.ClockIn, AttendanceActionState.InProgress)]
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.Failed)]
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.Skipped)]
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.InProgress)]
    public async Task ExecuteAsync_ManualUnsafeStates_DoesNotCallProvider(
        AttendanceActionType actionType,
        AttendanceActionState unsafeState)
    {
        var provider = FakeProvider.Success();
        var stateStore =
            actionType == AttendanceActionType.ClockIn
                ? new FakeAttendanceStateStore(
                    new DailyAttendanceRecord
                    {
                        AttendanceDate = AttendanceDate,
                        ClockInState = unsafeState
                    })
                : new FakeAttendanceStateStore(
                    new DailyAttendanceRecord
                    {
                        AttendanceDate = AttendanceDate,
                        ClockInState = AttendanceActionState.Succeeded,
                        ClockOutState = unsafeState
                    });

        var service = CreateService(
            provider,
            stateStore);

        var result = await service.ExecuteAsync(
            actionType,
            ClockOutLateUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualMissingCredentials_DoesNotCallProviderOrChangeState()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore,
            includeCredentials: false);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.MissingCredentials,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualMissingConfiguration_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();
        var service = new AttendanceActionExecutionService(
            new FakeConfigurationStore(null),
            new FakeCredentialStore(
                new StoredCredentials(
                    "test-user",
                    "test-password")),
            new FakeProviderResolver(provider),
            stateStore,
            new AttendanceStateEvaluator(),
            new FakeProviderExecutionGate(true));

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.MissingConfiguration,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualExecutionGateDisabled_BlocksBeforeCredentialsAndProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore,
            includeCredentials: false,
            executionAllowed: false);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.ExecutionDisabled,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualRetryClockInFailed_CallsProviderOnceAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Equal(
            AttendanceActionState.Succeeded,
            stateStore.SavedRecords[^1].ClockInState);
    }

    [Fact]
    public async Task ExecuteAsync_ManualRetryClockInFailed_ProviderRejects_RemainsFailed()
    {
        var provider = FakeProvider.Rejected();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.ProviderRejected,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(
            AttendanceActionState.Failed,
            stateStore.SavedRecords[^1].ClockInState);
    }

    [Theory]
    [InlineData(AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionState.Succeeded)]
    [InlineData(AttendanceActionState.InProgress)]
    [InlineData(AttendanceActionState.Skipped)]
    [InlineData(AttendanceActionState.Due)]
    [InlineData(AttendanceActionState.NotDue)]
    public async Task ExecuteAsync_ManualRetryClockInUnsafeStates_DoesNotCallProvider(
        AttendanceActionState unsafeState)
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = unsafeState
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualRetryClockInNonWorkday_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = NonWorkdayAttendanceDate,
                ClockInState = AttendanceActionState.Failed
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            NonWorkdayUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualRetryClockOutFailed_CallsProviderOnceAndStoresSucceeded()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.Failed
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.True(result.ProviderConfirmed);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(1, provider.ClockOutCallCount);
        Assert.Equal(
            AttendanceActionState.Succeeded,
            stateStore.SavedRecords[^1].ClockOutState);
    }

    [Theory]
    [InlineData(AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionState.Succeeded)]
    [InlineData(AttendanceActionState.InProgress)]
    [InlineData(AttendanceActionState.Skipped)]
    public async Task ExecuteAsync_ManualRetryClockOutUnsafeStates_DoesNotCallProvider(
        AttendanceActionState unsafeState)
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = unsafeState
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_ManualRetryClockOutWhenClockInNotSucceeded_DoesNotCallProvider()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed,
                ClockOutState = AttendanceActionState.Failed
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.False(result.ProviderRequestSent);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    private static readonly DateOnly AttendanceDate =
        new(2026, 9, 8);

    private static readonly DateOnly NonWorkdayAttendanceDate =
        new(2026, 9, 12);

    private static readonly DateTimeOffset ClockInEarlyUtc =
        new(2026, 9, 8, 10, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockInDueUtc =
        new(2026, 9, 8, 10, 50, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockInLateUtc =
        new(2026, 9, 8, 11, 10, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockOutEarlyUtc =
        new(2026, 9, 8, 18, 50, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockOutDueUtc =
        new(2026, 9, 8, 19, 20, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockOutLateUtc =
        new(2026, 9, 8, 19, 35, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset NonWorkdayUtc =
        new(2026, 9, 12, 15, 0, 0, TimeSpan.Zero);

    private static AttendanceActionExecutionService CreateService(
        FakeProvider provider,
        FakeAttendanceStateStore stateStore,
        bool includeCredentials = true,
        bool executionAllowed = true)
    {
        StoredCredentials? credentials =
            includeCredentials
                ? new StoredCredentials(
                    "test-user",
                    "test-password")
                : null;

        return new AttendanceActionExecutionService(
            new FakeConfigurationStore(Configuration()),
            new FakeCredentialStore(credentials),
            new FakeProviderResolver(provider),
            stateStore,
            new AttendanceStateEvaluator(),
            new FakeProviderExecutionGate(executionAllowed));
    }

    private static ClockAssistantConfiguration Configuration()
        => new()
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.invalid/",
            WorkDays = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            },
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime =
                TimeSpan.FromMinutes(15)
        };

    private sealed class FakeConfigurationStore
        : IClockAssistantConfigurationStore
    {
        private ClockAssistantConfiguration? _configuration;

        public FakeConfigurationStore(
            ClockAssistantConfiguration? configuration)
        {
            _configuration = configuration;
        }

        public Task SaveAsync(
            ClockAssistantConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            _configuration = configuration;
            return Task.CompletedTask;
        }

        public Task<ClockAssistantConfiguration?> GetAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(_configuration);

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            _configuration = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialStore
        : ICredentialStore
    {
        private StoredCredentials? _credentials;

        public FakeCredentialStore(
            StoredCredentials? credentials)
        {
            _credentials = credentials;
        }

        public Task SaveCredentialsAsync(
            StoredCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            _credentials = credentials;
            return Task.CompletedTask;
        }

        public Task<StoredCredentials?> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(_credentials);

        public Task DeleteCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            _credentials = null;
            return Task.CompletedTask;
        }
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

        public ITimeClockProvider Resolve(
            ClockAssistantConfiguration configuration,
            StoredCredentials? credentials = null)
            => _provider;
    }

    private sealed class FakeAttendanceStateStore
        : IAttendanceStateStore
    {
        private DailyAttendanceRecord? _record;

        public FakeAttendanceStateStore(
            DailyAttendanceRecord? record = null)
        {
            _record = record;
        }

        public List<DailyAttendanceRecord> SavedRecords { get; } = new();

        public Task<DailyAttendanceRecord?> LoadAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
        {
            var result =
                _record is not null &&
                _record.AttendanceDate == attendanceDate
                    ? _record
                    : null;

            return Task.FromResult(result);
        }

        public Task SaveAsync(
            DailyAttendanceRecord record,
            CancellationToken cancellationToken = default)
        {
            _record = record;
            SavedRecords.Add(record);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
        {
            if (_record?.AttendanceDate == attendanceDate)
            {
                _record = null;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeProviderExecutionGate
        : IAttendanceProviderExecutionGate
    {
        private readonly bool _allowed;

        public FakeProviderExecutionGate(bool allowed)
        {
            _allowed = allowed;
        }

        public bool IsExecutionAllowed(
            AttendanceActionType actionType,
            DateOnly attendanceDate,
            DateTimeOffset utcNow)
            => _allowed;
    }
    private sealed class FakeProvider
        : ITimeClockProvider
    {
        private readonly bool _success;
        private readonly bool _throw;

        private FakeProvider(
            bool success,
            bool shouldThrow)
        {
            _success = success;
            _throw = shouldThrow;
        }

        public int ClockInCallCount { get; private set; }

        public int ClockOutCallCount { get; private set; }

        public static FakeProvider Success()
            => new(true, false);

        public static FakeProvider Rejected()
            => new(false, false);

        public static FakeProvider Throwing()
            => new(false, true);

        public Task<ProviderResult> TestConnectionAsync()
            => Task.FromResult(
                new ProviderResult
                {
                    Success = true,
                    Action = "TestConnection",
                    Timestamp = DateTimeOffset.UtcNow,
                    TechnicalStatus = "Ready"
                });

        public Task<ProviderResult> ValidateCredentialsAsync(
            string username,
            string password)
            => Task.FromResult(
                new ProviderResult
                {
                    Success = true,
                    Action = "ValidateCredentials",
                    Timestamp = DateTimeOffset.UtcNow,
                    TechnicalStatus = "SyntheticValidated"
                });
        public Task<ProviderResult> ClockInAsync()
        {
            ClockInCallCount++;
            return Execute("ClockIn");
        }

        public Task<ProviderResult> ClockOutAsync()
        {
            ClockOutCallCount++;
            return Execute("ClockOut");
        }

        private Task<ProviderResult> Execute(
            string action)
        {
            if (_throw)
            {
                throw new InvalidOperationException(
                    "Synthetic provider exception.");
            }

            return Task.FromResult(
                new ProviderResult
                {
                    Success = _success,
                    Action = action,
                    Timestamp = DateTimeOffset.UtcNow,
                    TechnicalStatus =
                        _success
                            ? "ProviderConfirmed"
                            : "SyntheticRejected"
                });
        }
    }
}