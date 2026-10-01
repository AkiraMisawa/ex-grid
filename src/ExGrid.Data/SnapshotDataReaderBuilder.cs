using System.Data.Common;
using System.Globalization;
using ExGrid.Data.Db;

namespace ExGrid.Data;

/// <summary>
/// Builds a Snapshot from a <see cref="DbDataReader"/> (ADR-0063's third way in): ADO.NET's reader,
/// which every .NET database driver provides and which Entity Framework Core runs on. The reader
/// already knows each column's type, so nothing is guessed: a <see cref="decimal"/> is Decimal; a
/// <see cref="double"/> or a <see cref="float"/> is Double; an integer is Integer, an unsigned one
/// within a 64-bit Integer's range; a <see cref="DateTime"/>, a <see cref="DateOnly"/>, a
/// <see cref="DateTimeOffset"/> and a <see cref="TimeOnly"/> are Date, each the clock it shows — a
/// <see cref="DateTime"/>'s <see cref="DateTime.Kind"/> ignored, a <see cref="DateTimeOffset"/>'s
/// offset dropped, a <see cref="TimeOnly"/> on the first day; a <see cref="bool"/> is Boolean; a
/// <see cref="string"/> and a <see cref="char"/> are Text. <see cref="DBNull"/> is a Blank.
/// <para>
/// A column of any other type — a <see cref="Guid"/>, a <c>byte[]</c>, a <see cref="TimeSpan"/> — is
/// refused by name, unless the Consumer declares its kind and how to read it, with a conversion such
/// as <see cref="Text{TField}(string, Func{TField, string?}, string?, string?)"/>.
/// </para>
/// <para>
/// With no column declared, every column of the reader is read, under its own name. Once one is
/// declared, only the declared columns are read, in the order declared, each under the name and the
/// caption it declares. A load either yields a Snapshot or fails whole, naming the row and the
/// column of the value it could not read. The declaration is reused: every build reads through the
/// same columns.
/// </para>
/// </summary>
public sealed class SnapshotDataReaderBuilder
{
    private readonly List<Declared> declared = [];
    private string? key;

    /// <summary>Chooses the reader's column <paramref name="field"/>, read by its own type, under
    /// <paramref name="name"/> — the field's own name when <see langword="null"/> — and
    /// <paramref name="caption"/>.</summary>
    public SnapshotDataReaderBuilder Column(string field, string? name = null, string? caption = null)
        => Add(field, name, caption, null, null);

    /// <summary>Chooses the reader's column <paramref name="field"/> as Text, reading each value as a
    /// <typeparamref name="TField"/> and converting it with <paramref name="read"/>; a
    /// <see langword="null"/> it returns is a Blank. It reads a column of a type no kind reads by itself,
    /// such as a <see cref="Guid"/>, or reads a column otherwise than by its own type.</summary>
    public SnapshotDataReaderBuilder Text<TField>(string field, Func<TField, string?> read, string? name = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Add(field, name, caption, SnapshotKind.Text, (n, c, o) => DbFields.Text(n, c, o, (r, at) => read(r.GetFieldValue<TField>(at))));
    }

    /// <summary>Chooses the reader's column <paramref name="field"/> as Decimal, reading each value as a
    /// <typeparamref name="TField"/> and converting it with <paramref name="read"/>; a
    /// <see langword="null"/> it returns is a Blank.</summary>
    public SnapshotDataReaderBuilder Decimal<TField>(string field, Func<TField, decimal?> read, string? name = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Add(field, name, caption, SnapshotKind.Decimal, (n, c, o) => DbFields.Decimal(n, c, o, (r, at) => read(r.GetFieldValue<TField>(at))));
    }

    /// <summary>Chooses the reader's column <paramref name="field"/> as Double, reading each value as a
    /// <typeparamref name="TField"/> and converting it with <paramref name="read"/> — a
    /// <see cref="TimeSpan"/> as its hours, say; a <see langword="null"/> it returns is a Blank.</summary>
    public SnapshotDataReaderBuilder Double<TField>(string field, Func<TField, double?> read, string? name = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Add(field, name, caption, SnapshotKind.Double, (n, c, o) => DbFields.Double(n, c, o, (r, at) => read(r.GetFieldValue<TField>(at))));
    }

    /// <summary>Chooses the reader's column <paramref name="field"/> as Integer, reading each value as a
    /// <typeparamref name="TField"/> and converting it with <paramref name="read"/>; a
    /// <see langword="null"/> it returns is a Blank.</summary>
    public SnapshotDataReaderBuilder Integer<TField>(string field, Func<TField, long?> read, string? name = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Add(field, name, caption, SnapshotKind.Integer, (n, c, o) => DbFields.Integer(n, c, o, (r, at) => read(r.GetFieldValue<TField>(at))));
    }

    /// <summary>Chooses the reader's column <paramref name="field"/> as Date, reading each value as a
    /// <typeparamref name="TField"/> and converting it with <paramref name="read"/> to the clock it
    /// shows, its <see cref="DateTime.Kind"/> ignored; a <see langword="null"/> it returns is a Blank.</summary>
    public SnapshotDataReaderBuilder Date<TField>(string field, Func<TField, DateTime?> read, string? name = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Add(field, name, caption, SnapshotKind.Date, (n, c, o) => DbFields.Date(n, c, o, (r, at) => read(r.GetFieldValue<TField>(at))));
    }

    /// <summary>Chooses the reader's column <paramref name="field"/> as Boolean, reading each value as a
    /// <typeparamref name="TField"/> and converting it with <paramref name="read"/>; a
    /// <see langword="null"/> it returns is a Blank.</summary>
    public SnapshotDataReaderBuilder Boolean<TField>(string field, Func<TField, bool?> read, string? name = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Add(field, name, caption, SnapshotKind.Boolean, (n, c, o) => DbFields.Boolean(n, c, o, (r, at) => read(r.GetFieldValue<TField>(at))));
    }

    /// <summary>
    /// Names the Record Key: the Snapshot column, Text or Integer, whose value tells each row from every
    /// other. A build then refuses a Blank key and a key carried by two rows, naming it, and a key
    /// column the reader gives as another kind, by name.
    /// </summary>
    /// <exception cref="ArgumentException">A column is declared under no such name, or it is declared
    /// as neither Text nor Integer.</exception>
    public SnapshotDataReaderBuilder Key(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (declared.Count > 0)
        {
            var column = declared.Find(d => d.Name == name)
                ?? throw new ArgumentException($"No column named '{name}' is declared; declare it before naming it the Record Key.", nameof(name));
            if (column.Kind is { } kind && kind is not (SnapshotKind.Text or SnapshotKind.Integer))
                throw new ArgumentException($"Only a Text or an Integer column can be the Record Key; '{name}' is {kind}.", nameof(name));
        }
        key = name;
        return this;
    }

    /// <summary>
    /// Reads <paramref name="reader"/>'s current result set, from the row it stands before to its end,
    /// into a Snapshot, in slices sized by time that yield between them and report the rows read
    /// (<see cref="SnapshotLoadOptions"/>). A cancelled build throws
    /// <see cref="OperationCanceledException"/> and yields nothing. The reader is read with
    /// <see cref="DbDataReader.ReadAsync(CancellationToken)"/>; it is neither moved to its next result
    /// set nor closed.
    /// </summary>
    /// <exception cref="SnapshotException">A column is missing from the reader, or of a type no kind
    /// reads by itself, named; or a value could not be read, naming its row and its column; or a Record
    /// Key is Blank or carried twice.</exception>
    public async ValueTask<Snapshot> BuildAsync(DbDataReader reader, SnapshotLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        var fields = Fields(reader);
        var builder = new SnapshotColumnsBuilder(options, cancellationToken);
        foreach (var field in fields)
            field.Declare(builder);
        if (key is not null)
            builder.Key(key);

        long rows = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var f = 0;
            try
            {
                for (; f < fields.Length; f++)
                    fields[f].Read(reader);
            }
            catch (UnsignedOverflow overflow)
            {
                throw new SnapshotException(rows + 1, fields[f].Name, overflow.Message);
            }
            catch (Exception e) when (e is not (SnapshotException or OperationCanceledException))
            {
                throw new SnapshotException(rows + 1, fields[f].Name, $"reading the value threw {e.GetType().Name}: {e.Message}", e);
            }
            rows++;
            if ((rows & 63) == 0)
                await builder.CheckpointAsync().ConfigureAwait(false);
        }
        builder.TotalRows = rows;
        builder.Report(new SnapshotProgress(rows, rows));
        return await builder.BuildAsync().ConfigureAwait(false);
    }

    /// <summary>The fields to read: the declared ones, matched to the reader by name, or every column of
    /// the reader. Every refusal about the reader's columns is made here, before a row is read.</summary>
    private DbField[] Fields(DbDataReader reader)
    {
        var count = reader.FieldCount;
        if (count == 0)
            throw new SnapshotException("The reader has no columns.");
        var names = new string[count];
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var repeated = new HashSet<string>(StringComparer.Ordinal);
        for (var o = 0; o < count; o++)
        {
            names[o] = reader.GetName(o) ?? "";
            if (!ordinals.TryAdd(names[o], o))
                repeated.Add(names[o]);
        }

        var fields = new List<DbField>();
        var unread = new List<(string Name, Type? Type)>();
        if (declared.Count == 0)
        {
            var unnamed = Array.IndexOf(names, "");
            if (unnamed >= 0)
                throw new SnapshotException(string.Create(CultureInfo.InvariantCulture, $"The reader's column {unnamed} has no name; name it in the query, or declare the columns to read."));
            if (repeated.Count > 0)
                throw new SnapshotException(null, repeated.First(), "the reader has two columns of this name, and a Snapshot's names are unique; name them apart in the query, or declare the columns to read.");
            for (var o = 0; o < count; o++)
            {
                var type = reader.GetFieldType(o);
                if (DbFields.ByType(type, names[o], names[o], o) is { } field)
                    fields.Add(field);
                else
                    unread.Add((names[o], type));
            }
        }
        else
        {
            var missing = declared.Where(d => !ordinals.ContainsKey(d.Field)).ToList();
            if (missing.Count > 0)
                throw Missing(missing, names);
            foreach (var d in declared)
            {
                if (repeated.Contains(d.Field))
                    throw new SnapshotException(null, d.Name, $"the reader has two columns named '{d.Field}', so which one it is cannot be told; name them apart in the query.");
                var ordinal = ordinals[d.Field];
                var type = reader.GetFieldType(ordinal);
                if ((d.Converted?.Invoke(d.Name, d.Caption, ordinal) ?? DbFields.ByType(type, d.Name, d.Caption, ordinal)) is { } field)
                    fields.Add(field);
                else
                    unread.Add((d.Name, type));
            }
        }
        if (unread.Count > 0)
            throw Unread(unread);

        if (key is not null)
        {
            var keyField = fields.Find(f => f.Name == key)
                ?? throw new SnapshotException(null, key, declared.Count == 0
                    ? "the Record Key is not a column of the reader."
                    : "the Record Key is not among the columns declared.");
            if (keyField.Kind is not (SnapshotKind.Text or SnapshotKind.Integer))
                throw new SnapshotException(null, key, $"the reader gives the Record Key as {keyField.Kind}; only a Text or an Integer column can be the Record Key.");
        }
        return [.. fields];
    }

    private static SnapshotException Missing(List<Declared> missing, string[] names)
    {
        var first = missing[0];
        var reason = first.Field == first.Name ? "it is missing from the reader" : $"its field, '{first.Field}', is missing from the reader";
        if (missing.Count > 1)
        {
            var others = missing.Skip(1).Select(d => $"'{d.Name}'").ToList();
            reason += others.Count == 1 ? $", and so is {others[0]}" : $", and so are {string.Join(", ", others.SkipLast(1))} and {others[^1]}";
        }
        var near = Array.Find(names, n => string.Equals(n.Trim(), first.Field.Trim(), StringComparison.OrdinalIgnoreCase));
        if (near is not null)
            reason += $"; the reader has '{near}'";
        return new SnapshotException(null, first.Name, reason + ".");
    }

    private static SnapshotException Unread(List<(string Name, Type? Type)> unread)
    {
        var (name, type) = unread[0];
        var reason = $"its type, {DbFields.Shown(type)}, is not one a Snapshot reads by itself; declare its kind and how to read it";
        if (unread.Count > 1)
        {
            var others = unread.Skip(1).Select(u => $"'{u.Name}' ({DbFields.Shown(u.Type)})").ToList();
            reason += others.Count == 1 ? $". So is {others[0]}" : $". So are {string.Join(", ", others.SkipLast(1))} and {others[^1]}";
        }
        return new SnapshotException(null, name, reason + ".");
    }

    private SnapshotDataReaderBuilder Add(string field, string? name, string? caption, SnapshotKind? kind, Func<string, string, int, DbField>? converted)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        var columnName = name ?? field;
        if (columnName.Length == 0)
            throw new ArgumentException("A column has a name.", nameof(name));
        if (declared.Exists(d => d.Name == columnName))
            throw new ArgumentException($"A column named '{columnName}' is already declared; a column's name is unique within a Snapshot.", nameof(name));
        declared.Add(new Declared(field, columnName, caption ?? columnName, kind, converted));
        return this;
    }

    /// <summary>A column declared: the reader's field it reads, its name and caption, and — when the
    /// Consumer declared how to read it — its kind and the field that reads it.</summary>
    private sealed record Declared(string Field, string Name, string Caption, SnapshotKind? Kind, Func<string, string, int, DbField>? Converted);
}
