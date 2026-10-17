namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Pure helpers for one-exception-per-date list edits.
/// </summary>
public static class ScheduleExceptionListEditor
{
    public static IReadOnlyList<ScheduleDayException> Upsert(
        IEnumerable<ScheduleDayException>? existing,
        DateOnly date,
        ScheduleExceptionType type)
    {
        var items =
            (existing ?? Array.Empty<ScheduleDayException>())
            .Where(item => item.Date != date)
            .ToList();

        items.Add(
            new ScheduleDayException
            {
                Date = date,
                Type = type
            });

        return items
            .OrderBy(item => item.Date)
            .ToArray();
    }

    public static IReadOnlyList<ScheduleDayException> Remove(
        IEnumerable<ScheduleDayException>? existing,
        DateOnly date)
    {
        return (existing ?? Array.Empty<ScheduleDayException>())
            .Where(item => item.Date != date)
            .OrderBy(item => item.Date)
            .ToArray();
    }
}
