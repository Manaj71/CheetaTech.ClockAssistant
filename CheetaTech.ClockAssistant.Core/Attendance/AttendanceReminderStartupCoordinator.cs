using System.Diagnostics;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceReminderStartupCoordinator
    : IAttendanceReminderStartupCoordinator
{
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly IAttendanceReminderPlanningService _planningService;
    private readonly IAttendanceReminderScheduler _reminderScheduler;

    public AttendanceReminderStartupCoordinator(
        IClockAssistantConfigurationStore configurationStore,
        IAttendanceReminderPlanningService planningService,
        IAttendanceReminderScheduler reminderScheduler)
    {
        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));

        _planningService =
            planningService
            ?? throw new ArgumentNullException(nameof(planningService));

        _reminderScheduler =
            reminderScheduler
            ?? throw new ArgumentNullException(nameof(reminderScheduler));
    }

    public async Task<AttendanceReminderScheduleResult?> ScheduleNextAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var plan =
            await _planningService.PlanNextAsync(
                utcNow,
                cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var result =
            await _reminderScheduler.ScheduleOneTimeAsync(
                plan.TriggerAtUtc,
                cancellationToken);

        Trace.WriteLine(
            $"REMINDER_NEXT_SCHEDULED Action={plan.ActionType} Result={result}");

        return result;
    }

    public async Task<AttendanceReminderScheduleResult?> ScheduleNextAfterActionAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var plan =
            await _planningService.PlanNextAsync(
                utcNow,
                cancellationToken,
                actionType);

        if (plan is null)
        {
            return null;
        }

        var result =
            await _reminderScheduler.ScheduleOneTimeAsync(
                plan.TriggerAtUtc,
                cancellationToken);

        Trace.WriteLine(
            $"REMINDER_NEXT_SCHEDULED Action={plan.ActionType} Result={result}");

        return result;
    }

    public async Task<AttendanceReminderRescheduleResult> RescheduleAfterConfigurationChangeAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // Distinguish intentional empty plans from missing configuration:
        // PlanNextAsync returns null for both; only cancel after config is present.
        ClockAssistantConfiguration? configuration;

        try
        {
            configuration =
                await _configurationStore.GetAsync(cancellationToken)
                    .ConfigureAwait(false);
        }
        catch
        {
            return AttendanceReminderRescheduleResult.PlanningFailed;
        }

        if (configuration is null)
        {
            return AttendanceReminderRescheduleResult.MissingConfiguration;
        }

        AttendanceReminderPlan? plan;

        try
        {
            plan =
                await _planningService.PlanNextAsync(
                        utcNow,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch
        {
            return AttendanceReminderRescheduleResult.PlanningFailed;
        }

        if (plan is null)
        {
            var cancelResult =
                await _reminderScheduler.CancelScheduledReminderAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            return cancelResult == AttendanceReminderCancelResult.Cancelled
                ? AttendanceReminderRescheduleResult.Cancelled
                : AttendanceReminderRescheduleResult.CancelFailed;
        }

        AttendanceReminderScheduleResult scheduleResult;

        try
        {
            scheduleResult =
                await _reminderScheduler.ScheduleOneTimeAsync(
                        plan.TriggerAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch
        {
            return AttendanceReminderRescheduleResult.ScheduleFailed;
        }

        return scheduleResult switch
        {
            AttendanceReminderScheduleResult.Scheduled =>
                AttendanceReminderRescheduleResult.Scheduled,

            AttendanceReminderScheduleResult.ExactAlarmPermissionRequired =>
                AttendanceReminderRescheduleResult.ExactAlarmPermissionRequired,

            _ =>
                AttendanceReminderRescheduleResult.ScheduleFailed
        };
    }
}
