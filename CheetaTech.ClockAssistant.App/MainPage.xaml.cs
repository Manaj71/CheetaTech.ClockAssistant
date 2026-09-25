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
    private readonly ILocalNotificationService _localNotificationService;

    private readonly Label _attendanceDateValue;
    private readonly Label _shiftContextValue;
    private readonly Label _clockInStatusValue;
    private readonly Label _clockInTimeValue;
    private readonly Label _clockOutStatusValue;
    private readonly Label _clockOutTimeValue;
    private readonly Label _summaryLabel;
    private readonly Label _resultLabel;
    private readonly Button _actionButton;
    private readonly Button _secondaryActionButton;

    private AttendanceActionType? _availableActionType;
    private AttendanceHomePrimaryActionMode _primaryActionMode;
    private bool _isBusy;

    public MainPage(
        IClockAssistantConfigurationStore configurationStore,
        IAttendanceStateStore attendanceStateStore,
        AttendanceStateEvaluator stateEvaluator,
        IAttendanceActionExecutionService executionService,
        IAttendanceActionAuditLog auditLog,
        IAttendanceReminderStartupCoordinator reminderCoordinator,
        AttendanceHistoryRecorder historyRecorder,
        ILocalNotificationService localNotificationService)
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

        _localNotificationService =
            localNotificationService
            ?? throw new ArgumentNullException(nameof(localNotificationService));

        InitializeComponent();

        _attendanceDateValue =
            (FindByName("AttendanceDateValue") as Label)
            ?? throw new InvalidOperationException("AttendanceDateValue is missing.");
        _shiftContextValue =
            (FindByName("ShiftContextValue") as Label)
            ?? throw new InvalidOperationException("ShiftContextValue is missing.");
        _clockInStatusValue =
            (FindByName("ClockInStatusValue") as Label)
            ?? throw new InvalidOperationException("ClockInStatusValue is missing.");
        _clockInTimeValue =
            (FindByName("ClockInTimeValue") as Label)
            ?? throw new InvalidOperationException("ClockInTimeValue is missing.");
        _clockOutStatusValue =
            (FindByName("ClockOutStatusValue") as Label)
            ?? throw new InvalidOperationException("ClockOutStatusValue is missing.");
        _clockOutTimeValue =
            (FindByName("ClockOutTimeValue") as Label)
            ?? throw new InvalidOperationException("ClockOutTimeValue is missing.");
        _summaryLabel =
            (FindByName("SummaryLabel") as Label)
            ?? throw new InvalidOperationException("SummaryLabel is missing.");
        _resultLabel =
            (FindByName("ResultLabel") as Label)
            ?? throw new InvalidOperationException("ResultLabel is missing.");
        _actionButton =
            (FindByName("ActionButton") as Button)
            ?? throw new InvalidOperationException("ActionButton is missing.");
        _secondaryActionButton =
            (FindByName("SecondaryActionButton") as Button)
            ?? throw new InvalidOperationException("SecondaryActionButton is missing.");

        _actionButton.Clicked += OnActionButtonClicked;
        _secondaryActionButton.Clicked += OnSecondaryActionButtonClicked;
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

        if (_availableActionType is null ||
            _primaryActionMode == AttendanceHomePrimaryActionMode.None ||
            _isBusy)
        {
            return;
        }

        if (_primaryActionMode == AttendanceHomePrimaryActionMode.ManualRetry)
        {
            await ExecuteManualRetryActionAsync(_availableActionType.Value);
            return;
        }

        await ExecuteManualActionAsync(_availableActionType.Value);
    }

    private async void OnSecondaryActionButtonClicked(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_availableActionType is null || _isBusy)
        {
            return;
        }

        await CompleteElsewhereAsync(_availableActionType.Value);
    }

    private async Task ExecuteManualRetryActionAsync(
        AttendanceActionType actionType)
    {
        var actionName =
            actionType == AttendanceActionType.ClockIn
                ? "Clock In"
                : "Clock Out";

        var alreadyState =
            actionType == AttendanceActionType.ClockIn
                ? "Clocked In"
                : "Clocked Out";

        var confirm = await DisplayAlert(
            $"Retry {actionName}?",
            $"Only retry if UKG shows you are not already {alreadyState}." + Environment.NewLine +
            $"This will send another {actionName} request to UKG.",
            "Retry",
            "Cancel");

        if (!confirm)
        {
            return;
        }

        await ExecuteHomeProviderActionAsync(
            actionType,
            AttendanceActionExecutionMode.ManualRetry);
    }

    private async Task ExecuteManualActionAsync(
        AttendanceActionType actionType)
    {
        await ExecuteHomeProviderActionAsync(
            actionType,
            AttendanceActionExecutionMode.Manual);
    }

    private async Task ExecuteHomeProviderActionAsync(
        AttendanceActionType actionType,
        AttendanceActionExecutionMode executionMode)
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
                        executionMode);
            }
            catch (Exception exception)
            {
                await TryAppendExceptionAuditAsync(
                    actionType,
                    exception);

                await TryCancelStaleAttendanceNotificationAsync(actionType);

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

            if (AttendanceActionRecovery.ShouldClearActionNotification(result))
            {
                await TryCancelStaleAttendanceNotificationAsync(actionType);
            }

            await TryScheduleReminderAfterResultAsync(
                actionType,
                result,
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

        var plan =
            AttendanceHomeActionPlanner.Plan(evaluation);

        var attendanceDateText =
            evaluation.AttendanceDate.ToString(
                "D",
                CultureInfo.CurrentCulture);

        var clockInTimeLabel =
            configuration.ClockInTime.HasValue
                ? $"Configured {configuration.ClockInTime.Value.ToString("t", CultureInfo.CurrentCulture)}"
                : "No configured time";

        var clockOutTimeLabel =
            configuration.ClockOutTime.HasValue
                ? $"Configured {configuration.ClockOutTime.Value.ToString("t", CultureInfo.CurrentCulture)}"
                : "No configured time";

        var shiftContext =
            string.Join(
                " – ",
                clockInTimeLabel.Replace("Configured ", string.Empty, StringComparison.Ordinal),
                clockOutTimeLabel.Replace("Configured ", string.Empty, StringComparison.Ordinal));

        var clockInStatus =
            GetActionStatusLabel(
                evaluation.ClockInState,
                currentRecord?.ClockInCompletionSource);

        var clockOutStatus =
            GetActionStatusLabel(
                evaluation.ClockOutState,
                currentRecord?.ClockOutCompletionSource,
                plan.ActionType == AttendanceActionType.ClockOut &&
                plan.PrimaryMode != AttendanceHomePrimaryActionMode.None);

        var actionLabel =
            plan.PrimaryMode switch
            {
                AttendanceHomePrimaryActionMode.Manual when
                    plan.ActionType == AttendanceActionType.ClockIn =>
                    "Clock In",

                AttendanceHomePrimaryActionMode.Manual when
                    plan.ActionType == AttendanceActionType.ClockOut =>
                    "Clock Out",

                AttendanceHomePrimaryActionMode.ManualRetry when
                    plan.ActionType == AttendanceActionType.ClockIn =>
                    "Retry Clock In",

                AttendanceHomePrimaryActionMode.ManualRetry when
                    plan.ActionType == AttendanceActionType.ClockOut =>
                    "Retry Clock Out",

                _ => null
            };

        var secondaryActionLabel =
            plan.OfferCompletedElsewhere && plan.ActionType is not null
                ? plan.ActionType == AttendanceActionType.ClockIn
                    ? "I already Clocked In elsewhere"
                    : "I already Clocked Out elsewhere"
                : null;

        return new AttendanceHomeSnapshot(
            attendanceDateText,
            shiftContext,
            clockInStatus,
            clockInTimeLabel,
            clockOutStatus,
            clockOutTimeLabel,
            plan.Summary,
            actionLabel,
            secondaryActionLabel,
            plan.ActionType,
            plan.PrimaryMode);
    }

    private void ApplySnapshot(AttendanceHomeSnapshot snapshot)
    {
        _attendanceDateValue.Text = snapshot.AttendanceDate;
        _shiftContextValue.Text = snapshot.ShiftContext;
        _clockInStatusValue.Text = snapshot.ClockInStatus;
        _clockInTimeValue.Text = snapshot.ClockInTime;
        _clockOutStatusValue.Text = snapshot.ClockOutStatus;
        _clockOutTimeValue.Text = snapshot.ClockOutTime;
        _summaryLabel.Text = snapshot.Summary;

        _availableActionType = snapshot.AvailableActionType;
        _primaryActionMode = snapshot.PrimaryActionMode;
        _actionButton.Text = snapshot.ActionLabel ?? string.Empty;
        _actionButton.IsVisible = snapshot.ActionLabel is not null;
        _actionButton.IsEnabled = !_isBusy && snapshot.ActionLabel is not null;
        _secondaryActionButton.Text = snapshot.SecondaryActionLabel ?? string.Empty;
        _secondaryActionButton.IsVisible = snapshot.SecondaryActionLabel is not null;
        _secondaryActionButton.IsEnabled = !_isBusy && snapshot.SecondaryActionLabel is not null;
    }

    private void ApplyUnavailableSnapshot(
        string summary,
        string message)
    {
        _attendanceDateValue.Text = string.Empty;
        _shiftContextValue.Text = string.Empty;
        _clockInStatusValue.Text = "Unavailable";
        _clockInTimeValue.Text = string.Empty;
        _clockOutStatusValue.Text = "Unavailable";
        _clockOutTimeValue.Text = string.Empty;
        _summaryLabel.Text = summary;
        _availableActionType = null;
        _primaryActionMode = AttendanceHomePrimaryActionMode.None;
        _actionButton.IsVisible = false;
        _actionButton.IsEnabled = false;
        _secondaryActionButton.IsVisible = false;
        _secondaryActionButton.IsEnabled = false;
        _resultLabel.Text = message;
        _resultLabel.IsVisible = true;
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        _actionButton.IsEnabled = !isBusy && _actionButton.IsVisible;
        _secondaryActionButton.IsEnabled = !isBusy && _secondaryActionButton.IsVisible;
    }

    private static string GetActionStatusLabel(
        AttendanceActionState state,
        AttendanceActionCompletionSource? completionSource,
        bool clockOutAvailable = false)
    {
        return state switch
        {
            AttendanceActionState.Succeeded when
                completionSource == AttendanceActionCompletionSource.CompletedElsewhere =>
                "Completed elsewhere",

            AttendanceActionState.Succeeded => "Confirmed",
            AttendanceActionState.InProgress => "In progress",
            AttendanceActionState.Failed => "Not confirmed",
            AttendanceActionState.Unknown => "Status uncertain",
            AttendanceActionState.Skipped => "Not completed",
            _ when clockOutAvailable => "Scheduled",
            _ => "Not completed"
        };
    }

    private async Task CompleteElsewhereAsync(
        AttendanceActionType actionType)
    {
        var actionName =
            actionType == AttendanceActionType.ClockIn
                ? "Clock In"
                : "Clock Out";

        var confirm = await DisplayAlert(
            $"Mark {actionName} as completed elsewhere?",
            $"ShiftPilot will stop offering {actionName} for today.\nThis does not send anything to UKG.",
            "Yes",
            "Cancel");

        if (!confirm)
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var configuration = await _configurationStore.GetAsync();
        if (configuration is null)
        {
            _resultLabel.Text = "Attendance configuration is unavailable.";
            _resultLabel.IsVisible = true;
            await RefreshStateAsync();
            return;
        }

        var evaluation = _stateEvaluator.Evaluate(configuration, utcNow);
        var currentRecord = await _attendanceStateStore.LoadAsync(evaluation.AttendanceDate);
        if (currentRecord is null || currentRecord.AttendanceDate != evaluation.AttendanceDate)
        {
            currentRecord = new DailyAttendanceRecord { AttendanceDate = evaluation.AttendanceDate };
        }

        var freshEvaluation = _stateEvaluator.Evaluate(configuration, utcNow, currentRecord);
        var freshPlan = AttendanceHomeActionPlanner.Plan(freshEvaluation);

        if (!freshPlan.OfferCompletedElsewhere ||
            freshPlan.ActionType != actionType ||
            !AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(actionType, currentRecord))
        {
            _resultLabel.Text =
                $"{actionName} could not be marked completed elsewhere safely.";

            _resultLabel.IsVisible = true;

            await RefreshStateAsync();
            return;
        }

        var updatedRecord = actionType switch
        {
            AttendanceActionType.ClockIn =>
                currentRecord with
                {
                    ClockInState = AttendanceActionState.Succeeded,
                    ClockInCompletionSource = AttendanceActionCompletionSource.CompletedElsewhere
                },
            AttendanceActionType.ClockOut =>
                currentRecord with
                {
                    ClockOutState = AttendanceActionState.Succeeded,
                    ClockOutCompletionSource = AttendanceActionCompletionSource.CompletedElsewhere
                },
            _ => currentRecord
        };

        if (ReferenceEquals(updatedRecord, currentRecord))
        {
            _resultLabel.Text = $"{actionName} could not be marked completed elsewhere safely.";
            _resultLabel.IsVisible = true;
            return;
        }

        await _attendanceStateStore.SaveAsync(updatedRecord);
        await TryCancelStaleAttendanceNotificationAsync(actionType);
        await TryScheduleReminderAsync(actionType, utcNow);
        _resultLabel.Text = $"{actionName} marked completed elsewhere.";
        _resultLabel.IsVisible = true;
        await RefreshStateAsync();
    }

    private async Task TryCancelStaleAttendanceNotificationAsync(
        AttendanceActionType actionType)
    {
        try
        {
            await _localNotificationService.CancelAsync(
                AttendanceReminderNotificationIds.For(actionType));
        }
        catch
        {
        }
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

    private async Task TryScheduleReminderAfterResultAsync(
        AttendanceActionType actionType,
        AttendanceActionExecutionResult result,
        DateTimeOffset utcNow)
    {
        try
        {
            if (AttendanceActionRecovery.ShouldSkipActionWhenSchedulingNext(result))
            {
                await _reminderCoordinator.ScheduleNextAfterActionAsync(
                    actionType,
                    utcNow);
            }
            else
            {
                await _reminderCoordinator.ScheduleNextAsync(utcNow);
            }
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
                AttendanceActionRecovery.AllowsSafeProviderRetry(result)
                    ? (
                        $"{actionName} failed",
                        "The provider did not receive the action. You can retry when connectivity returns."
                      )
                    : (
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
                    "Stored credentials could not be read. Open ShiftPilot and update credentials."
                ),

            AttendanceActionExecutionStatus.MissingCredentials =>
                (
                    $"{actionName} unavailable",
                    "Stored credentials are unavailable. Open ShiftPilot."
                ),

            AttendanceActionExecutionStatus.MissingConfiguration =>
                (
                    $"{actionName} unavailable",
                    "Attendance configuration is unavailable. Open ShiftPilot."
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
        string AttendanceDate,
        string ShiftContext,
        string ClockInStatus,
        string ClockInTime,
        string ClockOutStatus,
        string ClockOutTime,
        string Summary,
        string? ActionLabel,
        string? SecondaryActionLabel,
        AttendanceActionType? AvailableActionType,
        AttendanceHomePrimaryActionMode PrimaryActionMode)
    {
        public static AttendanceHomeSnapshot Unavailable(
            string summary)
        {
            return new AttendanceHomeSnapshot(
                string.Empty,
                string.Empty,
                "Unavailable",
                string.Empty,
                "Unavailable",
                string.Empty,
                summary,
                null,
                null,
                null,
                AttendanceHomePrimaryActionMode.None);
        }
    }
}
