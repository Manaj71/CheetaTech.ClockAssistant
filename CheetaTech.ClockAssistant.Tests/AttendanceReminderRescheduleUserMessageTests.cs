using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceReminderRescheduleUserMessageTests
{
    [Theory]
    [InlineData(AttendanceReminderRescheduleResult.Scheduled)]
    [InlineData(AttendanceReminderRescheduleResult.Cancelled)]
    public void ForSuccessfulSave_SuccessOutcomes_DoNotClaimReminderFailure(
        AttendanceReminderRescheduleResult result)
    {
        var message =
            AttendanceReminderRescheduleUserMessage.ForSuccessfulSave(
                result);

        Assert.StartsWith("Settings saved successfully.", message);
        Assert.DoesNotContain("could not be updated", message);
    }

    [Theory]
    [InlineData(AttendanceReminderRescheduleResult.ExactAlarmPermissionRequired)]
    [InlineData(AttendanceReminderRescheduleResult.ScheduleFailed)]
    [InlineData(AttendanceReminderRescheduleResult.PlanningFailed)]
    [InlineData(AttendanceReminderRescheduleResult.CancelFailed)]
    [InlineData(AttendanceReminderRescheduleResult.MissingConfiguration)]
    public void ForSuccessfulSave_FailureOutcomes_WarnWithoutClaimingSaveFailed(
        AttendanceReminderRescheduleResult result)
    {
        var message =
            AttendanceReminderRescheduleUserMessage.ForSuccessfulSave(
                result);

        Assert.StartsWith("Settings saved", message);
        Assert.Contains("could not", message);
        Assert.DoesNotContain(
            "Settings could not be saved",
            message);
    }
}
