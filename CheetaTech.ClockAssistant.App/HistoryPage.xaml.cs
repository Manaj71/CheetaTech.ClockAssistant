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
                .Select((group, index) =>
                    new HistoryDayGroup(
                        group.Key,
                        isAlternateBand: index % 2 == 1,
                        group
                            .OrderByDescending(
                                entry => entry.OccurredAtUtc)
                            .Select(entry =>
                                CreateRow(
                                    entry,
                                    isAlternateBand: index % 2 == 1))))
                .ToList();

        return new ObservableCollection<HistoryDayGroup>(
            groupedEntries);
    }

    private static HistoryRow CreateRow(
        AttendanceHistoryEntry entry,
        bool isAlternateBand)
    {
        var localTime =
            entry.OccurredAtUtc.ToLocalTime();

        var actionLabel = GetActionLabel(entry.ActionType);
        var timeLabel = localTime.ToString(
            "t",
            CultureInfo.CurrentCulture);
        var status = GetStatusDisplay(entry.Outcome);

        return new HistoryRow(
            actionLabel,
            timeLabel,
            status.Letter,
            status.Description,
            $"{actionLabel}, {timeLabel}, {status.Description}",
            isAlternateBand);
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

    private static (string Letter, string Description) GetStatusDisplay(
        AttendanceHistoryOutcome outcome)
    {
        return outcome switch
        {
            AttendanceHistoryOutcome.Confirmed =>
                ("C", "Confirmed"),

            AttendanceHistoryOutcome.ConfirmedNeedsAttention =>
                ("A", "Confirmed, needs attention"),

            AttendanceHistoryOutcome.NotConfirmed =>
                ("F", "Not confirmed"),

            AttendanceHistoryOutcome.Uncertain =>
                ("U", "Uncertain"),

            AttendanceHistoryOutcome.NotCompleted =>
                ("N", "Not completed"),

            _ =>
                ("?", outcome.ToString())
        };
    }

    public sealed class HistoryDayGroup
        : ObservableCollection<HistoryRow>
    {
        public HistoryDayGroup(
            DateOnly attendanceDate,
            bool isAlternateBand,
            IEnumerable<HistoryRow> items)
            : base(items)
        {
            DateLabel =
                attendanceDate.ToString(
                    "D",
                    CultureInfo.CurrentCulture);
            IsAlternateBand = isAlternateBand;
        }

        public string DateLabel { get; }

        public bool IsAlternateBand { get; }
    }

    public sealed class HistoryRow
    {
        public HistoryRow(
            string actionLabel,
            string timeLabel,
            string statusLetter,
            string statusDescription,
            string accessibilityLabel,
            bool isAlternateBand)
        {
            ActionLabel = actionLabel;
            TimeLabel = timeLabel;
            StatusLetter = statusLetter;
            StatusDescription = statusDescription;
            AccessibilityLabel = accessibilityLabel;
            IsAlternateBand = isAlternateBand;
        }

        public string ActionLabel { get; }

        public string TimeLabel { get; }

        public string StatusLetter { get; }

        public string StatusDescription { get; }

        public string AccessibilityLabel { get; }

        public bool IsAlternateBand { get; }
    }
}
