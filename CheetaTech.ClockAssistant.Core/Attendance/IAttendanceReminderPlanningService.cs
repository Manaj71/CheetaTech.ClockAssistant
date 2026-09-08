namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceReminderPlanningService
{
    Task<AttendanceReminderPlan?> PlanNextAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}