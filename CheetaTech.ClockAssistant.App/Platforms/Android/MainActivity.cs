using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace CheetaTech.ClockAssistant.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		ApplyLightStatusBarIcons();
	}

	protected override void OnResume()
	{
		base.OnResume();
		// MAUI/Shell can re-apply light-status-bar flags after create; reassert contrast.
		ApplyLightStatusBarIcons();
	}

	/// <summary>
	/// Keep the existing purple status-bar background; force light/white system icons/text.
	/// AppearanceLightStatusBars=false means dark background → light foreground icons.
	/// </summary>
	private void ApplyLightStatusBarIcons()
	{
		var window = Window;
		if (window is null)
		{
			return;
		}

		var controller = WindowCompat.GetInsetsController(window, window.DecorView);
		if (controller is null)
		{
			return;
		}

		controller.AppearanceLightStatusBars = false;
	}
}
