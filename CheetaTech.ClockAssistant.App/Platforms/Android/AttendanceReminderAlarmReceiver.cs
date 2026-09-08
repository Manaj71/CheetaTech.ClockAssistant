using Android.App;
using Android.Content;
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

    private const int ClockInReminderNotificationId = 6101;
    private const int ClockOutReminderNotificationId = 6102;

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

            if (decisionService is null ||
                notificationService is null)
            {
                return;
            }

            var decision =
                await decisionService.EvaluateAsync(
                    DateTimeOffset.UtcNow);

            if (decision is null ||
                !decision.NotificationEligible ||
                decision.ActionType is null)
            {
                return;
            }

            switch (decision.ActionType.Value)
            {
                case AttendanceActionType.ClockIn:
                    await notificationService.ShowAsync(
                        ClockInReminderNotificationId,
                        "Clock In reminder",
                        "Clock In is due. Expand this notification for actions.",
                        AttendanceActionType.ClockIn);
                    break;

                case AttendanceActionType.ClockOut:
                    await notificationService.ShowAsync(
                        ClockOutReminderNotificationId,
                        "Clock Out reminder",
                        "Clock Out is due. Expand this notification for actions.",
                        AttendanceActionType.ClockOut);
                    break;
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