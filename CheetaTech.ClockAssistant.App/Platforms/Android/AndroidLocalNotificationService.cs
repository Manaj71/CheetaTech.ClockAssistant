using Android.App;
using Android.Content;
using AndroidX.Core.App;
using CheetaTech.ClockAssistant.App.Services.Notifications;
using CheetaTech.ClockAssistant.Core.Attendance;
using Microsoft.Maui.ApplicationModel;

namespace CheetaTech.ClockAssistant.App.Platforms.Android;

public sealed class AndroidLocalNotificationService : ILocalNotificationService
{
    internal const string ChannelId = "attendance-reminders-v1";
    private const string ChannelName = "Attendance reminders";
    private const string ChannelDescription = "Clock In and Clock Out reminders.";

    private readonly NotificationManagerCompat _notificationManager;

    public AndroidLocalNotificationService()
    {
        var appContext = Platform.AppContext
            ?? throw new InvalidOperationException(
                "MAUI Android application context is unavailable.");

        _notificationManager = NotificationManagerCompat.From(appContext)
            ?? throw new InvalidOperationException(
                "Android notification manager compatibility service is unavailable.");

        EnsureNotificationChannel();
    }

    public async Task<PermissionStatus> GetPermissionStatusAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return PermissionStatus.Granted;
        }

        return await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
    }

    public async Task<PermissionStatus> RequestPermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return PermissionStatus.Granted;
        }

        return await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    public async Task<bool> ShowAsync(
        int notificationId,
        string title,
        string message,
        AttendanceActionType actionType)
    {
        if (notificationId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(notificationId),
                "Notification id must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Notification title is required.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Notification message is required.",
                nameof(message));
        }

        if (await GetPermissionStatusAsync() != PermissionStatus.Granted)
        {
            return false;
        }

        EnsureNotificationChannel();

        var appContext = Platform.AppContext
            ?? throw new InvalidOperationException(
                "MAUI Android application context is unavailable.");

        var builder = new NotificationCompat.Builder(
            appContext,
            ChannelId);

        builder.SetContentTitle(title);
        builder.SetContentText(message);
        builder.SetSmallIcon(
            global::CheetaTech.ClockAssistant.App.Resource.Drawable.notification_small_icon);
        builder.SetPriority((int)NotificationPriority.High);
        builder.SetVisibility(NotificationCompat.VisibilityPublic);
        builder.SetAutoCancel(true);

        var snoozeIntent = new Intent(
            appContext,
            typeof(AttendanceNotificationActionReceiver));
        snoozeIntent.SetAction(
            AttendanceNotificationActionReceiver.ActionSnooze);
        snoozeIntent.PutExtra(
            AttendanceNotificationActionReceiver.ExtraNotificationId,
            notificationId);
        snoozeIntent.PutExtra(
            AttendanceNotificationActionReceiver.ExtraActionType,
            (int)actionType);

        var snoozePendingIntent = PendingIntent.GetBroadcast(
            appContext,
            60011,
            snoozeIntent,
            GetActionPendingIntentFlags())
            ?? throw new InvalidOperationException(
                "Snooze pending intent could not be created.");

        var actionIntent = new Intent(
            appContext,
            typeof(AttendanceNotificationActionReceiver));
        actionIntent.SetAction(
            AttendanceNotificationActionReceiver.ActionConfirm);
        actionIntent.PutExtra(
            AttendanceNotificationActionReceiver.ExtraNotificationId,
            notificationId);
        actionIntent.PutExtra(
            AttendanceNotificationActionReceiver.ExtraActionType,
            (int)actionType);

        var actionPendingIntent = PendingIntent.GetBroadcast(
            appContext,
            60012,
            actionIntent,
            GetActionPendingIntentFlags())
            ?? throw new InvalidOperationException(
                "Attendance action pending intent could not be created.");

        var snoozeActionBuilder =
            new NotificationCompat.Action.Builder(
                global::CheetaTech.ClockAssistant.App.Resource.Drawable.notification_small_icon,
                new Java.Lang.String("Snooze"),
                snoozePendingIntent);

        snoozeActionBuilder.SetAuthenticationRequired(false);
        snoozeActionBuilder.SetShowsUserInterface(false);

        var snoozeAction = snoozeActionBuilder.Build()
            ?? throw new InvalidOperationException(
                "Snooze notification action could not be created.");

        var localActionBuilder =
            new NotificationCompat.Action.Builder(
                global::CheetaTech.ClockAssistant.App.Resource.Drawable.notification_small_icon,
                new Java.Lang.String(actionType == AttendanceActionType.ClockIn ? "Clock In" : "Clock Out"),
                actionPendingIntent);

        localActionBuilder.SetAuthenticationRequired(false);
        localActionBuilder.SetShowsUserInterface(false);

        var localAction = localActionBuilder.Build()
            ?? throw new InvalidOperationException(
                "Attendance notification action could not be created.");

        builder.AddAction(snoozeAction);
        builder.AddAction(localAction);

        var notification = builder.Build()
            ?? throw new InvalidOperationException(
                "Android notification could not be constructed.");

        _notificationManager.Notify(notificationId, notification);
        return true;
    }

    private static void EnsureNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var appContext = Platform.AppContext
            ?? throw new InvalidOperationException(
                "MAUI Android application context is unavailable.");

        var manager = appContext.GetSystemService(Context.NotificationService)
            as NotificationManager;

        if (manager is null)
        {
            throw new InvalidOperationException(
                "Android notification manager is unavailable.");
        }

        var channel = new NotificationChannel(
            ChannelId,
            ChannelName,
            NotificationImportance.High)
        {
            Description = ChannelDescription
        };

        manager.CreateNotificationChannel(channel);
    }

    private static PendingIntentFlags GetActionPendingIntentFlags()
    {
        var flags = PendingIntentFlags.UpdateCurrent;

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            flags |= PendingIntentFlags.Immutable;
        }

        return flags;
    }}