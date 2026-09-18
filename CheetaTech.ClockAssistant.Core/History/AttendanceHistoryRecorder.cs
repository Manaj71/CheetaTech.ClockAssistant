using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Core.History;

public sealed class AttendanceHistoryRecorder
{
    private readonly IAttendanceHistoryStore _historyStore;

    public AttendanceHistoryRecorder(
        IAttendanceHistoryStore historyStore)
    {
        _historyStore =
            historyStore
            ?? throw new ArgumentNullException(nameof(historyStore));
    }

    public async Task<bool> TryRecordAsync(
        AttendanceActionExecutionResult result,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(result);

            var entry =
                new AttendanceHistoryEntry(
                    result.AttendanceDate ?? DateOnly.FromDateTime(occurredAtUtc.UtcDateTime),
                    occurredAtUtc,
                    result.ActionType,
                    MapOutcome(result.Status, result.ProviderConfirmed),
                    result.ProviderConfirmed);

            await _historyStore.AppendAsync(
                    entry,
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AttendanceHistoryOutcome MapOutcome(
        AttendanceActionExecutionStatus status,
        bool providerConfirmed)
    {
        return status switch
        {
            AttendanceActionExecutionStatus.Succeeded =>
                AttendanceHistoryOutcome.Confirmed,

            AttendanceActionExecutionStatus.PersistenceFailed
                when providerConfirmed =>
                AttendanceHistoryOutcome.ConfirmedNeedsAttention,

            AttendanceActionExecutionStatus.ProviderRejected =>
                AttendanceHistoryOutcome.NotConfirmed,

            AttendanceActionExecutionStatus.ProviderUnknown =>
                AttendanceHistoryOutcome.Uncertain,

            AttendanceActionExecutionStatus.ExecutionDisabled =>
                AttendanceHistoryOutcome.NotCompleted,

            AttendanceActionExecutionStatus.NotEligible =>
                AttendanceHistoryOutcome.NotCompleted,

            AttendanceActionExecutionStatus.CredentialReadFailed =>
                AttendanceHistoryOutcome.NotCompleted,

            AttendanceActionExecutionStatus.MissingCredentials =>
                AttendanceHistoryOutcome.NotCompleted,

            AttendanceActionExecutionStatus.MissingConfiguration =>
                AttendanceHistoryOutcome.NotCompleted,

            AttendanceActionExecutionStatus.ProviderResolutionFailed =>
                AttendanceHistoryOutcome.NotCompleted,

            AttendanceActionExecutionStatus.PersistenceFailed =>
                AttendanceHistoryOutcome.NotCompleted,

            _ => AttendanceHistoryOutcome.NotCompleted
        };
    }
}
