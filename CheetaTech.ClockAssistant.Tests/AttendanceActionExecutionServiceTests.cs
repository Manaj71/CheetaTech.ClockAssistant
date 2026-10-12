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

        Assert.False(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.True(
            AttendanceActionRecovery.IsUncertainProviderOutcome(result));
        Assert.True(
            AttendanceActionRecovery.ShouldClearActionNotification(result));
    }

    [Fact]
    public async Task ExecuteAsync_PreSendNetworkFailure_StoresFailedAllowsSafeRetry()
    {
        var provider = FakeProvider.RejectedPreSend();
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
        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);

        Assert.Equal(
            AttendanceActionState.Failed,
            stateStore.SavedRecords[^1].ClockInState);
        Assert.False(
            stateStore.SavedRecords[^1].ClockInProviderRequestSent);
        Assert.NotEqual(
            AttendanceActionState.Succeeded,
            stateStore.SavedRecords[^1].ClockInState);

        Assert.True(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.True(
            AttendanceActionRecovery.HasProvenPreSendFailure(
                stateStore.SavedRecords[^1],
                AttendanceActionType.ClockIn));
        Assert.False(
            AttendanceActionRecovery.ShouldClearActionNotification(result));
        Assert.False(
            AttendanceActionRecovery.ShouldSkipActionWhenSchedulingNext(result));

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockInDueUtc,
            stateStore.SavedRecords[^1]);

        Assert.True(evaluation.ClockInNotificationEligible);

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            stateStore.SavedRecords[^1]);

        Assert.Equal(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.Equal(
            AttendanceHomePrimaryActionMode.ManualRetry,
            plan.PrimaryMode);
        Assert.Contains(
            "was not sent to UKG",
            plan.Summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NullProviderRequestSent_TreatedConservativelyAsMayHaveBeenSent()
    {
        var provider = FakeProvider.RejectedUnspecifiedBoundary();
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
        Assert.True(
            stateStore.SavedRecords[^1].ClockInProviderRequestSent);
        Assert.False(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.True(
            AttendanceActionRecovery.ShouldClearActionNotification(result));
        Assert.False(
            AttendanceActionRecovery.HasProvenPreSendFailure(
                stateStore.SavedRecords[^1],
                AttendanceActionType.ClockIn));
    }

    [Fact]
    public async Task ExecuteAsync_PreSendFailure_NotificationRetryRemainsExecutable()
    {
        var provider = FakeProvider.RejectedPreSend();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var first = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.True(
            AttendanceActionRecovery.AllowsSafeProviderRetry(first));

        provider = FakeProvider.Success();
        service = CreateService(
            provider,
            stateStore);

        var retry = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Notification);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            retry.Status);
        Assert.True(retry.ProviderConfirmed);
        Assert.Equal(1, provider.ClockInCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PostSendFailure_NotificationRetryBlocked()
    {
        var provider = FakeProvider.Rejected();
        var stateStore = new FakeAttendanceStateStore();

        var service = CreateService(
            provider,
            stateStore);

        var first = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.True(first.ProviderRequestSent);
        Assert.True(
            AttendanceActionRecovery.ShouldClearActionNotification(first));

        provider = FakeProvider.Success();
        service = CreateService(
            provider,
            stateStore);

        var retry = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Notification);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            retry.Status);
        Assert.Equal(0, provider.ClockInCallCount);

        // Post-send Failed: Home ManualRetry is not offered; CE remains.
        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockInDueUtc,
            stateStore.SavedRecords[^1]);

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            stateStore.SavedRecords[^1]);

        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
        Assert.Contains(
            "may have been processed",
            plan.Summary,
            StringComparison.Ordinal);

        var homeRetry = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            homeRetry.Status);
        Assert.Equal(0, provider.ClockInCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreSendNetworkFailure_ClockOut_DoesNotMarkComplete()
    {
        var provider = FakeProvider.RejectedPreSend();
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

        Assert.False(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);
        Assert.Equal(
            AttendanceActionState.Failed,
            stateStore.SavedRecords[^1].ClockOutState);
        Assert.False(
            stateStore.SavedRecords[^1].ClockOutProviderRequestSent);
        Assert.Equal(
            AttendanceActionState.Succeeded,
            stateStore.SavedRecords[^1].ClockInState);

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockOutDueUtc,
            stateStore.SavedRecords[^1]);

        Assert.NotEqual(AttendanceDayState.Completed, evaluation.DayState);
        Assert.True(evaluation.ClockOutNotificationEligible);

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            stateStore.SavedRecords[^1]);

        Assert.Equal(AttendanceActionType.ClockOut, plan.ActionType);
        Assert.Equal(
            AttendanceHomePrimaryActionMode.ManualRetry,
            plan.PrimaryMode);
        Assert.True(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.False(
            AttendanceActionRecovery.ShouldClearActionNotification(result));
    }

    [Fact]
    public async Task ExecuteAsync_ConfirmedSuccess_DoesNotAllowRetryAndClearsNotification()
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

        Assert.False(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.True(
            AttendanceActionRecovery.IsConfirmedProviderOutcome(result));
        Assert.True(
            AttendanceActionRecovery.ShouldClearActionNotification(result));
        Assert.True(
            AttendanceActionRecovery.ShouldSkipActionWhenSchedulingNext(result));

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockInDueUtc,
            stateStore.SavedRecords[^1]);

        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        // Confirmed Clock In is not retriable; Clock Out may become available.
        Assert.NotEqual(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.NotEqual(
            AttendanceHomePrimaryActionMode.ManualRetry,
            plan.PrimaryMode);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrow_Uncertain_NoBlindRetryClearsNotification()
    {
        var provider = FakeProvider.Throwing();
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
            AttendanceActionExecutionStatus.ProviderUnknown,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.False(result.ProviderConfirmed);
        Assert.Equal(
            AttendanceActionState.Unknown,
            stateStore.SavedRecords[^1].ClockOutState);

        Assert.False(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.True(
            AttendanceActionRecovery.ShouldClearActionNotification(result));

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockOutDueUtc,
            stateStore.SavedRecords[^1]);

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            stateStore.SavedRecords[^1]);

        Assert.Equal(AttendanceActionType.ClockOut, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
        Assert.Contains(
            "may have been processed",
            plan.Summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SentExplicitRejection_NotificationClearsHomeBlocksOrdinaryRetry()
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

        Assert.False(
            AttendanceActionRecovery.AllowsSafeProviderRetry(result));
        Assert.True(
            AttendanceActionRecovery.ShouldClearActionNotification(result));

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockInDueUtc,
            stateStore.SavedRecords[^1]);

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            stateStore.SavedRecords[^1]);

        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);

        var homeRetry = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            homeRetry.Status);
        Assert.Equal(1, provider.ClockInCallCount);
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
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.Failed)]
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionType.ClockOut, AttendanceActionState.Skipped)]
    public async Task ExecuteAsync_ManualUnsafeStates_DoesNotCallProvider(
        AttendanceActionType actionType,
        AttendanceActionState unsafeState)
    {
        // InProgress is covered by RecoverAbandonedInProgress_* (escalates, then
        // remains provider-ineligible without a punch call).
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
                ClockInState = AttendanceActionState.Failed,
                ClockInProviderRequestSent = false
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
                ClockInState = AttendanceActionState.Failed,
                ClockInProviderRequestSent = false
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
    [InlineData(true)]
    [InlineData(null)]
    public async Task ExecuteAsync_ManualRetryClockInPostSendOrUnknownBoundary_DoesNotCallProvider(
        bool? providerRequestSent)
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed,
                ClockInProviderRequestSent = providerRequestSent
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProviderResponseMappedFailed_DoesNotPermitHomeManualRetry()
    {
        var provider = FakeProvider.RejectedUnknownProviderResponse();
        var stateStore = new FakeAttendanceStateStore();
        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceActionExecutionStatus.ProviderRejected,
            result.Status);
        Assert.True(result.ProviderRequestSent);
        Assert.Equal(
            AttendanceActionState.Failed,
            stateStore.SavedRecords[^1].ClockInState);
        Assert.True(
            stateStore.SavedRecords[^1].ClockInProviderRequestSent);

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockInDueUtc,
            stateStore.SavedRecords[^1]);

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            stateStore.SavedRecords[^1]);

        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);

        var homeRetry = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            homeRetry.Status);
        Assert.Equal(1, provider.ClockInCallCount);
    }

    [Theory]
    [InlineData(AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionState.Succeeded)]
    [InlineData(AttendanceActionState.Skipped)]
    [InlineData(AttendanceActionState.Due)]
    [InlineData(AttendanceActionState.NotDue)]
    public async Task ExecuteAsync_ManualRetryClockInUnsafeStates_DoesNotCallProvider(
        AttendanceActionState unsafeState)
    {
        // InProgress is covered by RecoverAbandonedInProgress_* (escalates first).
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
                ClockOutState = AttendanceActionState.Failed,
                ClockOutProviderRequestSent = false
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
    [InlineData(AttendanceActionState.Skipped)]
    public async Task ExecuteAsync_ManualRetryClockOutUnsafeStates_DoesNotCallProvider(
        AttendanceActionState unsafeState)
    {
        // InProgress is covered by RecoverAbandonedInProgress_* (escalates first).
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

    [Fact]
    public async Task RecoverAbandonedInProgress_ClockIn_EscalatesToUnknownSentTrueWithoutProviderCall()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress,
                ClockInProviderRequestSent = false
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);

        var recovered = Assert.Single(stateStore.SavedRecords);
        Assert.Equal(AttendanceActionState.Unknown, recovered.ClockInState);
        Assert.True(recovered.ClockInProviderRequestSent);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ClockOut_EscalatesToUnknownSentTrueWithoutProviderCall()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.InProgress,
                ClockOutProviderRequestSent = null
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.Equal(0, provider.ClockOutCallCount);

        var recovered = Assert.Single(stateStore.SavedRecords);
        Assert.Equal(AttendanceActionState.Succeeded, recovered.ClockInState);
        Assert.Equal(AttendanceActionState.Unknown, recovered.ClockOutState);
        Assert.True(recovered.ClockOutProviderRequestSent);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_LeavesNonInProgressStatesUnchanged()
    {
        var provider = FakeProvider.Success();
        var initial = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = AttendanceActionState.Succeeded,
            ClockInProviderRequestSent = true,
            ClockOutState = AttendanceActionState.Failed,
            ClockOutProviderRequestSent = true
        };
        var stateStore = new FakeAttendanceStateStore(initial);
        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            ClockOutDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);
        Assert.Empty(stateStore.SavedRecords);
        Assert.Equal(0, provider.ClockOutCallCount);

        var loaded = await stateStore.LoadAsync(AttendanceDate);
        Assert.Equal(initial, loaded);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_DoesNotEscalateCurrentProcessInFlightAction()
    {
        var provider = new BlockingFakeProvider();
        var stateStore = new FakeAttendanceStateStore();
        var service = CreateService(provider, stateStore);

        var executeTask = service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        await provider.WaitUntilEnteredAsync();

        Assert.Equal(
            AttendanceActionState.InProgress,
            stateStore.SavedRecords[^1].ClockInState);

        var concurrentTask = service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.False(concurrentTask.IsCompleted);
        Assert.Equal(
            AttendanceActionState.InProgress,
            stateStore.SavedRecords[^1].ClockInState);

        provider.Release();

        var first = await executeTask;
        var second = await concurrentTask;

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            first.Status);
        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            second.Status);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(
            AttendanceActionState.Succeeded,
            stateStore.SavedRecords[^1].ClockInState);
        Assert.DoesNotContain(
            stateStore.SavedRecords,
            record =>
                record.ClockInState == AttendanceActionState.Unknown);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ConcurrentEntryPointsShareOneBarrier()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress
            });

        var service = CreateService(provider, stateStore);

        var executeTask = service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);
        var completedElsewhereTask = service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        var executeResult = await executeTask;
        var completedElsewhereResult = await completedElsewhereTask;

        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            executeResult.Status);
        Assert.Equal(
            AttendanceCompletedElsewhereStatus.Succeeded,
            completedElsewhereResult.Status);

        var recoverySaves = stateStore.SavedRecords.Count(
            record =>
                record.ClockInState == AttendanceActionState.Unknown &&
                record.ClockInProviderRequestSent == true);

        Assert.Equal(1, recoverySaves);
        Assert.Contains(
            stateStore.SavedRecords,
            record =>
                record.ClockInState == AttendanceActionState.Succeeded &&
                record.ClockInCompletionSource ==
                AttendanceActionCompletionSource.CompletedElsewhere);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_RepeatedEntryDoesNotRerunRecovery()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress
            });

        var service = CreateService(provider, stateStore);

        await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        var savesAfterFirst = stateStore.SaveAttemptCount;

        await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(savesAfterFirst, stateStore.SaveAttemptCount);
        Assert.Equal(
            AttendanceActionState.Unknown,
            (await stateStore.LoadAsync(AttendanceDate))!.ClockInState);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_PersistenceFailure_BlocksProviderExecution()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress
            },
            failNextSaves: 1);

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.PersistenceFailed,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
        Assert.Equal(
            AttendanceActionState.InProgress,
            (await stateStore.LoadAsync(AttendanceDate))!.ClockInState);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_PersistenceFailure_BlocksCompletedElsewhere()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress
            },
            failNextSaves: 1);

        var service = CreateService(provider, stateStore);

        var result = await service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceCompletedElsewhereStatus.PersistenceFailed,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
        Assert.Equal(
            AttendanceActionState.InProgress,
            (await stateStore.LoadAsync(AttendanceDate))!.ClockInState);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_AfterRecovery_OrdinaryManualRetryRemainsBlocked()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress
            });

        var service = CreateService(provider, stateStore);

        await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        var retry = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            retry.Status);
        Assert.Equal(0, provider.ClockInCallCount);

        var evaluation = new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            ClockInDueUtc,
            await stateStore.LoadAsync(AttendanceDate));

        var plan = AttendanceHomeActionPlanner.Plan(
            evaluation,
            await stateStore.LoadAsync(AttendanceDate));

        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ThenCompletedElsewhere_SucceedsUnderSharedGate()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.InProgress
            });

        var service = CreateService(provider, stateStore);

        var result = await service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceCompletedElsewhereStatus.Succeeded,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(
            AttendanceActionCompletionSource.CompletedElsewhere,
            stateStore.SavedRecords[^1].ClockInCompletionSource);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ClockOutOnD_RestartAfterLocalMidnightOnDPlus1()
    {
        // Local midnight America/Toronto (EDT): 2026-09-09 00:30 = 04:30 UTC.
        // Use Clock Out Manual so today's early NotDue Clock In is not auto-executed.
        var provider = FakeProvider.Success();
        var dayD = AttendanceDate;
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = dayD,
                ClockInState = AttendanceActionState.Succeeded,
                ClockInProviderRequestSent = true,
                ClockOutState = AttendanceActionState.InProgress,
                ClockOutProviderRequestSent = false
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            DayAfterAttendanceLocalEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);

        var recoveredDayD = await stateStore.LoadAsync(dayD);
        Assert.NotNull(recoveredDayD);
        Assert.Equal(AttendanceActionState.Succeeded, recoveredDayD.ClockInState);
        Assert.Equal(AttendanceActionState.Unknown, recoveredDayD.ClockOutState);
        Assert.True(recoveredDayD.ClockOutProviderRequestSent);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ClockInOnD_RestartAfterLocalMidnightOnDPlus1()
    {
        var provider = FakeProvider.Success();
        var dayD = AttendanceDate;
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = dayD,
                ClockInState = AttendanceActionState.InProgress
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            DayAfterAttendanceLocalEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);
        Assert.Equal(
            AttendanceActionExecutionStatus.NotEligible,
            result.Status);

        var recoveredDayD = await stateStore.LoadAsync(dayD);
        Assert.NotNull(recoveredDayD);
        Assert.Equal(AttendanceActionState.Unknown, recoveredDayD.ClockInState);
        Assert.True(recoveredDayD.ClockInProviderRequestSent);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_FirstCallerOnDPlus1_SecondCallerOnD_DoesNotMissDayD()
    {
        var provider = FakeProvider.Success();
        var dayD = AttendanceDate;
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = dayD,
                ClockInState = AttendanceActionState.InProgress
            });

        var service = CreateService(provider, stateStore);

        // First process caller after local midnight recovers D even though its
        // attendance date is D+1.
        await service.ExecuteAsync(
            AttendanceActionType.ClockOut,
            DayAfterAttendanceLocalEarlyUtc,
            AttendanceActionExecutionMode.Manual);

        var afterFirstCaller = await stateStore.LoadAsync(dayD);
        Assert.NotNull(afterFirstCaller);
        Assert.Equal(AttendanceActionState.Unknown, afterFirstCaller.ClockInState);
        Assert.True(afterFirstCaller.ClockInProviderRequestSent);

        // Second caller on D does not re-run recovery; day D remains recovered.
        await service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        var recoveredDayD = await stateStore.LoadAsync(dayD);
        Assert.NotNull(recoveredDayD);
        Assert.Equal(AttendanceActionState.Succeeded, recoveredDayD.ClockInState);
        Assert.Equal(
            AttendanceActionCompletionSource.CompletedElsewhere,
            recoveredDayD.ClockInCompletionSource);
        Assert.True(recoveredDayD.ClockInProviderRequestSent);
        Assert.Equal(0, provider.ClockInCallCount);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ClockOutOnD_DoesNotBlockEligibleClockInOnDPlus1()
    {
        var provider = FakeProvider.Success();
        var dayD = AttendanceDate;
        var dayAfter = dayD.AddDays(1);
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = dayD,
                ClockInState = AttendanceActionState.Succeeded,
                ClockInProviderRequestSent = true,
                ClockOutState = AttendanceActionState.InProgress,
                ClockOutProviderRequestSent = false
            });

        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            DayAfterAttendanceClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);

        var recoveredDayD = await stateStore.LoadAsync(dayD);
        Assert.NotNull(recoveredDayD);
        Assert.Equal(AttendanceActionState.Succeeded, recoveredDayD.ClockInState);
        Assert.Equal(AttendanceActionState.Unknown, recoveredDayD.ClockOutState);
        Assert.True(recoveredDayD.ClockOutProviderRequestSent);

        var today = await stateStore.LoadAsync(dayAfter);
        Assert.NotNull(today);
        Assert.Equal(AttendanceActionState.Succeeded, today.ClockInState);
    }

    [Fact]
    public async Task RecoverAbandonedInProgress_ForgottenClockOutNotAttempted_LeavesPriorDayUnchanged_AndTodayClockInEligible()
    {
        var provider = FakeProvider.Success();
        var dayD = AttendanceDate;
        var dayAfter = dayD.AddDays(1);
        var priorDay = new DailyAttendanceRecord
        {
            AttendanceDate = dayD,
            ClockInState = AttendanceActionState.Succeeded,
            ClockInProviderRequestSent = true,
            ClockOutState = AttendanceActionState.NotDue
        };

        var stateStore = new FakeAttendanceStateStore(new[] { priorDay });
        var service = CreateService(provider, stateStore);

        var result = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            DayAfterAttendanceClockInDueUtc,
            AttendanceActionExecutionMode.Manual);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            result.Status);
        Assert.Equal(1, provider.ClockInCallCount);

        var unchangedPrior = await stateStore.LoadAsync(dayD);
        Assert.Equal(priorDay, unchangedPrior);
        Assert.Equal(AttendanceActionState.Succeeded, unchangedPrior!.ClockInState);
        Assert.Equal(AttendanceActionState.NotDue, unchangedPrior.ClockOutState);

        var today = await stateStore.LoadAsync(dayAfter);
        Assert.NotNull(today);
        Assert.Equal(AttendanceActionState.Succeeded, today.ClockInState);
    }

    [Fact]
    public async Task MarkCompletedElsewhereAsync_EligibleFailedClockIn_SucceedsWithoutProviderCall()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed,
                ClockInProviderRequestSent = true
            });

        var service = CreateService(provider, stateStore);

        var result = await service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceCompletedElsewhereStatus.Succeeded,
            result.Status);
        Assert.Equal(AttendanceDate, result.AttendanceDate);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(0, provider.ClockOutCallCount);

        var saved = Assert.Single(stateStore.SavedRecords);
        Assert.Equal(AttendanceActionState.Succeeded, saved.ClockInState);
        Assert.Equal(
            AttendanceActionCompletionSource.CompletedElsewhere,
            saved.ClockInCompletionSource);
    }

    [Theory]
    [InlineData(AttendanceActionState.Succeeded)]
    [InlineData(AttendanceActionState.Skipped)]
    public async Task MarkCompletedElsewhereAsync_DisallowedClockInStates_AreRejected(
        AttendanceActionState disallowedState)
    {
        // Abandoned InProgress is recovered to Unknown then may use CE
        // (RecoverAbandonedInProgress_ThenCompletedElsewhere_*).
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = disallowedState
            });

        var service = CreateService(provider, stateStore);

        var result = await service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceCompletedElsewhereStatus.NotEligible,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Empty(stateStore.SavedRecords);
    }

    [Fact]
    public async Task MarkCompletedElsewhereAsync_UnknownClockIn_RemainsEligibleCompletedElsewhere()
    {
        // Existing Completed Elsewhere eligibility allows Unknown recovery.
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Unknown
            });

        var service = CreateService(provider, stateStore);

        var result = await service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        Assert.Equal(
            AttendanceCompletedElsewhereStatus.Succeeded,
            result.Status);
        Assert.Equal(0, provider.ClockInCallCount);
        Assert.Equal(
            AttendanceActionCompletionSource.CompletedElsewhere,
            stateStore.SavedRecords[^1].ClockInCompletionSource);
    }

    [Fact]
    public async Task MarkCompletedElsewhereAsync_DoesNotOverwriteInFlightProviderAction()
    {
        var provider = new BlockingFakeProvider();
        var stateStore = new FakeAttendanceStateStore();
        var service = CreateService(provider, stateStore);

        var executeTask = service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        await provider.WaitUntilEnteredAsync();

        Assert.Equal(
            AttendanceActionState.InProgress,
            stateStore.SavedRecords[^1].ClockInState);

        var completedElsewhereTask = service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        // CE must wait on the shared gate while provider execution is in flight.
        Assert.False(completedElsewhereTask.IsCompleted);

        provider.Release();

        var executeResult = await executeTask;
        var completedElsewhereResult = await completedElsewhereTask;

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            executeResult.Status);
        Assert.True(executeResult.ProviderConfirmed);
        Assert.Equal(
            AttendanceCompletedElsewhereStatus.NotEligible,
            completedElsewhereResult.Status);
        Assert.Equal(1, provider.ClockInCallCount);

        var final = stateStore.SavedRecords[^1];
        Assert.Equal(AttendanceActionState.Succeeded, final.ClockInState);
        Assert.NotEqual(
            AttendanceActionCompletionSource.CompletedElsewhere,
            final.ClockInCompletionSource);
    }

    [Fact]
    public async Task MarkCompletedElsewhereAsync_ReloadsStateUnderSharedGateAfterExecute()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed,
                ClockInProviderRequestSent = false
            });

        var service = CreateService(provider, stateStore);

        // First succeed via provider so persisted state becomes Succeeded.
        var executeResult = await service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc,
            AttendanceActionExecutionMode.ManualRetry);

        Assert.Equal(
            AttendanceActionExecutionStatus.Succeeded,
            executeResult.Status);

        var loadCountBefore = stateStore.LoadCount;

        var completedElsewhereResult =
            await service.MarkCompletedElsewhereAsync(
                AttendanceActionType.ClockIn,
                ClockInDueUtc);

        Assert.True(stateStore.LoadCount > loadCountBefore);
        Assert.Equal(
            AttendanceCompletedElsewhereStatus.NotEligible,
            completedElsewhereResult.Status);
        Assert.NotEqual(
            AttendanceActionCompletionSource.CompletedElsewhere,
            stateStore.SavedRecords[^1].ClockInCompletionSource);
        Assert.Equal(1, provider.ClockInCallCount);
    }

    [Fact]
    public async Task MarkCompletedElsewhereAsync_ConcurrentRequests_OnlyOneSucceeds()
    {
        var provider = FakeProvider.Success();
        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed
            });

        var service = CreateService(provider, stateStore);

        var first = service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);
        var second = service.MarkCompletedElsewhereAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(result =>
            result.Status == AttendanceCompletedElsewhereStatus.Succeeded));
        Assert.Equal(1, results.Count(result =>
            result.Status == AttendanceCompletedElsewhereStatus.NotEligible));
        Assert.Equal(0, provider.ClockInCallCount);

        Assert.Single(
            stateStore.SavedRecords,
            record =>
                record.ClockInCompletionSource ==
                AttendanceActionCompletionSource.CompletedElsewhere);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentDuplicateClockIn_CallsProviderOnce()
    {
        var provider = new BlockingFakeProvider();
        var stateStore = new FakeAttendanceStateStore();
        var service = CreateService(provider, stateStore);

        var first = service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);
        var second = service.ExecuteAsync(
            AttendanceActionType.ClockIn,
            ClockInDueUtc);

        await provider.WaitUntilEnteredAsync();
        provider.Release();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, provider.ClockInCallCount);
        Assert.Contains(
            results,
            result => result.Status == AttendanceActionExecutionStatus.Succeeded);
        Assert.Contains(
            results,
            result => result.Status == AttendanceActionExecutionStatus.NotEligible);
    }

    private static readonly DateOnly AttendanceDate =
        new(2026, 9, 8);

    private static readonly DateOnly NonWorkdayAttendanceDate =
        new(2026, 9, 12);

    /// <summary>
    /// 00:30 America/Toronto on 2026-09-09 (EDT, UTC-4) — local midnight+ of
    /// AttendanceDate, not a UTC-date rollover.
    /// </summary>
    private static readonly DateTimeOffset DayAfterAttendanceLocalEarlyUtc =
        new(2026, 9, 9, 4, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Same local Clock In due instant as ClockInDueUtc, one local attendance day later.
    /// </summary>
    private static readonly DateTimeOffset DayAfterAttendanceClockInDueUtc =
        new(2026, 9, 9, 10, 50, 0, TimeSpan.Zero);

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
        ITimeClockProvider provider,
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
        private readonly Dictionary<DateOnly, DailyAttendanceRecord> _records = new();
        private int _remainingSaveFailures;

        public FakeAttendanceStateStore(
            DailyAttendanceRecord? record = null,
            int failNextSaves = 0)
        {
            if (record is not null)
            {
                _records[record.AttendanceDate] = record;
            }

            _remainingSaveFailures = failNextSaves;
        }

        public FakeAttendanceStateStore(
            IEnumerable<DailyAttendanceRecord> records,
            int failNextSaves = 0)
        {
            foreach (var record in records)
            {
                _records[record.AttendanceDate] = record;
            }

            _remainingSaveFailures = failNextSaves;
        }

        public List<DailyAttendanceRecord> SavedRecords { get; } = new();

        public int LoadCount { get; private set; }

        public int SaveAttemptCount { get; private set; }

        public Task<DailyAttendanceRecord?> LoadAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;

            _records.TryGetValue(attendanceDate, out var result);
            return Task.FromResult(result);
        }

        public Task SaveAsync(
            DailyAttendanceRecord record,
            CancellationToken cancellationToken = default)
        {
            SaveAttemptCount++;

            if (_remainingSaveFailures > 0)
            {
                _remainingSaveFailures--;
                throw new InvalidOperationException(
                    "Synthetic attendance persistence failure.");
            }

            _records[record.AttendanceDate] = record;
            SavedRecords.Add(record);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
        {
            _records.Remove(attendanceDate);
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
    private sealed class BlockingFakeProvider
        : ITimeClockProvider
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ClockInCallCount { get; private set; }

        public int ClockOutCallCount { get; private set; }

        public Task WaitUntilEnteredAsync()
            => _entered.Task;

        public void Release()
            => _release.TrySetResult();

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

        public async Task<ProviderResult> ClockInAsync()
        {
            ClockInCallCount++;
            _entered.TrySetResult();
            await _release.Task.ConfigureAwait(false);

            return new ProviderResult
            {
                Success = true,
                Action = "ClockIn",
                Timestamp = DateTimeOffset.UtcNow,
                TechnicalStatus = "ProviderConfirmed",
                ProviderRequestSent = true
            };
        }

        public Task<ProviderResult> ClockOutAsync()
        {
            ClockOutCallCount++;
            throw new InvalidOperationException(
                "ClockOut was not expected in this test.");
        }
    }

    private sealed class FakeProvider
        : ITimeClockProvider
    {
        private readonly bool _success;
        private readonly bool _throw;
        private readonly bool? _providerRequestSent;
        private readonly string? _failureTechnicalStatus;

        private FakeProvider(
            bool success,
            bool shouldThrow,
            bool? providerRequestSent = null,
            string? failureTechnicalStatus = null)
        {
            _success = success;
            _throw = shouldThrow;
            _providerRequestSent = providerRequestSent;
            _failureTechnicalStatus = failureTechnicalStatus;
        }

        public int ClockInCallCount { get; private set; }

        public int ClockOutCallCount { get; private set; }

        public static FakeProvider Success()
            => new(true, false, providerRequestSent: true);

        public static FakeProvider Rejected()
            => new(false, false, providerRequestSent: true);

        public static FakeProvider RejectedUnknownProviderResponse()
            => new(
                false,
                false,
                providerRequestSent: true,
                failureTechnicalStatus: "UnknownProviderResponse");

        public static FakeProvider RejectedPreSend()
            => new(false, false, providerRequestSent: false, failureTechnicalStatus: "NetworkUnavailable");

        public static FakeProvider RejectedUnspecifiedBoundary()
            => new(false, false, providerRequestSent: null, failureTechnicalStatus: "SyntheticRejected");

        public static FakeProvider NetworkUnavailable()
            => RejectedPreSend();

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
                            : _failureTechnicalStatus ?? "SyntheticRejected",
                    ProviderRequestSent =
                        _success
                            ? true
                            : _providerRequestSent
                });
        }
    }
}