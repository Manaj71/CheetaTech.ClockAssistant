namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceStatePersistence
{
    string? Get(string key);

    void Set(string key, string value);

    void Remove(string key);
}
