namespace CheetaTech.ClockAssistant.Core.Attendance;

/// <summary>
/// User-facing copy after a successful configuration save followed by reschedule.
/// Distinguishes save success from reminder-update outcomes.
/// </summary>
public static class AttendanceReminderRescheduleUserMessage
{
    public static string ForSuccessfulSave(
        AttendanceReminderRescheduleResult rescheduleResult)
    {
        return rescheduleResult switch
        {
            AttendanceReminderRescheduleResult.Scheduled =>
                "Settings saved successfully. Attendance reminders were updated.",

            AttendanceReminderRescheduleResult.Cancelled =>
                "Settings saved successfully. No upcoming attendance reminder is scheduled.",

            AttendanceReminderRescheduleResult.ExactAlarmPermissionRequired =>
                "Settings saved, but reminders could not be updated. Allow Alarms & reminders for ShiftPilot in system settings.",

            AttendanceReminderRescheduleResult.CancelFailed =>
                "Settings saved, but the previous reminder could not be cleared. Your previous reminder may still be active.",

            AttendanceReminderRescheduleResult.ScheduleFailed =>
                "Settings saved, but reminders could not be updated. Your previous reminder may still be active.",

            AttendanceReminderRescheduleResult.PlanningFailed =>
                "Settings saved, but reminders could not be updated. Your previous reminder may still be active.",

            AttendanceReminderRescheduleResult.MissingConfiguration =>
                "Settings saved, but reminders could not be updated. Your previous reminder may still be active.",

            _ =>
                "Settings saved, but reminders could not be updated. Your previous reminder may still be active."
        };
    }
}
