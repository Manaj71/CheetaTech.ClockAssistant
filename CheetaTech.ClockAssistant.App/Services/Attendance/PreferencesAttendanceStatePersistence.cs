using CheetaTech.ClockAssistant.Core.Attendance;
using Microsoft.Maui.Storage;

namespace CheetaTech.ClockAssistant.App.Services.Attendance;

public sealed class PreferencesAttendanceStatePersistence
    : IAttendanceStatePersistence
{
    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = Preferences.Default.Get(key, string.Empty);

        return string.IsNullOrEmpty(value)
            ? null
            : value;
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Preferences.Default.Set(key, value);
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Preferences.Default.Remove(key);
    }
}
