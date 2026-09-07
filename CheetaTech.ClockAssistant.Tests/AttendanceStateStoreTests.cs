using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceStateStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsAttendanceState()
    {
        var persistence = new InMemoryAttendanceStatePersistence();
        var store = new AttendanceStateStore(persistence);
        var record = CreateRecord(new DateOnly(2026, 9, 7));

        await store.SaveAsync(record);
        var loaded = await store.LoadAsync(record.AttendanceDate);

        Assert.Equal(record, loaded);
    }

    [Fact]
    public async Task NewStoreInstance_RecoversPersistedState()
    {
        var persistence = new InMemoryAttendanceStatePersistence();
        var firstStore = new AttendanceStateStore(persistence);
        var record = CreateRecord(new DateOnly(2026, 9, 7));

        await firstStore.SaveAsync(record);

        var restartedStore = new AttendanceStateStore(persistence);
        var loaded = await restartedStore.LoadAsync(record.AttendanceDate);

        Assert.Equal(record, loaded);
    }

    [Fact]
    public async Task DifferentAttendanceDates_AreIsolated()
    {
        var persistence = new InMemoryAttendanceStatePersistence();
        var store = new AttendanceStateStore(persistence);

        var first = CreateRecord(new DateOnly(2026, 9, 7));
        var second = new DailyAttendanceRecord
        {
            AttendanceDate = new DateOnly(2026, 9, 8),
            ClockInState = AttendanceActionState.NotDue,
            ClockOutState = AttendanceActionState.NotDue
        };

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        Assert.Equal(first, await store.LoadAsync(first.AttendanceDate));
        Assert.Equal(second, await store.LoadAsync(second.AttendanceDate));
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyRequestedAttendanceDate()
    {
        var persistence = new InMemoryAttendanceStatePersistence();
        var store = new AttendanceStateStore(persistence);

        var first = CreateRecord(new DateOnly(2026, 9, 7));
        var second = CreateRecord(new DateOnly(2026, 9, 8));

        await store.SaveAsync(first);
        await store.SaveAsync(second);
        await store.DeleteAsync(first.AttendanceDate);

        Assert.Null(await store.LoadAsync(first.AttendanceDate));
        Assert.Equal(second, await store.LoadAsync(second.AttendanceDate));
    }

    [Fact]
    public async Task LoadAsync_CorruptPersistedState_FailsClosed()
    {
        var persistence = new InMemoryAttendanceStatePersistence();
        var store = new AttendanceStateStore(persistence);
        var attendanceDate = new DateOnly(2026, 9, 7);

        persistence.Set(
            "ClockAssistant.AttendanceState.v1.20260907",
            "{not-valid-json");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.LoadAsync(attendanceDate));
    }

    [Fact]
    public async Task SaveAsync_PersistenceVerificationFailure_FailsClosed()
    {
        var persistence = new NonPersistingAttendanceStatePersistence();
        var store = new AttendanceStateStore(persistence);
        var record = CreateRecord(new DateOnly(2026, 9, 7));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(record));
    }

    private static DailyAttendanceRecord CreateRecord(DateOnly attendanceDate)
    {
        return new DailyAttendanceRecord
        {
            AttendanceDate = attendanceDate,
            ClockInState = AttendanceActionState.Succeeded,
            ClockOutState = AttendanceActionState.InProgress
        };
    }

    private sealed class InMemoryAttendanceStatePersistence
        : IAttendanceStatePersistence
    {
        private readonly Dictionary<string, string> _values = new();

        public string? Get(string key)
        {
            return _values.TryGetValue(key, out var value)
                ? value
                : null;
        }

        public void Set(string key, string value)
        {
            _values[key] = value;
        }

        public void Remove(string key)
        {
            _values.Remove(key);
        }
    }

    private sealed class NonPersistingAttendanceStatePersistence
        : IAttendanceStatePersistence
    {
        public string? Get(string key)
        {
            return null;
        }

        public void Set(string key, string value)
        {
        }

        public void Remove(string key)
        {
        }
    }
}
