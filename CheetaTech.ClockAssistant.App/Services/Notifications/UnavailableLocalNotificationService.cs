using CheetaTech.ClockAssistant.Core.Attendance;
using Microsoft.Maui.ApplicationModel;

namespace CheetaTech.ClockAssistant.App.Services.Notifications;

public sealed class UnavailableLocalNotificationService
    : ILocalNotificationService
{
    public Task<PermissionStatus> GetPermissionStatusAsync()
        => Task.FromResult(PermissionStatus.Disabled);

    public Task<PermissionStatus> RequestPermissionAsync()
        => Task.FromResult(PermissionStatus.Disabled);

    public Task<bool> ShowAsync(
        int notificationId,
        string title,
        string message,
        AttendanceActionType actionType)
        => Task.FromResult(false);
}