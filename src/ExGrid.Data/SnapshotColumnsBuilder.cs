using System.Globalization;
using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// Builds a Snapshot from columns (ADR-0063's fourth way in): for data a reader has read itself — a
/// CSV, a <c>DbDataReader</c>, an Arrow stream, Parquet, a message stream. Each declared column is
/// appended to on its own, a value at a time or a whole span at a time; a row is complete when every
/// column has reached it.
/// <para>
/// A reader paces the load by calling <see cref="CheckpointAsync"/> between rows or between batches
/// of rows: it looks at the <see cref="CancellationToken"/>, and when the slice has spent its budget
/// it reports progress and yields, so a browser keeps painting. A load that meets a value it cannot
/// read fails whole, naming the row and the column, and yields no Snapshot; once refused, the
/// builder builds nothing.
/// </para>
/// </summary>
public sealed class SnapshotColumnsBuilder
{
    private readonly List<ColumnBuilder> columns = [];
    private readonly Pacer pacer;
    private readonly CancellationToken cancellationToken;
    private SnapshotTuning tuning = SnapshotTuning.Default;
    private string? key;
    private bool refused;
    private bool built;

    /// <summary>A builder pacing its load by <paramref name="options"/>, and cancelled by
    /// <paramref name="cancellationToken"/>.</summary>
    public SnapshotColumnsBuilder(SnapshotLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        pacer = new Pacer(options, cancellationToken);
        this.cancellationToken = cancellationToken;
    }

    /// <summary>The version the Snapshot will carry — 0 unless a reader restores the one a Snapshot
    /// was written with, as an Arrow stream carries it.</summary>
    public long Version { get; set; }

    /// <summary>The rows the load expects, when the reader knows, for the progress it reports.</summary>
    public long? TotalRows { get; set; }

    /// <summary>The columns declared, in order.</summary>
    public IReadOnlyList<ColumnBuilder> Columns => columns.AsReadOnly();

    /// <summary>The rows every column has reached: the complete rows so far.</summary>
    public int RowCount
    {
        get
        {
            if (columns.Count == 0)
                return 0;
            var rows = int.MaxValue;
            foreach (var column in columns)
                rows = Math.Min(rows, column.Count);
            return rows;
        }
    }

    internal SnapshotTuning Tuning
    {
        get => tuning;
        set
        {
            if (columns.Count > 0)
                throw new InvalidOperationException("The tuning is set before any column is declared.");
            tuning = value;
        }
    }

    /// <summary>Declares a Text column and returns what appends to it.</summary>
    public TextColumnBuilder Text(string name, string? caption = null) => Add(new TextColumnBuilder(this, name, caption));

    /// <summary>Declares a Decimal column and returns what appends to it.</summary>
    public DecimalColumnBuilder Decimal(string name, string? caption = null) => Add(new DecimalColumnBuilder(this, name, caption));

    /// <summary>Declares a Double column and returns what appends to it.</summary>
    public DoubleColumnBuilder Double(string name, string? caption = null) => Add(new DoubleColumnBuilder(this, name, caption));

    /// <summary>Declares an Integer column and returns what appends to it.</summary>
    public IntegerColumnBuilder Integer(string name, string? caption = null) => Add(new IntegerColumnBuilder(this, name, caption));

    /// <summary>Declares a Date column and returns what appends to it.</summary>
    public DateColumnBuilder Date(string name, string? caption = null) => Add(new DateColumnBuilder(this, name, caption));

    /// <summary>Declares a Boolean column and returns what appends to it.</summary>
    public BooleanColumnBuilder Boolean(string name, string? caption = null) => Add(new BooleanColumnBuilder(this, name, caption));

    /// <summary>
    /// Names the Record Key: a declared Text or Integer column whose value tells each row from every
    /// other. The build then refuses a Blank key and a key carried twice, naming it.
    /// </summary>
    /// <exception cref="ArgumentException">No such column is declared, or it is neither Text nor Integer.</exception>
    public SnapshotColumnsBuilder Key(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var column = columns.Find(c => c.Name == name)
            ?? throw new ArgumentException($"No column named '{name}' is declared; declare it before naming it the Record Key.", nameof(name));
        if (column.Kind is not (SnapshotKind.Text or SnapshotKind.Integer))
            throw new ArgumentException($"Only a Text or an Integer column can be the Record Key; '{name}' is {column.Kind}.", nameof(name));
        key = name;
        return this;
    }

    /// <summary>
    /// A point between rows where the load may pause: it throws when the load was cancelled, and when
    /// the current slice has spent its budget it reports progress — the complete rows, and the bytes
    /// when the reader gives them — and yields. It costs a look at the clock otherwise.
    /// </summary>
    /// <exception cref="OperationCanceledException">The load was cancelled.</exception>
    public ValueTask CheckpointAsync(long? bytes = null, long? totalBytes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return pacer.Due
            ? pacer.EndSliceAsync(new SnapshotProgress(RowCount, TotalRows, bytes, totalBytes))
            : ValueTask.CompletedTask;
    }

    /// <summary>Builds the Snapshot on the calling thread, once every column holds the same number of rows.</summary>
    /// <exception cref="SnapshotException">A Record Key is Blank or carried twice.</exception>
    /// <exception cref="InvalidOperationException">The columns hold different numbers of rows, the load
    /// was refused, or the builder has already built.</exception>
    public Snapshot Build()
    {
        var finish = Finish();
        finish.Step(long.MaxValue);
        return finish.Snapshot();
    }

    /// <summary>
    /// Builds the Snapshot, indexing its Record Keys in slices and yielding between them, once every
    /// column holds the same number of rows.
    /// </summary>
    /// <exception cref="SnapshotException">A Record Key is Blank or carried twice.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled.</exception>
    /// <exception cref="InvalidOperationException">The columns hold different numbers of rows, the load
    /// was refused, or the builder has already built.</exception>
    public async ValueTask<Snapshot> BuildAsync()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var finish = Finish();
        while (!finish.Step(pacer.Deadline))
            await pacer.EndSliceAsync(new SnapshotProgress(finish.IndexedRows, finish.Rows)).ConfigureAwait(false);
        pacer.Report(new SnapshotProgress(finish.Rows, finish.Rows));
        return finish.Snapshot();
    }

    /// <summary>Reports <paramref name="state"/> at once, as a reader does when its last row is read.</summary>
    internal void Report(SnapshotProgress state) => pacer.Report(state);

    internal void CheckOpen()
    {
        if (refused)
            throw new InvalidOperationException("The load was refused; a refused load builds nothing.");
        if (built)
            throw new InvalidOperationException("The Snapshot has been built; a builder builds once.");
    }

    internal SnapshotException Refuse(SnapshotException refusal)
    {
        refused = true;
        return refusal;
    }

    private TColumn Add<TColumn>(TColumn column)
        where TColumn : ColumnBuilder
    {
        CheckOpen();
        if (columns.Exists(c => c.Name == column.Name))
            throw new ArgumentException($"A column named '{column.Name}' is already declared; a column's name is unique within a Snapshot.", nameof(column));
        columns.Add(column);
        return column;
    }

    private Finishing Finish()
    {
        CheckOpen();
        var rows = columns.Count == 0 ? 0 : columns[0].Count;
        foreach (var column in columns)
        {
            if (column.Count != rows)
            {
                var counts = string.Join(", ", columns.Select(c => string.Create(CultureInfo.InvariantCulture, $"'{c.Name}' {c.Count:N0}")));
                throw new InvalidOperationException($"Every column holds the same number of rows when the Snapshot is built; they hold {counts}.");
            }
        }
        built = true;

        var shape = new Shape([.. columns.Select(c => (c.Name, c.Caption, c.Kind))], key, null, tuning);
        var perColumn = columns.Select(c => c.SealAll()).ToArray();
        var segments = new List<Segment>();
        var segmentCount = perColumn.Length == 0 ? 0 : perColumn[0].Count;
        for (var s = 0; s < segmentCount; s++)
        {
            var data = new ColumnData[perColumn.Length];
            for (var c = 0; c < data.Length; c++)
                data[c] = perColumn[c][s];
            var length = Math.Min(tuning.SegmentLength, rows - (s << tuning.SegmentShift));
            segments.Add(new Segment(length, data, null, s << tuning.SegmentShift, null, null));
        }

        var stores = new TextStore?[columns.Count];
        var textCounts = new int[columns.Count];
        for (var c = 0; c < columns.Count; c++)
        {
            if (columns[c] is TextColumnBuilder text)
            {
                textCounts[c] = text.Interner.Count;
                stores[c] = text.Interner.ToStore();
            }
        }
        return new Finishing(shape, segments, stores, textCounts, rows, Version);
    }

    /// <summary>The last step of a build: the Record Keys indexed, in steps, then the Snapshot made.</summary>
    private sealed class Finishing(Shape shape, List<Segment> segments, TextStore?[] stores, int[] counts, int rows, long version)
    {
        private readonly KeyIndexer? keys = shape.Key is null
            ? null
            : new KeyIndexer(shape, segments, rows) { TextOf = code => stores[shape.KeyOrdinal]!.Text(code) };

        public int Rows => rows;

        public int IndexedRows => keys?.Rows ?? rows;

        public bool Step(long deadline) => keys is null || keys.Step(deadline);

        public Snapshot Snapshot() => Writers.Base(shape, segments, stores, counts, keys?.Index, rows, version);
    }
}
