using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.App;

public partial class AppShell : Shell
{
    private readonly ISetupReadinessService _setupReadinessService;
    private readonly SetupPage _setupPage;
    private readonly MainPage _mainPage;
    private bool _startupRouteResolved;

    public AppShell(
        ISetupReadinessService setupReadinessService,
        SetupPage setupPage,
        MainPage mainPage)
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

            if (readiness.SetupRequired)
            {
                ShowSetupPage();
            }
            else
            {
                ShowMainPage();
            }

            _startupRouteResolved = true;
        }
        catch
        {
            ShowSetupPage();
            _startupRouteResolved = true;
        }
    }

    private void ShowSetupPage()
    {
        Items.Clear();

        Items.Add(
            new ShellContent
            {
                Title = "Setup",
                Route = "SetupPage",
                Content = _setupPage
            });
    }

    private void ShowMainPage()
    {
        Items.Clear();

        Items.Add(
            new ShellContent
            {
                Title = "Home",
                Route = "MainPage",
                Content = _mainPage
            });
    }
}

