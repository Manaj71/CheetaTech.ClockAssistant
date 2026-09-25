using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.App;

public partial class AppShell : Shell
{
    private readonly ISetupReadinessService _setupReadinessService;
    private readonly SetupPage _setupPage;
    private readonly MainPage _mainPage;
    private readonly HistoryPage _historyPage;
    private readonly SettingsPage _settingsPage;
    private bool _startupRouteResolved;

    public AppShell(
        ISetupReadinessService setupReadinessService,
        SetupPage setupPage,
        MainPage mainPage,
        HistoryPage historyPage,
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

        _historyPage =
            historyPage
            ?? throw new ArgumentNullException(
                nameof(historyPage));

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

        // One FlyoutItem with AsMultipleItems keeps every destination as a
        // permanent peer in the hamburger/flyout. Separate FlyoutItems were
        // leaving Home absent after navigating to History or Settings.
        var destinations =
            new FlyoutItem
            {
                Route = "Authenticated",
                FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems
            };

        var homeDestination =
            CreateFlyoutDestination(
                title: "Home",
                sectionRoute: "Home",
                contentRoute: "MainPage",
                content: _mainPage);

        var historyDestination =
            CreateFlyoutDestination(
                title: "History",
                sectionRoute: "History",
                contentRoute: "HistoryPage",
                content: _historyPage);

        var settingsDestination =
            CreateFlyoutDestination(
                title: "Settings",
                sectionRoute: "Settings",
                contentRoute: "SettingsPage",
                content: _settingsPage);

        destinations.Items.Add(homeDestination);
        destinations.Items.Add(historyDestination);
        destinations.Items.Add(settingsDestination);

        Items.Add(destinations);

        destinations.CurrentItem = homeDestination;
        CurrentItem = destinations;
    }

    private static Tab CreateFlyoutDestination(
        string title,
        string sectionRoute,
        string contentRoute,
        Page content)
    {
        var section =
            new Tab
            {
                Title = title,
                Route = sectionRoute
            };

        section.Items.Add(
            new ShellContent
            {
                Title = title,
                Route = contentRoute,
                Content = content
            });

        return section;
    }
}
