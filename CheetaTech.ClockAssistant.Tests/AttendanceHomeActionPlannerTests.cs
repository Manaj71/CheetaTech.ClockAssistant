using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceHomeActionPlannerTests
{
    [Fact]
    public void Plan_FailedClockIn_OffersRetryAndCompletedElsewhere()
    {
        var evaluation = Evaluate(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Failed
            },
            ClockInDueUtc);

        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Equal(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.ManualRetry, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
        Assert.Contains("Clock In needs attention", plan.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "No attendance action is currently required",
            plan.Summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_UnknownClockIn_OffersCompletedElsewhereWithoutRetry()
    {
        var evaluation = Evaluate(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Unknown
            },
            ClockInDueUtc);

        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Equal(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
        Assert.Contains("Status uncertain", plan.Summary, StringComparison.Ordinal);
        Assert.Contains(
            "Check UKG before taking another provider action",
            plan.Summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_FailedClockOut_OffersRetryAndCompletedElsewhere()
    {
        var evaluation = Evaluate(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.Failed
            },
            ClockOutDueUtc);

        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Equal(AttendanceActionType.ClockOut, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.ManualRetry, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
        Assert.Contains("Clock Out needs attention", plan.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_UnknownClockOut_OffersCompletedElsewhereWithoutRetry()
    {
        var evaluation = Evaluate(
            new DailyAttendanceRecord
            {
                AttendanceDate = AttendanceDate,
                ClockInState = AttendanceActionState.Succeeded,
                ClockOutState = AttendanceActionState.Unknown
            },
            ClockOutDueUtc);

        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Equal(AttendanceActionType.ClockOut, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
    }

    [Fact]
    public void Plan_DueClockIn_OffersManualWithoutRetryMode()
    {
        var evaluation = Evaluate(
            record: null,
            ClockInDueUtc);

        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Equal(AttendanceActionType.ClockIn, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.Manual, plan.PrimaryMode);
        Assert.True(plan.OfferCompletedElsewhere);
        Assert.Equal("Manual Clock In is available.", plan.Summary);
    }

    [Theory]
    [InlineData(AttendanceActionState.Failed)]
    [InlineData(AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionState.Due)]
    [InlineData(AttendanceActionState.NotDue)]
    public void IsSafeCompletedElsewhere_ClockIn_AllowsRecoverableStates(
        AttendanceActionState state)
    {
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = state
        };

        Assert.True(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockIn,
                record));
    }

    [Theory]
    [InlineData(AttendanceActionState.Succeeded)]
    [InlineData(AttendanceActionState.InProgress)]
    [InlineData(AttendanceActionState.Skipped)]
    public void IsSafeCompletedElsewhere_ClockIn_BlocksTerminalOrUnsafeStates(
        AttendanceActionState state)
    {
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = state
        };

        Assert.False(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockIn,
                record));
    }

    [Theory]
    [InlineData(AttendanceActionState.Failed)]
    [InlineData(AttendanceActionState.Unknown)]
    [InlineData(AttendanceActionState.Due)]
    [InlineData(AttendanceActionState.NotDue)]
    public void IsSafeCompletedElsewhere_ClockOut_RequiresSucceededClockIn(
        AttendanceActionState clockOutState)
    {
        var allowed = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = AttendanceActionState.Succeeded,
            ClockOutState = clockOutState
        };

        var blocked = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = AttendanceActionState.Failed,
            ClockOutState = clockOutState
        };

        Assert.True(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockOut,
                allowed));
        Assert.False(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockOut,
                blocked));
    }

    [Theory]
    [InlineData(AttendanceActionState.Succeeded)]
    [InlineData(AttendanceActionState.InProgress)]
    [InlineData(AttendanceActionState.Skipped)]
    public void IsSafeCompletedElsewhere_ClockOut_BlocksTerminalOrUnsafeStates(
        AttendanceActionState clockOutState)
    {
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = AttendanceActionState.Succeeded,
            ClockOutState = clockOutState
        };

        Assert.False(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockOut,
                record));
    }

    [Fact]
    public void CompletedElsewhereRecovery_FailedClockIn_BecomesSucceededWithSource()
    {
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = AttendanceActionState.Failed
        };

        Assert.True(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockIn,
                record));

        var updated = record with
        {
            ClockInState = AttendanceActionState.Succeeded,
            ClockInCompletionSource = AttendanceActionCompletionSource.CompletedElsewhere
        };

        var evaluation = Evaluate(updated, ClockOutDueUtc);
        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Equal(AttendanceActionType.ClockOut, plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.Manual, plan.PrimaryMode);
        Assert.Equal(
            AttendanceActionCompletionSource.CompletedElsewhere,
            updated.ClockInCompletionSource);
        Assert.NotEqual(
            AttendanceActionCompletionSource.ProviderConfirmed,
            updated.ClockInCompletionSource);
    }

    [Fact]
    public void CompletedElsewhereRecovery_FailedClockOut_CompletesDay()
    {
        var record = new DailyAttendanceRecord
        {
            AttendanceDate = AttendanceDate,
            ClockInState = AttendanceActionState.Succeeded,
            ClockOutState = AttendanceActionState.Failed
        };

        Assert.True(
            AttendanceHomeActionPlanner.IsSafeCompletedElsewhere(
                AttendanceActionType.ClockOut,
                record));

        var updated = record with
        {
            ClockOutState = AttendanceActionState.Succeeded,
            ClockOutCompletionSource = AttendanceActionCompletionSource.CompletedElsewhere
        };

        var evaluation = Evaluate(updated, ClockOutDueUtc);
        var plan = AttendanceHomeActionPlanner.Plan(evaluation);

        Assert.Null(plan.ActionType);
        Assert.Equal(AttendanceHomePrimaryActionMode.None, plan.PrimaryMode);
        Assert.False(plan.OfferCompletedElsewhere);
        Assert.Equal(AttendanceDayState.Completed, evaluation.DayState);
    }

    private static AttendanceStateEvaluation Evaluate(
        DailyAttendanceRecord? record,
        DateTimeOffset utcNow)
    {
        return new AttendanceStateEvaluator().Evaluate(
            Configuration(),
            utcNow,
            record);
    }

    private static ClockAssistantConfiguration Configuration()
        => new()
        {
            ProviderType = "UKG",
            ProviderUrl = "https://example.invalid/",
            WorkDays = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            },
            ClockInTime = new TimeOnly(7, 0),
            ClockOutTime = new TimeOnly(15, 30),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15)
        };

    private static readonly DateOnly AttendanceDate = new(2026, 9, 8);

    private static readonly DateTimeOffset ClockInDueUtc =
        new(2026, 9, 8, 10, 50, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ClockOutDueUtc =
        new(2026, 9, 8, 19, 20, 0, TimeSpan.Zero);
}
