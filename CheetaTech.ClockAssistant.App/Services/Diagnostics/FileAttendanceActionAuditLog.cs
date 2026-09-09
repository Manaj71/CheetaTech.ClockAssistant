using System.Globalization;
using System.Text;
using CheetaTech.ClockAssistant.Core.Attendance;
using Microsoft.Maui.Storage;

namespace CheetaTech.ClockAssistant.App.Services.Diagnostics;

public sealed class FileAttendanceActionAuditLog
    : IAttendanceActionAuditLog
{
    private const string FileName =
        "attendance-action-audit.log";

    private readonly SemaphoreSlim _writeGate =
        new(1, 1);

    public FileAttendanceActionAuditLog()
    {
        LogFilePath =
            Path.Combine(
                FileSystem.AppDataDirectory,
                FileName);
    }

    public string LogFilePath { get; }

    public Task AppendReceivedAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var line =
            string.Join(
                " | ",
                $"Utc={FormatUtc(utcNow)}",
                "Event=ACTION_RECEIVED",
                $"Action={actionType}");

        return AppendLineAsync(
            line,
            cancellationToken);
    }

    public Task AppendResultAsync(
        AttendanceActionExecutionResult result,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var attendanceDate =
            result.AttendanceDate?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture)
            ?? "None";

        var line =
            string.Join(
                " | ",
                $"Utc={FormatUtc(utcNow)}",
                "Event=ACTION_RESULT",
                $"Action={result.ActionType}",
                $"AttendanceDate={attendanceDate}",
                $"Status={result.Status}",
                $"ProviderRequestSent={result.ProviderRequestSent}",
                $"ProviderConfirmed={result.ProviderConfirmed}");

        return AppendLineAsync(
            line,
            cancellationToken);
    }

    public Task AppendExceptionAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        string stage,
        string exceptionType,
        CancellationToken cancellationToken = default)
    {
        var safeStage =
            string.IsNullOrWhiteSpace(stage)
                ? "Unknown"
                : stage.Replace("|", "_", StringComparison.Ordinal);

        var safeExceptionType =
            string.IsNullOrWhiteSpace(exceptionType)
                ? "Unknown"
                : exceptionType.Replace("|", "_", StringComparison.Ordinal);

        var line =
            string.Join(
                " | ",
                $"Utc={FormatUtc(utcNow)}",
                "Event=ACTION_EXCEPTION",
                $"Action={actionType}",
                $"Stage={safeStage}",
                $"ExceptionType={safeExceptionType}");

        return AppendLineAsync(
            line,
            cancellationToken);
    }
    private async Task AppendLineAsync(
        string line,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(
            cancellationToken);

        try
        {
            await File.AppendAllTextAsync(
                LogFilePath,
                line + Environment.NewLine,
                Encoding.UTF8,
                cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static string FormatUtc(
        DateTimeOffset utcNow)
    {
        return utcNow
            .ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);
    }
}