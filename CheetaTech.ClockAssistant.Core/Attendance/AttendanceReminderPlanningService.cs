using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceReminderPlanningService
    : IAttendanceReminderPlanningService
{
    private const int PlanningHorizonDays = 8;

    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly IAttendanceStateStore _attendanceStateStore;
    private readonly AttendanceStateEvaluator _stateEvaluator;

    public AttendanceReminderPlanningService(
        IClockAssistantConfigurationStore configurationStore,
        IAttendanceStateStore attendanceStateStore,
        AttendanceStateEvaluator stateEvaluator)
    {
        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));

        _attendanceStateStore =
            attendanceStateStore
            ?? throw new ArgumentNullException(nameof(attendanceStateStore));

        _stateEvaluator =
            stateEvaluator
            ?? throw new ArgumentNullException(nameof(stateEvaluator));
    }

    public async Task<AttendanceReminderPlan?> PlanNextAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var configuration =
            await _configurationStore.GetAsync(cancellationToken);

        if (configuration is null)
        {
            return null;
        }

        var timeZone =
            ResolveTimeZone(configuration.TimeZoneId);

        var localNow =
            TimeZoneInfo.ConvertTime(utcNow, timeZone);

        for (var dayOffset = 0;
             dayOffset < PlanningHorizonDays;
             dayOffset++)
        {
            var attendanceDate =
                DateOnly.FromDateTime(
                    localNow.DateTime.Date.AddDays(dayOffset));

            var record =
                await _attendanceStateStore.LoadAsync(
                    attendanceDate,
                    cancellationToken);

            var candidates =
                BuildCandidates(
                    configuration,
                    timeZone,
                    attendanceDate);

            foreach (var candidate in candidates)
            {
                var probeUtc =
                    candidate.TriggerAtUtc <= utcNow
                        ? utcNow
                        : candidate.TriggerAtUtc;

                var evaluation =
                    _stateEvaluator.Evaluate(
                        configuration,
                        probeUtc,
                        record);

                var eligible =
                    candidate.ActionType switch
                    {
                        AttendanceActionType.ClockIn =>
                            evaluation.ClockInNotificationEligible,

                        AttendanceActionType.ClockOut =>
                            evaluation.ClockOutNotificationEligible,

                        _ => false
                    };

                if (!eligible)
                {
                    continue;
                }

                return new AttendanceReminderPlan(
                    candidate.ActionType,
                    evaluation.AttendanceDate,
                    candidate.TriggerAtUtc <= utcNow
                        ? utcNow
                        : candidate.TriggerAtUtc,
                    IsImmediate:
                        candidate.TriggerAtUtc <= utcNow);
            }
        }

        return null;
    }

    private static IReadOnlyList<AttendanceReminderPlan> BuildCandidates(
        ClockAssistantConfiguration configuration,
        TimeZoneInfo timeZone,
        DateOnly attendanceDate)
    {
        var result =
            new List<AttendanceReminderPlan>(2);

        if (configuration.ClockInTime is not null)
        {
            var clockInTriggerLocal =
                attendanceDate.ToDateTime(
                    configuration.ClockInTime.Value,
                    DateTimeKind.Unspecified)
                - configuration.NotificationLeadTime;

            var clockInUtc =
                ConvertLocalToUtc(
                    clockInTriggerLocal,
                    timeZone);

            if (clockInUtc is not null)
            {
                result.Add(
                    new AttendanceReminderPlan(
                        AttendanceActionType.ClockIn,
                        attendanceDate,
                        clockInUtc.Value,
                        IsImmediate: false));
            }
        }

        if (configuration.ClockOutTime is not null)
        {
            var clockOutTriggerLocal =
                attendanceDate.ToDateTime(
                    configuration.ClockOutTime.Value,
                    DateTimeKind.Unspecified)
                - configuration.NotificationLeadTime;

            var clockOutUtc =
                ConvertLocalToUtc(
                    clockOutTriggerLocal,
                    timeZone);

            if (clockOutUtc is not null)
            {
                result.Add(
                    new AttendanceReminderPlan(
                        AttendanceActionType.ClockOut,
                        attendanceDate,
                        clockOutUtc.Value,
                        IsImmediate: false));
            }
        }

        return result
            .OrderBy(item => item.TriggerAtUtc)
            .ToArray();
    }

    private static DateTimeOffset? ConvertLocalToUtc(
        DateTime localTime,
        TimeZoneInfo timeZone)
    {
        if (timeZone.IsInvalidTime(localTime))
        {
            return null;
        }

        var utcDateTime =
            TimeZoneInfo.ConvertTimeToUtc(
                localTime,
                timeZone);

        return new DateTimeOffset(
            utcDateTime,
            TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveTimeZone(
        string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException(
                "A configured time zone is required.",
                nameof(timeZoneId));
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(
                    timeZoneId,
                    out var windowsTimeZoneId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    windowsTimeZoneId);
            }

            throw;
        }
    }
}