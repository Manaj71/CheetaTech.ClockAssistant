namespace CheetaTech.ClockAssistant.Core.Configuration;

/// <summary>
/// Local reminder-schedule exception category.
/// These values are not UKG leave records.
/// </summary>
public enum ScheduleExceptionType
{
    StatutoryHoliday = 0,
    Vacation = 1,
    SickDay = 2,
    RegularDayOff = 3,
    Other = 4
}
