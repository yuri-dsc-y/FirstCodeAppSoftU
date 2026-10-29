using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WeightTracker.Models;
using WeightTracker.Services;
using Path = System.IO.Path;

namespace WeightTracker;

public partial class MainWindow : Window
{
    private static readonly Dictionary<string, (string Accent, string Soft, string Light, string Dark)> Themes = new()
    {
        ["Forest Green"] = ("#237A57", "#E8F3EC", "#F6F8F4", "#202A24"),
        ["Ocean Blue"] = ("#2676A8", "#E8F2F8", "#F5F8FA", "#202A31"),
        ["Midnight Violet"] = ("#7457A6", "#F0EBF7", "#F7F6FA", "#282431"),
        ["Warm Amber"] = ("#A96518", "#FBF0DF", "#FAF7F0", "#30281D"),
        ["Monochrome"] = ("#4D5960", "#E9EDEF", "#F5F6F6", "#24292C")
    };

    private readonly TrackerStore _store;
    private readonly string _dataPath;
    private readonly DispatcherTimer _dayTimer;
    private TrackerSettings _settings;
    private bool _initializing = true;
    private DateOnly _lastDay = DateOnly.FromDateTime(DateTime.Today);
    private readonly ObservableCollection<MeasurementRow> _history = [];

    public MainWindow()
    {
        InitializeComponent();
        _dataPath = ResolveDataPath();
        try
        {
            _store = new TrackerStore(_dataPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show($"Cannot use the data folder:\n{Path.GetDirectoryName(_dataPath)}\n\n{error.Message}\n\nChoose another persistent folder.",
                "Data folder unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            var folderDialog = new OpenFolderDialog { Title = "Choose a persistent data folder" };
            if (folderDialog.ShowDialog() != true)
            {
                Application.Current.Shutdown();
                throw;
            }
            _dataPath = Path.Combine(folderDialog.FolderName, "weight_tracker.db");
            _store = new TrackerStore(_dataPath);
        }

        _settings = _store.GetSettings();
        EntryDate.SelectedDate = DateTime.Today;
        DataPathLabel.Text = _dataPath;
        HistoryGrid.ItemsSource = _history;
        LoadSettingsControls();
        _initializing = false;
        ApplyTheme();
        RefreshAll();
        _dayTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _dayTimer.Tick += (_, _) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today != _lastDay) { _lastDay = today; RefreshAll(); }
        };
        _dayTimer.Start();
        Closed += (_, _) => { _dayTimer.Stop(); _store.Dispose(); };
    }

    private static string ResolveDataPath()
    {
        var appFolder = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(appFolder, "portable.flag")))
            return Path.Combine(appFolder, "data", "weight_tracker.db");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = string.IsNullOrWhiteSpace(local) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : local;
        return Path.Combine(basePath, "WeightTracker", "weight_tracker.db");
    }

    private static string ComboText(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    private void LoadSettingsControls()
    {
        SelectCombo(AppearanceSelector, _settings.Appearance);
        SelectCombo(SchemeSelector, _settings.Scheme);
        SelectCombo(UnitSelector, _settings.Unit);
        SelectCombo(PrecisionSelector, _settings.Precision.ToString(CultureInfo.InvariantCulture));
        SelectCombo(DateFormatSelector, _settings.DateFormat);
    }

    private static void SelectCombo(ComboBox combo, string value) =>
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Content?.ToString() == value);

    private void ApplyTheme()
    {
        var colors = Themes[_settings.Scheme];
        var dark = _settings.Appearance == "Dark";
        Resources["AccentBrush"] = CreateBrush(colors.Accent);
        Resources["AccentSoftBrush"] = CreateBrush(dark ? "#37433D" : colors.Soft);
        Resources["WindowBrush"] = CreateBrush(dark ? colors.Dark : colors.Light);
        Resources["SurfaceBrush"] = CreateBrush(dark ? "#303A35" : "#FFFFFF");
        Resources["TextBrush"] = CreateBrush(dark ? "#EDF1EE" : "#202A24");
        Resources["MutedBrush"] = CreateBrush(dark ? "#AEB9B2" : "#728078");
    }

    private static SolidColorBrush CreateBrush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private string DisplayWeight(double kg) => $"{WeightService.FromKilograms(kg, _settings.Unit).ToString($"F{_settings.Precision}", CultureInfo.CurrentCulture)} {_settings.Unit}";
    private string DisplayChange(double deltaKg)
    {
        var value = WeightService.FromKilograms(deltaKg, _settings.Unit);
        var sign = value > 0 ? "+" : "";
        return $"{sign}{value.ToString($"F{_settings.Precision}", CultureInfo.CurrentCulture)} {_settings.Unit}";
    }
    private string FormatDate(DateOnly day) => day.ToDateTime(TimeOnly.MinValue).ToString(_settings.DateFormat, CultureInfo.CurrentCulture);

    private void RefreshAll()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        TodayLabel.Text = today.ToDateTime(TimeOnly.MinValue).ToString("dddd, " + _settings.DateFormat, CultureInfo.CurrentCulture);
        var quote = QuoteService.ForDay(QuoteService.Load(Path.Combine(AppContext.BaseDirectory, "Resources", "quotes.json")), today);
        QuoteText.Text = quote is null ? "A record of your own progress, one day at a time." : $"\"{quote.Text}\"";
        QuoteAttribution.Text = quote is null ? "No verified quotations bundled" :
            string.IsNullOrWhiteSpace(quote.SourceTitle) ? quote.Author : $"{quote.Author} | {quote.SourceTitle}";
        var all = _store.GetMeasurements().OrderBy(item => item.Day).ToArray();
        CountValue.Text = all.Length.ToString(CultureInfo.InvariantCulture);
        if (all.Length == 0)
        {
            LatestValue.Text = PreviousChange.Text = PeriodChange.Text = HighestValue.Text = LowestValue.Text = "--";
            LatestDetail.Text = PreviousDetail.Text = PeriodDetail.Text = "No measurements yet";
        }
        else
        {
            var latest = all[^1];
            LatestValue.Text = DisplayWeight(latest.WeightKg);
            LatestDetail.Text = $"Latest entry {FormatDate(latest.Day)}";
            if (all.Length > 1)
            {
                PreviousChange.Text = DisplayChange(latest.WeightKg - all[^2].WeightKg);
                PreviousDetail.Text = $"Since {FormatDate(all[^2].Day)}";
            }
            else { PreviousChange.Text = "--"; PreviousDetail.Text = "One entry so far"; }
            var highest = all.MaxBy(item => item.WeightKg)!;
            var lowest = all.MinBy(item => item.WeightKg)!;
            HighestValue.Text = $"{DisplayWeight(highest.WeightKg)} | {FormatDate(highest.Day)}";
            LowestValue.Text = $"{DisplayWeight(lowest.WeightKg)} | {FormatDate(lowest.Day)}";
        }
        var (monday, sunday) = WeightService.WeekBounds(today);
        var current = all.Where(item => item.Day >= monday && item.Day <= sunday).ToArray();
        CurrentAverage.Text = current.Length == 0 ? "--" : DisplayWeight(current.Average(item => item.WeightKg));
        CurrentAverageDetail.Text = current.Length == 0 ? "No measurements this week" : $"Current week | {current.Length} measurements";
        var prior = _store.GetWeeklyAverages().FirstOrDefault(item => item.Monday == monday.AddDays(-7));
        PriorAverage.Text = prior is null ? "--" : DisplayWeight(prior.AverageKg);
        PriorAverageDetail.Text = prior is null ? "No data" : $"{prior.MeasurementCount} measurements";
        RecentGrid.ItemsSource = all.Reverse().Take(5).Select(ToRow).ToArray();
        RefreshHistory(); RefreshWeekly(); RefreshPeriod(); DrawChart();
    }

    private MeasurementRow ToRow(Measurement item) => new(item, FormatDate(item.Day), DisplayWeight(item.WeightKg));

    private void RefreshHistory()
    {
        var filter = HistoryFilter.Text.Trim();
        var rows = _store.GetMeasurements().Where(item => filter.Length == 0 ||
            FormatDate(item.Day).Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
            item.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            item.Note.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
        _history.Clear();
        foreach (var row in rows) _history.Add(ToRow(row));
    }

    private void RefreshWeekly()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentMonday = WeightService.WeekBounds(today).Monday;
        WeeklyGrid.ItemsSource = _store.GetWeeklyAverages().Select(item => new WeeklyRow(
            FormatDate(item.Monday), FormatDate(item.Monday.AddDays(6)), DisplayWeight(item.AverageKg), item.MeasurementCount,
            item.Monday == currentMonday ? "Current, incomplete" : item.Monday.AddDays(6) < today ? "Completed" : "In progress")).ToArray();
    }

    private (DateOnly? Start, DateOnly End) SelectedRange()
    {
        var selected = ComboText(RangeSelector);
        if (selected == "Custom")
            return (RangeStart.SelectedDate is { } start ? DateOnly.FromDateTime(start) : DateOnly.FromDateTime(DateTime.Today.AddDays(-29)),
                RangeEnd.SelectedDate is { } end ? DateOnly.FromDateTime(end) : DateOnly.FromDateTime(DateTime.Today));
        return WeightService.DateRange(string.IsNullOrEmpty(selected) ? "All time" : selected, DateOnly.FromDateTime(DateTime.Today));
    }

    private void RefreshPeriod()
    {
        var (start, end) = SelectedRange();
        if (start is not null && start.Value > end) { PeriodChange.Text = "--"; PeriodDetail.Text = "Start date must be before end date"; return; }
        var rows = _store.GetMeasurements(start, end).OrderBy(item => item.Day).ToArray();
        var change = WeightService.Change(rows, _settings.Unit);
        PeriodChange.Text = change is null ? "--" : DisplayChange(rows[^1].WeightKg - rows[0].WeightKg);
        PeriodDetail.Text = rows.Length < 2 ? "Needs two measurements" : $"{FormatDate(rows[0].Day)} to {FormatDate(rows[^1].Day)}";
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();
        var (start, end) = SelectedRange();
        if (start is not null && start.Value > end) { AddChartText("Start date must be before end date", 20, 40, 13); return; }
        var rows = _store.GetMeasurements(start, end).OrderBy(item => item.Day).ToArray();
        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        if (width < 130 || height < 130) return;
        if (rows.Length == 0)
        {
            AddChartText(_store.GetMeasurements().Count == 0 ? "Add a measurement to begin your chart." : "No measurements in this date range.", 24, height / 2, 14);
            return;
        }
        const double left = 55, right = 18, top = 36, bottom = 40;
        var plotWidth = width - left - right;
        var plotHeight = height - top - bottom;
        var weekly = _store.GetWeeklyAverages().Where(item => (start is null || item.Monday >= start.Value) && item.Monday <= end).OrderBy(item => item.Monday).ToArray();
        var plottedWeights = rows.Select(item => item.WeightKg)
            .Concat(weekly.Select(item => item.AverageKg)).ToArray();
        var minimumWeight = plottedWeights.Min();
        var maximumWeight = plottedWeights.Max();
        var padding = Math.Max((maximumWeight - minimumWeight) * 0.15, maximumWeight * 0.02);
        var yMinimum = Math.Max(0, minimumWeight - padding);
        var yMaximum = maximumWeight + padding;
        var firstDay = Math.Min(rows[0].Day.DayNumber,
            weekly.Length == 0 ? rows[0].Day.DayNumber : weekly[0].Monday.DayNumber);
        var lastDay = Math.Max(rows[^1].Day.DayNumber,
            weekly.Length == 0 ? rows[^1].Day.DayNumber : weekly[^1].Monday.DayNumber);
        var daySpan = Math.Max(1, lastDay - firstDay);
        const double markerInset = 6;
        double X(DateOnly day) => left + markerInset +
            (day.DayNumber - firstDay) / (double)daySpan * (plotWidth - markerInset * 2);
        double Y(double kg) => top + (yMaximum - kg) / (yMaximum - yMinimum) * plotHeight;
        for (var tick = 0; tick <= 4; tick++)
        {
            var y = top + tick / 4d * plotHeight;
            var value = yMaximum - tick / 4d * (yMaximum - yMinimum);
            ChartCanvas.Children.Add(new Line
            {
                X1 = left, Y1 = y, X2 = left + plotWidth, Y2 = y,
                Stroke = CreateBrush("#22879086"), StrokeThickness = 1
            });
            AddChartText(
                $"{WeightService.FromKilograms(value, _settings.Unit).ToString($"F{_settings.Precision}", CultureInfo.CurrentCulture)}",
                2, y - 7, 9);
        }
        ChartCanvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = top + plotHeight, Stroke = CreateBrush("#748078") });
        ChartCanvas.Children.Add(new Line { X1 = left, Y1 = top + plotHeight, X2 = left + plotWidth, Y2 = top + plotHeight, Stroke = CreateBrush("#748078") });
        AddChartText(_settings.Unit, 2, top - 20, 9);
        AddChartText(FormatDate(DateOnly.FromDayNumber(firstDay)), left, top + plotHeight + 8, 10);
        AddChartText(FormatDate(DateOnly.FromDayNumber(lastDay)), left + plotWidth - 90, top + plotHeight + 8, 10);
        AddChartText("* Measurements", left + 4, 5, 10, CreateBrush(Themes[_settings.Scheme].Accent));
        AddChartText("+ Weekly averages", left + 120, 5, 10, CreateBrush("#D18B36"));
        if (rows.Length > 1)
        {
            var series = new Polyline { Stroke = CreateBrush(Themes[_settings.Scheme].Accent), StrokeThickness = 2 };
            foreach (var row in rows) series.Points.Add(new Point(X(row.Day), Y(row.WeightKg)));
            ChartCanvas.Children.Add(series);
        }
        foreach (var row in rows)
        {
            var marker = new Ellipse { Width = 9, Height = 9, Fill = CreateBrush(Themes[_settings.Scheme].Accent), ToolTip = $"{FormatDate(row.Day)} | {DisplayWeight(row.WeightKg)}" };
            Canvas.SetLeft(marker, X(row.Day) - 4.5); Canvas.SetTop(marker, Y(row.WeightKg) - 4.5); ChartCanvas.Children.Add(marker);
        }
        if (weekly.Length > 1)
        {
            var series = new Polyline { Stroke = CreateBrush("#D18B36"), StrokeThickness = 2, StrokeDashArray = [5, 3] };
            foreach (var row in weekly) series.Points.Add(new Point(X(row.Monday), Y(row.AverageKg)));
            ChartCanvas.Children.Add(series);
        }
        var weeklyLabelInterval = weekly.Length == 0 ? 1 :
            Math.Max(1, (int)Math.Ceiling(64 / Math.Max(1, plotWidth * 7 / daySpan)));
        var lastLabeledX = double.NegativeInfinity;
        for (var index = 0; index < weekly.Length; index++)
        {
            var row = weekly[index];
            var markerX = X(row.Monday);
            var markerY = Y(row.AverageKg);
            var marker = new Polygon { Points = new PointCollection([new Point(5, 0), new Point(10, 5), new Point(5, 10), new Point(0, 5)]),
                Fill = CreateBrush("#D18B36"), ToolTip = $"Week of {FormatDate(row.Monday)} | {DisplayWeight(row.AverageKg)} | {row.MeasurementCount} measurements" };
            Canvas.SetLeft(marker, markerX - 5); Canvas.SetTop(marker, markerY - 5); ChartCanvas.Children.Add(marker);
            var isLast = index == weekly.Length - 1;
            if (index % weeklyLabelInterval != 0 &&
                (!isLast || markerX - lastLabeledX < 64))
                continue;
            var label = new TextBlock
            {
                Text = DisplayWeight(row.AverageKg),
                Width = 68,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Foreground = CreateBrush("#A96518"),
                Background = (Brush)FindResource("SurfaceBrush"),
                ToolTip = $"{FormatDate(row.Monday)} | {row.MeasurementCount} measurements"
            };
            Canvas.SetLeft(label, Math.Clamp(markerX - label.Width / 2, left, left + plotWidth - label.Width));
            Canvas.SetTop(label, Math.Max(18, markerY - 21));
            ChartCanvas.Children.Add(label);
            lastLabeledX = markerX;
        }
    }

    private void AddChartText(string text, double x, double y, double size, Brush? foreground = null)
    {
        var label = new TextBlock { Text = text, FontSize = size, Foreground = foreground ?? (Brush)FindResource("MutedBrush") };
        Canvas.SetLeft(label, x); Canvas.SetTop(label, y); ChartCanvas.Children.Add(label);
    }

    private void SaveMeasurement_Click(object sender, RoutedEventArgs e)
    {
        if (EntryDate.SelectedDate is not { } selected || !double.TryParse(EntryWeight.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        { MessageBox.Show("Enter a valid date and positive weight.", "Check measurement", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            var day = DateOnly.FromDateTime(selected);
            var existing = _store.GetMeasurement(day);
            long? editId = null;
            if (existing is not null)
            {
                if (MessageBox.Show("A record exists for this date. Edit that record?", "Date already recorded", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                editId = existing.Id;
            }
            _store.SaveMeasurement(day, WeightService.ToKilograms(value, _settings.Unit), EntryNote.Text, editId);
            EntryWeight.Clear(); EntryNote.Clear(); RefreshAll();
        }
        catch (Exception error) { MessageBox.Show(error.Message, "Could not save", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void EditSelected_Click(object sender, RoutedEventArgs e)
    {
        if ((HistoryGrid.SelectedItem as MeasurementRow)?.Measurement is not { } row) return;
        var panel = new StackPanel { Margin = new Thickness(18), Width = 320 };
        var day = new DatePicker { SelectedDate = row.Day.ToDateTime(TimeOnly.MinValue), Margin = new Thickness(0, 4, 0, 10) };
        var weight = new TextBox { Text = WeightService.FromKilograms(row.WeightKg, _settings.Unit).ToString($"F{_settings.Precision}", CultureInfo.CurrentCulture), Margin = new Thickness(0, 4, 0, 10) };
        var note = new TextBox { Text = row.Note, MaxLength = 500, Margin = new Thickness(0, 4, 0, 10) };
        panel.Children.Add(new TextBlock { Text = "Date" }); panel.Children.Add(day);
        panel.Children.Add(new TextBlock { Text = $"Weight ({_settings.Unit})" }); panel.Children.Add(weight);
        panel.Children.Add(new TextBlock { Text = "Note" }); panel.Children.Add(note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel" }; var save = new Button { Content = "Save" };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        var dialog = new Window { Title = "Edit measurement", Owner = this, Content = panel, SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("WindowBrush") };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        save.Click += (_, _) =>
        {
            if (day.SelectedDate is not { } chosen || !double.TryParse(weight.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var amount))
            { MessageBox.Show(dialog, "Enter a valid date and weight."); return; }
            try { _store.SaveMeasurement(DateOnly.FromDateTime(chosen), WeightService.ToKilograms(amount, _settings.Unit), note.Text, row.Id); dialog.DialogResult = true; }
            catch (Exception error) { MessageBox.Show(dialog, error.Message, "Could not save", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        if (dialog.ShowDialog() == true) RefreshAll();
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if ((HistoryGrid.SelectedItem as MeasurementRow)?.Measurement is not { } row) return;
        if (MessageBox.Show($"Delete the measurement for {FormatDate(row.Day)}?", "Delete measurement", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        { _store.DeleteMeasurement(row.Id); RefreshAll(); }
    }

    private void DeleteAll_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Permanently delete all measurements and weekly averages? Settings remain. Export a backup first if needed.", "Delete all records", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        { _store.DeleteAll(); RefreshAll(); }
    }

    private void Settings_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        _settings = new TrackerSettings(ComboText(UnitSelector), ComboText(AppearanceSelector), ComboText(SchemeSelector),
            int.TryParse(ComboText(PrecisionSelector), out var precision) ? precision : 1, ComboText(DateFormatSelector));
        _store.SaveSettings(_settings); ApplyTheme(); RefreshAll();
    }

    private void Range_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        var custom = ComboText(RangeSelector) == "Custom";
        RangeStart.Visibility = RangeEnd.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        if (custom) { RangeStart.SelectedDate ??= DateTime.Today.AddDays(-29); RangeEnd.SelectedDate ??= DateTime.Today; }
        RefreshPeriod(); DrawChart();
    }

    private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();
    private void HistoryFilter_Changed(object sender, TextChangedEventArgs e) => RefreshHistory();
    private void Overview_Click(object sender, RoutedEventArgs e) => ShowPage(OverviewPage);
    private void History_Click(object sender, RoutedEventArgs e) { ShowPage(HistoryPage); RefreshHistory(); }
    private void Weekly_Click(object sender, RoutedEventArgs e) { ShowPage(WeeklyPage); RefreshWeekly(); }
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage);

    private void ShowPage(UIElement page)
    {
        OverviewPage.Visibility = HistoryPage.Visibility = WeeklyPage.Visibility = SettingsPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(_dataPath)!) { UseShellExecute = true });

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "SQLite database (*.db)|*.db", FileName = "weight-tracker-backup.db" };
        if (dialog.ShowDialog(this) == true) RunDataAction(() => _store.BackupTo(dialog.FileName), "Backup complete");
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "SQLite database (*.db)|*.db" };
        if (dialog.ShowDialog(this) == true && MessageBox.Show("Restoring replaces current records and settings. Continue?", "Restore database", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            RunDataAction(() => _store.RestoreFrom(dialog.FileName), "Restore complete");
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "weight-tracker-export.json" };
        if (dialog.ShowDialog(this) == true) RunDataAction(() => DataTransferService.ExportJson(_store, dialog.FileName), "Export complete");
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "weight-tracker-export.csv" };
        if (dialog.ShowDialog(this) == true) RunDataAction(() => DataTransferService.ExportCsv(_store, dialog.FileName), "Export complete");
    }

    private void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
        if (dialog.ShowDialog(this) == true) RunDataAction(() => DataTransferService.ImportJson(_store, dialog.FileName), "Import complete");
    }

    private void RunDataAction(Action action, string success)
    {
        try { action(); MessageBox.Show(success, "Weight Tracker", MessageBoxButton.OK, MessageBoxImage.Information); RefreshAll(); }
        catch (Exception error) { MessageBox.Show(error.Message, "Data operation failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private sealed record MeasurementRow(Measurement Measurement, string DisplayDate, string DisplayWeight)
    {
        public long Id => Measurement.Id;
        public DateOnly Day => Measurement.Day;
        public double WeightKg => Measurement.WeightKg;
        public string Note => Measurement.Note;
    }

    private sealed record WeeklyRow(string DisplayMonday, string DisplaySunday, string DisplayAverage, int MeasurementCount, string Status);
}
