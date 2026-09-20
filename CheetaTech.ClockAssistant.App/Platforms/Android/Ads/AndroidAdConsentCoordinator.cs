#if ANDROID
using Android.Util;
using Xamarin.Google.UserMesssagingPlatform;

namespace CheetaTech.ClockAssistant.App.Platforms.Android.Ads;

/// <summary>
/// Process-scoped UMP consent coordinator. Failures never throw into MAUI.
/// Does not persist consent interpretations or reset consent state.
/// </summary>
internal static class AndroidAdConsentCoordinator
{
	private static readonly object Gate = new();
	private static Task<bool>? _sessionTask;
	private static bool _privacyOptionsRequired;

	/// <summary>
	/// True when UMP reports a privacy-options entry point is required.
	/// Retained for a later Settings/privacy UI step; unused in P11-S02-R01.
	/// </summary>
	public static bool IsPrivacyOptionsRequired => _privacyOptionsRequired;

	/// <summary>
	/// Ensures consent info is refreshed once per process, then returns whether ads may be requested.
	/// </summary>
	public static Task<bool> EnsureCanRequestAdsAsync(global::Android.App.Activity activity)
	{
		ArgumentNullException.ThrowIfNull(activity);

		lock (Gate)
		{
			_sessionTask ??= RunConsentFlowAsync(activity);
			return _sessionTask;
		}
	}

	private static async Task<bool> RunConsentFlowAsync(global::Android.App.Activity activity)
	{
		try
		{
			var consentInformation = UserMessagingPlatform.GetConsentInformation(activity);
			await RequestConsentInfoUpdateAsync(activity, consentInformation).ConfigureAwait(false);

			try
			{
				await LoadAndShowConsentFormIfRequiredAsync(activity).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Warn(nameof(AndroidAdConsentCoordinator), $"Consent form failed: {ex.Message}");
			}

			CapturePrivacyOptionsStatus(consentInformation);
			return consentInformation.CanRequestAds();
		}
		catch (Exception ex)
		{
			Log.Warn(nameof(AndroidAdConsentCoordinator), $"Consent update failed: {ex.Message}");

			try
			{
				var consentInformation = UserMessagingPlatform.GetConsentInformation(activity);
				CapturePrivacyOptionsStatus(consentInformation);
				return consentInformation.CanRequestAds();
			}
			catch (Exception fallbackEx)
			{
				Log.Warn(nameof(AndroidAdConsentCoordinator), $"Consent fallback failed: {fallbackEx.Message}");
				return false;
			}
		}
	}

	private static void CapturePrivacyOptionsStatus(IConsentInformation consentInformation)
	{
		try
		{
			_privacyOptionsRequired =
				consentInformation.PrivacyOptionsRequirementStatus
				== ConsentInformationPrivacyOptionsRequirementStatus.Required;
		}
		catch
		{
			_privacyOptionsRequired = false;
		}
	}

	private static Task RequestConsentInfoUpdateAsync(
		global::Android.App.Activity activity,
		IConsentInformation consentInformation)
	{
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var parameters = new ConsentRequestParameters.Builder().Build();

		consentInformation.RequestConsentInfoUpdate(
			activity,
			parameters,
			new ConsentInfoUpdateSuccessListener(() => tcs.TrySetResult(true)),
			new ConsentInfoUpdateFailureListener(formError =>
			{
				var message = formError?.Message ?? "Unknown consent update failure.";
				tcs.TrySetException(new InvalidOperationException(message));
			}));

		return tcs.Task;
	}

	private static Task LoadAndShowConsentFormIfRequiredAsync(global::Android.App.Activity activity)
	{
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		UserMessagingPlatform.LoadAndShowConsentFormIfRequired(
			activity,
			new ConsentFormLoadAndShowListener(formError =>
			{
				if (formError is null)
				{
					tcs.TrySetResult(true);
					return;
				}

				tcs.TrySetException(new InvalidOperationException(formError.Message ?? "Consent form failed."));
			}));

		return tcs.Task;
	}

	private sealed class ConsentInfoUpdateSuccessListener : Java.Lang.Object, IConsentInformationOnConsentInfoUpdateSuccessListener
	{
		private readonly Action _onSuccess;

		public ConsentInfoUpdateSuccessListener(Action onSuccess) => _onSuccess = onSuccess;

		public void OnConsentInfoUpdateSuccess() => _onSuccess();
	}

	private sealed class ConsentInfoUpdateFailureListener : Java.Lang.Object, IConsentInformationOnConsentInfoUpdateFailureListener
	{
		private readonly Action<FormError?> _onFailure;

		public ConsentInfoUpdateFailureListener(Action<FormError?> onFailure) => _onFailure = onFailure;

		public void OnConsentInfoUpdateFailure(FormError? error) => _onFailure(error);
	}

	private sealed class ConsentFormLoadAndShowListener : Java.Lang.Object, IConsentFormOnConsentFormDismissedListener
	{
		private readonly Action<FormError?> _onDismissed;

		public ConsentFormLoadAndShowListener(Action<FormError?> onDismissed) => _onDismissed = onDismissed;

		public void OnConsentFormDismissed(FormError? error) => _onDismissed(error);
	}
}
#endif
