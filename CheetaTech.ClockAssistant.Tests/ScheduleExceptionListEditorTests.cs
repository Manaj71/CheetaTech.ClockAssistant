using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class ScheduleExceptionListEditorTests
{
    [Fact]
    public void Upsert_AddsException_AndKeepsSortedUniqueDates()
    {
        var existing = new[]
        {
            new ScheduleDayException
            {
                Date = new DateOnly(2026, 10, 15),
                Type = ScheduleExceptionType.Vacation
            }
        };

        var result = ScheduleExceptionListEditor.Upsert(
            existing,
            new DateOnly(2026, 10, 13),
            ScheduleExceptionType.StatutoryHoliday);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 10, 13), result[0].Date);
        Assert.Equal(
            ScheduleExceptionType.StatutoryHoliday,
            result[0].Type);
        Assert.Equal(new DateOnly(2026, 10, 15), result[1].Date);
    }

    [Fact]
    public void Upsert_SameDate_ReplacesTypeWithoutDuplicate()
    {
        var existing = new[]
        {
            new ScheduleDayException
            {
                Date = new DateOnly(2026, 10, 13),
                Type = ScheduleExceptionType.Vacation
            }
        };

        var result = ScheduleExceptionListEditor.Upsert(
            existing,
            new DateOnly(2026, 10, 13),
            ScheduleExceptionType.SickDay);

        Assert.Single(result);
        Assert.Equal(ScheduleExceptionType.SickDay, result[0].Type);
    }

    [Fact]
    public void Remove_DeletesMatchingDate_PreservesOthers()
    {
        var existing = new[]
        {
            new ScheduleDayException
            {
                Date = new DateOnly(2026, 10, 13),
                Type = ScheduleExceptionType.StatutoryHoliday
            },
            new ScheduleDayException
            {
                Date = new DateOnly(2026, 10, 14),
                Type = ScheduleExceptionType.Other
            }
        };

        var result = ScheduleExceptionListEditor.Remove(
            existing,
            new DateOnly(2026, 10, 13));

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 10, 14), result[0].Date);
    }

    [Fact]
    public void Upsert_NullExisting_StartsEmptyList()
    {
        var result = ScheduleExceptionListEditor.Upsert(
            null,
            new DateOnly(2026, 10, 13),
            ScheduleExceptionType.RegularDayOff);

        Assert.Single(result);
        Assert.Equal(
            ScheduleExceptionType.RegularDayOff,
            result[0].Type);
    }
}
