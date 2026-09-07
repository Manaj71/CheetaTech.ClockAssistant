namespace CheetaTech.ClockAssistant.Core.Attendance;

public interface IAttendanceStateStore
{
    Task<DailyAttendanceRecord?> LoadAsync(
        DateOnly attendanceDate,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        DailyAttendanceRecord record,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        DateOnly attendanceDate,
        CancellationToken cancellationToken = default);
}
