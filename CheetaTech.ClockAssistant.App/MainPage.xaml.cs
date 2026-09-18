using System.Globalization;
using CheetaTech.ClockAssistant.App.Services.Diagnostics;
using CheetaTech.ClockAssistant.App.Services.Notifications;
using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.History;

namespace CheetaTech.ClockAssistant.App;

public partial class MainPage : ContentPage
{
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly IAttendanceStateStore _attendanceStateStore;
    private readonly AttendanceStateEvaluator _stateEvaluator;
    private readonly IAttendanceActionExecutionService _executionService;
    private readonly IAttendanceActionAuditLog _auditLog;
    private readonly IAttendanceReminderStartupCoordinator _reminderCoordinator;
    private readonly AttendanceHistoryRecorder _historyRecorder;

    private readonly Label _clockInStatusValue;
    private readonly Label _clockInTimeValue;
    private readonly Label _clockOutStatusValue;
    private readonly Label _clockOutTimeValue;
    private readonly Label _summaryLabel;
    private readonly Label _resultLabel;
    private readonly Button _actionButton;

    private AttendanceActionType? _availableActionType;
    private bool _isBusy;

    public MainPage(
        IClockAssistantConfigurationStore configurationStore,
        IAttendanceStateStore attendanceStateStore,
        AttendanceStateEvaluator stateEvaluator,
        IAttendanceActionExecutionService executionService,
        IAttendanceActionAuditLog auditLog,
        IAttendanceReminderStartupCoordinator reminderCoordinator,
        AttendanceHistoryRecorder historyRecorder)
    {
        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));

        _attendanceStateStore =
            attendanceStateStore
            ?? throw new ArgumentNullException(nameof(attendanceStateStore));

        _stateEvaluator =
            stateEvaluator
            ?? throw new ArgumentNullException(nameof(stateEvaluator));

        _executionService =
            executionService
            ?? throw new ArgumentNullException(nameof(executionService));

        _auditLog =
            auditLog
            ?? throw new ArgumentNullException(nameof(auditLog));

        _reminderCoordinator =
            reminderCoordinator
            ?? throw new ArgumentNullException(nameof(reminderCoordinator));

        _historyRecorder =
            historyRecorder
            ?? throw new ArgumentNullException(nameof(historyRecorder));

        InitializeComponent();

        _clockInStatusValue =
            (this.FindByName("ClockInStatusValue") as Label)
            ?? throw new InvalidOperationException("ClockInStatusValue is missing.");
        _clockInTimeValue =
            (this.FindByName("ClockInTimeValue") as Label)
            ?? throw new InvalidOperationException("ClockInTimeValue is missing.");
        _clockOutStatusValue =
            (this.FindByName("ClockOutStatusValue") as Label)
            ?? throw new InvalidOperationException("ClockOutStatusValue is missing.");
        _clockOutTimeValue =
            (this.FindByName("ClockOutTimeValue") as Label)
            ?? throw new InvalidOperationException("ClockOutTimeValue is missing.");
        _summaryLabel =
            (this.FindByName("SummaryLabel") as Label)
            ?? throw new InvalidOperationException("SummaryLabel is missing.");
        _resultLabel =
            (this.FindByName("ResultLabel") as Label)
            ?? throw new InvalidOperationException("ResultLabel is missing.");
        _actionButton =
            (this.FindByName("ActionButton") as Button)
            ?? throw new InvalidOperationException("ActionButton is missing.");

        _actionButton.Clicked += OnActionButtonClicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await RefreshStateAsync();
    }

    private async void OnActionButtonClicked(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_availableActionType is null || _isBusy)
        {
            return;
        }

        await ExecuteManualActionAsync(_availableActionType.Value);
    }

    private async Task ExecuteManualActionAsync(
        AttendanceActionType actionType)
    {
        SetBusy(true);

        try
        {
            var actionUtcNow =
                DateTimeOffset.UtcNow;

            await TryAppendReceivedAuditAsync(
                actionType,
                actionUtcNow);

            AttendanceActionExecutionResult result;

            try
            {
                result =
                    await _executionService.ExecuteAsync(
                        actionType,
                        actionUtcNow,
                        AttendanceActionExecutionMode.Manual);
            }
            catch (Exception exception)
            {
                await TryAppendExceptionAuditAsync(
                    actionType,
                    exception);

                _resultLabel.Text =
                    GetUnexpectedFailureText(actionType);
                _resultLabel.IsVisible = true;

                await RefreshStateAsync();
                return;
            }

            var resultUtcNow =
                DateTimeOffset.UtcNow;

            await TryAppendResultAuditAsync(
                result,
                resultUtcNow);
            await TryScheduleReminderAsync(
                actionType,
                resultUtcNow);
            await TryRecordHistoryAsync(
                result,
                actionUtcNow);

            var (title, message) =
                GetActionResultText(
                    actionType,
                    result);

            _resultLabel.Text =
                string.Join(
                    Environment.NewLine,
                    title,
                    message);
            _resultLabel.IsVisible = true;

            await RefreshStateAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshStateAsync()
    {
        try
        {
            var snapshot =
                await BuildSnapshotAsync();

            ApplySnapshot(snapshot);
        }
        catch
        {
            ApplyUnavailableSnapshot(
                "Attendance status is temporarily unavailable.",
                "Your attendance functions are not affected.");
        }
    }

    private async Task<AttendanceHomeSnapshot> BuildSnapshotAsync()
    {
        var configuration =
            await _configurationStore.GetAsync();

        if (configuration is null)
        {
            return AttendanceHomeSnapshot.Unavailable(
                "Attendance configuration is unavailable.");
        }

        var utcNow =
            DateTimeOffset.UtcNow;

        var initialEvaluation =
            _stateEvaluator.Evaluate(
                configuration,
                utcNow);

        var currentRecord =
            await _attendanceStateStore.LoadAsync(
                initialEvaluation.AttendanceDate);

        var evaluation =
            _stateEvaluator.Evaluate(
                configuration,
                utcNow,
                currentRecord);

        var availableActionType =
            DetermineManualAction(evaluation);

        var clockInTimeLabel =
            configuration.ClockInTime.HasValue
                ? $"Configured {configuration.ClockInTime.Value.ToString("t", CultureInfo.CurrentCulture)}"
                : "No configured time";

        var clockOutTimeLabel =
            configuration.ClockOutTime.HasValue
                ? $"Configured {configuration.ClockOutTime.Value.ToString("t", CultureInfo.CurrentCulture)}"
                : "No configured time";

        var clockInStatus =
            GetClockInStatusLabel(
                evaluation.ClockInState);

        var clockOutStatus =
            GetClockOutStatusLabel(
                evaluation,
                clockOutTimeLabel,
                availableActionType);

        var summary =
            availableActionType switch
            {
                AttendanceActionType.ClockIn =>
                    "Manual Clock In is available.",

                AttendanceActionType.ClockOut =>
                    "Manual Clock Out is available.",

                _ =>
                    "No attendance action is currently required."
            };

        var actionLabel =
            availableActionType switch
            {
                AttendanceActionType.ClockIn => "Clock In",
                AttendanceActionType.ClockOut => "Clock Out",
                _ => null
            };

        return new AttendanceHomeSnapshot(
            clockInStatus,
            clockInTimeLabel,
            clockOutStatus,
            clockOutTimeLabel,
            summary,
            actionLabel,
            availableActionType);
    }

    private void ApplySnapshot(AttendanceHomeSnapshot snapshot)
    {
        _clockInStatusValue.Text = snapshot.ClockInStatus;
        _clockInTimeValue.Text = snapshot.ClockInTime;
        _clockOutStatusValue.Text = snapshot.ClockOutStatus;
        _clockOutTimeValue.Text = snapshot.ClockOutTime;
        _summaryLabel.Text = snapshot.Summary;

        _availableActionType = snapshot.AvailableActionType;
        _actionButton.Text = snapshot.ActionLabel ?? string.Empty;
        _actionButton.IsVisible = snapshot.ActionLabel is not null;
        _actionButton.IsEnabled = !_isBusy && snapshot.ActionLabel is not null;
    }

    private void ApplyUnavailableSnapshot(
        string summary,
        string message)
    {
        _clockInStatusValue.Text = "Unavailable";
        _clockInTimeValue.Text = string.Empty;
        _clockOutStatusValue.Text = "Unavailable";
        _clockOutTimeValue.Text = string.Empty;
        _summaryLabel.Text = summary;
        _availableActionType = null;
        _actionButton.IsVisible = false;
        _actionButton.IsEnabled = false;
        _resultLabel.Text = message;
        _resultLabel.IsVisible = true;
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        _actionButton.IsEnabled = !isBusy && _actionButton.IsVisible;
    }

    private static AttendanceActionType? DetermineManualAction(
        AttendanceStateEvaluation evaluation)
    {
        if (evaluation.DayState == AttendanceDayState.NotScheduled ||
            evaluation.DayState == AttendanceDayState.Error)
        {
            return null;
        }

        if (evaluation.ClockInState is AttendanceActionState.NotDue or AttendanceActionState.Due)
        {
            return AttendanceActionType.ClockIn;
        }

        if (evaluation.ClockInState == AttendanceActionState.Succeeded &&
            evaluation.ClockOutState is AttendanceActionState.NotDue or AttendanceActionState.Due)
        {
            return AttendanceActionType.ClockOut;
        }

        return null;
    }

    private static string GetClockInStatusLabel(
        AttendanceActionState state)
    {
        return state switch
        {
            AttendanceActionState.Succeeded => "Confirmed",
            AttendanceActionState.InProgress => "In progress",
            AttendanceActionState.Failed => "Not confirmed",
            AttendanceActionState.Unknown => "Status uncertain",
            AttendanceActionState.Skipped => "Not completed",
            _ => "Not completed"
        };
    }

    private static string GetClockOutStatusLabel(
        AttendanceStateEvaluation evaluation,
        string clockOutTimeLabel,
        AttendanceActionType? availableActionType)
    {
        return evaluation.ClockOutState switch
        {
            AttendanceActionState.Succeeded => "Confirmed",
            AttendanceActionState.InProgress => "In progress",
            AttendanceActionState.Failed => "Not confirmed",
            AttendanceActionState.Unknown => "Status uncertain",
            AttendanceActionState.Skipped => "Not completed",
            _ when evaluation.ClockInState == AttendanceActionState.Succeeded &&
                availableActionType == AttendanceActionType.ClockOut =>
                $"Scheduled {clockOutTimeLabel.Replace("Configured ", string.Empty, StringComparison.Ordinal)}",
            _ => "Not completed"
        };
    }

    private async Task TryAppendReceivedAuditAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow)
    {
        try
        {
            await _auditLog.AppendReceivedAsync(
                actionType,
                utcNow);
        }
        catch
        {
        }
    }

    private async Task TryAppendResultAuditAsync(
        AttendanceActionExecutionResult result,
        DateTimeOffset utcNow)
    {
        try
        {
            await _auditLog.AppendResultAsync(
                result,
                utcNow);
        }
        catch
        {
        }
    }

    private async Task TryAppendExceptionAuditAsync(
        AttendanceActionType actionType,
        Exception exception)
    {
        try
        {
            await _auditLog.AppendExceptionAsync(
                actionType,
                DateTimeOffset.UtcNow,
                "MainPage",
                exception.GetType().Name);
        }
        catch
        {
        }
    }

    private async Task TryScheduleReminderAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow)
    {
        try
        {
            await _reminderCoordinator.ScheduleNextAfterActionAsync(
                actionType,
                utcNow);
        }
        catch
        {
        }
    }

    private async Task TryRecordHistoryAsync(
        AttendanceActionExecutionResult result,
        DateTimeOffset utcNow)
    {
        try
        {
            await _historyRecorder.TryRecordAsync(
                result,
                utcNow);
        }
        catch
        {
        }
    }

    private static (string Title, string Message) GetActionResultText(
        AttendanceActionType actionType,
        AttendanceActionExecutionResult result)
    {
        var actionName =
            actionType == AttendanceActionType.ClockIn
                ? "Clock In"
                : "Clock Out";

        return result.Status switch
        {
            AttendanceActionExecutionStatus.Succeeded =>
                (
                    $"{actionName} successful",
                    $"Provider confirmed {actionName}."
                ),

            AttendanceActionExecutionStatus.ExecutionDisabled =>
                (
                    $"{actionName} not authorized",
                    "Controlled live execution is not enabled for this attendance date."
                ),

            AttendanceActionExecutionStatus.NotEligible =>
                (
                    $"{actionName} no longer due",
                    "Attendance state changed before the action was processed."
                ),

            AttendanceActionExecutionStatus.ProviderRejected =>
                (
                    $"{actionName} failed",
                    "The provider did not confirm the action."
                ),

            AttendanceActionExecutionStatus.ProviderUnknown =>
                (
                    $"{actionName} status uncertain",
                    "Do not retry until the provider status is checked."
                ),

            AttendanceActionExecutionStatus.PersistenceFailed
                when result.ProviderConfirmed =>
                (
                    $"{actionName} needs attention",
                    "The provider may have confirmed the action, but local state was not saved. Do not retry."
                ),

            AttendanceActionExecutionStatus.CredentialReadFailed =>
                (
                    $"{actionName} unavailable",
                    "Stored credentials could not be read. Open Clock Assistant and update credentials."
                ),

            AttendanceActionExecutionStatus.MissingCredentials =>
                (
                    $"{actionName} unavailable",
                    "Stored credentials are unavailable. Open Clock Assistant."
                ),

            AttendanceActionExecutionStatus.MissingConfiguration =>
                (
                    $"{actionName} unavailable",
                    "Attendance configuration is unavailable. Open Clock Assistant."
                ),

            AttendanceActionExecutionStatus.ProviderResolutionFailed =>
                (
                    $"{actionName} unavailable",
                    "The configured provider could not be prepared."
                ),

            _ =>
                (
                    $"{actionName} needs attention",
                    "The action could not be completed safely."
                )
        };
    }

    private static string GetUnexpectedFailureText(
        AttendanceActionType actionType)
    {
        var actionName =
            actionType == AttendanceActionType.ClockIn
                ? "Clock In"
                : "Clock Out";

        return string.Join(
            Environment.NewLine,
            $"{actionName} needs attention",
            "The action did not complete safely. Check UKG status before retrying.");
    }

    private sealed record AttendanceHomeSnapshot(
        string ClockInStatus,
        string ClockInTime,
        string ClockOutStatus,
        string ClockOutTime,
        string Summary,
        string? ActionLabel,
        AttendanceActionType? AvailableActionType)
    {
        public static AttendanceHomeSnapshot Unavailable(
            string summary)
        {
            return new AttendanceHomeSnapshot(
                "Unavailable",
                string.Empty,
                "Unavailable",
                string.Empty,
                summary,
                null,
                null);
        }
    }

    }
