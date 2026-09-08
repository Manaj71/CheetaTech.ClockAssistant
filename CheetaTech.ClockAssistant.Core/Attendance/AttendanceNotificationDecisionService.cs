using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceNotificationDecisionService
    : IAttendanceNotificationDecisionService
{
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly IAttendanceStateStore _attendanceStateStore;
    private readonly AttendanceStateEvaluator _attendanceStateEvaluator;

    public AttendanceNotificationDecisionService(
        IClockAssistantConfigurationStore configurationStore,
        IAttendanceStateStore attendanceStateStore,
        AttendanceStateEvaluator attendanceStateEvaluator)
    {
        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));

        _attendanceStateStore =
            attendanceStateStore
            ?? throw new ArgumentNullException(nameof(attendanceStateStore));

        _attendanceStateEvaluator =
            attendanceStateEvaluator
            ?? throw new ArgumentNullException(nameof(attendanceStateEvaluator));
    }

    public async Task<AttendanceNotificationDecision?> EvaluateAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var configuration =
            await _configurationStore.GetAsync(cancellationToken);

        if (configuration is null)
        {
            return null;
        }

        var initialEvaluation =
            _attendanceStateEvaluator.Evaluate(
                configuration,
                utcNow);

        var persistedRecord =
            await _attendanceStateStore.LoadAsync(
                initialEvaluation.AttendanceDate,
                cancellationToken);

        var evaluation =
            _attendanceStateEvaluator.Evaluate(
                configuration,
                utcNow,
                persistedRecord);

        AttendanceActionType? actionType = null;

        if (evaluation.ClockInNotificationEligible)
        {
            actionType = AttendanceActionType.ClockIn;
        }
        else if (evaluation.ClockOutNotificationEligible)
        {
            actionType = AttendanceActionType.ClockOut;
        }

        return new AttendanceNotificationDecision(
            evaluation.AttendanceDate,
            actionType,
            evaluation);
    }
}