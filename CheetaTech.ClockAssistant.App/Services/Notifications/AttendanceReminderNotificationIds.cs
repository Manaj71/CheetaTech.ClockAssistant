using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public static class AttendanceReminderNotificationIds
{
    public const int ClockIn = 6101;
    public const int ClockOut = 6102;

    public static int For(AttendanceActionType actionType)
        => actionType switch
        {
            AttendanceActionType.ClockIn => ClockIn,
            AttendanceActionType.ClockOut => ClockOut,
            _ => throw new ArgumentOutOfRangeException(nameof(actionType))
        };
}
