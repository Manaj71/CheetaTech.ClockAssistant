using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.History;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class AttendanceHistoryRecorderTests
{
    [Theory]
    [InlineData(AttendanceActionExecutionStatus.Succeeded, true, AttendanceHistoryOutcome.Confirmed)]
    [InlineData(AttendanceActionExecutionStatus.PersistenceFailed, true, AttendanceHistoryOutcome.ConfirmedNeedsAttention)]
    [InlineData(AttendanceActionExecutionStatus.PersistenceFailed, false, AttendanceHistoryOutcome.NotCompleted)]
    [InlineData(AttendanceActionExecutionStatus.ProviderRejected, false, AttendanceHistoryOutcome.NotConfirmed)]
    [InlineData(AttendanceActionExecutionStatus.ProviderUnknown, false, AttendanceHistoryOutcome.Uncertain)]
    [InlineData(AttendanceActionExecutionStatus.ExecutionDisabled, false, AttendanceHistoryOutcome.NotCompleted)]
    [InlineData(AttendanceActionExecutionStatus.NotEligible, false, AttendanceHistoryOutcome.NotCompleted)]
    [InlineData(AttendanceActionExecutionStatus.MissingCredentials, false, AttendanceHistoryOutcome.NotCompleted)]
    [InlineData(AttendanceActionExecutionStatus.MissingConfiguration, false, AttendanceHistoryOutcome.NotCompleted)]
    [InlineData(AttendanceActionExecutionStatus.CredentialReadFailed, false, AttendanceHistoryOutcome.NotCompleted)]
    [InlineData(AttendanceActionExecutionStatus.ProviderResolutionFailed, false, AttendanceHistoryOutcome.NotCompleted)]
    public async Task TryRecordAsync_MapsOutcomeCorrectly(
        AttendanceActionExecutionStatus status,
        bool providerConfirmed,
        AttendanceHistoryOutcome expectedOutcome)
    {
        var store = new CapturingAttendanceHistoryStore();
        var recorder = new AttendanceHistoryRecorder(store);
        var occurredAtUtc = new DateTimeOffset(2026, 9, 16, 14, 30, 45, TimeSpan.Zero);
        var attendanceDate = new DateOnly(2026, 9, 16);

        var result = new AttendanceActionExecutionResult(
            AttendanceActionType.ClockIn,
            status,
            attendanceDate,
            ProviderRequestSent: true,
            ProviderConfirmed: providerConfirmed);

        var recorded = await recorder.TryRecordAsync(result, occurredAtUtc);

        Assert.True(recorded);
        var entry = Assert.Single(store.Entries);
        Assert.Equal(expectedOutcome, entry.Outcome);
        Assert.Equal(attendanceDate, entry.AttendanceDate);
        Assert.Equal(occurredAtUtc, entry.OccurredAtUtc);
        Assert.Equal(AttendanceActionType.ClockIn, entry.ActionType);
        Assert.Equal(providerConfirmed, entry.ProviderConfirmed);
    }

    [Fact]
    public async Task TryRecordAsync_ThrowingStore_ReturnsFalseAndDoesNotThrow()
    {
        var recorder = new AttendanceHistoryRecorder(new ThrowingAttendanceHistoryStore());
        var result = new AttendanceActionExecutionResult(
            AttendanceActionType.ClockOut,
            AttendanceActionExecutionStatus.Succeeded,
            new DateOnly(2026, 9, 16),
            ProviderRequestSent: true,
            ProviderConfirmed: true);

        var recorded = await recorder.TryRecordAsync(result, DateTimeOffset.UtcNow);

        Assert.False(recorded);
    }

    private sealed class CapturingAttendanceHistoryStore
        : IAttendanceHistoryStore
    {
        public List<AttendanceHistoryEntry> Entries { get; } = new();

        public Task AppendAsync(
            AttendanceHistoryEntry entry,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AttendanceHistoryEntry>> GetRecentAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AttendanceHistoryEntry>>(Entries);
        }
    }

    private sealed class ThrowingAttendanceHistoryStore
        : IAttendanceHistoryStore
    {
        public Task AppendAsync(
            AttendanceHistoryEntry entry,
            CancellationToken cancellationToken = default)
        {
            throw new IOException("Synthetic history write failure.");
        }

        public Task<IReadOnlyList<AttendanceHistoryEntry>> GetRecentAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AttendanceHistoryEntry>>([]);
        }
    }
}
