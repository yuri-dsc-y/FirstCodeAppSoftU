using System.Text.Json;
using System.IO;

namespace WeightTracker.Services;

public sealed record LocalQuote(string Id, string Text, string Author, string? SourceTitle, string? SourceUrl);

public static class QuoteService
{
    public static IReadOnlyList<LocalQuote> Load(string path)
    {
        if (!File.Exists(path)) return [];
        var quotes = JsonSerializer.Deserialize<List<LocalQuote>>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (quotes.Count > 366 || quotes.Select(item => item.Id).Distinct().Count() != quotes.Count ||
            quotes.Select(item => item.Text).Distinct().Count() != quotes.Count ||
            quotes.Any(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Text) ||
                               string.IsNullOrWhiteSpace(item.Author)))
            throw new InvalidDataException("The local quote file is invalid.");
        return quotes;
    }

    public static LocalQuote? ForDay(IReadOnlyList<LocalQuote> quotes, DateOnly day) =>
        quotes.Count == 0 ? null : quotes[(day.DayOfYear - 1) % quotes.Count];
}