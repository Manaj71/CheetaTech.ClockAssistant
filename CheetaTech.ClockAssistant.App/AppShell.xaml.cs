using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.App;

public partial class AppShell : Shell
{
    private readonly ISetupReadinessService _setupReadinessService;
    private readonly SetupPage _setupPage;
    private readonly MainPage _mainPage;
    private readonly SettingsPage _settingsPage;
    private bool _startupRouteResolved;

    public AppShell(
        ISetupReadinessService setupReadinessService,
        SetupPage setupPage,
        MainPage mainPage,
        SettingsPage settingsPage)
    {
        InitializeComponent();

        _setupReadinessService =
            setupReadinessService
            ?? throw new ArgumentNullException(
                nameof(setupReadinessService));

        _setupPage =
            setupPage
            ?? throw new ArgumentNullException(
                nameof(setupPage));

        _mainPage =
            mainPage
            ?? throw new ArgumentNullException(
                nameof(mainPage));

        _settingsPage =
            settingsPage
            ?? throw new ArgumentNullException(
                nameof(settingsPage));

        _setupPage.SetupCompleted +=
            OnSetupCompleted;

        // Android MAUI Shell requires an active Shell item before the
        // native Shell renderer begins creating its view hierarchy.
        // Setup is the safe startup fallback until readiness is resolved.
        ShowSetupPage();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_startupRouteResolved)
        {
            return;
        }

        try
        {
            var readiness = await _setupReadinessService
                .EvaluateAsync();

            if (!readiness.SetupRequired)
            {
                ShowMainPage();
            }

            _startupRouteResolved = true;
        }
        catch
        {
            // SetupPage is already the synchronous safe startup fallback.
            _startupRouteResolved = true;
        }
    }

    private void ShowSetupPage()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Items.Clear();

        Items.Add(
            new ShellContent
            {
                Title = "Setup",
                Route = "SetupPage",
                Content = _setupPage
            });
    }

    private void OnSetupCompleted(
        object? sender,
        EventArgs e)
    {
        ShowMainPage();
        _startupRouteResolved = true;
    }
    private void ShowMainPage()
    {
        Items.Clear();
        FlyoutBehavior = FlyoutBehavior.Flyout;

        var homeItem =
            new FlyoutItem
            {
                Title = "Home",
                Route = "Home"
            };

        homeItem.Items.Add(
            new ShellContent
            {
                Title = "Home",
                Route = "MainPage",
                Content = _mainPage
            });

        var settingsItem =
            new FlyoutItem
            {
                Title = "Settings",
                Route = "Settings"
            };

        settingsItem.Items.Add(
            new ShellContent
            {
                Title = "Settings",
                Route = "SettingsPage",
                Content = _settingsPage
            });

        Items.Add(homeItem);
        Items.Add(settingsItem);

        CurrentItem = homeItem;
    }
}
