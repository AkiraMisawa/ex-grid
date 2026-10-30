using System.Globalization;

namespace ExGrid.Data;

/// <summary>
/// A load or a Change Batch refused (ADR-0064). A load that meets a value it cannot read fails
/// whole, naming the row and the column, and yields no Snapshot; a batch that names a key wrongly
/// is refused whole, naming the key, and nothing of it is applied.
/// </summary>
public sealed class SnapshotException : Exception
{
    /// <summary>A refusal that names neither a row nor a column.</summary>
    public SnapshotException(string message)
        : base(message)
    {
    }

    /// <summary>A refusal that names neither a row nor a column, caused by another exception.</summary>
    public SnapshotException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// A refusal naming the row and the column it met, worded the same way for every way in:
    /// <c>Row 12,345, column 'Notional': 'abc' is not a number.</c>
    /// </summary>
    /// <param name="row">The 1-based number of the row refused, when there is one.</param>
    /// <param name="column">The name of the column refused, when there is one.</param>
    /// <param name="reason">What was wrong, as a sentence that follows the row and the column.</param>
    /// <param name="innerException">What caused it, when it was another exception.</param>
    public SnapshotException(long? row, string? column, string reason, Exception? innerException = null)
        : base(Format(row, column, reason), innerException)
    {
        Row = row;
        Column = column;
    }

    /// <summary>The 1-based number of the row refused, or <see langword="null"/> when the refusal is
    /// not about one row.</summary>
    public long? Row { get; }

    /// <summary>The name of the column refused, or <see langword="null"/> when the refusal is not
    /// about one column. A refusal about a Record Key names the key's column.</summary>
    public string? Column { get; }

    /// <summary>The Record Key refused — a <see cref="string"/> or a <see cref="long"/> — or
    /// <see langword="null"/> when the refusal is not about a key.</summary>
    public object? Key { get; init; }

    private static string Format(long? row, string? column, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return (row, column) switch
        {
            ({ } r, { } c) => string.Create(CultureInfo.InvariantCulture, $"Row {r:N0}, column '{c}': {reason}"),
            ({ } r, null) => string.Create(CultureInfo.InvariantCulture, $"Row {r:N0}: {reason}"),
            (null, { } c) => $"Column '{c}': {reason}",
            _ => reason,
        };
    }
}
