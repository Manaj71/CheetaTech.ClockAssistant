using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;
using CheetaTech.ClockAssistant.App.Services.Security;

namespace CheetaTech.ClockAssistant.App;

public partial class SettingsPage : ContentPage
{
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly ICredentialStore _credentialStore;
    private readonly ISetupPersistenceService _setupPersistenceService;
    private readonly IDeviceAuthenticationService _deviceAuthenticationService;

    private bool _authenticated;
    private bool _authenticationInProgress;

    private bool _loaded;

    public SettingsPage(
        IClockAssistantConfigurationStore configurationStore,
        ICredentialStore credentialStore,
        ISetupPersistenceService setupPersistenceService,
        IDeviceAuthenticationService deviceAuthenticationService)
    {
        InitializeComponent();

        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));

        _credentialStore =
            credentialStore
            ?? throw new ArgumentNullException(nameof(credentialStore));

        _setupPersistenceService =
            setupPersistenceService
            ?? throw new ArgumentNullException(nameof(setupPersistenceService));

        _deviceAuthenticationService =
            deviceAuthenticationService
            ?? throw new ArgumentNullException(
                nameof(deviceAuthenticationService));

        ProviderTypePicker.ItemsSource = new[] { "UKG" };
        ExecutionModePicker.ItemsSource =
            Enum.GetNames<ClockExecutionMode>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await UnlockSettingsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _authenticated = false;
        _loaded = false;
        SettingsScrollView.IsVisible = false;
        LockedPanel.IsVisible = true;

        PasswordEntry.Text = string.Empty;
        PasswordEntry.IsPassword = true;
        TogglePasswordButton.Text = "Show";
    }

    private async void OnUnlockSettingsClicked(
        object? sender,
        EventArgs e)
    {
        await UnlockSettingsAsync();
    }

    private async Task UnlockSettingsAsync()
    {
        if (_authenticated
            || _authenticationInProgress)
        {
            return;
        }

        _authenticationInProgress = true;
        LockStatusLabel.Text =
            "Verify your device identity to view or change account settings.";

        try
        {
            var authenticationStatus =
                await _deviceAuthenticationService
                    .AuthenticateAsync();

            if (authenticationStatus
                == DeviceAuthenticationStatus.NotConfigured)
            {
                LockStatusLabel.Text =
                    "Set up a screen lock (PIN, password, pattern, or biometrics) in your phone settings, then try again.";

                return;
            }

            if (authenticationStatus
                == DeviceAuthenticationStatus.Unavailable)
            {
                LockStatusLabel.Text =
                    "Device verification is not available on this device.";

                return;
            }

            if (authenticationStatus
                != DeviceAuthenticationStatus.Succeeded)
            {
                LockStatusLabel.Text =
                    "Device verification was canceled or unsuccessful. Try again.";

                return;
            }

            _authenticated = true;
            LockedPanel.IsVisible = false;
            SettingsScrollView.IsVisible = true;

            if (!_loaded)
            {
                await LoadCurrentSettingsAsync();
            }
        }
        catch
        {
            LockStatusLabel.Text =
                "Settings could not be unlocked.";
        }
        finally
        {
            _authenticationInProgress = false;
        }
    }

    private async Task LoadCurrentSettingsAsync()
    {
        SetBusy(true);

        try
        {
            StatusLabel.Text = string.Empty;

            var configuration =
                await _configurationStore.GetAsync();

            var credentials =
                await _credentialStore.GetCredentialsAsync();

            if (configuration is null)
            {
                StatusLabel.Text =
                    "Saved configuration could not be loaded.";

                return;
            }

            var providerType =
                string.IsNullOrWhiteSpace(configuration.ProviderType)
                    ? "UKG"
                    : configuration.ProviderType;

            if (!ProviderTypePicker.Items.Contains(providerType))
            {
                ProviderTypePicker.Items.Add(providerType);
            }

            ProviderTypePicker.SelectedItem = providerType;
            ProviderUrlEntry.Text = configuration.ProviderUrl;

            // Never expose the stored password in the UI.
            UsernameEntry.Text = credentials?.Username ?? string.Empty;
            PasswordEntry.Text = string.Empty;
            PasswordEntry.IsPassword = true;
            TogglePasswordButton.Text = "Show";

            SetWorkDays(configuration.WorkDays);

            if (configuration.ClockInTime is TimeOnly clockInTime)
            {
                ClockInTimePicker.Time =
                    clockInTime.ToTimeSpan();
            }

            if (configuration.ClockOutTime is TimeOnly clockOutTime)
            {
                ClockOutTimePicker.Time =
                    clockOutTime.ToTimeSpan();
            }

            TimeZoneEntry.Text = configuration.TimeZoneId;

            NotificationLeadMinutesEntry.Text =
                ((int)configuration.NotificationLeadTime.TotalMinutes)
                .ToString();

            var executionModeName =
                configuration.ExecutionMode.ToString();

            ExecutionModePicker.SelectedItem =
                executionModeName;

            _loaded = true;
        }
        catch
        {
            // Do not surface potentially sensitive exception/provider details.
            StatusLabel.Text =
                "Settings could not be loaded.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnCancelSettingsClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        // Cancel never persists. Reload the last trusted values so every
        // unsaved field edit is discarded before leaving this page.
        PasswordEntry.Text = string.Empty;
        PasswordEntry.IsPassword = true;
        TogglePasswordButton.Text = "Show";
        StatusLabel.Text = string.Empty;

        _loaded = false;
        await LoadCurrentSettingsAsync();

        if (Shell.Current is null)
        {
            StatusLabel.Text =
                "Your unsaved changes were discarded, but Home could not be opened.";

            return;
        }

        try
        {
            await Shell.Current.GoToAsync("//Home/MainPage");
        }
        catch
        {
            StatusLabel.Text =
                "Your unsaved changes were discarded, but Home could not be opened.";
        }
    }
    private async void OnSaveSettingsClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        SetBusy(true);

        try
        {
            StatusLabel.Text = string.Empty;

            var providerType =
                ProviderTypePicker.SelectedItem as string
                ?? string.Empty;

            var providerUrl =
                ProviderUrlEntry.Text?.Trim()
                ?? string.Empty;

            var username =
                UsernameEntry.Text?.Trim()
                ?? string.Empty;

            var timeZoneId =
                TimeZoneEntry.Text?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(providerType))
            {
                StatusLabel.Text = "Provider is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(providerUrl))
            {
                StatusLabel.Text = "Provider URL is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                StatusLabel.Text = "Username is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                StatusLabel.Text = "Time zone is required.";
                return;
            }

            if (!int.TryParse(
                    NotificationLeadMinutesEntry.Text,
                    out var notificationLeadMinutes)
                || notificationLeadMinutes < 0)
            {
                StatusLabel.Text =
                    "Notification lead minutes must be zero or greater.";
                return;
            }

            if (ExecutionModePicker.SelectedItem is not string executionModeText
                || !Enum.TryParse<ClockExecutionMode>(
                    executionModeText,
                    ignoreCase: false,
                    out var executionMode))
            {
                StatusLabel.Text = "Execution mode is required.";
                return;
            }

            var workDays =
                GetSelectedWorkDays();

            if (workDays.Count == 0)
            {
                StatusLabel.Text =
                    "Select at least one work day.";
                return;
            }

            var currentCredentials =
                await _credentialStore.GetCredentialsAsync();

            if (currentCredentials is null)
            {
                StatusLabel.Text =
                    "Stored credentials are not available.";
                return;
            }

            var requestedPassword =
                PasswordEntry.Text
                ?? string.Empty;

            var passwordToPersist =
                string.IsNullOrEmpty(requestedPassword)
                    ? currentCredentials.Password
                    : requestedPassword;

            var configuration =
                new ClockAssistantConfiguration
                {
                    ProviderType = providerType,
                    ProviderUrl = providerUrl,
                    WorkDays = workDays,
                    ClockInTime =
                        ClockInTimePicker.Time is TimeSpan clockInSpan
                            ? TimeOnly.FromTimeSpan(clockInSpan)
                            : null,
                    ClockOutTime =
                        ClockOutTimePicker.Time is TimeSpan clockOutSpan
                            ? TimeOnly.FromTimeSpan(clockOutSpan)
                            : null,
                    TimeZoneId = timeZoneId,
                    NotificationLeadTime =
                        TimeSpan.FromMinutes(notificationLeadMinutes),
                    ExecutionMode = executionMode
                };

            var candidate =
                new SetupCandidate(
                    configuration,
                    username,
                    passwordToPersist);

            var result =
                await _setupPersistenceService
                    .PrepareTrustedPersistenceAsync(candidate);

            if (!result.Success
                || !result.ConfigurationSaved
                || !result.CredentialsSaved)
            {
                StatusLabel.Text =
                    "Settings could not be saved. Your previous settings are unchanged.";

                return;
            }

            if (result.ProviderValidationPerformed)
            {
                StatusLabel.Text =
                    "Settings were saved, but an unexpected issue was detected.";

                return;
            }

            PasswordEntry.Text = string.Empty;
            PasswordEntry.IsPassword = true;
            TogglePasswordButton.Text = "Show";

            StatusLabel.Text =
                "Settings saved successfully.";
        }
        catch
        {
            // Keep exception/provider details out of the UI.
            StatusLabel.Text =
                "Settings could not be saved.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnTogglePasswordClicked(
        object? sender,
        EventArgs e)
    {
        PasswordEntry.IsPassword =
            !PasswordEntry.IsPassword;

        TogglePasswordButton.Text =
            PasswordEntry.IsPassword
                ? "Show"
                : "Hide";
    }

    private void SetWorkDays(
        IReadOnlyCollection<DayOfWeek> workDays)
    {
        MondayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Monday);

        TuesdayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Tuesday);

        WednesdayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Wednesday);

        ThursdayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Thursday);

        FridayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Friday);

        SaturdayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Saturday);

        SundayCheckBox.IsChecked =
            workDays.Contains(DayOfWeek.Sunday);
    }

    private IReadOnlyCollection<DayOfWeek> GetSelectedWorkDays()
    {
        var days =
            new List<DayOfWeek>();

        if (MondayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Monday);
        }

        if (TuesdayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Tuesday);
        }

        if (WednesdayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Wednesday);
        }

        if (ThursdayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Thursday);
        }

        if (FridayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Friday);
        }

        if (SaturdayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Saturday);
        }

        if (SundayCheckBox.IsChecked)
        {
            days.Add(DayOfWeek.Sunday);
        }

        return days;
    }

    private void SetBusy(
        bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        CancelSettingsButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;
    }
}