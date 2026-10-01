using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// Builds a Snapshot from the Consumer's records, through an accessor per column (ADR-0063). A typed
/// accessor reads a value without boxing it; an untyped one, returning <see cref="object"/> under a
/// declared kind, remains for columns known only at run time. The records are kept, by reference and
/// in order, behind the rows.
/// <para>
/// A build either yields a Snapshot or refuses whole, naming the row and the column of the value it
/// could not read — never a Snapshot with a row left out, whose totals would be quietly short. The
/// declaration is reused: every build, and every batch made by <see cref="Batch"/>, reads the
/// records through the same columns.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the Consumer's records.</typeparam>
public sealed class SnapshotBuilder<T>
{
    private readonly List<ObjectColumn<T>> columns = [];
    private string? key;

    internal SnapshotTuning Tuning { get; set; } = SnapshotTuning.Default;

    /// <summary>Declares a Text column. <see langword="null"/> is a Blank; the empty string is a value.</summary>
    public SnapshotBuilder<T> Text(string name, Func<T, string?> value, string? caption = null)
        => Add(new TextObjectColumn<T>(name, caption, Checked(value)));

    /// <summary>Declares a Decimal column, held exactly. <see langword="null"/> is a Blank; zero is a value.</summary>
    public SnapshotBuilder<T> Decimal(string name, Func<T, decimal?> value, string? caption = null)
        => Add(new DecimalObjectColumn<T>(name, caption, Checked(value)));

    /// <summary>Declares a Double column, held as each value comes, non-finite values included.
    /// <see langword="null"/> is a Blank; zero and NaN are values.</summary>
    public SnapshotBuilder<T> Double(string name, Func<T, double?> value, string? caption = null)
        => Add(new DoubleObjectColumn<T>(name, caption, Checked(value)));

    /// <summary>Declares an Integer column of 64-bit integers. <see langword="null"/> is a Blank; zero is a value.</summary>
    public SnapshotBuilder<T> Integer(string name, Func<T, long?> value, string? caption = null)
        => Add(new IntegerObjectColumn<T>(name, caption, Checked(value)));

    /// <summary>Declares an Integer column read through an <see cref="int"/> accessor.
    /// <see langword="null"/> is a Blank; zero is a value.</summary>
    public SnapshotBuilder<T> Integer(string name, Func<T, int?> value, string? caption = null)
        => Add(new Int32ObjectColumn<T>(name, caption, Checked(value)));

    /// <summary>Declares a Date column. A <see cref="DateTime"/> is held as its ticks, with its
    /// <see cref="DateTime.Kind"/> ignored. <see langword="null"/> is a Blank.</summary>
    public SnapshotBuilder<T> Date(string name, Func<T, DateTime?> value, string? caption = null)
        => Add(new DateObjectColumn<T, DateTime>(name, caption, Checked(value), Clock.Ticks));

    /// <summary>Declares a Date column. A <see cref="DateOnly"/> is held as its midnight.
    /// <see langword="null"/> is a Blank.</summary>
    public SnapshotBuilder<T> Date(string name, Func<T, DateOnly?> value, string? caption = null)
        => Add(new DateObjectColumn<T, DateOnly>(name, caption, Checked(value), Clock.Ticks));

    /// <summary>Declares a Date column. A <see cref="DateTimeOffset"/> is held as the clock it shows,
    /// with its offset dropped. <see langword="null"/> is a Blank.</summary>
    public SnapshotBuilder<T> Date(string name, Func<T, DateTimeOffset?> value, string? caption = null)
        => Add(new DateObjectColumn<T, DateTimeOffset>(name, caption, Checked(value), Clock.Ticks));

    /// <summary>Declares a Boolean column. <see langword="null"/> is a Blank; false is a value.</summary>
    public SnapshotBuilder<T> Boolean(string name, Func<T, bool?> value, string? caption = null)
        => Add(new BooleanObjectColumn<T>(name, caption, Checked(value)));

    /// <summary>
    /// Declares a column known only at run time, whose accessor returns <see cref="object"/> under a
    /// declared kind. A Text column takes any value: a value that is not a string is Text by its
    /// invariant text (ADR-0059) — an enum by its name, a <see cref="Guid"/> in its D form, a number
    /// or a date as the invariant culture writes it. The other kinds take a value of the kind, or one
    /// that converts to it exactly — any integer type for Decimal and Integer, a <see cref="float"/>
    /// for Double, and <see cref="DateTime"/>, <see cref="DateOnly"/> and <see cref="DateTimeOffset"/>
    /// for Date; a value of any other type, a string included, fails the build, naming the row and the
    /// column. <see langword="null"/> and <see cref="DBNull"/> are Blanks. Each value comes boxed; a
    /// typed accessor is the way to read many.
    /// </summary>
    public SnapshotBuilder<T> Column(string name, SnapshotKind kind, Func<T, object?> value, string? caption = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind a Snapshot holds.");
        return Add(new UntypedObjectColumn<T>(name, caption, kind, Checked(value)));
    }

    /// <summary>
    /// Names the Record Key: a declared Text or Integer column whose value tells each record from every
    /// other. A build then refuses a Blank key and a key carried by two records, naming it, and the
    /// Snapshot takes Change Batches that change and remove records by it.
    /// </summary>
    /// <exception cref="ArgumentException">No such column is declared, or it is neither Text nor Integer.</exception>
    public SnapshotBuilder<T> Key(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var column = columns.Find(c => c.Name == name)
            ?? throw new ArgumentException($"No column named '{name}' is declared; declare it before naming it the Record Key.", nameof(name));
        if (column.Kind is not (SnapshotKind.Text or SnapshotKind.Integer))
            throw new ArgumentException($"Only a Text or an Integer column can be the Record Key; '{name}' is {column.Kind}.", nameof(name));
        key = name;
        return this;
    }

    /// <summary>Builds a Snapshot from <paramref name="records"/> in one go, on the calling thread.</summary>
    /// <exception cref="SnapshotException">A value could not be read, or a Record Key is Blank or carried twice.</exception>
    public Snapshot Build(IReadOnlyList<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var build = Start(records);
        build.ReadStep(long.MaxValue);
        build.IndexStep(long.MaxValue);
        return build.Finish();
    }

    /// <summary>
    /// Builds a Snapshot from <paramref name="records"/> in slices sized by time, yielding between them
    /// and reporting progress after each (<see cref="SnapshotLoadOptions"/>). A cancelled build throws
    /// <see cref="OperationCanceledException"/> and yields nothing. The records must not change while
    /// it runs; a change in their number is refused.
    /// </summary>
    /// <exception cref="SnapshotException">A value could not be read, or a Record Key is Blank or carried twice.</exception>
    public async ValueTask<Snapshot> BuildAsync(IReadOnlyList<T> records, SnapshotLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();
        var build = Start(records);
        var pacer = new Pacer(options, cancellationToken);
        while (!build.ReadStep(pacer.Deadline))
            await pacer.EndSliceAsync(new SnapshotProgress(build.Rows, build.Total)).ConfigureAwait(false);
        while (!build.IndexStep(pacer.Deadline))
            await pacer.EndSliceAsync(new SnapshotProgress(build.Rows, build.Total)).ConfigureAwait(false);
        pacer.Report(new SnapshotProgress(build.Total, build.Total));
        return build.Finish();
    }

    /// <summary>
    /// A Change Batch read through these columns: the records added, the records changed (found by
    /// their Record Key) and the keys removed — each key a <see cref="string"/> for a Text key, an
    /// integer for an Integer one.
    /// </summary>
    /// <exception cref="SnapshotException">A value of a record could not be read, or a key is Blank or carried twice.</exception>
    public ChangeBatch Batch(IEnumerable<T>? added = null, IEnumerable<T>? changed = null, IEnumerable<object>? removedKeys = null)
        => ChangeBatch.Of(
            added is null ? null : Build(AsList(added)),
            changed is null ? null : Build(AsList(changed)),
            removedKeys?.ToList());

    private ObjectsBuild<T> Start(IReadOnlyList<T> records)
    {
        var shape = new Shape([.. columns.Select(c => (c.Name, c.Caption, c.Kind))], key, typeof(T), Tuning);
        return new ObjectsBuild<T>(shape, [.. columns], records);
    }

    private SnapshotBuilder<T> Add(ObjectColumn<T> column)
    {
        if (string.IsNullOrEmpty(column.Name))
            throw new ArgumentException("A column has a name.", "name");
        if (columns.Exists(c => c.Name == column.Name))
            throw new ArgumentException($"A column named '{column.Name}' is already declared; a column's name is unique within a Snapshot.", nameof(column));
        columns.Add(column);
        return this;
    }

    private static TAccessor Checked<TAccessor>(TAccessor accessor)
        where TAccessor : Delegate
    {
        ArgumentNullException.ThrowIfNull(accessor, "value");
        return accessor;
    }

    private static IReadOnlyList<T> AsList(IEnumerable<T> records) => records as IReadOnlyList<T> ?? [.. records];
}
