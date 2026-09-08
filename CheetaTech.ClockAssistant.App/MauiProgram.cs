using CheetaTech.ClockAssistant.App.Services.Security;
using CheetaTech.ClockAssistant.App.Services.Credentials;
using CheetaTech.ClockAssistant.App.Services.Configuration;
using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Providers.UKG;
using Microsoft.Extensions.Logging;

namespace CheetaTech.ClockAssistant.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		builder.Services.AddSingleton<ICredentialStore, MauiCredentialStore>();
		builder.Services.AddSingleton<IClockAssistantConfigurationStore, MauiPreferencesConfigurationStore>();
		builder.Services.AddSingleton<IConfigurationCompletenessEvaluator, ConfigurationCompletenessEvaluator>();
		builder.Services.AddSingleton<ISetupReadinessService, SetupReadinessService>();
		builder.Services.AddSingleton<ISetupLifecycleService, DryRunSetupLifecycleService>();
		builder.Services.AddSingleton<ITrustedSetupPersistenceOrchestrator, TrustedSetupPersistenceOrchestrator>();
		builder.Services.AddSingleton<ISetupPersistenceService, TrustedSetupPersistenceService>();

		builder.Services.AddSingleton<HttpClient>();

		builder.Services.AddSingleton<ITimeClockProviderFactory, UkgTimeClockProviderFactory>();
		builder.Services.AddSingleton<ITimeClockProviderResolver, TimeClockProviderResolver>();

		// CredentialUpdateService resolves the provider from the candidate
		// ClockAssistantConfiguration through ITimeClockProviderResolver.
		builder.Services.AddSingleton<ICredentialUpdateService, DeferredCredentialUpdateService>();

		builder.Services.AddSingleton<ICredentialSetupWorkflow, DryRunCredentialSetupWorkflow>();
		builder.Services.AddTransient<SetupPage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<SettingsPage>();

#if ANDROID
builder.Services.AddSingleton<
    CheetaTech.ClockAssistant.App.Services.Security.IDeviceAuthenticationService,
    CheetaTech.ClockAssistant.App.Services.Security.AndroidDeviceAuthenticationService>();
#else
builder.Services.AddSingleton<
    CheetaTech.ClockAssistant.App.Services.Security.IDeviceAuthenticationService,
    CheetaTech.ClockAssistant.App.Services.Security.UnavailableDeviceAuthenticationService>();
#endif
		builder.Services.AddTransient<AppShell>();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.IAttendanceStatePersistence, CheetaTech.ClockAssistant.App.Services.Attendance.PreferencesAttendanceStatePersistence>();
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.IAttendanceStateStore, CheetaTech.ClockAssistant.Core.Attendance.AttendanceStateStore>();
#if ANDROID
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.App.Services.Notifications.ILocalNotificationService, CheetaTech.ClockAssistant.App.Platforms.Android.AndroidLocalNotificationService>();
#else
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.App.Services.Notifications.ILocalNotificationService, CheetaTech.ClockAssistant.App.Services.Notifications.UnavailableLocalNotificationService>();
#endif

        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.AttendanceStateEvaluator>();
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.IAttendanceNotificationDecisionService, CheetaTech.ClockAssistant.Core.Attendance.AttendanceNotificationDecisionService>();

#if ANDROID
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.App.Services.Notifications.IAttendanceReminderScheduler, CheetaTech.ClockAssistant.App.Platforms.Android.AndroidAttendanceReminderScheduler>();
#else
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.App.Services.Notifications.IAttendanceReminderScheduler, CheetaTech.ClockAssistant.App.Services.Notifications.UnavailableAttendanceReminderScheduler>();
#endif
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.AttendanceSnoozePlanner>();

        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.IAttendanceProviderExecutionGate>(
            _ => new CheetaTech.ClockAssistant.Core.Attendance.ControlledDateAttendanceProviderExecutionGate(
                new DateOnly(2026, 9, 9)));
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.IAttendanceActionExecutionService, CheetaTech.ClockAssistant.Core.Attendance.AttendanceActionExecutionService>();

        builder.Services.AddSingleton<CheetaTech.ClockAssistant.Core.Attendance.IAttendanceReminderPlanningService, CheetaTech.ClockAssistant.Core.Attendance.AttendanceReminderPlanningService>();
        builder.Services.AddSingleton<CheetaTech.ClockAssistant.App.Services.Notifications.IAttendanceReminderStartupCoordinator, CheetaTech.ClockAssistant.App.Services.Notifications.AttendanceReminderStartupCoordinator>();

        return builder.Build();
	}
}
