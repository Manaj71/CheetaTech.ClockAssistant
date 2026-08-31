using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.App;

public partial class SetupPage : ContentPage
{
    private readonly ISetupLifecycleService _setupLifecycleService;

    public SetupPage(
        ISetupLifecycleService setupLifecycleService)
    {
        InitializeComponent();

        _setupLifecycleService =
            setupLifecycleService
            ?? throw new ArgumentNullException(
                nameof(setupLifecycleService));

        ProviderTypePicker.SelectedIndex = 0;
        ExecutionModePicker.SelectedIndex = 0;
    }

    private async void OnDryRunSetupCheckClicked(
        object? sender,
        EventArgs e)
    {
        DryRunButton.IsEnabled = false;
        BusyIndicator.IsVisible = true;
        BusyIndicator.IsRunning = true;

        try
        {
            var configuration =
                BuildConfigurationCandidate();

            var candidate =
                new SetupCandidate(
                    Configuration: configuration,
                    Username:
                        UsernameEntry.Text?.Trim()
                        ?? string.Empty,
                    Password:
                        PasswordEntry.Text
                        ?? string.Empty);

            var result =
                await _setupLifecycleService
                    .EvaluateCandidateAsync(candidate);

            StatusLabel.Text =
                BuildStatusMessage(result);
        }
        catch (Exception ex)
        {
            StatusLabel.Text =
                $"DryRun Setup check failed locally: {ex.Message}";
        }
        finally
        {
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
            DryRunButton.IsEnabled = true;
        }
    }

    private ClockAssistantConfiguration BuildConfigurationCandidate()
    {
        var workDays =
            new List<DayOfWeek>();

        if (MondayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Monday);
        }

        if (TuesdayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Tuesday);
        }

        if (WednesdayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Wednesday);
        }

        if (ThursdayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Thursday);
        }

        if (FridayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Friday);
        }

        if (SaturdayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Saturday);
        }

        if (SundayCheckBox.IsChecked)
        {
            workDays.Add(DayOfWeek.Sunday);
        }

        var leadMinutes = ParseLeadMinutes();

        var executionMode =
            ParseExecutionMode();

        return new ClockAssistantConfiguration
        {
            ProviderType =
                ProviderTypePicker.SelectedItem?.ToString()?.Trim()
                ?? string.Empty,

            ProviderUrl =
                ProviderUrlEntry.Text?.Trim()
                ?? string.Empty,

            WorkDays = workDays,

            ClockInTime =
                ClockInTimePicker.Time.HasValue
                    ? TimeOnly.FromTimeSpan(
                        ClockInTimePicker.Time.Value)
                    : null,

            ClockOutTime =
                ClockOutTimePicker.Time.HasValue
                    ? TimeOnly.FromTimeSpan(
                        ClockOutTimePicker.Time.Value)
                    : null,

            TimeZoneId =
                TimeZoneEntry.Text?.Trim()
                ?? string.Empty,

            NotificationLeadTime =
                TimeSpan.FromMinutes(leadMinutes),

            ExecutionMode =
                executionMode
        };
    }

    private int ParseLeadMinutes()
    {
        if (!int.TryParse(
                NotificationLeadMinutesEntry.Text?.Trim(),
                out var minutes))
        {
            return -1;
        }

        return minutes;
    }

    private ClockExecutionMode ParseExecutionMode()
    {
        var selected =
            ExecutionModePicker.SelectedItem
                ?.ToString();

        if (Enum.TryParse<ClockExecutionMode>(
                selected,
                ignoreCase: false,
                out var mode))
        {
            return mode;
        }

        return ClockExecutionMode.BasicConfirmation;
    }

    private static string BuildStatusMessage(
        SetupLifecycleResult result)
    {
        if (result.Success)
        {
            return result.Message;
        }

        if (result.Issues.Count == 0)
        {
            return result.Message;
        }

        return
            $"{result.Message} " +
            $"Review: {string.Join(", ", result.Issues)}.";
    }
}


