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
    {
        if (evaluation.DayState == AttendanceDayState.NotScheduled)
        {
            return None("No attendance action is currently required.");
        }

        if (evaluation.ClockInState == AttendanceActionState.Failed)
        {
            return new AttendanceHomeActionPlan(
                AttendanceActionType.ClockIn,
                AttendanceHomePrimaryActionMode.ManualRetry,
                OfferCompletedElsewhere: true,
                Summary:
                    "Clock In needs attention." + Environment.NewLine +
                    "The provider did not confirm it." + Environment.NewLine +
                    "Check UKG status before retrying.");
        }

        if (evaluation.ClockInState == AttendanceActionState.Unknown)
        {
            return new AttendanceHomeActionPlan(
                AttendanceActionType.ClockIn,
                AttendanceHomePrimaryActionMode.None,
                OfferCompletedElsewhere: true,
                Summary:
                    "Status uncertain." + Environment.NewLine +
                    "Check UKG before taking another provider action.");
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
                return new AttendanceHomeActionPlan(
                    AttendanceActionType.ClockOut,
                    AttendanceHomePrimaryActionMode.ManualRetry,
                    OfferCompletedElsewhere: true,
                    Summary:
                        "Clock Out needs attention." + Environment.NewLine +
                        "The provider did not confirm it." + Environment.NewLine +
                        "Check UKG status before retrying.");
            }

            if (evaluation.ClockOutState == AttendanceActionState.Unknown)
            {
                return new AttendanceHomeActionPlan(
                    AttendanceActionType.ClockOut,
                    AttendanceHomePrimaryActionMode.None,
                    OfferCompletedElsewhere: true,
                    Summary:
                        "Status uncertain." + Environment.NewLine +
                        "Check UKG before taking another provider action.");
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

    private static AttendanceHomeActionPlan None(string summary)
        => new(
            null,
            AttendanceHomePrimaryActionMode.None,
            OfferCompletedElsewhere: false,
            summary);
}
