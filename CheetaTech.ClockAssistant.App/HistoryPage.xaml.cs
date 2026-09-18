using System.Collections.ObjectModel;
using System.Globalization;

using CheetaTech.ClockAssistant.Core.Attendance;
using CheetaTech.ClockAssistant.Core.History;

namespace CheetaTech.ClockAssistant.App;

public partial class HistoryPage : ContentPage
{
    private readonly IAttendanceHistoryStore _historyStore;

    public HistoryPage(
        IAttendanceHistoryStore historyStore)
    {
        InitializeComponent();

        _historyStore =
            historyStore
            ?? throw new ArgumentNullException(
                nameof(historyStore));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var entries =
                await _historyStore.GetRecentAsync(
                    DateTimeOffset.UtcNow);

            var groups = BuildGroups(entries);

            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;

            EmptyStatePanel.IsVisible = groups.Count == 0;
            HistoryCollectionView.IsVisible = groups.Count > 0;
            HistoryCollectionView.ItemsSource = groups;
        }
        catch
        {
            HistoryCollectionView.ItemsSource = null;
            HistoryCollectionView.IsVisible = false;
            EmptyStatePanel.IsVisible = false;

            ErrorLabel.Text =
                "History is temporarily unavailable.\n" +
                "Your attendance functions are not affected.";

            ErrorLabel.IsVisible = true;
        }
    }

    private static ObservableCollection<HistoryDayGroup> BuildGroups(
        IReadOnlyList<AttendanceHistoryEntry> entries)
    {
        var groupedEntries =
            entries
                .GroupBy(entry => entry.AttendanceDate)
                .OrderByDescending(group => group.Key)
                .Select(group =>
                    new HistoryDayGroup(
                        group.Key,
                        group
                            .OrderByDescending(
                                entry => entry.OccurredAtUtc)
                            .Select(CreateRow)))
                .ToList();

        return new ObservableCollection<HistoryDayGroup>(
            groupedEntries);
    }

    private static HistoryRow CreateRow(
        AttendanceHistoryEntry entry)
    {
        var localTime =
            entry.OccurredAtUtc.ToLocalTime();

        return new HistoryRow(
            GetActionLabel(entry.ActionType),
            localTime.ToString(
                "t",
                CultureInfo.CurrentCulture),
            GetOutcomeLabel(entry.Outcome));
    }

    private static string GetActionLabel(
        AttendanceActionType actionType)
    {
        return actionType switch
        {
            AttendanceActionType.ClockIn => "Clock In",
            AttendanceActionType.ClockOut => "Clock Out",
            _ => actionType.ToString()
        };
    }

    private static string GetOutcomeLabel(
        AttendanceHistoryOutcome outcome)
    {
        return outcome switch
        {
            AttendanceHistoryOutcome.Confirmed =>
                "Confirmed",

            AttendanceHistoryOutcome.ConfirmedNeedsAttention =>
                "Confirmed — local save issue",

            AttendanceHistoryOutcome.NotConfirmed =>
                "Not confirmed",

            AttendanceHistoryOutcome.Uncertain =>
                "Status uncertain",

            AttendanceHistoryOutcome.NotCompleted =>
                "Not completed",

            _ => outcome.ToString()
        };
    }

    public sealed class HistoryDayGroup
        : ObservableCollection<HistoryRow>
    {
        public HistoryDayGroup(
            DateOnly attendanceDate,
            IEnumerable<HistoryRow> items)
            : base(items)
        {
            DateLabel =
                attendanceDate.ToString(
                    "D",
                    CultureInfo.CurrentCulture);
        }

        public string DateLabel { get; }
    }

    public sealed class HistoryRow
    {
        public HistoryRow(
            string actionLabel,
            string timeLabel,
            string outcomeLabel)
        {
            ActionLabel = actionLabel;
            TimeLabel = timeLabel;
            OutcomeLabel = outcomeLabel;
        }

        public string ActionLabel { get; }

        public string TimeLabel { get; }

        public string OutcomeLabel { get; }
    }
}