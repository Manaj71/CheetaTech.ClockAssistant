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
		builder.Services.AddSingleton<ISetupPersistenceService, DryRunGuardedSetupPersistenceService>();

		builder.Services.AddSingleton<HttpClient>();

		builder.Services.AddSingleton<ITimeClockProviderFactory, UkgTimeClockProviderFactory>();
		builder.Services.AddSingleton<ITimeClockProviderResolver, TimeClockProviderResolver>();

		// CredentialUpdateService resolves the provider from the candidate
		// ClockAssistantConfiguration through ITimeClockProviderResolver.
		builder.Services.AddSingleton<ICredentialUpdateService, CredentialUpdateService>();

		builder.Services.AddSingleton<ICredentialSetupWorkflow, DryRunCredentialSetupWorkflow>();
		builder.Services.AddTransient<SetupPage>();
		builder.Services.AddTransient<MainPage>();
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

		return builder.Build();
	}
}







