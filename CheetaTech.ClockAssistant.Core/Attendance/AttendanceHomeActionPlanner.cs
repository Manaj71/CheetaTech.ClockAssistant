namespace CheetaTech.ClockAssistant.Core.Attendance;

public enum AttendanceHomePrimaryActionMode
{
    None = 0,
    Manual = 1,
    ManualRetry = 2
}

public sealed record AttendanceHomeActionPlan(
    AttendanceActionType? ActionType,
    AttendanceHomePrimaryActionMode PrimaryMode,
    bool OfferCompletedElsewhere,
    string Summary);

public static class AttendanceHomeActionPlanner
{
    public static AttendanceHomeActionPlan Plan(
        AttendanceStateEvaluation evaluation)
        => Plan(evaluation, record: null);

    public static AttendanceHomeActionPlan Plan(
        AttendanceStateEvaluation evaluation,
        DailyAttendanceRecord? record)
    {
        if (evaluation.DayState == AttendanceDayState.NotScheduled)
        {
            return None("No attendance action is currently required.");
        }

        if (evaluation.ClockInState == AttendanceActionState.Failed)
        {
            return PlanFailedAction(
                AttendanceActionType.ClockIn,
                record,
                actionLabel: "Clock In");
        }

        if (evaluation.ClockInState == AttendanceActionState.Unknown)
        {
            return new AttendanceHomeActionPlan(
                AttendanceActionType.ClockIn,
                AttendanceHomePrimaryActionMode.None,
                OfferCompletedElsewhere: true,
                Summary: UncertainSummary());
        }

        if (evaluation.ClockInState is AttendanceActionState.NotDue or AttendanceActionState.Due)
        {
            return new AttendanceHomeActionPlan(
                AttendanceActionType.ClockIn,
                AttendanceHomePrimaryActionMode.Manual,
                OfferCompletedElsewhere: true,
                Summary: "Manual Clock In is available.");
        }

        if (evaluation.ClockInState == AttendanceActionState.Succeeded)
        {
            if (evaluation.ClockOutState == AttendanceActionState.Failed)
            {
                return PlanFailedAction(
                    AttendanceActionType.ClockOut,
                    record,
                    actionLabel: "Clock Out");
            }

            if (evaluation.ClockOutState == AttendanceActionState.Unknown)
            {
                return new AttendanceHomeActionPlan(
                    AttendanceActionType.ClockOut,
                    AttendanceHomePrimaryActionMode.None,
                    OfferCompletedElsewhere: true,
                    Summary: UncertainSummary());
            }

            if (evaluation.ClockOutState is AttendanceActionState.NotDue or AttendanceActionState.Due)
            {
                return new AttendanceHomeActionPlan(
                    AttendanceActionType.ClockOut,
                    AttendanceHomePrimaryActionMode.Manual,
                    OfferCompletedElsewhere: true,
                    Summary: "Manual Clock Out is available.");
            }
        }

        return None("No attendance action is currently required.");
    }

    public static bool IsSafeCompletedElsewhere(
        AttendanceActionType actionType,
        DailyAttendanceRecord record)
    {
        return actionType switch
        {
            AttendanceActionType.ClockIn =>
                record.ClockInState is AttendanceActionState.NotDue
                    or AttendanceActionState.Due
                    or AttendanceActionState.Failed
                    or AttendanceActionState.Unknown,

            AttendanceActionType.ClockOut =>
                record.ClockInState == AttendanceActionState.Succeeded &&
                record.ClockOutState is AttendanceActionState.NotDue
                    or AttendanceActionState.Due
                    or AttendanceActionState.Failed
                    or AttendanceActionState.Unknown,

            _ => false
        };
    }

    private static AttendanceHomeActionPlan PlanFailedAction(
        AttendanceActionType actionType,
        DailyAttendanceRecord? record,
        string actionLabel)
    {
        var provenPreSend =
            record is not null &&
            AttendanceActionRecovery.HasProvenPreSendFailure(
                record,
                actionType);

        if (provenPreSend)
        {
            return new AttendanceHomeActionPlan(
                actionType,
                AttendanceHomePrimaryActionMode.ManualRetry,
                OfferCompletedElsewhere: true,
                Summary:
                    $"The {actionLabel} request was not sent to UKG. You can retry.");
        }

        // Failed without proven pre-send (true, null, or missing record):
        // offer Completed Elsewhere only — never ordinary ManualRetry.
        return new AttendanceHomeActionPlan(
            actionType,
            AttendanceHomePrimaryActionMode.None,
            OfferCompletedElsewhere: true,
            Summary: UncertainSummary());
    }

    private static string UncertainSummary()
        => "UKG has not confirmed this action. The request may have been processed. " +
           "Check your attendance in the UKG app or portal before taking further action.";

    private static AttendanceHomeActionPlan None(string summary)
        => new(
            null,
            AttendanceHomePrimaryActionMode.None,
            OfferCompletedElsewhere: false,
            summary);
}
