namespace CheetaTech.ClockAssistant.Core.History;

public interface IAttendanceHistoryStore
{
    Task AppendAsync(
        AttendanceHistoryEntry entry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceHistoryEntry>> GetRecentAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
