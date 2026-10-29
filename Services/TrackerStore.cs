using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using WeightTracker.Models;

namespace WeightTracker.Services;

public sealed class TrackerStore : IDisposable
{
    private const int SchemaVersion = 1;
    private readonly string _path;
    private readonly SqliteConnection _connection;

    public string DatabasePath => _path;

    public TrackerStore(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path }.ToString());
        _connection.Open();
        Migrate();
    }

    public IReadOnlyList<Measurement> GetMeasurements(DateOnly? start = null, DateOnly? end = null)
    {
        using var command = _connection.CreateCommand();
        var predicates = new List<string>();
        if (start is not null)
        {
            predicates.Add("day >= $start");
            command.Parameters.AddWithValue("$start", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (end is not null)
        {
            predicates.Add("day <= $end");
            command.Parameters.AddWithValue("$end", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        command.CommandText = "SELECT id, day, weight_kg, note FROM measurements" +
                              (predicates.Count == 0 ? "" : " WHERE " + string.Join(" AND ", predicates)) +
                              " ORDER BY day DESC";
        using var reader = command.ExecuteReader();
        var rows = new List<Measurement>();
        while (reader.Read())
            rows.Add(new Measurement(reader.GetInt64(0), DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd"),
                reader.GetDouble(2), reader.GetString(3)));
        return rows;
    }

    public Measurement? GetMeasurement(DateOnly day) => GetMeasurements(day, day).FirstOrDefault();

    public long SaveMeasurement(DateOnly day, double weightKg, string note, long? id = null)
    {
        if (!double.IsFinite(weightKg) || weightKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(weightKg), "Weight must be finite and greater than zero.");
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$weight", weightKg);
        command.Parameters.AddWithValue("$note", note.Trim());
        if (id is null)
        {
            command.CommandText = "INSERT INTO measurements(day, weight_kg, note) VALUES ($day, $weight, $note)";
            command.ExecuteNonQuery();
            command.CommandText = "SELECT last_insert_rowid()";
            id = (long)command.ExecuteScalar()!;
        }
        else
        {
            command.Parameters.AddWithValue("$id", id.Value);
            command.CommandText = "UPDATE measurements SET day=$day, weight_kg=$weight, note=$note WHERE id=$id";
            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("The selected measurement no longer exists.");
        }
        RecalculateWeeks(transaction);
        transaction.Commit();
        return id.Value;
    }

    public void DeleteMeasurement(long id)
    {
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM measurements WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
        RecalculateWeeks(transaction);
        transaction.Commit();
    }

    public void DeleteAll()
    {
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM measurements; DELETE FROM weekly_averages;";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<WeeklyAverage> GetWeeklyAverages()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT monday, average_kg, measurement_count FROM weekly_averages ORDER BY monday DESC";
        using var reader = command.ExecuteReader();
        var results = new List<WeeklyAverage>();
        while (reader.Read())
            results.Add(new WeeklyAverage(DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd"),
                reader.GetDouble(1), reader.GetInt32(2)));
        return results;
    }

    public TrackerSettings GetSettings()
    {
        var unit = GetSetting("unit", "kg");
        var appearance = GetSetting("appearance", "Light");
        var scheme = GetSetting("scheme", "Forest Green");
        var dateFormat = GetSetting("date_format", "yyyy-MM-dd");
        if (unit is not ("kg" or "lb")) unit = "kg";
        if (appearance is not ("Light" or "Dark")) appearance = "Light";
        if (scheme is not ("Forest Green" or "Ocean Blue" or "Midnight Violet" or "Warm Amber" or "Monochrome")) scheme = "Forest Green";
        if (dateFormat is not ("yyyy-MM-dd" or "MM/dd/yyyy" or "dd/MM/yyyy")) dateFormat = "yyyy-MM-dd";
        return new TrackerSettings(unit, appearance, scheme,
            int.TryParse(GetSetting("precision", "1"), out var precision) ? Math.Clamp(precision, 0, 2) : 1, dateFormat);
    }

    public void SaveSettings(TrackerSettings settings)
    {
        using var transaction = _connection.BeginTransaction();
        foreach (var (key, value) in new Dictionary<string, string>
                 {
                     ["unit"] = settings.Unit, ["appearance"] = settings.Appearance,
                     ["scheme"] = settings.Scheme, ["precision"] = settings.Precision.ToString(CultureInfo.InvariantCulture),
                     ["date_format"] = settings.DateFormat
                 })
        {
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO settings(key,value) VALUES($key,$value) " +
                                  "ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void ImportMeasurements(IReadOnlyCollection<ImportMeasurement> imported)
    {
        var dates = new HashSet<DateOnly>();
        foreach (var row in imported)
        {
            if (!double.IsFinite(row.WeightKg) || row.WeightKg <= 0 || !dates.Add(row.Date))
                throw new InvalidDataException("Import contains a duplicate date or invalid weight.");
        }
        using var transaction = _connection.BeginTransaction();
        foreach (var row in imported)
        {
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO measurements(day,weight_kg,note) VALUES($day,$weight,$note) " +
                                  "ON CONFLICT(day) DO UPDATE SET weight_kg=excluded.weight_kg,note=excluded.note";
            command.Parameters.AddWithValue("$day", row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$weight", row.WeightKg);
            command.Parameters.AddWithValue("$note", row.Note ?? "");
            command.ExecuteNonQuery();
        }
        RecalculateWeeks(transaction);
        transaction.Commit();
    }

    public void BackupTo(string destination)
    {
        var fullDestination = Path.GetFullPath(destination);
        if (string.Equals(fullDestination, _path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a backup path separate from the active database.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullDestination }.ToString());
        target.Open();
        _connection.BackupDatabase(target);
    }

    public void RestoreFrom(string source)
    {
        var fullSource = Path.GetFullPath(source);
        if (string.Equals(fullSource, _path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a backup file separate from the active database.");
        using var sourceConnection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullSource, Mode = SqliteOpenMode.ReadOnly }.ToString());
        sourceConnection.Open();
        using (var check = sourceConnection.CreateCommand())
        {
            check.CommandText = "PRAGMA quick_check";
            if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal))
                throw new InvalidDataException("The selected database failed its integrity check.");
            check.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(check.ExecuteScalar(), CultureInfo.InvariantCulture) != SchemaVersion)
                throw new InvalidDataException("The selected database schema is not supported.");
        }
        var sourceRows = new List<ImportMeasurement>();
        using (var read = sourceConnection.CreateCommand())
        {
            read.CommandText = "SELECT day, weight_kg, note FROM measurements ORDER BY day";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                var day = DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var weight = reader.GetDouble(1);
                if (!double.IsFinite(weight) || weight <= 0)
                    throw new InvalidDataException("The backup contains an invalid weight.");
                sourceRows.Add(new ImportMeasurement(day, weight, reader.GetString(2)));
            }
        }
        var sourceSettings = new Dictionary<string, string>();
        using (var readSettings = sourceConnection.CreateCommand())
        {
            readSettings.CommandText = "SELECT key,value FROM settings";
            using var reader = readSettings.ExecuteReader();
            while (reader.Read()) sourceSettings[reader.GetString(0)] = reader.GetString(1);
        }
        using var transaction = _connection.BeginTransaction();
        using (var clear = _connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM measurements; DELETE FROM settings; DELETE FROM weekly_averages;";
            clear.ExecuteNonQuery();
        }
        foreach (var row in sourceRows)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO measurements(day,weight_kg,note) VALUES($day,$weight,$note)";
            insert.Parameters.AddWithValue("$day", row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$weight", row.WeightKg);
            insert.Parameters.AddWithValue("$note", row.Note ?? "");
            insert.ExecuteNonQuery();
        }
        foreach (var (key, value) in sourceSettings)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO settings(key,value) VALUES($key,$value)";
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$value", value);
            insert.ExecuteNonQuery();
        }
        RecalculateWeeks(transaction);
        transaction.Commit();
    }

    private string GetSetting(string key, string fallback)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key=$key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar()?.ToString() ?? fallback;
    }

    private void Migrate()
    {
        using var versionCommand = _connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(versionCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version > SchemaVersion) throw new InvalidOperationException("Database schema is newer than this app.");
        if (version == SchemaVersion) return;
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE measurements(id INTEGER PRIMARY KEY, day TEXT NOT NULL UNIQUE,
                weight_kg REAL NOT NULL CHECK(weight_kg > 0), note TEXT NOT NULL DEFAULT '');
            CREATE INDEX measurements_day_idx ON measurements(day);
            CREATE TABLE weekly_averages(monday TEXT PRIMARY KEY, average_kg REAL NOT NULL CHECK(average_kg > 0),
                measurement_count INTEGER NOT NULL CHECK(measurement_count > 0));
            CREATE TABLE settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private void RecalculateWeeks(SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM weekly_averages;
            INSERT INTO weekly_averages(monday, average_kg, measurement_count)
            SELECT date(day, '-' || ((CAST(strftime('%w', day) AS INTEGER) + 6) % 7) || ' days'),
                AVG(weight_kg), COUNT(*) FROM measurements GROUP BY 1;
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}

public sealed record ImportMeasurement(DateOnly Date, double WeightKg, string? Note);