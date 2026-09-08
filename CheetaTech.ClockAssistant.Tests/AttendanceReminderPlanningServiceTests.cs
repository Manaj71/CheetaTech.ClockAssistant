using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceReminderPlanningServiceTests
{
    [Fact]
    public async Task PlanNextAsync_NoConfiguration_ReturnsNull()
    {
        var service = CreateService(
            includeConfiguration: false);

        var result =
            await service.PlanNextAsync(
                new DateTimeOffset(
                    2026, 9, 8, 10, 0, 0, TimeSpan.Zero));

        Assert.Null(result);
    }

    [Fact]
    public async Task PlanNextAsync_BeforeClockInLead_ReturnsClockInLeadStart()
    {
        var service = CreateService();

        var result =
            await service.PlanNextAsync(
                new DateTimeOffset(
                    2026, 9, 8, 10, 0, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Equal(
            AttendanceActionType.ClockIn,
            result.ActionType);
        Assert.Equal(
            new DateOnly(2026, 9, 8),
            result.AttendanceDate);
        Assert.Equal(
            new DateTimeOffset(
                2026, 9, 8, 10, 45, 0, TimeSpan.Zero),
            result.TriggerAtUtc);
        Assert.False(result.IsImmediate);
    }

    [Fact]
    public async Task PlanNextAsync_ClockInAlreadyDue_ReturnsImmediate()
    {
        var now =
            new DateTimeOffset(
                2026, 9, 8, 10, 50, 0, TimeSpan.Zero);

        var service = CreateService();

        var result =
            await service.PlanNextAsync(now);

        Assert.NotNull(result);
        Assert.Equal(
            AttendanceActionType.ClockIn,
            result.ActionType);
        Assert.Equal(now, result.TriggerAtUtc);
        Assert.True(result.IsImmediate);
    }

    [Fact]
    public async Task PlanNextAsync_Weekend_SkipsToNextEvaluatorEligibleWorkday()
    {
        var service = CreateService();

        var result =
            await service.PlanNextAsync(
                new DateTimeOffset(
                    2026, 9, 12, 14, 0, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Equal(
            AttendanceActionType.ClockIn,
            result.ActionType);
        Assert.Equal(
            new DateOnly(2026, 9, 14),
            result.AttendanceDate);
        Assert.Equal(
            new DateTimeOffset(
                2026, 9, 14, 10, 45, 0, TimeSpan.Zero),
            result.TriggerAtUtc);
    }

    [Fact]
    public async Task PlanNextAsync_ClockInSucceeded_ReturnsClockOutLeadStart()
    {
        var stateStore =
            new FakeAttendanceStateStore(
                new DailyAttendanceRecord
                {
                    AttendanceDate =
                        new DateOnly(2026, 9, 8),
                    ClockInState =
                        AttendanceActionState.Succeeded
                });

        var service =
            CreateService(
                stateStore: stateStore);

        var result =
            await service.PlanNextAsync(
                new DateTimeOffset(
                    2026, 9, 8, 12, 0, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Equal(
            AttendanceActionType.ClockOut,
            result.ActionType);
        Assert.Equal(
            new DateTimeOffset(
                2026, 9, 8, 19, 15, 0, TimeSpan.Zero),
            result.TriggerAtUtc);
        Assert.False(result.IsImmediate);
    }

    [Fact]
    public async Task PlanNextAsync_ClockOutAlreadyDue_ReturnsImmediate()
    {
        var now =
            new DateTimeOffset(
                2026, 9, 8, 19, 20, 0, TimeSpan.Zero);

        var stateStore =
            new FakeAttendanceStateStore(
                new DailyAttendanceRecord
                {
                    AttendanceDate =
                        new DateOnly(2026, 9, 8),
                    ClockInState =
                        AttendanceActionState.Succeeded
                });

        var service =
            CreateService(
                stateStore: stateStore);

        var result =
            await service.PlanNextAsync(now);

        Assert.NotNull(result);
        Assert.Equal(
            AttendanceActionType.ClockOut,
            result.ActionType);
        Assert.Equal(now, result.TriggerAtUtc);
        Assert.True(result.IsImmediate);
    }

    private static AttendanceReminderPlanningService CreateService(
        ClockAssistantConfiguration? configuration = null,
        FakeAttendanceStateStore? stateStore = null,
        bool includeConfiguration = true)
    {
        if (includeConfiguration && configuration is null)
        {
            configuration = Configuration();
        }

        return new AttendanceReminderPlanningService(
            new FakeConfigurationStore(configuration),
            stateStore ?? new FakeAttendanceStateStore(),
            new AttendanceStateEvaluator());
    }

    private static ClockAssistantConfiguration Configuration()
        => new()
        {
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

    private sealed class FakeAttendanceStateStore
        : IAttendanceStateStore
    {
        private readonly List<DailyAttendanceRecord> _records = new();

        public FakeAttendanceStateStore(
            params DailyAttendanceRecord[] records)
        {
            _records.AddRange(records);
        }

        public Task<DailyAttendanceRecord?> LoadAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                _records.FirstOrDefault(
                    item =>
                        item.AttendanceDate ==
                        attendanceDate));

        public Task SaveAsync(
            DailyAttendanceRecord record,
            CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(
                item =>
                    item.AttendanceDate ==
                    record.AttendanceDate);

            _records.Add(record);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(
                item =>
                    item.AttendanceDate ==
                    attendanceDate);

            return Task.CompletedTask;
        }
    }
}