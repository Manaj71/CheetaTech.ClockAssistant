#if ANDROID
using Android.Util;
using Android.Views;
using Android.Widget;
using CheetaTech.ClockAssistant.App.Controls;
using Google.Android.Gms.Ads;
using Google.Android.Gms.Ads.Initialization;
using Microsoft.Maui.Handlers;
using View = Android.Views.View;

namespace CheetaTech.ClockAssistant.App.Platforms.Android.Ads;

/// <summary>
/// Android handler for <see cref="AdMobBannerView"/>. Consent-gates Mobile Ads init and loads
/// one Google anchored adaptive test banner. Failures leave an empty zero-height view.
/// </summary>
public sealed class AdMobBannerViewHandler : ViewHandler<AdMobBannerView, FrameLayout>
{
	private const string TestBannerAdUnitId = "ca-app-pub-3940256099942544/9214589741";

	private static readonly object InitGate = new();
	private static Task? _mobileAdsInitTask;
	private static int _activeLoadGeneration;

	private AdView? _adView;
	private int _loadGeneration;
	private bool _adRequested;

	public AdMobBannerViewHandler()
		: base(PropertyMapper)
	{
	}

	public static IPropertyMapper<AdMobBannerView, AdMobBannerViewHandler> PropertyMapper =
		new PropertyMapper<AdMobBannerView, AdMobBannerViewHandler>(ViewMapper);

	protected override FrameLayout CreatePlatformView()
	{
		var context = Context ?? throw new InvalidOperationException("Android context is unavailable.");
		return new FrameLayout(context)
		{
			LayoutParameters = new ViewGroup.LayoutParams(
				ViewGroup.LayoutParams.MatchParent,
				ViewGroup.LayoutParams.WrapContent)
		};
	}

	protected override void ConnectHandler(FrameLayout platformView)
	{
		base.ConnectHandler(platformView);
		_ = BeginBannerLoadAsync(platformView);
	}

	protected override void DisconnectHandler(FrameLayout platformView)
	{
		DestroyAdView(platformView);
		base.DisconnectHandler(platformView);
	}

	private async Task BeginBannerLoadAsync(FrameLayout platformView)
	{
		_loadGeneration = Interlocked.Increment(ref _activeLoadGeneration);

		try
		{
			var activity = platformView.Context as global::Android.App.Activity
				?? Platform.CurrentActivity;
			if (activity is null)
			{
				return;
			}

			var canRequestAds = await AndroidAdConsentCoordinator
				.EnsureCanRequestAdsAsync(activity)
				.ConfigureAwait(true);

			if (!canRequestAds || !IsHandlerCurrent(platformView))
			{
				return;
			}

			await EnsureMobileAdsInitializedAsync(activity).ConfigureAwait(true);

			if (!IsHandlerCurrent(platformView) || _adRequested)
			{
				return;
			}

			_adRequested = true;
			CreateAndLoadAdView(platformView, activity);
		}
		catch (Exception ex)
		{
			Log.Warn(nameof(AdMobBannerViewHandler), $"Banner load failed: {ex.Message}");
			DestroyAdView(platformView);
		}
	}

	private void CreateAndLoadAdView(FrameLayout platformView, global::Android.App.Activity activity)
	{
		DestroyAdView(platformView);

		var adWidth = GetAdWidthDp(platformView, activity);
		var adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSize(activity, adWidth);

		var adView = new AdView(activity)
		{
			AdUnitId = TestBannerAdUnitId,
			AdSize = adSize
		};

		platformView.RemoveAllViews();
		platformView.AddView(adView, new FrameLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			ViewGroup.LayoutParams.WrapContent));

		_adView = adView;
		adView.LoadAd(new AdRequest.Builder().Build());
	}

	private static int GetAdWidthDp(FrameLayout platformView, global::Android.App.Activity activity)
	{
		var displayMetrics = activity.Resources?.DisplayMetrics;
		var density = displayMetrics?.Density ?? 1f;

		var widthPx = platformView.Width;
		if (widthPx <= 0)
		{
			widthPx = displayMetrics?.WidthPixels
				?? activity.Window?.DecorView?.Width
				?? 0;
		}

		if (widthPx <= 0)
		{
			return 320;
		}

		var widthDp = (int)(widthPx / density);
		return Math.Max(widthDp, 320);
	}

	private static Task EnsureMobileAdsInitializedAsync(global::Android.App.Activity activity)
	{
		lock (InitGate)
		{
			if (_mobileAdsInitTask is not null)
			{
				return _mobileAdsInitTask;
			}

			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_mobileAdsInitTask = tcs.Task;

			try
			{
				MobileAds.Initialize(activity, new MobileAdsInitListener(() => tcs.TrySetResult(true)));
			}
			catch (Exception ex)
			{
				Log.Warn(nameof(AdMobBannerViewHandler), $"Mobile Ads init failed: {ex.Message}");
				tcs.TrySetResult(false);
			}

			return _mobileAdsInitTask;
		}
	}

	private bool IsHandlerCurrent(FrameLayout platformView)
		=> _loadGeneration == Volatile.Read(ref _activeLoadGeneration)
			&& PlatformView == platformView
			&& VirtualView is not null;

	private void DestroyAdView(FrameLayout platformView)
	{
		try
		{
			if (_adView is not null)
			{
				platformView.RemoveView(_adView);
				_adView.Destroy();
				_adView.Dispose();
				_adView = null;
			}
			else
			{
				platformView.RemoveAllViews();
			}
		}
		catch (Exception ex)
		{
			Log.Warn(nameof(AdMobBannerViewHandler), $"AdView destroy failed: {ex.Message}");
			_adView = null;
		}

		_adRequested = false;
	}

	private sealed class MobileAdsInitListener : Java.Lang.Object, IOnInitializationCompleteListener
	{
		private readonly Action _onComplete;

		public MobileAdsInitListener(Action onComplete) => _onComplete = onComplete;

		public void OnInitializationComplete(IInitializationStatus status) => _onComplete();
	}
}
#endif
