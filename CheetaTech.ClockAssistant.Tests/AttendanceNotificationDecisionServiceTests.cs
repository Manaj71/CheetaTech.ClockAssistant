using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceNotificationDecisionServiceTests
{
    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenConfigurationDoesNotExist()
    {
        var service = CreateService(
            configuration: null,
            persistedRecord: null);

        var result = await service.EvaluateAsync(
            new DateTimeOffset(2026, 9, 7, 10, 50, 0, TimeSpan.Zero));

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsClockIn_WhenClockInNotificationIsDue()
    {
        var service = CreateService(
            CreateConfiguration(),
            persistedRecord: null);

        var result = await service.EvaluateAsync(
            new DateTimeOffset(2026, 9, 7, 10, 50, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.True(result.NotificationEligible);
        Assert.Equal(AttendanceActionType.ClockIn, result.ActionType);
        Assert.True(result.Evaluation.ClockInNotificationEligible);
        Assert.False(result.Evaluation.ClockOutNotificationEligible);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNoAction_BeforeClockInLeadWindow()
    {
        var service = CreateService(
            CreateConfiguration(),
            persistedRecord: null);

        var result = await service.EvaluateAsync(
            new DateTimeOffset(2026, 9, 7, 10, 30, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.False(result.NotificationEligible);
        Assert.Null(result.ActionType);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsClockOut_WhenClockInSucceededAndClockOutIsDue()
    {
        var configuration = CreateConfiguration();

        var service = CreateService(
            configuration,
            new DailyAttendanceRecord
            {
                AttendanceDate = new DateOnly(2026, 9, 7),
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.NotDue
            });

        var result = await service.EvaluateAsync(
            new DateTimeOffset(2026, 9, 7, 19, 20, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.True(result.NotificationEligible);
        Assert.Equal(AttendanceActionType.ClockOut, result.ActionType);
        Assert.False(result.Evaluation.ClockInNotificationEligible);
        Assert.True(result.Evaluation.ClockOutNotificationEligible);
    }

    [Fact]
    public async Task EvaluateAsync_BlocksDuplicateClockIn_WhenClockInAlreadySucceeded()
    {
        var configuration = CreateConfiguration();

        var service = CreateService(
            configuration,
            new DailyAttendanceRecord
            {
                AttendanceDate = new DateOnly(2026, 9, 7),
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.NotDue
            });

        var result = await service.EvaluateAsync(
            new DateTimeOffset(2026, 9, 7, 11, 0, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.False(result.Evaluation.ClockInNotificationEligible);
        Assert.Null(result.ActionType);
    }

    private static AttendanceNotificationDecisionService CreateService(
        ClockAssistantConfiguration? configuration,
        DailyAttendanceRecord? persistedRecord)
    {
        return new AttendanceNotificationDecisionService(
            new FakeConfigurationStore(configuration),
            new FakeAttendanceStateStore(persistedRecord),
            new AttendanceStateEvaluator());
    }

    private static ClockAssistantConfiguration CreateConfiguration()
    {
        return new ClockAssistantConfiguration
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
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };
    }

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
        private DailyAttendanceRecord? _record;

        public FakeAttendanceStateStore(
            DailyAttendanceRecord? record)
        {
            _record = record;
        }

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
}