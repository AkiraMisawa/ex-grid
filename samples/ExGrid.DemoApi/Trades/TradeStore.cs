using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>Where the store is on its way to serving.</summary>
internal enum TradeStoreState
{
    /// <summary>Finding the generated database, or copying it for this run.</summary>
    Starting,

    /// <summary>Generating the trades: the first start for this count.</summary>
    Generating,

    /// <summary>Serving.</summary>
    Ready,

    /// <summary>The trades could not be made ready, and the server stops.</summary>
    Failed,
}

/// <summary>How far generation has got.</summary>
internal sealed record GenerationProgress(long Done, long Of)
{
    /// <summary>The share done, rounded down to a tenth of a percent, so it reads 100 only when
    /// it is.</summary>
    public double Percent => Of == 0 ? 100 : Math.Floor(1000.0 * Done / Of) / 10;
}

/// <summary>A committed change: the Source Version it moved the data on to, and the Record Keys
/// of the trades it changed, added or removed, in ordinal order.</summary>
internal sealed record TradeChange(string Version, string[] TradeIds);

/// <summary>A page of trades in <c>TradeId</c> order, read at one Source Version, with how many
/// trades that version holds.</summary>
internal sealed record TradePage(string Version, long Total, long Start, IReadOnlyList<Trade> Trades);

/// <summary>
/// One read of the trades at one Source Version (ADR-0065). Every command it makes runs in one
/// read transaction, so each of them sees the trades as they were when the read began, whatever
/// the live updates commit meanwhile, and <see cref="Version"/> is that state's.
/// <para>
/// An answer computed here therefore says truly which data it came from. That is what lets a
/// field's Items and a cell's Details refuse a question asked under a version that has moved on:
/// compare the version asked under with this read's <see cref="Version"/>, not with
/// <see cref="TradeStore.Version"/>, which may move between the two.
/// </para>
/// </summary>
internal sealed class TradeRead(SqliteConnection connection, SqliteTransaction transaction, string version, long trades)
{
    /// <summary>The Source Version of everything this read sees.</summary>
    public string Version => version;

    /// <summary>How many trades this version holds.</summary>
    public long Trades => trades;

    /// <summary>A command inside this read. Parameters are the caller's to add; the caller disposes it.</summary>
    public SqliteCommand Command(string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}

/// <summary>
/// The trades the server owns (ADR-0068), and the one place that reads or changes them.
/// <list type="bullet">
/// <item>At first start for a count, it generates the trades into a file named by that count
/// (<see cref="TradeDatabase"/>), and reuses the file at every later start.</item>
/// <item>Each start serves a copy of that file of its own, deleted when the server stops. Every
/// run therefore starts from the generated trades, whatever an earlier run's live updates did,
/// and two servers never change each other's data.</item>
/// <item>Reads go through <see cref="ReadAsync{T}"/>, which hands them one state of the data and
/// its Source Version together.</item>
/// <item>Changes go through <see cref="ApplyLiveChangesAsync"/>, one transaction each, which moves
/// the change counter on and tells <see cref="Changes"/>.</item>
/// </list>
/// <para>
/// The Source Version is this run's name and the change counter, <c>3f2a9c1e-17</c>. The counter
/// alone would name two different states after a restart, since every run starts again from the
/// generated trades at 0; with the run's name, an answer computed before a restart is refused
/// after it rather than taken for current (ADR-0065).
/// </para>
/// </summary>
internal sealed partial class TradeStore(DemoApiOptions options, ILogger<TradeStore> logger) : IAsyncDisposable
{
    /// <summary>How many committed states the <c>changes</c> table keeps: enough for a page that
    /// polls to catch up, without growing for as long as live updates run.</summary>
    public const int KeptChanges = 10_000;

    /// <summary>Of how many ticks one also cancels a trade and books another.</summary>
    public const int BookingOneTickIn = 8;

    // The live updates' numbers: tick n's come from this seed and n, so the same ticks from the
    // same state give the same trades.
    private const ulong LiveSeed = 0x4C495645_2026_0930;

    private readonly string _run = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<TradeChange> _changes = Channel.CreateUnbounded<TradeChange>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private volatile TradeStoreState _state = TradeStoreState.Starting;
    private volatile GenerationProgress? _progress;
    private volatile Latest? _latest;
    private SqliteConnection? _writer;
    private string? _readConnectionString;
    private string? _workingPath;

    /// <summary>Where the store is on its way to serving.</summary>
    public TradeStoreState State => _state;

    /// <summary>Completes when the store serves; faults when it cannot.</summary>
    public Task Ready => _ready.Task;

    /// <summary>How far generation has got, while it runs.</summary>
    public GenerationProgress? Generating => _state == TradeStoreState.Generating ? _progress : null;

    /// <summary>The current Source Version, once ready. A read takes its own from
    /// <see cref="TradeRead.Version"/>.</summary>
    public string? Version => _latest is { } latest ? FormatVersion(latest.Counter) : null;

    /// <summary>How many trades there are now, once ready.</summary>
    public long? TradeCount => _latest?.Trades;

    /// <summary>The file this run serves, once ready.</summary>
    public string? WorkingPath => _workingPath;

    /// <summary>
    /// Every committed change, in the order committed. <see cref="TradesHubBroadcaster"/> is its
    /// one reader, and says each on the hub.
    /// </summary>
    public ChannelReader<TradeChange> Changes => _changes.Reader;

    /// <summary>Generates the trades if this is the first start for the count, copies them for
    /// this run, and opens the copy.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = options.DataDirectory;
            Directory.CreateDirectory(directory);
            RemoveLeftovers(directory);
            var generated = Path.Combine(directory, TradeDatabase.FileName(options.TradeCount));
            if (File.Exists(generated))
            {
                logger.LogInformation("Using the {Count} trades generated earlier at {Path}.", options.TradeCount, generated);
                TradeDatabase.Verify(generated, options.TradeCount);
            }
            else
            {
                await GenerateAsync(generated, cancellationToken);
            }

            var runs = Path.Combine(directory, "runs");
            Directory.CreateDirectory(runs);
            RemoveLeftovers(runs);
            var working = Path.Combine(runs,
                $"{Path.GetFileNameWithoutExtension(generated)}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.{_run}.sqlite");
            File.Copy(generated, working, overwrite: true);
            _workingPath = working;

            // Held from here on, so DisposeAsync closes it whatever fails below.
            var writer = _writer = TradeDatabase.Open(working, SqliteOpenMode.ReadWrite);
            // Write-ahead logging: a read sees the data as it was when the read began, and a
            // live update never waits for a read to finish, nor a read for an update.
            TradeDatabase.Execute(writer, null, "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");
            var (counter, trades) = ReadLatest(writer, null);
            _readConnectionString = TradeDatabase.ConnectionString(working, SqliteOpenMode.ReadOnly, pooling: true);
            _latest = new Latest(counter, trades, NextNumber(writer));
            _state = TradeStoreState.Ready;
            _ready.TrySetResult();
            logger.LogInformation("Serving {Count} trades at version {Version} from {Path}.", trades, Version, working);
        }
        catch (Exception e)
        {
            _state = TradeStoreState.Failed;
            if (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
                _ready.TrySetCanceled(cancellationToken);
            else
                _ready.TrySetException(e);
            throw;
        }
    }

    /// <summary>
    /// Runs <paramref name="read"/> against one state of the trades, handing it that state's
    /// Source Version (<see cref="TradeRead"/>). Everything the read does happens inside
    /// <paramref name="read"/>: the transaction ends when it returns, so what it returns must
    /// already be read — a stream of rows is written out inside it, not handed back.
    /// </summary>
    public async Task<T> ReadAsync<T>(Func<TradeRead, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        var connectionString = _readConnectionString
            ?? throw new InvalidOperationException($"The trades are not ready: {_state}.");
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        // Deferred: the read transaction begins at its first statement, which reads the version,
        // so the version and every row after it come from the same state.
        await using var transaction = connection.BeginTransaction(deferred: true);
        var (counter, trades) = ReadLatest(connection, transaction);
        return await read(new TradeRead(connection, transaction, FormatVersion(counter), trades), cancellationToken);
    }

    /// <summary>A page of trades in <c>TradeId</c> order: <paramref name="count"/> of them from the
    /// <paramref name="start"/>th, fewer at the end.</summary>
    public Task<TradePage> ReadPageAsync(long start, int count, CancellationToken cancellationToken) =>
        ReadAsync(async (read, token) =>
        {
            await using var command = read.Command(
                $"SELECT {TradeDatabase.TradeColumns} FROM trades ORDER BY TradeId LIMIT $count OFFSET $start");
            command.Parameters.AddWithValue("$count", count);
            command.Parameters.AddWithValue("$start", start);
            var trades = new List<Trade>(count);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                trades.Add(Trade.Read(reader));
            return new TradePage(read.Version, read.Trades, start, trades);
        }, cancellationToken);

    /// <summary>
    /// One tick of the live updates, in one transaction with the change counter, so no read ever
    /// sees half of it (ADR-0066):
    /// <list type="bullet">
    /// <item><paramref name="tradesPerTick"/> trades have their P&amp;L moved, and one in four its
    /// notional amended too;</item>
    /// <item>one tick in <see cref="BookingOneTickIn"/> also cancels a trade and books a new one,
    /// so a grid sees a row leave and another arrive.</item>
    /// </list>
    /// Tick <c>n</c>'s numbers come from <c>n</c>, so the same ticks from the same state make the
    /// same trades. The change is told to <see cref="Changes"/> once committed.
    /// </summary>
    public async Task<TradeChange> ApplyLiveChangesAsync(int tradesPerTick, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tradesPerTick, 1);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var writer = _writer ?? throw new InvalidOperationException($"The trades are not ready: {_state}.");
            var latest = _latest!;
            var counter = latest.Counter + 1;
            var trades = latest.Trades;
            var next = latest.NextNumber;
            var random = new SplitMix64(LiveSeed, (ulong)counter);
            var touched = new SortedSet<string>(StringComparer.Ordinal);

            using var transaction = writer.BeginTransaction();
            using var select = Command(writer, transaction, "SELECT Notional, Pnl FROM trades WHERE TradeId = $id", "$id");
            using var update = Command(writer, transaction,
                "UPDATE trades SET Notional = $notional, Pnl = $pnl WHERE TradeId = $id", "$notional", "$pnl", "$id");
            for (var left = Math.Min(tradesPerTick, trades); left > 0; left--)
            {
                if (Pick(ref random, next, touched, select) is not { } picked)
                    break;
                var (id, notional, pnl) = picked;
                // The P&L moves by up to 0.05% of the notional, and never by nothing: every trade
                // the hub names has a value that changed.
                var move = 1 + random.NextLong(Math.Max(1, notional / 2_000));
                pnl += random.Next(2) == 0 ? move : -move;
                // One change in four amends the notional by 10,000 as well, never below 10,000.
                if (random.Next(4) == 0)
                    notional += notional > 1_000_000 && random.Next(2) == 0 ? -1_000_000 : 1_000_000;
                update.Parameters[0].Value = notional;
                update.Parameters[1].Value = pnl;
                update.Parameters[2].Value = id;
                update.ExecuteNonQuery();
                touched.Add(id);
            }

            if (random.Next(BookingOneTickIn) == 0 && trades > 1
                && Pick(ref random, next, touched, select) is { } cancelled)
            {
                TradeDatabase.Execute(writer, transaction, "DELETE FROM trades WHERE TradeId = $id", ("$id", cancelled.Id));
                touched.Add(cancelled.Id);
                var booked = TradeGenerator.Generate(next++);
                using var insert = TradeDatabase.InsertCommand(writer, transaction, out var values);
                TradeDatabase.SetValues(values, booked);
                insert.ExecuteNonQuery();
                touched.Add(booked.TradeId);
            }

            var ids = touched.ToArray();
            TradeDatabase.Execute(writer, transaction,
                "INSERT INTO changes (Version, Trades, TradeIds) VALUES ($version, $trades, $ids)",
                ("$version", counter), ("$trades", trades), ("$ids", JsonSerializer.Serialize(ids)));
            TradeDatabase.Execute(writer, transaction, "DELETE FROM changes WHERE Version <= $oldest",
                ("$oldest", counter - KeptChanges));
            transaction.Commit();

            _latest = new Latest(counter, trades, next);
            var change = new TradeChange(FormatVersion(counter), ids);
            _changes.Writer.TryWrite(change);
            return change;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Closes the copy this run served, and deletes it.</summary>
    public async ValueTask DisposeAsync()
    {
        _changes.Writer.TryComplete();
        await _writeLock.WaitAsync();
        try
        {
            if (_writer is { } writer)
            {
                await writer.DisposeAsync();
                _writer = null;
            }
            if (_readConnectionString is { } readConnectionString)
            {
                using var pooled = new SqliteConnection(readConnectionString);
                SqliteConnection.ClearPool(pooled);
                _readConnectionString = null;
            }
            if (_workingPath is { } working)
            {
                foreach (var suffix in (string[])["", "-wal", "-shm"])
                    TryDelete(working + suffix);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task GenerateAsync(string path, CancellationToken cancellationToken)
    {
        var count = options.TradeCount;
        _progress = new GenerationProgress(0, count);
        _state = TradeStoreState.Generating;
        logger.LogInformation("Generating {Count} trades into {Path}: the first start for this count.", count, path);
        // Generated under a name of its own and moved into place only when whole, so a
        // generation that is stopped leaves nothing the next start could mistake for data.
        var temporary = $"{path}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.{_run}.tmp";
        var stopwatch = Stopwatch.StartNew();
        var loggedTenths = 0L;
        try
        {
            await Task.Run(() => TradeDatabase.Generate(temporary, count, done =>
            {
                _progress = new GenerationProgress(done, count);
                var tenths = done * 10 / count;
                if (tenths > loggedTenths && done < count)
                {
                    loggedTenths = tenths;
                    logger.LogInformation("Generated {Done} of {Count} trades ({Percent}%).", done, count, tenths * 10);
                }
            }, cancellationToken), cancellationToken);
            try
            {
                File.Move(temporary, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another server generated the same count first. Generation is deterministic,
                // so its file holds exactly what this one does.
                TryDelete(temporary);
            }
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
        logger.LogInformation("Generated {Count} trades in {Seconds:0.0} s: {Megabytes:0.0} MB.",
            count, stopwatch.Elapsed.TotalSeconds, new FileInfo(path).Length / 1_048_576.0);
        _state = TradeStoreState.Starting;
    }

    private string FormatVersion(long counter) => $"{_run}-{counter.ToString(CultureInfo.InvariantCulture)}";

    private static (long Counter, long Trades) ReadLatest(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Version, Trades FROM changes ORDER BY Version DESC LIMIT 1";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("The database holds no state in its changes table.");
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static long NextNumber(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT max(TradeId) FROM trades";
        return command.ExecuteScalar() is string last ? TradeGenerator.Number(last) + 1 : 0;
    }

    // Trades are numbered densely and cancelled rarely, so a number drawn in the range is nearly
    // always a trade; one in a gap, or one already touched this tick, is drawn again.
    private static (string Id, long Notional, long Pnl)? Pick(
        ref SplitMix64 random, long next, SortedSet<string> touched, SqliteCommand select)
    {
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var id = TradeGenerator.TradeId(random.NextLong(next));
            if (touched.Contains(id))
                continue;
            select.Parameters[0].Value = id;
            using var reader = select.ExecuteReader();
            if (reader.Read())
                return (id, reader.GetInt64(0), reader.GetInt64(1));
        }
        return null;
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql,
        params string[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var name in parameters)
            command.Parameters.Add(new SqliteParameter(name, null));
        return command;
    }

    // What an earlier process left behind when it was killed: a generation's temporary file, or
    // a run's copy. Only a process that is no longer running is cleaned up after.
    private void RemoveLeftovers(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var match = LeftoverName().Match(Path.GetFileName(file));
            if (!match.Success
                || !int.TryParse(match.Groups["pid"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                || pid == Environment.ProcessId
                || IsRunning(pid))
            {
                continue;
            }
            logger.LogInformation("Removing {Path}, which a server that has stopped left behind.", file);
            TryDelete(file);
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [GeneratedRegex(@"\.(?<pid>\d+)\.[0-9a-f]{8}\.(sqlite(-wal|-shm|-journal)?|tmp)$", RegexOptions.CultureInvariant)]
    private static partial Regex LeftoverName();

    private sealed record Latest(long Counter, long Trades, long NextNumber);
}
