using Microsoft.Data.Sqlite;
using WeightTracker.Models;
using WeightTracker.Services;

var root = Path.Combine(Path.GetTempPath(), "WeightTracker.Tests." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var checks = 0;
try
{
    var dbPath = Path.Combine(root, "tracker.db");
    using (var store = new TrackerStore(dbPath))
    {
        store.SaveMeasurement(new DateOnly(2024, 1, 1), 80, "first");
        store.SaveMeasurement(new DateOnly(2024, 1, 3), 82, "third");
        var week = store.GetWeeklyAverages().Single();
        Check(week.Monday == new DateOnly(2024, 1, 1) && week.MeasurementCount == 2 && week.AverageKg == 81, "weekly average counts actual entries");
        try { store.SaveMeasurement(new DateOnly(2024, 1, 1), 85, "duplicate"); throw new Exception("duplicate date was accepted"); }
        catch (SqliteException) { checks++; }
        var secondId = store.GetMeasurement(new DateOnly(2024, 1, 3))!.Id;
        store.SaveMeasurement(new DateOnly(2024, 1, 3), 84, "edited", secondId);
        Check(store.GetWeeklyAverages().Single().AverageKg == 82, "edit recalculates summary");
        store.DeleteMeasurement(secondId);
        Check(store.GetWeeklyAverages().Single().MeasurementCount == 1, "delete recalculates count");
        store.SaveSettings(new TrackerSettings(Unit: "lb", Scheme: "Ocean Blue"));
        Check(store.GetSettings().Unit == "lb" && store.GetSettings().Scheme == "Ocean Blue", "settings persist");
        Check(WeightService.FromKilograms(WeightService.ToKilograms(150, "lb"), "lb") is > 149.99 and < 150.01, "pound conversion");
        Check(WeightService.WeekBounds(new DateOnly(2024, 3, 1)) == (new DateOnly(2024, 2, 26), new DateOnly(2024, 3, 3)), "week crosses month boundary");
        Check(WeightService.DateRange("6 months", new DateOnly(2024, 8, 31)).Start == new DateOnly(2024, 2, 29), "month range handles leap day");
        Check(QuoteService.ForDay([], new DateOnly(2024, 2, 29)) is null, "empty local quote collection");
        var quotesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Resources", "quotes.json"));
        var quotes = QuoteService.Load(quotesPath);
        Check(quotes.Count == 100, "loads all supplied local quotes");
        Check(QuoteService.ForDay(quotes, new DateOnly(2024, 1, 1)) is { Text: "You are not prepared!", Author: "Illidan Stormrage" },
            "daily selection preserves supplied text and attribution");

        var exportPath = Path.Combine(root, "export.json");
        DataTransferService.ExportJson(store, exportPath);
        using var importStore = new TrackerStore(Path.Combine(root, "import.db"));
        DataTransferService.ImportJson(importStore, exportPath);
        Check(importStore.GetMeasurements().Count == 1, "JSON export/import round trip");
        var invalidPath = Path.Combine(root, "invalid.json");
        File.WriteAllText(invalidPath, "{\"format\":1,\"measurements\":[{\"date\":\"2024-01-05\",\"weight_kg\":70},{\"date\":\"bad\",\"weight_kg\":1}]}");
        try { DataTransferService.ImportJson(importStore, invalidPath); throw new Exception("invalid import was accepted"); }
        catch (InvalidDataException) { Check(importStore.GetMeasurements().Count == 1, "invalid import leaves existing data unchanged"); }
        var backupPath = Path.Combine(root, "backup.db");
        store.BackupTo(backupPath);
        using var restoreStore = new TrackerStore(Path.Combine(root, "restore.db"));
        restoreStore.RestoreFrom(backupPath);
        Check(restoreStore.GetMeasurements().Count == 1, "SQLite backup restore");
    }
    Console.WriteLine($"PASS: {checks} checks");
}
finally
{
    try { Directory.Delete(root, true); } catch (IOException) { }
}

void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}