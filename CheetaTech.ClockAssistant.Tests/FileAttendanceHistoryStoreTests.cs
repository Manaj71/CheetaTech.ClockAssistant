using System.Linq;
using System.Text.Json;
using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.History;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class FileAttendanceHistoryStoreTests
{
    [Fact]
    public async Task GetRecentAsync_MissingFile_ReturnsEmpty()
    {
        var context = await CreateContextAsync();

        var entries = await context.Store.GetRecentAsync(context.Now);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task AppendAsync_SingleEntry_RoundTripsFields()
    {
        var context = await CreateContextAsync();
        var entry = CreateEntry(
            new DateOnly(2026, 9, 16),
            new DateTimeOffset(2026, 9, 16, 14, 30, 45, TimeSpan.Zero),
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.Confirmed,
            true);

        await context.Store.AppendAsync(entry);

        var entries = await context.Store.GetRecentAsync(context.Now);

        var actual = Assert.Single(entries);
        Assert.Equal(entry, actual);
    }

    [Fact]
    public async Task AppendAsync_ClockInAndClockOut_BothRetained()
    {
        var context = await CreateContextAsync();
        var first = CreateEntry(
            new DateOnly(2026, 9, 16),
            new DateTimeOffset(2026, 9, 16, 14, 0, 0, TimeSpan.Zero),
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.Confirmed,
            true);
        var second = CreateEntry(
            new DateOnly(2026, 9, 16),
            new DateTimeOffset(2026, 9, 16, 19, 0, 0, TimeSpan.Zero),
            AttendanceActionType.ClockOut,
            AttendanceHistoryOutcome.NotConfirmed,
            false);

        await context.Store.AppendAsync(first);
        await context.Store.AppendAsync(second);

        var entries = await context.Store.GetRecentAsync(context.Now);

        Assert.Equal(2, entries.Count);
        Assert.Contains(first, entries);
        Assert.Contains(second, entries);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var context = await CreateContextAsync();
        var older = CreateEntry(
            new DateOnly(2026, 9, 16),
            new DateTimeOffset(2026, 9, 16, 14, 0, 0, TimeSpan.Zero),
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.Confirmed,
            true);
        var newer = CreateEntry(
            new DateOnly(2026, 9, 16),
            new DateTimeOffset(2026, 9, 16, 16, 0, 0, TimeSpan.Zero),
            AttendanceActionType.ClockOut,
            AttendanceHistoryOutcome.Uncertain,
            false);

        await context.Store.AppendAsync(older);
        await context.Store.AppendAsync(newer);

        var entries = await context.Store.GetRecentAsync(context.Now);

        Assert.Equal(newer, entries[0]);
        Assert.Equal(older, entries[1]);
    }

    [Fact]
    public async Task GetRecentAsync_OldEntriesArePruned()
    {
        var context = await CreateContextAsync();
        var oldEntry = CreateEntry(
            new DateOnly(2026, 8, 1),
            context.Now.AddDays(-31),
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.NotCompleted,
            false);
        var currentEntry = CreateEntry(
            new DateOnly(2026, 9, 16),
            context.Now,
            AttendanceActionType.ClockOut,
            AttendanceHistoryOutcome.Confirmed,
            true);

        await context.Store.AppendAsync(oldEntry);
        await context.Store.AppendAsync(currentEntry);

        var entries = await context.Store.GetRecentAsync(context.Now);

        Assert.DoesNotContain(entries, entry => entry == oldEntry);
        Assert.Contains(currentEntry, entries);
    }

    [Fact]
    public async Task AppendAsync_PrunesPersistedExpiredEntries()
    {
        var context = await CreateContextAsync();
        var oldEntry = CreateEntry(
            new DateOnly(2026, 8, 1),
            context.Now.AddDays(-31),
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.NotCompleted,
            false);
        var freshEntry = CreateEntry(
            new DateOnly(2026, 9, 16),
            context.Now,
            AttendanceActionType.ClockOut,
            AttendanceHistoryOutcome.Confirmed,
            true);

        await context.Store.AppendAsync(oldEntry);
        await context.Store.AppendAsync(freshEntry);

        var envelope = await ReadEnvelopeAsync(context.FilePath);

        Assert.Equal(1, envelope.Entries.Count);
        Assert.Contains(freshEntry, envelope.Entries);
        Assert.DoesNotContain(envelope.Entries, entry => entry == oldEntry);
    }

    [Fact]
    public async Task AppendAsync_ConcurrentAppends_PreservesAllEntries()
    {
        var context = await CreateContextAsync();
        var entries = Enumerable.Range(0, 20)
            .Select(index => CreateEntry(
                new DateOnly(2026, 9, 16),
                context.Now.AddMinutes(index),
                index % 2 == 0 ? AttendanceActionType.ClockIn : AttendanceActionType.ClockOut,
                AttendanceHistoryOutcome.Confirmed,
                true))
            .ToArray();

        await Task.WhenAll(entries.Select(entry => context.Store.AppendAsync(entry)));

        var actual = await context.Store.GetRecentAsync(context.Now.AddHours(1));

        Assert.Equal(entries.Length, actual.Count);
        foreach (var entry in entries)
        {
            Assert.Contains(entry, actual);
        }
    }

    [Fact]
    public async Task GetRecentAsync_MalformedJson_ReturnsEmptyAndRecovers()
    {
        var context = await CreateContextAsync();
        await File.WriteAllTextAsync(context.FilePath, "{not-valid-json");

        var empty = await context.Store.GetRecentAsync(context.Now);

        Assert.Empty(empty);

        var entry = CreateEntry(
            new DateOnly(2026, 9, 16),
            context.Now,
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.Confirmed,
            true);

        await context.Store.AppendAsync(entry);

        var recovered = await context.Store.GetRecentAsync(context.Now);

        Assert.Single(recovered);
        Assert.Equal(entry, recovered[0]);
    }

    [Fact]
    public async Task AppendAsync_StoreWriteFailure_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var blockedSegment = Path.Combine(root, "blocked");
        await File.WriteAllTextAsync(blockedSegment, "blocked");

        var filePath = Path.Combine(blockedSegment, "attendance-history-v1.json");
        var store = new FileAttendanceHistoryStore(filePath);
        var entry = CreateEntry(
            new DateOnly(2026, 9, 16),
            DateTimeOffset.UtcNow,
            AttendanceActionType.ClockIn,
            AttendanceHistoryOutcome.Confirmed,
            true);

        await Assert.ThrowsAnyAsync<IOException>(
            () => store.AppendAsync(entry));
    }

    private static AttendanceHistoryEntry CreateEntry(
        DateOnly attendanceDate,
        DateTimeOffset occurredAtUtc,
        AttendanceActionType actionType,
        AttendanceHistoryOutcome outcome,
        bool providerConfirmed)
    {
        return new AttendanceHistoryEntry(
            attendanceDate,
            occurredAtUtc,
            actionType,
            outcome,
            providerConfirmed);
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var filePath = Path.Combine(root, "attendance-history-v1.json");
        var store = new FileAttendanceHistoryStore(filePath);
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        return new TestContext(root, filePath, store, now);
    }

    private static async Task<HistoryEnvelope> ReadEnvelopeAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<HistoryEnvelope>(json, JsonOptions)
            ?? new HistoryEnvelope();
    }

    private sealed record TestContext(
        string RootPath,
        string FilePath,
        FileAttendanceHistoryStore Store,
        DateTimeOffset Now);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private sealed class HistoryEnvelope
    {
        public int SchemaVersion { get; set; }

        public List<AttendanceHistoryEntry> Entries { get; set; } = [];
    }
}
