using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceReminderRescheduleCoordinatorTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly AttendanceReminderPlan SamplePlan =
        new(
            AttendanceActionType.ClockIn,
            new DateOnly(2026, 9, 9),
            new DateTimeOffset(2026, 9, 9, 10, 45, 0, TimeSpan.Zero),
            IsImmediate: false);

    [Fact]
    public async Task Reschedule_ValidPlan_SchedulesWithoutCancel()
    {
        var scheduler = new FakeReminderScheduler
        {
            ScheduleResult = AttendanceReminderScheduleResult.Scheduled
        };

        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: SamplePlan,
            scheduler: scheduler);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.Scheduled,
            result);
        Assert.Equal(1, scheduler.ScheduleCallCount);
        Assert.Equal(0, scheduler.CancelCallCount);
        Assert.Equal(
            SamplePlan.TriggerAtUtc,
            scheduler.LastScheduledTriggerUtc);
    }

    [Fact]
    public async Task Reschedule_EmptyPlan_CancelsExistingReminder()
    {
        var scheduler = new FakeReminderScheduler
        {
            CancelResult = AttendanceReminderCancelResult.Cancelled
        };

        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: null,
            scheduler: scheduler);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.Cancelled,
            result);
        Assert.Equal(0, scheduler.ScheduleCallCount);
        Assert.Equal(1, scheduler.CancelCallCount);
    }

    [Fact]
    public async Task Reschedule_PlanningThrows_DoesNotCancel()
    {
        var scheduler = new FakeReminderScheduler();
        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: null,
            scheduler: scheduler,
            planningThrows: true);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.PlanningFailed,
            result);
        Assert.Equal(0, scheduler.CancelCallCount);
        Assert.Equal(0, scheduler.ScheduleCallCount);
    }

    [Fact]
    public async Task Reschedule_SchedulerFailure_DoesNotCancel()
    {
        var scheduler = new FakeReminderScheduler
        {
            ScheduleResult = AttendanceReminderScheduleResult.Failed
        };

        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: SamplePlan,
            scheduler: scheduler);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.ScheduleFailed,
            result);
        Assert.Equal(1, scheduler.ScheduleCallCount);
        Assert.Equal(0, scheduler.CancelCallCount);
    }

    [Fact]
    public async Task Reschedule_ExactAlarmPermissionRequired_DoesNotCancel()
    {
        var scheduler = new FakeReminderScheduler
        {
            ScheduleResult =
                AttendanceReminderScheduleResult.ExactAlarmPermissionRequired
        };

        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: SamplePlan,
            scheduler: scheduler);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.ExactAlarmPermissionRequired,
            result);
        Assert.Equal(1, scheduler.ScheduleCallCount);
        Assert.Equal(0, scheduler.CancelCallCount);
    }

    [Fact]
    public async Task Reschedule_CancelFailure_IsReportedNotSuccess()
    {
        var scheduler = new FakeReminderScheduler
        {
            CancelResult = AttendanceReminderCancelResult.Failed
        };

        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: null,
            scheduler: scheduler);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.CancelFailed,
            result);
        Assert.Equal(1, scheduler.CancelCallCount);
        Assert.NotEqual(
            AttendanceReminderRescheduleResult.Cancelled,
            result);
        Assert.NotEqual(
            AttendanceReminderRescheduleResult.Scheduled,
            result);
    }

    [Fact]
    public async Task Reschedule_MissingConfiguration_DoesNotCancel()
    {
        var scheduler = new FakeReminderScheduler();
        var coordinator = CreateCoordinator(
            configuration: null,
            plan: null,
            scheduler: scheduler);

        var result =
            await coordinator.RescheduleAfterConfigurationChangeAsync(
                UtcNow);

        Assert.Equal(
            AttendanceReminderRescheduleResult.MissingConfiguration,
            result);
        Assert.Equal(0, scheduler.CancelCallCount);
        Assert.Equal(0, scheduler.ScheduleCallCount);
    }

    [Fact]
    public async Task ScheduleNextAsync_NullPlan_StillDoesNotCancel()
    {
        // Existing startup semantics must remain: null plan => null, no cancel.
        var scheduler = new FakeReminderScheduler();
        var coordinator = CreateCoordinator(
            configuration: CreateConfiguration(),
            plan: null,
            scheduler: scheduler);

        var result = await coordinator.ScheduleNextAsync(UtcNow);

        Assert.Null(result);
        Assert.Equal(0, scheduler.CancelCallCount);
        Assert.Equal(0, scheduler.ScheduleCallCount);
    }

    private static AttendanceReminderStartupCoordinator CreateCoordinator(
        ClockAssistantConfiguration? configuration,
        AttendanceReminderPlan? plan,
        FakeReminderScheduler scheduler,
        bool planningThrows = false)
    {
        return new AttendanceReminderStartupCoordinator(
            new FakeConfigurationStore(configuration),
            new FakePlanningService(plan, planningThrows),
            scheduler);
    }

    private static ClockAssistantConfiguration CreateConfiguration()
        => new()
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.invalid/",
            WorkDays =
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            ],
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15)
        };

    private sealed class FakeConfigurationStore
        : IClockAssistantConfigurationStore
    {
        private readonly ClockAssistantConfiguration? _configuration;

        public FakeConfigurationStore(
            ClockAssistantConfiguration? configuration)
        {
            _configuration = configuration;
        }

        public Task SaveAsync(
            ClockAssistantConfiguration configuration,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ClockAssistantConfiguration?> GetAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(_configuration);

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakePlanningService
        : IAttendanceReminderPlanningService
    {
        private readonly AttendanceReminderPlan? _plan;
        private readonly bool _throws;

        public FakePlanningService(
            AttendanceReminderPlan? plan,
            bool throws)
        {
            _plan = plan;
            _throws = throws;
        }

        public Task<AttendanceReminderPlan?> PlanNextAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default,
            AttendanceActionType? skipActionType = null)
        {
            if (_throws)
            {
                throw new InvalidOperationException(
                    "Synthetic planning failure.");
            }

            return Task.FromResult(_plan);
        }
    }

    private sealed class FakeReminderScheduler
        : IAttendanceReminderScheduler
    {
        public AttendanceReminderScheduleResult ScheduleResult { get; init; }
            = AttendanceReminderScheduleResult.Scheduled;

        public AttendanceReminderCancelResult CancelResult { get; init; }
            = AttendanceReminderCancelResult.Cancelled;

        public int ScheduleCallCount { get; private set; }

        public int CancelCallCount { get; private set; }

        public DateTimeOffset? LastScheduledTriggerUtc { get; private set; }

        public Task<AttendanceReminderScheduleResult> ScheduleOneTimeAsync(
            DateTimeOffset triggerAtUtc,
            CancellationToken cancellationToken = default)
        {
            ScheduleCallCount++;
            LastScheduledTriggerUtc = triggerAtUtc;
            return Task.FromResult(ScheduleResult);
        }

        public Task<AttendanceReminderCancelResult> CancelScheduledReminderAsync(
            CancellationToken cancellationToken = default)
        {
            CancelCallCount++;
            return Task.FromResult(CancelResult);
        }

        public Task<bool> OpenExactAlarmSettingsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
