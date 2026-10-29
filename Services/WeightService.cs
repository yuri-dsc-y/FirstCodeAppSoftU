using WeightTracker.Models;

namespace WeightTracker.Services;

public static class WeightService
{
    public const double KilogramsPerPound = 0.45359237;

    public static double ToKilograms(double value, string unit)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Weight must be a finite positive number.");
        return unit == "lb" ? value * KilogramsPerPound : value;
    }

    public static double FromKilograms(double value, string unit) =>
        unit == "lb" ? value / KilogramsPerPound : value;

    public static (DateOnly Monday, DateOnly Sunday) WeekBounds(DateOnly day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6));
    }

    public static (DateOnly? Start, DateOnly End) DateRange(string range, DateOnly today)
    {
        if (range == "All time") return (null, today);
        if (range == "7 days") return (today.AddDays(-6), today);
        if (range == "30 days") return (today.AddDays(-29), today);
        if (range == "90 days") return (today.AddDays(-89), today);
        var months = range == "6 months" ? 6 : range == "12 months" ? 12 :
            throw new ArgumentOutOfRangeException(nameof(range));
        var monthIndex = today.Year * 12 + today.Month - 1 - months;
        var year = Math.DivRem(monthIndex, 12, out var zeroBasedMonth);
        var month = zeroBasedMonth + 1;
        var day = Math.Min(today.Day, DateTime.DaysInMonth(year, month));
        return (new DateOnly(year, month, day), today);
    }

    public static double? Change(IReadOnlyList<Measurement> measurements, string unit)
    {
        if (measurements.Count < 2) return null;
        var ordered = measurements.OrderBy(item => item.Day).ToArray();
        return FromKilograms(ordered[^1].WeightKg - ordered[0].WeightKg, unit);
    }
}