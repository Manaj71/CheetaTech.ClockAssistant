namespace CheetaTech.ClockAssistant.Core.Attendance;

/// <summary>
/// Classifies Clock In/Out execution outcomes for safe recovery:
/// definite-not-sent (retry allowed), uncertain (no blind retry),
/// and confirmed (never retry).
/// </summary>
public static class AttendanceActionRecovery
{
    public static bool AllowsSafeProviderRetry(
        AttendanceActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return !result.ProviderRequestSent &&
               !result.ProviderConfirmed &&
               result.Status is
                   AttendanceActionExecutionStatus.ProviderRejected
                   or AttendanceActionExecutionStatus.PersistenceFailed;
    }

    public static bool IsUncertainProviderOutcome(
        AttendanceActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.ProviderRequestSent &&
               !result.ProviderConfirmed;
    }

    public static bool IsConfirmedProviderOutcome(
        AttendanceActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.ProviderConfirmed;
    }

    /// <summary>
    /// Persisted proof that the last provider attempt for this action did not
    /// begin the punch POST. Required for notification ManualRetry; never infer
    /// from Failed state alone.
    /// </summary>
    public static bool HasProvenPreSendFailure(
        DailyAttendanceRecord record,
        AttendanceActionType actionType)
    {
        ArgumentNullException.ThrowIfNull(record);

        return actionType switch
        {
            AttendanceActionType.ClockIn =>
                record.ClockInState == AttendanceActionState.Failed &&
                record.ClockInProviderRequestSent == false,

            AttendanceActionType.ClockOut =>
                record.ClockOutState == AttendanceActionState.Failed &&
                record.ClockOutProviderRequestSent == false,

            _ => false
        };
    }

    /// <summary>
    /// Action notifications must clear only when the provider outcome is
    /// confirmed or the attempt is uncertain / post-send (no safe blind retry
    /// from the notification action). Definite pre-send failures keep the
    /// eligible Clock In/Out action notification available for retry.
    /// </summary>
    public static bool ShouldClearActionNotification(
        AttendanceActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (IsConfirmedProviderOutcome(result) ||
            result.Status == AttendanceActionExecutionStatus.Succeeded)
        {
            return true;
        }

        if (AllowsSafeProviderRetry(result))
        {
            return false;
        }

        // Uncertain / post-send / non-retryable: replace action with result.
        return result.ProviderRequestSent ||
               result.Status is
                   AttendanceActionExecutionStatus.ProviderUnknown
                   or AttendanceActionExecutionStatus.NotEligible
                   or AttendanceActionExecutionStatus.ExecutionDisabled
                   or AttendanceActionExecutionStatus.MissingCredentials
                   or AttendanceActionExecutionStatus.CredentialReadFailed
                   or AttendanceActionExecutionStatus.MissingConfiguration
                   or AttendanceActionExecutionStatus.ProviderResolutionFailed
                   or AttendanceActionExecutionStatus.PersistenceFailed;
    }

    /// <summary>
    /// After a confirmed success, skip the completed action when planning the
    /// next reminder. After definite pre-send failure, do not skip so the same
    /// action can remain reminder-eligible.
    /// </summary>
    public static bool ShouldSkipActionWhenSchedulingNext(
        AttendanceActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (AllowsSafeProviderRetry(result))
        {
            return false;
        }

        return IsConfirmedProviderOutcome(result) ||
               result.Status == AttendanceActionExecutionStatus.Succeeded ||
               IsUncertainProviderOutcome(result);
    }
}
