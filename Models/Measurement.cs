namespace WeightTracker.Models;

public sealed record Measurement(long Id, DateOnly Day, double WeightKg, string Note);

public sealed record WeeklyAverage(DateOnly Monday, double AverageKg, int MeasurementCount);

public sealed record TrackerSettings(
    string Unit = "kg",
    string Appearance = "Light",
    string Scheme = "Forest Green",
    int Precision = 1,
    string DateFormat = "yyyy-MM-dd");