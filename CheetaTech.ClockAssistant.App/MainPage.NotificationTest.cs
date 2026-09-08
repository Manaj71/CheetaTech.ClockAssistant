using CheetaTech.ClockAssistant.App.Services.Notifications;
using Microsoft.Maui.ApplicationModel;

namespace CheetaTech.ClockAssistant.App;

public partial class MainPage
{
    private bool _notificationTestBusy;

    private async void OnTestNotificationClicked(
        object? sender,
        EventArgs e)
    {
        if (_notificationTestBusy)
        {
            return;
        }

        _notificationTestBusy = true;

        try
        {
            var services = Handler?.MauiContext?.Services;

            var notificationService =
                services?.GetService(typeof(ILocalNotificationService))
                    as ILocalNotificationService;

            var scheduler =
                services?.GetService(typeof(IAttendanceReminderScheduler))
                    as IAttendanceReminderScheduler;

            if (notificationService is null || scheduler is null)
            {
                await DisplayAlertAsync(
                    "Reminder Test",
                    "The reminder services are unavailable.",
                    "OK");
                return;
            }

            var permissionStatus =
                await notificationService.GetPermissionStatusAsync();

            if (permissionStatus != PermissionStatus.Granted)
            {
                permissionStatus =
                    await notificationService.RequestPermissionAsync();
            }

            if (permissionStatus != PermissionStatus.Granted)
            {
                await DisplayAlertAsync(
                    "Notifications Required",
                    "Notification permission must be allowed before testing scheduled attendance reminders.",
                    "OK");
                return;
            }

            var triggerAtUtc =
                DateTimeOffset.UtcNow.AddMinutes(5);

            var scheduleResult =
                await scheduler.ScheduleOneTimeAsync(triggerAtUtc);

            if (scheduleResult ==
                AttendanceReminderScheduleResult.ExactAlarmPermissionRequired)
            {
                await DisplayAlertAsync(
                    "Alarms & Reminders Access",
                    "Android requires Alarms & reminders access so Clock Assistant can deliver attendance reminders close to the configured time. The system settings page will open. Enable the permission, return to Clock Assistant, then tap Schedule 5-Min Reminder again.",
                    "OK");

                var opened =
                    await scheduler.OpenExactAlarmSettingsAsync();

                if (!opened)
                {
                    await DisplayAlertAsync(
                        "Reminder Test",
                        "Clock Assistant could not open the Android Alarms & reminders settings page.",
                        "OK");
                }

                return;
            }

            if (scheduleResult != AttendanceReminderScheduleResult.Scheduled)
            {
                await DisplayAlertAsync(
                    "Reminder Test",
                    "The 5-minute reminder could not be scheduled.",
                    "OK");
                return;
            }

            await DisplayAlertAsync(
                "Reminder Scheduled",
                "A local attendance reminder is scheduled for about 5 minutes from now. You can press Home or lock the phone. Phase 5 state will be checked again when the alarm fires.",
                "OK");
        }
        catch
        {
            await DisplayAlertAsync(
                "Reminder Test",
                "The scheduled reminder test could not be completed.",
                "OK");
        }
        finally
        {
            _notificationTestBusy = false;
        }
    }
}