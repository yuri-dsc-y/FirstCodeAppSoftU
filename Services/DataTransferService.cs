using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WeightTracker.Services;

public static class DataTransferService
{
    public static void ExportJson(TrackerStore store, string path)
    {
        var records = store.GetMeasurements().Select(item => new
        {
            date = item.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            weight_kg = item.WeightKg,
            note = item.Note
        });
        File.WriteAllText(path, JsonSerializer.Serialize(new { format = 1, measurements = records },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void ExportCsv(TrackerStore store, string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("date,weight_kg,note");
        foreach (var item in store.GetMeasurements().OrderBy(row => row.Day))
            writer.WriteLine(string.Join(',', Csv(item.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                item.WeightKg.ToString("R", CultureInfo.InvariantCulture), Csv(item.Note)));
    }

    public static void ImportJson(TrackerStore store, string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("format", out var format) || format.GetInt32() != 1 ||
            !root.TryGetProperty("measurements", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Unsupported or malformed Weight Tracker JSON.");
        var validated = new List<ImportMeasurement>();
        foreach (var item in records.EnumerateArray())
        {
            if (!item.TryGetProperty("date", out var dateValue) ||
                !DateOnly.TryParseExact(dateValue.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day) ||
                !item.TryGetProperty("weight_kg", out var weightValue) || !weightValue.TryGetDouble(out var weight) ||
                !double.IsFinite(weight) || weight <= 0)
                throw new InvalidDataException("Import contains an invalid date or weight.");
            var note = item.TryGetProperty("note", out var noteValue) ? noteValue.GetString() ?? "" : "";
            validated.Add(new ImportMeasurement(day, weight, note));
        }
        store.ImportMeasurements(validated);
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}