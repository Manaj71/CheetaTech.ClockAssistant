using Android.App;
using Android.Content;
using AndroidX.Core.App;
using CheetaTech.ClockAssistant.App.Services.Notifications;
using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;
using Microsoft.Maui;

namespace CheetaTech.ClockAssistant.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AttendanceNotificationActionReceiver
    : BroadcastReceiver
{
    internal const string ActionSnooze =
        "CheetaTech.ClockAssistant.Action.SNOOZE";

    internal const string ActionConfirm =
        "CheetaTech.ClockAssistant.Action.CONFIRM_ATTENDANCE";

    internal const string ExtraNotificationId =
        "CheetaTech.ClockAssistant.Extra.NOTIFICATION_ID";

    internal const string ExtraActionType =
        "CheetaTech.ClockAssistant.Extra.ATTENDANCE_ACTION_TYPE";

    public override void OnReceive(
        Context? context,
        Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        var notificationId =
            intent.GetIntExtra(
                ExtraNotificationId,
                -1);

        var actionTypeValue =
            intent.GetIntExtra(
                ExtraActionType,
                -1);

        if (notificationId < 0 ||
            !Enum.IsDefined(
                typeof(AttendanceActionType),
                actionTypeValue))
        {
            return;
        }

        var pendingResult = GoAsync();

        if (pendingResult is not null)
        {
            _ = HandleAsync(
                context,
                intent.Action,
                notificationId,
                (AttendanceActionType)actionTypeValue,
                pendingResult);
        }
    }

    private static async Task HandleAsync(
        Context context,
        string? requestedAction,
        int notificationId,
        AttendanceActionType requestedActionType,
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

            if (decisionService is null)
            {
                return;
            }

            var now =
                DateTimeOffset.UtcNow;

            var decision =
                await decisionService.EvaluateAsync(now);

            if (decision is null ||
                !decision.NotificationEligible ||
                decision.ActionType != requestedActionType)
            {
                NotificationManagerCompat
                    .From(context)?
                    .Cancel(notificationId);

                return;
            }

            if (string.Equals(
                requestedAction,
                ActionSnooze,
                StringComparison.Ordinal))
            {
                await SnoozeAsync(
                    context,
                    notificationId,
                    requestedActionType,
                    now,
                    services);

                return;
            }

            if (string.Equals(
                requestedAction,
                ActionConfirm,
                StringComparison.Ordinal))
            {
                await ExecuteAttendanceActionBridgeAsync(
                    context,
                    notificationId,
                    requestedActionType,
                    now,
                    services);
            }
        }
        catch
        {
            // Fail closed.
        }
        finally
        {
            pendingResult.Finish();
        }
    }

    private static async Task SnoozeAsync(
        Context context,
        int notificationId,
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        IServiceProvider? services)
    {
        var configurationStore =
            services?.GetService(
                typeof(IClockAssistantConfigurationStore))
                as IClockAssistantConfigurationStore;

        var scheduler =
            services?.GetService(
                typeof(IAttendanceReminderScheduler))
                as IAttendanceReminderScheduler;

        var planner =
            services?.GetService(
                typeof(AttendanceSnoozePlanner))
                as AttendanceSnoozePlanner;

        if (configurationStore is null ||
            scheduler is null ||
            planner is null)
        {
            return;
        }

        var configuration =
            await configurationStore.GetAsync();

        if (configuration is null)
        {
            return;
        }

        var plan =
            planner.Plan(
                configuration,
                actionType,
                utcNow);

        if (plan is null)
        {
            return;
        }

        var result =
            await scheduler.ScheduleOneTimeAsync(
                plan.TriggerAtUtc);

        if (result ==
            AttendanceReminderScheduleResult.Scheduled)
        {
            NotificationManagerCompat
                .From(context)?
                .Cancel(notificationId);
        }
    }

    private static async Task ExecuteAttendanceActionBridgeAsync(
        Context context,
        int notificationId,
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        IServiceProvider? services)
    {
        var executionService =
            services?.GetService(
                typeof(IAttendanceActionExecutionService))
                as IAttendanceActionExecutionService;

        if (executionService is null)
        {
            return;
        }

        var result =
            await executionService.ExecuteAsync(
                actionType,
                utcNow);

        if (ShouldRearmNextReminder(result.Status))
        {
            var reminderCoordinator =
                services?.GetService(
                    typeof(IAttendanceReminderStartupCoordinator))
                    as IAttendanceReminderStartupCoordinator;

            if (reminderCoordinator is not null)
            {
                await reminderCoordinator.ScheduleNextAsync(
                    DateTimeOffset.UtcNow);
            }
        }

        var actionName =
            actionType == AttendanceActionType.ClockIn
                ? "Clock In"
                : "Clock Out";

        var (title, message) =
            result.Status switch
            {
                AttendanceActionExecutionStatus.Succeeded =>
                    (
                        $"{actionName} successful",
                        $"Provider confirmed {actionName}."
                    ),

                AttendanceActionExecutionStatus.ExecutionDisabled =>
                    (
                        $"{actionName} not authorized",
                        "Controlled live execution is not enabled for this attendance date."
                    ),

                AttendanceActionExecutionStatus.NotEligible =>
                    (
                        $"{actionName} no longer due",
                        "Attendance state changed before the action was processed."
                    ),

                AttendanceActionExecutionStatus.ProviderRejected =>
                    (
                        $"{actionName} failed",
                        "The provider did not confirm the action."
                    ),

                AttendanceActionExecutionStatus.ProviderUnknown =>
                    (
                        $"{actionName} status uncertain",
                        "Do not retry until the provider status is checked."
                    ),

                AttendanceActionExecutionStatus.PersistenceFailed
                    when result.ProviderConfirmed =>
                    (
                        $"{actionName} needs attention",
                        "The provider may have confirmed the action, but local state was not saved. Do not retry."
                    ),

                AttendanceActionExecutionStatus.MissingCredentials =>
                    (
                        $"{actionName} unavailable",
                        "Stored credentials are unavailable. Open Clock Assistant."
                    ),

                AttendanceActionExecutionStatus.MissingConfiguration =>
                    (
                        $"{actionName} unavailable",
                        "Attendance configuration is unavailable. Open Clock Assistant."
                    ),

                AttendanceActionExecutionStatus.ProviderResolutionFailed =>
                    (
                        $"{actionName} unavailable",
                        "The configured provider could not be prepared."
                    ),

                _ =>
                    (
                        $"{actionName} needs attention",
                        "The action could not be completed safely."
                    )
            };

        ShowResultNotification(
            context,
            notificationId,
            title,
            message);
    }

    private static bool ShouldRearmNextReminder(
        AttendanceActionExecutionStatus status)
    {
        return status is
            AttendanceActionExecutionStatus.Succeeded
            or AttendanceActionExecutionStatus.NotEligible;
    }
    private static void ShowResultNotification(
        Context context,
        int notificationId,
        string title,
        string message)
    {
        var manager =
            NotificationManagerCompat.From(context);

        if (manager is null)
        {
            return;
        }

        var builder =
            new NotificationCompat.Builder(
                context,
                AndroidLocalNotificationService.ChannelId);

        builder.SetContentTitle(title);
        builder.SetContentText(message);
        builder.SetSmallIcon(
            global::CheetaTech.ClockAssistant.App.Resource.Drawable.notification_small_icon);
        builder.SetPriority(
            (int)NotificationPriority.High);
        builder.SetVisibility(
            NotificationCompat.VisibilityPublic);
        builder.SetAutoCancel(true);

        var notification =
            builder.Build();

        if (notification is not null)
        {
            manager.Notify(
                notificationId,
                notification);
        }
    }
}