using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public sealed class AttendanceReminderStartupCoordinator
    : IAttendanceReminderStartupCoordinator
{
    private readonly IAttendanceReminderPlanningService _planningService;
    private readonly IAttendanceReminderScheduler _reminderScheduler;

    public AttendanceReminderStartupCoordinator(
        IAttendanceReminderPlanningService planningService,
        IAttendanceReminderScheduler reminderScheduler)
    {
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

        return await _reminderScheduler.ScheduleOneTimeAsync(
            plan.TriggerAtUtc);
    }
}