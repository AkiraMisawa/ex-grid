using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>
/// The generated database (ADR-0069): its schema, the name of its file, and how it is generated.
/// It is generated once per trade count and <see cref="FormatVersion"/>, and never written after:
/// each start of the server works on a copy of it (<see cref="TradeStore"/>).
/// </summary>
internal static class TradeDatabase
{
    /// <summary>
    /// The version of the schema and of the data <see cref="TradeGenerator"/> makes. It is part of
    /// the file's name, so moving it on makes the server generate afresh instead of reusing a
    /// file an older generator wrote. Move it whenever either changes; <c>TradeGeneratorTests</c>
    /// pins what this version generates, and fails until it is moved.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// The trades, and one row per committed state of them. Money is integer cents, so
    /// <c>SUM(Pnl)</c> is exact integer arithmetic; divide by 100 as a decimal, never as a double.
    /// <c>AVG</c> answers in floating point, so an exact Average is <c>SUM</c> over <c>COUNT</c>.
    /// The columns are named as the Pivot Fields are (<c>DemoPivotData</c>), so hand-written SQL
    /// reads like the layout it answers. The comments stay in the file, for whoever opens it with
    /// <c>sqlite3</c> and types <c>.schema</c>.
    /// </summary>
    public const string Schema = """
        CREATE TABLE trades (
            TradeId   TEXT    NOT NULL PRIMARY KEY, -- the Record Key: T10000000, T10000001, ...
            Region    TEXT    NOT NULL,
            Desk      TEXT    NOT NULL,
            Book      TEXT    NOT NULL,
            Product   TEXT    NOT NULL,
            Currency  TEXT,                         -- NULL is a Blank
            TradeDate TEXT    NOT NULL,             -- ISO 8601, yyyy-MM-dd; its month is substr(TradeDate, 1, 7)
            Notional  INTEGER NOT NULL,             -- cents
            Pnl       INTEGER NOT NULL,             -- cents
            Quantity  INTEGER NOT NULL,
            Confirmed INTEGER NOT NULL CHECK (Confirmed IN (0, 1))
        ) STRICT, WITHOUT ROWID;

        -- One row per committed state of the trades. Version is the change counter the Source
        -- Version is made from (ADR-0066); Trades is how many trades that state holds; TradeIds is
        -- a JSON array of the Record Keys the change touched. Version 0 is the generated data.
        CREATE TABLE changes (
            Version  INTEGER NOT NULL PRIMARY KEY,
            Trades   INTEGER NOT NULL,
            TradeIds TEXT    NOT NULL
        ) STRICT;

        -- What generated this file, checked whenever it is reused.
        CREATE TABLE generation (
            Name  TEXT NOT NULL PRIMARY KEY,
            Value TEXT NOT NULL
        ) STRICT, WITHOUT ROWID;
        """;

    /// <summary>Every column of <c>trades</c>, in the schema's order.</summary>
    public const string TradeColumns =
        "TradeId, Region, Desk, Book, Product, Currency, TradeDate, Notional, Pnl, Quantity, Confirmed";

    /// <summary>
    /// The collation that compares text as a pivot tells Items apart: ordinally, ignoring case,
    /// every letter that has one (<see cref="StringComparison.OrdinalIgnoreCase"/>, ADR-0060).
    /// SQLite's own <c>NOCASE</c> folds the ASCII letters only. Every read registers it
    /// (<see cref="AddItemCollation"/>); SQLite calls back into .NET for each comparison, so it is
    /// used only where <c>NOCASE</c> would not be the engine's comparison (<see cref="TradePivotSql"/>).
    /// </summary>
    public const string ItemCollation = "ITEM";

    /// <summary>How often generation reports its progress and looks at its cancellation.</summary>
    private const int ProgressStep = 10_000;

    /// <summary>The file generated for <paramref name="tradeCount"/> trades: named by the format
    /// version and the count, so a changed generator or another count never reuses it.</summary>
    public static string FileName(int tradeCount) =>
        $"trades-v{FormatVersion}-{tradeCount.ToString(CultureInfo.InvariantCulture)}.sqlite";

    /// <summary>
    /// Generates <paramref name="tradeCount"/> trades into a new file at <paramref name="path"/>,
    /// in one transaction through one prepared statement. The same count gives the same trades,
    /// and under the same SQLite the same bytes: the file's header names the SQLite that wrote it.
    /// </summary>
    /// <param name="progress">Told how many trades are written, every ten thousand and at the end.</param>
    public static void Generate(string path, int tradeCount, Action<long>? progress, CancellationToken cancellationToken)
    {
        using var connection = Open(path, SqliteOpenMode.ReadWriteCreate);
        // No journal and no syncing: a generation that fails is deleted whole, and only a
        // finished one is ever moved to where the server looks.
        Execute(connection, null, "PRAGMA journal_mode = OFF; PRAGMA synchronous = OFF;");
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, Schema);

        using var insert = InsertCommand(connection, transaction, out var values);
        for (long n = 0; n < tradeCount; n++)
        {
            SetValues(values, TradeGenerator.Generate(n));
            insert.ExecuteNonQuery();
            if ((n + 1) % ProgressStep == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(n + 1);
            }
        }

        Execute(connection, transaction, "INSERT INTO changes (Version, Trades, TradeIds) VALUES (0, $trades, '[]')",
            ("$trades", tradeCount));
        foreach (var (name, value) in Recorded(tradeCount))
            Execute(connection, transaction, "INSERT INTO generation (Name, Value) VALUES ($name, $value)",
                ("$name", name), ("$value", value));
        transaction.Commit();
        progress?.Invoke(tradeCount);
    }

    /// <summary>
    /// Refuses a generated file that does not say it holds what its name promises — one renamed
    /// or copied in by hand, or not a database at all — rather than serve it. (A generator changed
    /// without moving <see cref="FormatVersion"/> on writes a file this cannot tell apart;
    /// <c>TradeGeneratorTests</c>' pinned fingerprint is what catches that.)
    /// </summary>
    public static void Verify(string path, int tradeCount)
    {
        using var connection = Open(path, SqliteOpenMode.ReadOnly);
        var recorded = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Name, Value FROM generation";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                recorded[reader.GetString(0)] = reader.GetString(1);
        }
        catch (SqliteException e)
        {
            throw new InvalidOperationException($"{path} is not a generated trades database ({e.Message}). Delete it, and the server generates it again.", e);
        }

        foreach (var (name, value) in Recorded(tradeCount))
        {
            if (!recorded.TryGetValue(name, out var found) || found != value)
            {
                throw new InvalidOperationException(
                    $"{path} records {name} = {found ?? "nothing"}, where its name promises {value}. "
                    + "Delete it, and the server generates it again.");
            }
        }
    }

    /// <summary>An insert of one trade, whose <paramref name="values"/> <see cref="SetValues"/> fills.
    /// SQLite prepares it once, at its first execution, and reuses it for every trade after.</summary>
    public static SqliteCommand InsertCommand(SqliteConnection connection, SqliteTransaction transaction,
        out SqliteParameter[] values)
    {
        var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = $"INSERT INTO trades ({TradeColumns}) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)";
        values = new SqliteParameter[11];
        for (var i = 0; i < values.Length; i++)
            values[i] = insert.Parameters.Add(new SqliteParameter("$" + (i + 1).ToString(CultureInfo.InvariantCulture), null));
        return insert;
    }

    /// <summary>Sets an insert's values for one trade, in <see cref="TradeColumns"/>' order.</summary>
    public static void SetValues(SqliteParameter[] values, in GeneratedTrade trade)
    {
        values[0].Value = trade.TradeId;
        values[1].Value = trade.Region;
        values[2].Value = trade.Desk;
        values[3].Value = trade.Book;
        values[4].Value = trade.Product;
        values[5].Value = (object?)trade.Currency ?? DBNull.Value;
        values[6].Value = FormatDate(trade.TradeDate);
        values[7].Value = trade.NotionalCents;
        values[8].Value = trade.PnlCents;
        values[9].Value = trade.Quantity;
        values[10].Value = trade.Confirmed ? 1 : 0;
    }

    /// <summary>A date as the database stores it: ISO 8601 text, <c>yyyy-MM-dd</c>, which sorts as
    /// it reads and which SQLite's own date functions understand.</summary>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Opens the file, pooled or not; a connection the server keeps open is not pooled.</summary>
    public static SqliteConnection Open(string path, SqliteOpenMode mode, bool pooling = false)
    {
        var connection = new SqliteConnection(ConnectionString(path, mode, pooling));
        connection.Open();
        return connection;
    }

    /// <summary>Registers <see cref="ItemCollation"/> on an open connection.</summary>
    public static void AddItemCollation(SqliteConnection connection) =>
        connection.CreateCollation(ItemCollation, static (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));

    /// <summary>The connection string for the file.</summary>
    public static string ConnectionString(string path, SqliteOpenMode mode, bool pooling) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = pooling }.ToString();

    /// <summary>Runs SQL that answers nothing.</summary>
    public static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private static IEnumerable<(string Name, string Value)> Recorded(int tradeCount) =>
    [
        ("FormatVersion", FormatVersion.ToString(CultureInfo.InvariantCulture)),
        ("Seed", TradeGenerator.Seed.ToString(CultureInfo.InvariantCulture)),
        ("Trades", tradeCount.ToString(CultureInfo.InvariantCulture)),
    ];
}
