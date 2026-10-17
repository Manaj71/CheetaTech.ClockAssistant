using System.Globalization;
using CheetaTech.ClockAssistant.App.Services.Security;
using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.App;

public partial class SettingsPage : ContentPage
{
    private const string HomeRoute = "//Authenticated/Home/MainPage";
    private const string ContinueEditingChoice = "Continue Editing";
    private const string SaveChoice = "Save";
    private const string DiscardChoice = "Discard";

    private static readonly ScheduleExceptionType[] ExceptionTypes =
    [
        ScheduleExceptionType.StatutoryHoliday,
        ScheduleExceptionType.Vacation,
        ScheduleExceptionType.SickDay,
        ScheduleExceptionType.RegularDayOff,
        ScheduleExceptionType.Other
    ];

    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly ICredentialStore _credentialStore;
    private readonly ISetupPersistenceService _setupPersistenceService;
    private readonly IDeviceAuthenticationService _deviceAuthenticationService;
    private readonly IAttendanceReminderStartupCoordinator _reminderCoordinator;

    private bool _authenticated;
    private bool _authenticationInProgress;
    private bool _loaded;
    private bool _suppressDirtyRefresh;
    private bool _allowNavigation;
    private int _leaveGate;
    private Shell? _subscribedShell;
    private SettingsFormSnapshot? _baseline;
    private IReadOnlyList<ScheduleDayException> _scheduleExceptions =
        Array.Empty<ScheduleDayException>();

    public SettingsPage(
        IClockAssistantConfigurationStore configurationStore,
        ICredentialStore credentialStore,
        ISetupPersistenceService setupPersistenceService,
        IDeviceAuthenticationService deviceAuthenticationService,
        IAttendanceReminderStartupCoordinator reminderCoordinator)
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

        _reminderCoordinator =
            reminderCoordinator
            ?? throw new ArgumentNullException(nameof(reminderCoordinator));

        ProviderTypePicker.ItemsSource = new[] { "UKG" };
        ExecutionModePicker.ItemsSource =
            Enum.GetNames<ClockExecutionMode>();

        ExceptionTypePicker.ItemsSource =
            ExceptionTypes
                .Select(GetExceptionTypeLabel)
                .ToArray();
        ExceptionTypePicker.SelectedIndex = 0;
        ExceptionDatePicker.Date = DateTime.Today;

        WireDirtyTracking();
        RefreshDirtyState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        SubscribeToShellNavigation();
        await UnlockSettingsAsync();
    }

    protected override void OnDisappearing()
    {
        UnsubscribeFromShellNavigation();

        base.OnDisappearing();

        _authenticated = false;
        SettingsScrollView.IsVisible = false;
        LockedPanel.IsVisible = true;

        // Keep the loaded form in memory. Clearing _loaded would reload
        // from storage on the next unlock and drop unsaved edits.
        var hadReplacementPassword =
            !string.IsNullOrEmpty(PasswordEntry.Text);

        PasswordEntry.Text = string.Empty;
        PasswordEntry.IsPassword = true;
        TogglePasswordButton.Text = "Show";

        if (hadReplacementPassword)
        {
            ShowStatus(
                "The new password was cleared when Settings locked. Enter it again before saving.");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (BusyIndicator.IsRunning)
        {
            return true;
        }

        if (_allowNavigation || !ShouldGuardLeave())
        {
            return base.OnBackButtonPressed();
        }

        if (Volatile.Read(ref _leaveGate) != 0)
        {
            return true;
        }

        Dispatcher.Dispatch(
            async () => await RequestLeaveAsync(route: null, useShellBack: true));

        return true;
    }

    private void SubscribeToShellNavigation()
    {
        if (Shell.Current is not Shell shell)
        {
            return;
        }

        if (ReferenceEquals(_subscribedShell, shell))
        {
            return;
        }

        UnsubscribeFromShellNavigation();
        shell.Navigating += OnShellNavigating;
        _subscribedShell = shell;
    }

    private void UnsubscribeFromShellNavigation()
    {
        if (_subscribedShell is null)
        {
            return;
        }

        _subscribedShell.Navigating -= OnShellNavigating;
        _subscribedShell = null;
    }

    private void OnShellNavigating(
        object? sender,
        ShellNavigatingEventArgs e)
    {
        if (_allowNavigation || !IsLeavingSettings(e))
        {
            return;
        }

        if (BusyIndicator.IsRunning)
        {
            if (e.CanCancel)
            {
                e.Cancel();
            }

            return;
        }

        if (!ShouldGuardLeave())
        {
            return;
        }

        var target =
            e.Target?.Location?.OriginalString;

        if (IsSettingsRoute(target))
        {
            return;
        }

        if (!e.CanCancel)
        {
            // Navigation cannot be stopped. Non-secret edits stay on this
            // page instance; the replacement password is cleared on disappear.
            return;
        }

        if (Volatile.Read(ref _leaveGate) != 0)
        {
            e.Cancel();
            return;
        }

        e.Cancel();

        var useShellBack =
            e.Source is ShellNavigationSource.Pop
                or ShellNavigationSource.PopToRoot;

        var route =
            useShellBack
                ? null
                : target;

        Dispatcher.Dispatch(
            async () => await RequestLeaveAsync(route, useShellBack));
    }

    private bool IsLeavingSettings(ShellNavigatingEventArgs e)
    {
        if (ReferenceEquals(Shell.Current?.CurrentPage, this))
        {
            return true;
        }

        var current =
            e.Current?.Location?.OriginalString;

        return IsSettingsRoute(current);
    }

    private static bool IsSettingsRoute(string? route)
    {
        return route?.Contains(
                "SettingsPage",
                StringComparison.Ordinal)
            == true;
    }

    private bool ShouldGuardLeave()
    {
        return _loaded
            && _baseline is not null
            && IsFormDirty()
            && !BusyIndicator.IsRunning;
    }

    private async Task RequestLeaveAsync(
        string? route,
        bool useShellBack)
    {
        if (!TryEnterLeaveGate())
        {
            return;
        }

        try
        {
            if (BusyIndicator.IsRunning)
            {
                return;
            }

            if (!ShouldGuardLeave())
            {
                await NavigateAsync(
                    route,
                    useShellBack,
                    discarded: false,
                    saved: false);

                return;
            }

            var choice =
                await DisplayActionSheetAsync(
                    "Unsaved changes",
                    ContinueEditingChoice,
                    destruction: null,
                    SaveChoice,
                    DiscardChoice);

            if (choice == SaveChoice)
            {
                var followUp = await SaveSettingsCoreAsync();

                if (followUp == SettingsSaveFollowUp.ReadyToLeave)
                {
                    await NavigateAsync(
                        route,
                        useShellBack,
                        discarded: false,
                        saved: true);
                }

                return;
            }

            if (choice == DiscardChoice)
            {
                await DiscardEditsAsync();

                if (!_loaded)
                {
                    return;
                }

                await NavigateAsync(
                    route,
                    useShellBack,
                    discarded: true,
                    saved: false);
            }
        }
        finally
        {
            ExitLeaveGate();
        }
    }

    private async Task NavigateAsync(
        string? route,
        bool useShellBack,
        bool discarded,
        bool saved)
    {
        if (Shell.Current is not Shell shell)
        {
            ShowStatus(NavigationFailureMessage(discarded, saved));
            return;
        }

        _allowNavigation = true;

        try
        {
            if (!useShellBack
                && !string.IsNullOrWhiteSpace(route))
            {
                await shell.GoToAsync(route);
                return;
            }

            var handled = shell.SendBackButtonPressed();

            if (handled)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(route))
            {
                await shell.GoToAsync(route);
                return;
            }

            MoveApplicationToBackground();
        }
        catch
        {
            ShowStatus(NavigationFailureMessage(discarded, saved));
        }
        finally
        {
            _allowNavigation = false;
        }
    }

    private static string NavigationFailureMessage(
        bool discarded,
        bool saved)
    {
        if (saved)
        {
            return "Settings were saved, but the selected page could not be opened.";
        }

        if (discarded)
        {
            return "Your unsaved changes were discarded, but the selected page could not be opened.";
        }

        return "The selected page could not be opened.";
    }

    private static void MoveApplicationToBackground()
    {
#if ANDROID
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?.MoveTaskToBack(true);
#endif
    }

    private bool TryEnterLeaveGate()
    {
        return Interlocked.CompareExchange(ref _leaveGate, 1, 0) == 0;
    }

    private void ExitLeaveGate()
    {
        Interlocked.Exchange(ref _leaveGate, 0);
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
            else
            {
                RefreshDirtyState();
            }

            if (!string.IsNullOrEmpty(StatusLabel.Text))
            {
                await ScrollStatusIntoViewAsync();
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
        _suppressDirtyRefresh = true;
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

                _loaded = false;
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

            HighVisibilityRemindersSwitch.IsToggled =
                configuration.HighVisibilityReminders;

            var executionModeName =
                configuration.ExecutionMode.ToString();

            ExecutionModePicker.SelectedItem =
                executionModeName;

            _scheduleExceptions =
                (configuration.ScheduleExceptions
                    ?? Array.Empty<ScheduleDayException>())
                .OrderBy(item => item.Date)
                .ToArray();

            RefreshExceptionsList();
            ExceptionDatePicker.Date = DateTime.Today;
            ExceptionTypePicker.SelectedIndex = 0;

            _baseline = CaptureCurrentSnapshot();
            _loaded = true;
        }
        catch
        {
            // Do not surface potentially sensitive exception/provider details.
            StatusLabel.Text =
                "Settings could not be loaded.";

            _loaded = false;
        }
        finally
        {
            _suppressDirtyRefresh = false;
            SetBusy(false);
        }
    }

    private async Task DiscardEditsAsync()
    {
        PasswordEntry.Text = string.Empty;
        PasswordEntry.IsPassword = true;
        TogglePasswordButton.Text = "Show";
        StatusLabel.Text = string.Empty;
        _loaded = false;
        await LoadCurrentSettingsAsync();
    }

    private async void OnCancelSettingsClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        await RequestLeaveAsync(HomeRoute, useShellBack: false);
    }

    private void OnAddOrUpdateExceptionClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        if (ExceptionTypePicker.SelectedIndex < 0
            || ExceptionTypePicker.SelectedIndex >= ExceptionTypes.Length)
        {
            StatusLabel.Text = "Select an exception type.";
            return;
        }

        var selectedDate =
            ExceptionDatePicker.Date
            ?? DateTime.Today;

        var date = DateOnly.FromDateTime(selectedDate);
        var type = ExceptionTypes[ExceptionTypePicker.SelectedIndex];

        _scheduleExceptions =
            ScheduleExceptionListEditor.Upsert(
                _scheduleExceptions,
                date,
                type);

        RefreshExceptionsList();
        RefreshDirtyState();
        StatusLabel.Text =
            $"Exception for {date.ToString("D", CultureInfo.CurrentCulture)} ready to save.";
    }

    private void OnEditExceptionClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        if (sender is not Button { BindingContext: ScheduleExceptionRow row })
        {
            return;
        }

        ExceptionDatePicker.Date =
            row.Date.ToDateTime(TimeOnly.MinValue);
        ExceptionTypePicker.SelectedIndex =
            Array.IndexOf(ExceptionTypes, row.Type);
        StatusLabel.Text =
            "Edit the type if needed, then tap Add or update exception.";
    }

    private void OnRemoveExceptionClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        if (sender is not Button { BindingContext: ScheduleExceptionRow row })
        {
            return;
        }

        _scheduleExceptions =
            ScheduleExceptionListEditor.Remove(
                _scheduleExceptions,
                row.Date);

        RefreshExceptionsList();
        RefreshDirtyState();
        StatusLabel.Text =
            $"Removed exception for {row.Date.ToString("D", CultureInfo.CurrentCulture)}. Save Settings to apply.";
    }

    private async void OnSaveSettingsClicked(
        object? sender,
        EventArgs e)
    {
        if (BusyIndicator.IsRunning)
        {
            return;
        }

        await SaveSettingsCoreAsync();
    }

    private async Task<SettingsSaveFollowUp> SaveSettingsCoreAsync()
    {
        if (BusyIndicator.IsRunning)
        {
            return SettingsSaveFollowUp.StayedOnPage;
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
                ShowStatus("Provider is required.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            if (string.IsNullOrWhiteSpace(providerUrl))
            {
                ShowStatus("Provider URL is required.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowStatus("Username is required.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                ShowStatus("Time zone is required.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            if (!int.TryParse(
                    NotificationLeadMinutesEntry.Text,
                    out var notificationLeadMinutes)
                || notificationLeadMinutes < 0)
            {
                ShowStatus(
                    "Notification lead minutes must be zero or greater.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            if (ExecutionModePicker.SelectedItem is not string executionModeText
                || !Enum.TryParse<ClockExecutionMode>(
                    executionModeText,
                    ignoreCase: false,
                    out var executionMode))
            {
                ShowStatus("Execution mode is required.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            var workDays =
                GetSelectedWorkDays();

            if (workDays.Count == 0)
            {
                ShowStatus(
                    "Select at least one work day.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            var currentCredentials =
                await _credentialStore.GetCredentialsAsync();

            if (currentCredentials is null)
            {
                ShowStatus(
                    "Stored credentials are not available.");
                return SettingsSaveFollowUp.StayedOnPage;
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
                    ScheduleExceptions = _scheduleExceptions,
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
                    HighVisibilityReminders =
                        HighVisibilityRemindersSwitch.IsToggled,
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
                ShowStatus(
                    "Settings could not be saved. Your previous settings are unchanged.");

                return SettingsSaveFollowUp.StayedOnPage;
            }

            if (result.ProviderValidationPerformed)
            {
                ShowStatus(
                    "Settings were saved, but an unexpected issue was detected.");

                return SettingsSaveFollowUp.StayedOnPage;
            }

            PasswordEntry.Text = string.Empty;
            PasswordEntry.IsPassword = true;
            TogglePasswordButton.Text = "Show";
            _baseline = CaptureCurrentSnapshot();

            AttendanceReminderRescheduleResult rescheduleResult;

            try
            {
                rescheduleResult =
                    await _reminderCoordinator
                        .RescheduleAfterConfigurationChangeAsync(
                            DateTimeOffset.UtcNow);
            }
            catch
            {
                ShowStatus(
                    "Settings saved, but reminders could not be updated. Your previous reminder may still be active.");
                return SettingsSaveFollowUp.StayedOnPage;
            }

            ShowStatus(
                AttendanceReminderRescheduleUserMessage.ForSuccessfulSave(
                    rescheduleResult));

            return rescheduleResult
                    is AttendanceReminderRescheduleResult.Scheduled
                    or AttendanceReminderRescheduleResult.Cancelled
                ? SettingsSaveFollowUp.ReadyToLeave
                : SettingsSaveFollowUp.StayedOnPage;
        }
        catch
        {
            // Keep exception/provider details out of the UI.
            ShowStatus("Settings could not be saved.");
            return SettingsSaveFollowUp.StayedOnPage;
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

    private void WireDirtyTracking()
    {
        ProviderTypePicker.SelectedIndexChanged +=
            OnEditableValueChanged;
        ProviderUrlEntry.TextChanged +=
            OnEditableTextChanged;
        UsernameEntry.TextChanged +=
            OnEditableTextChanged;
        PasswordEntry.TextChanged +=
            OnEditableTextChanged;
        TimeZoneEntry.TextChanged +=
            OnEditableTextChanged;
        NotificationLeadMinutesEntry.TextChanged +=
            OnEditableTextChanged;
        ExecutionModePicker.SelectedIndexChanged +=
            OnEditableValueChanged;
        HighVisibilityRemindersSwitch.Toggled +=
            OnReminderSwitchToggled;

        MondayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;
        TuesdayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;
        WednesdayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;
        ThursdayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;
        FridayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;
        SaturdayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;
        SundayCheckBox.CheckedChanged += OnWorkDayCheckedChanged;

        ClockInTimePicker.PropertyChanged += OnTimePickerPropertyChanged;
        ClockOutTimePicker.PropertyChanged += OnTimePickerPropertyChanged;
    }

    private void OnEditableValueChanged(object? sender, EventArgs e)
    {
        RefreshDirtyState();
    }

    private void OnEditableTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        RefreshDirtyState();
    }

    private void OnWorkDayCheckedChanged(
        object? sender,
        CheckedChangedEventArgs e)
    {
        RefreshDirtyState();
    }

    private void OnReminderSwitchToggled(
        object? sender,
        ToggledEventArgs e)
    {
        RefreshDirtyState();
    }

    private void OnTimePickerPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == TimePicker.TimeProperty.PropertyName)
        {
            RefreshDirtyState();
        }
    }

    private void RefreshDirtyState()
    {
        if (_suppressDirtyRefresh)
        {
            SaveSettingsButton.IsEnabled = false;
            return;
        }

        var dirty = IsFormDirty();
        UnsavedChangesLabel.IsVisible = dirty;
        SaveSettingsButton.IsEnabled =
            dirty && !BusyIndicator.IsRunning;
    }

    private bool IsFormDirty()
    {
        if (_baseline is null)
        {
            return false;
        }

        return !SettingsFormSnapshot.AreEquivalent(
            _baseline,
            CaptureCurrentSnapshot());
    }

    private SettingsFormSnapshot CaptureCurrentSnapshot()
    {
        return SettingsFormSnapshot.Capture(
            ProviderTypePicker.SelectedItem as string,
            ProviderUrlEntry.Text,
            UsernameEntry.Text,
            !string.IsNullOrEmpty(PasswordEntry.Text),
            GetSelectedWorkDays(),
            ClockInTimePicker.Time,
            ClockOutTimePicker.Time,
            TimeZoneEntry.Text,
            NotificationLeadMinutesEntry.Text,
            HighVisibilityRemindersSwitch.IsToggled,
            ExecutionModePicker.SelectedItem as string,
            _scheduleExceptions);
    }

    private void ShowStatus(string message)
    {
        StatusLabel.Text = message;

        if (!string.IsNullOrEmpty(message))
        {
            _ = ScrollStatusIntoViewAsync();
        }
    }

    private async Task ScrollStatusIntoViewAsync()
    {
        try
        {
            await SettingsFormScroll.ScrollToAsync(
                StatusLabel,
                ScrollToPosition.MakeVisible,
                false);
        }
        catch
        {
            // The form may be detaching during navigation.
        }
    }

    private void RefreshExceptionsList()
    {
        BindableLayout.SetItemsSource(
            ExceptionsLayout,
            _scheduleExceptions
                .Select(item =>
                    new ScheduleExceptionRow(
                        item.Date,
                        item.Type,
                        item.Date.ToString(
                            "D",
                            CultureInfo.CurrentCulture),
                        GetExceptionTypeLabel(item.Type)))
                .ToArray());
    }

    private static string GetExceptionTypeLabel(
        ScheduleExceptionType type)
    {
        return type switch
        {
            ScheduleExceptionType.StatutoryHoliday => "Statutory Holiday",
            ScheduleExceptionType.Vacation => "Vacation",
            ScheduleExceptionType.SickDay => "Sick Day",
            ScheduleExceptionType.RegularDayOff => "Regular Day Off",
            ScheduleExceptionType.Other => "Other",
            _ => type.ToString()
        };
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
        AddOrUpdateExceptionButton.IsEnabled = !isBusy;
        ExceptionDatePicker.IsEnabled = !isBusy;
        ExceptionTypePicker.IsEnabled = !isBusy;
        RefreshDirtyState();

        if (isBusy)
        {
            _ = ScrollBusyIndicatorIntoViewAsync();
        }
    }

    private async Task ScrollBusyIndicatorIntoViewAsync()
    {
        try
        {
            await SettingsFormScroll.ScrollToAsync(
                BusyIndicator,
                ScrollToPosition.MakeVisible,
                false);
        }
        catch
        {
            // The form may be detaching during navigation.
        }
    }

    private sealed record ScheduleExceptionRow(
        DateOnly Date,
        ScheduleExceptionType Type,
        string DateLabel,
        string TypeLabel);

    private enum SettingsSaveFollowUp
    {
        StayedOnPage = 0,
        ReadyToLeave = 1
    }
}
