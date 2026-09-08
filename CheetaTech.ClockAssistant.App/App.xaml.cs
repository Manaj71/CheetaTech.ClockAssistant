using CheetaTech.ClockAssistant.App.Services.Notifications;

namespace CheetaTech.ClockAssistant.App;

public partial class App : Application
{
    private readonly AppShell _appShell;
    private readonly IAttendanceReminderStartupCoordinator _reminderStartupCoordinator;
    private int _startupReminderSchedulingStarted;

    public App(
        AppShell appShell,
        IAttendanceReminderStartupCoordinator reminderStartupCoordinator)
    {
        InitializeComponent();

        _appShell =
            appShell
            ?? throw new ArgumentNullException(
                nameof(appShell));

        _reminderStartupCoordinator =
            reminderStartupCoordinator
            ?? throw new ArgumentNullException(
                nameof(reminderStartupCoordinator));
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        var window =
            new Window(_appShell);

        if (Interlocked.Exchange(
                ref _startupReminderSchedulingStarted,
                1) == 0)
        {
            _ = ScheduleNextReminderSafelyAsync();
        }

        return window;
    }

    private async Task ScheduleNextReminderSafelyAsync()
    {
        try
        {
            await _reminderStartupCoordinator.ScheduleNextAsync(
                DateTimeOffset.UtcNow);
        }
        catch
        {
            // Fail closed. Reminder scheduling must not crash app startup.
        }
    }
}