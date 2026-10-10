using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace ExPivot.Engine;

/// <summary>
/// The digest of a report Window (ADR-0152): what Window Changes say their Baseline Window becomes,
/// so that a client which applies them to the Window it holds can tell whether the result is the
/// source's Window — before anything of it is shown. Window Changes that leave out a row whose
/// shown values changed — a subtotal, a grand total, a row whose percentage moved — make a Window
/// whose digest differs, and the client asks for a complete Window instead
/// (<see cref="PivotReportClient"/>).
/// <para>
/// <b>What it covers.</b> The Window as it stands after the update, every row of it in order, not
/// the rows Window Changes carry: its Report Version; its first row, its row count and the whole
/// report's row count; and per row its key (role, Value Field and Items), its Item path (field and
/// Item), its label cells (text, indent and expand/collapse button) and each value cell's shown
/// text. A cell's change mark (<see cref="PivotDisplayRow.ChangedIn"/>) and its raw values are not
/// covered.
/// </para>
/// <para>
/// <b>How it is computed.</b> A 64-bit FNV-1a over a canonical byte serialization, written as 16
/// lowercase hexadecimal digits. An integer is four bytes little-endian, a Boolean one byte (0 or
/// 1), a string its UTF-8 byte count as an integer (−1 for null) followed by its UTF-8 bytes, an
/// Item its kind (<see cref="PivotItemKind"/>, as an integer) and its value as a string, a list its
/// count followed by its entries. The serialization begins with the string
/// <c>ExPivot.WindowDigest/1</c>, and the fields follow in the order above: the Report
/// Version, the first row, the row count, the report's row count; then each row's role and Value
/// Field (its role as <see cref="PivotRowRole"/>'s integer), its key's Items, its path (each step's
/// field and Item), its labels (text,
/// indent, whether a button follows, and the button's field, Item kind and value, Item label and
/// collapsed state) and its values (shown text, null for an empty cell). It uses nothing that
/// differs between processes or platforms — no <see cref="object.GetHashCode"/> — so a server and a
/// browser compute the same digest of the same Window.
/// </para>
/// </summary>
public static class PivotReportDigest
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>The digest of a Window: <paramref name="rows"/>, starting at
    /// <paramref name="windowStart"/>, of the report <paramref name="metadata"/> describes.</summary>
    /// <param name="metadata">The report the Window belongs to.</param>
    /// <param name="windowStart">The Window's first row in the whole report.</param>
    /// <param name="rows">Every row of the Window, in order.</param>
    public static string Of(PivotReportMetadata metadata, int windowStart, IReadOnlyList<PivotDisplayRow> rows)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(rows);
        var digest = new Fnv();
        digest.Text("ExPivot.WindowDigest/1");
        digest.Text(metadata.Version.Value);
        digest.Int(windowStart);
        digest.Int(rows.Count);
        digest.Int(metadata.RowCount);
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row, nameof(rows));
            digest.Int((int)row.Role);
            digest.Int(row.ValueField);
            var items = row.Key.Items;
            digest.Int(items.Count);
            foreach (var item in items)
                digest.Item(item);
            digest.Int(row.RowPath.Count);
            foreach (var step in row.RowPath)
            {
                digest.Text(step.Field);
                digest.Item(step.Item);
            }
            digest.Int(row.Labels.Count);
            foreach (var label in row.Labels)
            {
                digest.Text(label.Text);
                digest.Int(label.Indent);
                digest.Bool(label.Toggle is not null);
                if (label.Toggle is { } toggle)
                {
                    digest.Text(toggle.Field);
                    digest.Item(toggle.Item);
                    digest.Text(toggle.ItemLabel);
                    digest.Bool(toggle.IsCollapsed);
                }
            }
            digest.Int(row.Values.Count);
            foreach (var value in row.Values)
                digest.Text(value?.Text);
        }
        return digest.Hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>The 64-bit FNV-1a of <paramref name="bytes"/>, for the tests that pin the function.</summary>
    internal static ulong Fnv1a(ReadOnlySpan<byte> bytes)
    {
        var digest = new Fnv();
        digest.Bytes(bytes);
        return digest.Hash;
    }

    private struct Fnv()
    {
        public ulong Hash { get; private set; } = Offset;

        public void Bytes(ReadOnlySpan<byte> bytes)
        {
            var hash = Hash;
            foreach (var b in bytes)
            {
                hash ^= b;
                hash = unchecked(hash * Prime);
            }
            Hash = hash;
        }

        public void Int(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            Bytes(bytes);
        }

        public void Bool(bool value) => Bytes([value ? (byte)1 : (byte)0]);

        public void Text(string? text)
        {
            if (text is null)
            {
                Int(-1);
                return;
            }
            var count = Encoding.UTF8.GetByteCount(text);
            Int(count);
            Span<byte> small = stackalloc byte[256];
            var bytes = count <= small.Length ? small[..count] : new byte[count];
            Encoding.UTF8.GetBytes(text, bytes);
            Bytes(bytes);
        }

        public void Item(PivotItemKey item)
        {
            Int((int)item.Kind);
            Text(item.Value);
        }
    }
}
