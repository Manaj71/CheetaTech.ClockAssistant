using System.Linq;
using System.Text.Json;
using CheetaTech.ClockAssistant.Core.Attendance;

namespace CheetaTech.ClockAssistant.Core.History;

public sealed class FileAttendanceHistoryStore
    : IAttendanceHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _filePath;
    private readonly int _retentionDays;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public FileAttendanceHistoryStore(
        string filePath,
        int retentionDays = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionDays));
        }

        _filePath = filePath;
        _retentionDays = retentionDays;
    }

    public async Task AppendAsync(
        AttendanceHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _writeGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var tempFilePath = CreateTemporaryFilePath();

        try
        {
            var entries =
                await LoadEntriesUnsafeAsync(cancellationToken)
                    .ConfigureAwait(false);

            entries.Add(entry);

            var normalizedEntries =
                NormalizeEntries(
                    entries,
                    entry.OccurredAtUtc);

            await WriteEnvelopeAsync(
                    normalizedEntries,
                    tempFilePath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();

            TryDeleteTemporaryFile(tempFilePath);
        }
    }

    public async Task<IReadOnlyList<AttendanceHistoryEntry>> GetRecentAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _writeGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var entries =
                await LoadEntriesUnsafeAsync(cancellationToken)
                    .ConfigureAwait(false);

            var retainedEntries =
                NormalizeEntries(
                    entries,
                    utcNow);

            return retainedEntries;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task<List<AttendanceHistoryEntry>> LoadEntriesUnsafeAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                        _filePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var envelope =
                JsonSerializer.Deserialize<HistoryFileEnvelope>(
                    json,
                    SerializerOptions);

            if (envelope is null ||
                envelope.SchemaVersion != 1 ||
                envelope.Entries is null)
            {
                return [];
            }

            return [.. envelope.Entries];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }

    private async Task WriteEnvelopeAsync(
        IReadOnlyList<AttendanceHistoryEntry> entries,
        string tempFilePath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(_filePath) ?? string.Empty);

        var envelope =
            new HistoryFileEnvelope
            {
                SchemaVersion = 1,
                Entries = [.. entries]
            };

        var json =
            JsonSerializer.Serialize(
                envelope,
                SerializerOptions);

        await File.WriteAllTextAsync(
                tempFilePath,
                json,
                cancellationToken)
            .ConfigureAwait(false);

        File.Move(
            tempFilePath,
            _filePath,
            true);
    }

    private List<AttendanceHistoryEntry> NormalizeEntries(
        IEnumerable<AttendanceHistoryEntry> entries,
        DateTimeOffset utcNow)
    {
        var cutoffUtc =
            utcNow.ToUniversalTime()
                .AddDays(-_retentionDays);

        return entries
            .Where(entry => entry.OccurredAtUtc >= cutoffUtc)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.AttendanceDate)
            .ThenBy(entry => entry.ActionType)
            .ThenBy(entry => entry.Outcome)
            .ThenByDescending(entry => entry.ProviderConfirmed)
            .ToList();
    }

    private string CreateTemporaryFilePath()
    {
        var directory =
            Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException(
                "History file path must include a directory.");

        var fileName =
            Path.GetFileNameWithoutExtension(_filePath);

        return Path.Combine(
            directory,
            $"{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void TryDeleteTemporaryFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
        }
    }

    private sealed class HistoryFileEnvelope
    {
        public int SchemaVersion { get; set; }

        public List<AttendanceHistoryEntry> Entries { get; set; } = [];
    }
}
