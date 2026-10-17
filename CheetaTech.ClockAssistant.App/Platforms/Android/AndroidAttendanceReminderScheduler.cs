using Android.App;
using Android.Content;
using Android.Provider;
using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.App.Platforms.Android;

public sealed class AndroidAttendanceReminderScheduler
    : IAttendanceReminderScheduler
{
    private const int ReminderRequestCode = 6201;

    private readonly Context _appContext;
    private readonly AlarmManager _alarmManager;

    public AndroidAttendanceReminderScheduler()
    {
        _appContext =
            global::Android.App.Application.Context
            ?? throw new InvalidOperationException(
                "Android application context is unavailable.");

        _alarmManager =
            _appContext.GetSystemService(Context.AlarmService)
                as AlarmManager
            ?? throw new InvalidOperationException(
                "Android AlarmManager is unavailable.");
    }

    public Task<AttendanceReminderScheduleResult> ScheduleOneTimeAsync(
        DateTimeOffset triggerAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsAndroidVersionAtLeast(31) &&
            !_alarmManager.CanScheduleExactAlarms())
        {
            return Task.FromResult(
                AttendanceReminderScheduleResult.ExactAlarmPermissionRequired);
        }

        try
        {
            var pendingIntent = CreateReminderPendingIntent(
                PendingIntentFlags.UpdateCurrent);

            if (pendingIntent is null)
            {
                return Task.FromResult(
                    AttendanceReminderScheduleResult.Failed);
            }

            var triggerAtMillis =
                triggerAtUtc.ToUnixTimeMilliseconds();

            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                _alarmManager.SetExactAndAllowWhileIdle(
                    AlarmType.RtcWakeup,
                    triggerAtMillis,
                    pendingIntent);
            }
            else
            {
                _alarmManager.SetExact(
                    AlarmType.RtcWakeup,
                    triggerAtMillis,
                    pendingIntent);
            }

            return Task.FromResult(
                AttendanceReminderScheduleResult.Scheduled);
        }
        catch (Java.Lang.SecurityException)
        {
            return Task.FromResult(
                AttendanceReminderScheduleResult.ExactAlarmPermissionRequired);
        }
        catch
        {
            return Task.FromResult(
                AttendanceReminderScheduleResult.Failed);
        }
    }

    public Task<AttendanceReminderCancelResult> CancelScheduledReminderAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var pendingIntent = CreateReminderPendingIntent(
                PendingIntentFlags.NoCreate);

            if (pendingIntent is null)
            {
                // Nothing currently scheduled for this identity.
                return Task.FromResult(
                    AttendanceReminderCancelResult.Cancelled);
            }

            _alarmManager.Cancel(pendingIntent);
            pendingIntent.Cancel();

            return Task.FromResult(
                AttendanceReminderCancelResult.Cancelled);
        }
        catch
        {
            return Task.FromResult(
                AttendanceReminderCancelResult.Failed);
        }
    }

    public Task<bool> OpenExactAlarmSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return Task.FromResult(false);
        }

        try
        {
            var intent = new Intent(
                Settings.ActionRequestScheduleExactAlarm);

            intent.SetData(
                global::Android.Net.Uri.Parse(
                    $"package:{_appContext.PackageName}"));

            intent.AddFlags(ActivityFlags.NewTask);
            _appContext.StartActivity(intent);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private PendingIntent? CreateReminderPendingIntent(
        PendingIntentFlags baseFlags)
    {
        var intent = new Intent(
            _appContext,
            typeof(AttendanceReminderAlarmReceiver));

        intent.SetAction(
            AttendanceReminderAlarmReceiver.ActionAttendanceReminder);

        return PendingIntent.GetBroadcast(
            _appContext,
            ReminderRequestCode,
            intent,
            ApplyImmutableFlag(baseFlags));
    }

    private static PendingIntentFlags ApplyImmutableFlag(
        PendingIntentFlags flags)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            flags |= PendingIntentFlags.Immutable;
        }

        return flags;
    }
}
