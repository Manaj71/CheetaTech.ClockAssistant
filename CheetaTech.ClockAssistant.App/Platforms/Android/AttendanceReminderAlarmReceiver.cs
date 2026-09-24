using Android.App;
using Android.Content;
using Android.Util;
using CheetaTech.ClockAssistant.App.Services.Notifications;
using CheetaTech.ClockAssistant.Core.Attendance;
using Microsoft.Maui;

namespace CheetaTech.ClockAssistant.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AttendanceReminderAlarmReceiver
    : BroadcastReceiver
{
    internal const string ActionAttendanceReminder =
        "CheetaTech.ClockAssistant.Action.ATTENDANCE_REMINDER";

    private const string ReminderLogTag = "ShiftPilotReminders";

    public override void OnReceive(
        Context? context,
        Intent? intent)
    {
        if (context is null ||
            intent is null ||
            !string.Equals(
                intent.Action,
                ActionAttendanceReminder,
                StringComparison.Ordinal))
        {
            return;
        }

        var pendingResult = GoAsync();

        if (pendingResult is null)
        {
            return;
        }

        _ = HandleAsync(pendingResult);
    }

    private static async Task HandleAsync(
        BroadcastReceiver.PendingResult pendingResult)
    {
        try
        {
            var services =
                IPlatformApplication.Current?.Services;

            var decisionService =
                services?.GetService(
                    typeof(IAttendanceNotificationDecisionService))
                    as IAttendanceNotificationDecisionService;

            var notificationService =
                services?.GetService(
                    typeof(ILocalNotificationService))
                    as ILocalNotificationService;

            var reminderCoordinator =
                services?.GetService(
                    typeof(IAttendanceReminderStartupCoordinator))
                    as IAttendanceReminderStartupCoordinator;

            if (decisionService is null ||
                notificationService is null)
            {
                return;
            }

            var utcNow = DateTimeOffset.UtcNow;

            var decision =
                await decisionService.EvaluateAsync(utcNow);

            if (decision is null ||
                !decision.NotificationEligible ||
                decision.ActionType is null)
            {
                // Alarm consumed with nothing to show — still advance the
                // one-at-a-time chain so a future Clock Out / next workday
                // reminder is registered without requiring an app open.
                if (reminderCoordinator is not null)
                {
                    await reminderCoordinator.ScheduleNextAsync(utcNow);
                }

                return;
            }

            var actionType = decision.ActionType.Value;

            Log.Info(
                ReminderLogTag,
                $"REMINDER_ALARM_RECEIVED Action={actionType}");

            switch (actionType)
            {
                case AttendanceActionType.ClockIn:
                    await notificationService.ShowAsync(
                        AttendanceReminderNotificationIds.ClockIn,
                        "Clock In reminder",
                        "Clock In is due. Expand this notification for actions.",
                        AttendanceActionType.ClockIn);
                    break;

                case AttendanceActionType.ClockOut:
                    await notificationService.ShowAsync(
                        AttendanceReminderNotificationIds.ClockOut,
                        "Clock Out reminder",
                        "Clock Out is due. Expand this notification for actions.",
                        AttendanceActionType.ClockOut);
                    break;
            }

            // Advance the chain immediately after delivery (e.g. Clock In
            // reminder → schedule Clock Out). Must not wait for the later
            // notification-action BroadcastReceiver, which runs a long
            // provider call under GoAsync's short lifetime.
            if (reminderCoordinator is not null)
            {
                await reminderCoordinator.ScheduleNextAfterActionAsync(
                    actionType,
                    DateTimeOffset.UtcNow);
            }
        }
        catch
        {
            // Fail closed. No attendance mutation and no provider call.
        }
        finally
        {
            pendingResult.Finish();
        }
    }
}
