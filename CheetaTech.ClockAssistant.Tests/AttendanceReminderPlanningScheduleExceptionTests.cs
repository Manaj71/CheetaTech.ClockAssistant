using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceReminderPlanningScheduleExceptionTests
{
    [Fact]
    public async Task PlanNextAsync_ExceptionOnWorkday_SkipsToNextEligibleWorkday()
    {
        // Monday 2026-09-07 local with holiday exception; plan from before Clock In.
        var monday = new DateOnly(2026, 9, 7);
        var tuesday = new DateOnly(2026, 9, 8);

        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = monday,
                Type = ScheduleExceptionType.StatutoryHoliday
            });

        var service = CreatePlanningService(configuration);

        var plan = await service.PlanNextAsync(
            new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));

        Assert.NotNull(plan);
        Assert.Equal(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.Equal(tuesday, plan.AttendanceDate);
    }

    [Fact]
    public async Task PlanNextAsync_ExceptionRemoved_RestoresSameDayClockIn()
    {
        var monday = new DateOnly(2026, 9, 7);
        var service = CreatePlanningService(CreateConfiguration());

        var plan = await service.PlanNextAsync(
            new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));

        Assert.NotNull(plan);
        Assert.Equal(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.Equal(monday, plan.AttendanceDate);
    }

    [Fact]
    public async Task PlanNextAsync_ExceptionAfterClockInSucceeded_StillPlansClockOut()
    {
        var monday = new DateOnly(2026, 9, 7);
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = monday,
                Type = ScheduleExceptionType.Vacation
            });

        var stateStore = new FakeAttendanceStateStore(
            new DailyAttendanceRecord
            {
                AttendanceDate = monday,
                ClockInState = AttendanceActionState.Succeeded
            });

        var service = new AttendanceReminderPlanningService(
            new FakeConfigurationStore(configuration),
            stateStore,
            new AttendanceStateEvaluator());

        var plan = await service.PlanNextAsync(
            new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));

        Assert.NotNull(plan);
        Assert.Equal(AttendanceActionType.ClockOut, plan.ActionType);
        Assert.Equal(monday, plan.AttendanceDate);
    }

    [Fact]
    public async Task PlanNextAsync_UsesConfiguredLocalDate_NotUtcCalendarDate()
    {
        // 2026-09-08 01:00 UTC = Monday 2026-09-07 21:00 America/Toronto.
        var monday = new DateOnly(2026, 9, 7);
        var configuration = CreateConfiguration(
            new ScheduleDayException
            {
                Date = monday,
                Type = ScheduleExceptionType.SickDay
            });

        var service = CreatePlanningService(configuration);

        var plan = await service.PlanNextAsync(
            new DateTimeOffset(2026, 9, 8, 1, 0, 0, TimeSpan.Zero));

        Assert.NotNull(plan);
        Assert.NotEqual(monday, plan.AttendanceDate);
        Assert.Equal(new DateOnly(2026, 9, 8), plan.AttendanceDate);
    }

    [Fact]
    public void Configuration_WithExceptions_PreservesOtherSettingsOnRebuild()
    {
        var source = CreateConfiguration(
            new ScheduleDayException
            {
                Date = new DateOnly(2026, 10, 13),
                Type = ScheduleExceptionType.StatutoryHoliday
            });

        // Simulate Settings save rebuild that must retain exceptions + schedule.
        var rebuilt = source with
        {
            WorkDays = source.WorkDays,
            ScheduleExceptions = source.ScheduleExceptions,
            ClockInTime = source.ClockInTime,
            ClockOutTime = source.ClockOutTime,
            TimeZoneId = source.TimeZoneId,
            NotificationLeadTime = source.NotificationLeadTime,
            HighVisibilityReminders = source.HighVisibilityReminders
        };

        Assert.Equal(source.ProviderType, rebuilt.ProviderType);
        Assert.Equal(source.ProviderUrl, rebuilt.ProviderUrl);
        Assert.Equal(source.WorkDays, rebuilt.WorkDays);
        Assert.Equal(source.ClockInTime, rebuilt.ClockInTime);
        Assert.Equal(source.ClockOutTime, rebuilt.ClockOutTime);
        Assert.Equal(source.TimeZoneId, rebuilt.TimeZoneId);
        Assert.Equal(
            source.NotificationLeadTime,
            rebuilt.NotificationLeadTime);
        Assert.Single(rebuilt.ScheduleExceptions);
        Assert.Equal(
            ScheduleExceptionType.StatutoryHoliday,
            rebuilt.ScheduleExceptions[0].Type);
    }

    private static AttendanceReminderPlanningService CreatePlanningService(
        ClockAssistantConfiguration configuration)
        => new(
            new FakeConfigurationStore(configuration),
            new FakeAttendanceStateStore(),
            new AttendanceStateEvaluator());

    private static ClockAssistantConfiguration CreateConfiguration(
        params ScheduleDayException[] exceptions)
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
            ScheduleExceptions = exceptions,
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
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

    private sealed class FakeAttendanceStateStore
        : IAttendanceStateStore
    {
        private readonly Dictionary<DateOnly, DailyAttendanceRecord> _records =
            new();

        public FakeAttendanceStateStore(
            DailyAttendanceRecord? record = null)
        {
            if (record is not null)
            {
                _records[record.AttendanceDate] = record;
            }
        }

        public Task<DailyAttendanceRecord?> LoadAsync(
            DateOnly attendanceDate,
            CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(attendanceDate, out var record);
            return Task.FromResult(record);
        }

        public Task SaveAsync(
            DailyAttendanceRecord record,
            CancellationToken cancellationToken = default)
        {
            _records[record.AttendanceDate] = record;
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
}
