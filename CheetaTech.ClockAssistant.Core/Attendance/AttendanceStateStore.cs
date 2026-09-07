using System.Globalization;
using System.Text.Json;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceStateStore : IAttendanceStateStore
{
    private const string KeyPrefix = "ClockAssistant.AttendanceState.v1.";

    private readonly IAttendanceStatePersistence _persistence;

    public AttendanceStateStore(IAttendanceStatePersistence persistence)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public Task<DailyAttendanceRecord?> LoadAsync(
        DateOnly attendanceDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = _persistence.Get(BuildKey(attendanceDate));
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Task.FromResult<DailyAttendanceRecord?>(null);
        }

        DailyAttendanceRecord? record;

        try
        {
            record = JsonSerializer.Deserialize<DailyAttendanceRecord>(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Persisted attendance state is invalid.",
                exception);
        }

        if (record is null || record.AttendanceDate != attendanceDate)
        {
            throw new InvalidOperationException(
                "Persisted attendance state does not match the requested attendance date.");
        }

        return Task.FromResult<DailyAttendanceRecord?>(record);
    }

    public Task SaveAsync(
        DailyAttendanceRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(record.AttendanceDate);
        var payload = JsonSerializer.Serialize(record);

        _persistence.Set(key, payload);

        var persistedPayload = _persistence.Get(key);
        if (!string.Equals(
                persistedPayload,
                payload,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Attendance state persistence verification failed.");
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        DateOnly attendanceDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(attendanceDate);
        _persistence.Remove(key);

        if (_persistence.Get(key) is not null)
        {
            throw new InvalidOperationException(
                "Attendance state delete verification failed.");
        }

        return Task.CompletedTask;
    }

    private static string BuildKey(DateOnly attendanceDate)
    {
        return KeyPrefix + attendanceDate.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);
    }
}
