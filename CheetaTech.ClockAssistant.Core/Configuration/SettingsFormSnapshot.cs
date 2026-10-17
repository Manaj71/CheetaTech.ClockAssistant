using System.Globalization;

namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Value comparison of editable Settings fields.
/// Stores whether a replacement password was entered, never the password text.
/// </summary>
public sealed class SettingsFormSnapshot
{
    private readonly string _providerType;
    private readonly string _providerUrl;
    private readonly string _username;
    private readonly bool _hasReplacementPassword;
    private readonly DayOfWeek[] _workDays;
    private readonly TimeSpan? _clockInTime;
    private readonly TimeSpan? _clockOutTime;
    private readonly string _timeZoneId;
    private readonly string _notificationLeadMinutes;
    private readonly bool _highVisibilityReminders;
    private readonly string _executionMode;
    private readonly (DateOnly Date, ScheduleExceptionType Type)[] _scheduleExceptions;

    private SettingsFormSnapshot(
        string providerType,
        string providerUrl,
        string username,
        bool hasReplacementPassword,
        DayOfWeek[] workDays,
        TimeSpan? clockInTime,
        TimeSpan? clockOutTime,
        string timeZoneId,
        string notificationLeadMinutes,
        bool highVisibilityReminders,
        string executionMode,
        (DateOnly Date, ScheduleExceptionType Type)[] scheduleExceptions)
    {
        _providerType = providerType;
        _providerUrl = providerUrl;
        _username = username;
        _hasReplacementPassword = hasReplacementPassword;
        _workDays = workDays;
        _clockInTime = clockInTime;
        _clockOutTime = clockOutTime;
        _timeZoneId = timeZoneId;
        _notificationLeadMinutes = notificationLeadMinutes;
        _highVisibilityReminders = highVisibilityReminders;
        _executionMode = executionMode;
        _scheduleExceptions = scheduleExceptions;
    }

    public static SettingsFormSnapshot Capture(
        string? providerType,
        string? providerUrl,
        string? username,
        bool hasReplacementPassword,
        IEnumerable<DayOfWeek>? workDays,
        TimeSpan? clockInTime,
        TimeSpan? clockOutTime,
        string? timeZoneId,
        string? notificationLeadMinutesText,
        bool highVisibilityReminders,
        string? executionMode,
        IEnumerable<ScheduleDayException>? scheduleExceptions)
    {
        var days =
            (workDays ?? Array.Empty<DayOfWeek>())
            .Distinct()
            .OrderBy(day => day)
            .ToArray();

        var exceptions =
            (scheduleExceptions ?? Array.Empty<ScheduleDayException>())
            .Where(item => item is not null)
            .Select(item => (item.Date, item.Type))
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Type)
            .ToArray();

        return new SettingsFormSnapshot(
            providerType?.Trim() ?? string.Empty,
            providerUrl?.Trim() ?? string.Empty,
            username?.Trim() ?? string.Empty,
            hasReplacementPassword,
            days,
            clockInTime,
            clockOutTime,
            timeZoneId?.Trim() ?? string.Empty,
            CanonicalLeadMinutes(notificationLeadMinutesText),
            highVisibilityReminders,
            executionMode?.Trim() ?? string.Empty,
            exceptions);
    }

    public static bool AreEquivalent(
        SettingsFormSnapshot left,
        SettingsFormSnapshot right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(
                left._providerType,
                right._providerType,
                StringComparison.Ordinal)
            && string.Equals(
                left._providerUrl,
                right._providerUrl,
                StringComparison.Ordinal)
            && string.Equals(
                left._username,
                right._username,
                StringComparison.Ordinal)
            && left._hasReplacementPassword == right._hasReplacementPassword
            && left._workDays.SequenceEqual(right._workDays)
            && left._clockInTime == right._clockInTime
            && left._clockOutTime == right._clockOutTime
            && string.Equals(
                left._timeZoneId,
                right._timeZoneId,
                StringComparison.Ordinal)
            && string.Equals(
                left._notificationLeadMinutes,
                right._notificationLeadMinutes,
                StringComparison.Ordinal)
            && left._highVisibilityReminders == right._highVisibilityReminders
            && string.Equals(
                left._executionMode,
                right._executionMode,
                StringComparison.Ordinal)
            && left._scheduleExceptions.SequenceEqual(right._scheduleExceptions);
    }

    private static string CanonicalLeadMinutes(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (int.TryParse(
                trimmed,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var minutes))
        {
            return minutes.ToString(CultureInfo.InvariantCulture);
        }

        return trimmed;
    }
}
