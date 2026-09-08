using CheetaTech.ClockAssistant.Core.Attendance;
using Microsoft.Maui.ApplicationModel;

namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public interface ILocalNotificationService
{
    Task<PermissionStatus> GetPermissionStatusAsync();
    Task<PermissionStatus> RequestPermissionAsync();

    Task<bool> ShowAsync(
        int notificationId,
        string title,
        string message,
        AttendanceActionType actionType);
}