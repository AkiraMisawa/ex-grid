using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ExGrid.Data.Xlsx;

/// <summary>What a written workbook says of itself, beside its sheets.</summary>
public sealed class XlsxWriteOptions
{
    /// <summary>The styles the cells' <see cref="XlsxCell.StyleIndex"/> point into; <see cref="XlsxStyles.Default"/> when null.</summary>
    public XlsxStyles? Styles { get; init; }

    /// <summary>The index of the sheet the workbook shows when it opens.</summary>
    public int ActiveSheet { get; init; }

    /// <summary>Whether the workbook counts dates from 1904. The serials written are the caller's.</summary>
    public bool Date1904 { get; init; }

    /// <summary>
    /// Whether the workbook asks to be calculated in full when Excel opens it (<c>fullCalcOnLoad</c>): every Formula is
    /// written with its Value, and Excel computes them again (ADR-0330 §6).
    /// </summary>
    public bool FullCalculationOnLoad { get; init; }

    /// <summary>The defined names the workbook part writes.</summary>
    public IReadOnlyList<XlsxDefinedName>? DefinedNames { get; init; }

    /// <summary>
    /// The uncompressed bytes the package's parts may hold together. A write that would go past it is refused by name
    /// (<see cref="XlsxException"/>, <see cref="XlsxRefusal.TooLarge"/>), naming the part, and the sheet, it met; the package
    /// is then abandoned, and only what an earlier <see cref="XlsxSheetWriter.FlushAsync"/> handed over has reached the
    /// stream. The default is <see cref="XlsxReadOptions.DefaultMaxUncompressedBytes"/>, the most a file is read with by
    /// default, so that what is written opens again. The package waits in memory between hand-offs, so a writer that never
    /// flushes before <see cref="XlsxWriter.CompleteAsync"/> holds about this much, compressed, at most; it is 1 to
    /// <see cref="XlsxReadOptions.DefaultMaxUncompressedBytes"/>.
    /// </summary>
    public long MaxUncompressedBytes { get; init; } = XlsxReadOptions.DefaultMaxUncompressedBytes;
}

/// <summary>
/// Writes an <c>.xlsx</c> workbook as a stream (ADR-0330 §1): each worksheet's cells go into the package as they are
/// written, row by row, and the parts that need every sheet — the shared strings, the styles, the workbook part, the
/// relationships and the content types — at <see cref="CompleteAsync"/>. The package is assembled in memory a little at
/// a time and handed to the output stream asynchronously by <see cref="XlsxSheetWriter.FlushAsync"/> and
/// <see cref="CompleteAsync"/>, so a response body that refuses synchronous writes takes it.
/// </summary>
public sealed class XlsxWriter : IAsyncDisposable
{
    internal static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        CloseOutput = true,
    };

    private readonly Stream output;
    private readonly ForwardBuffer buffer = new();
    private readonly ZipArchive zip;
    private readonly XlsxWriteOptions options;
    private readonly XlsxStyles styles;
    private readonly List<(string Name, XlsxSheetVisibility Visibility)> sheets = [];
    private readonly Dictionary<string, int> sharedIndex = new(StringComparer.Ordinal);
    private readonly List<string> shared = [];
    private readonly List<(int Sheet, XlsxTable Table)> tables = [];
    private XlsxSheetWriter? current;
    private bool completed;
    private bool packageClosed;
    private long sharedReferences;

    private XlsxWriter(Stream output, XlsxWriteOptions options)
    {
        this.output = output;
        this.options = options;
        styles = options.Styles ?? XlsxStyles.Default;
        if (options.MaxUncompressedBytes is < 1 or > XlsxReadOptions.DefaultMaxUncompressedBytes)
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxUncompressedBytes, $"MaxUncompressedBytes is 1 to {XlsxReadOptions.DefaultMaxUncompressedBytes:N0}.");
        Validate(styles);
        Validate(options.DefinedNames);
        zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true);
    }

    /// <summary>A writer of a workbook into <paramref name="stream"/>, which it leaves open.</summary>
    /// <param name="stream">Where the package goes; it need not seek.</param>
    /// <param name="options">What the workbook says of itself; null for the defaults.</param>
    public static XlsxWriter Create(Stream stream, XlsxWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new XlsxWriter(stream, options ?? new XlsxWriteOptions());
    }

    /// <summary>
    /// Begins the next worksheet, after the ones before it, ending the one being written. Its cells are then written in
    /// the order of the sheet: row by row, left to right.
    /// </summary>
    /// <param name="name">The sheet's name: 1 to 31 characters, none of <c>[ ] : * ? / \</c> and no control character or
    /// half of a surrogate pair, not beginning or ending with <c>'</c>, and no other sheet's in any case.</param>
    /// <param name="columns">The column widths, or null: ranges that do not overlap, each naming a cell format the styles
    /// hold.</param>
    /// <param name="visibility">Whether the sheet is shown.</param>
    public XlsxSheetWriter AddSheet(string name, IReadOnlyList<XlsxColumn>? columns = null, XlsxSheetVisibility visibility = XlsxSheetVisibility.Visible)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        CheckName(name);
        var last = -1;
        foreach (var c in (columns ?? []).OrderBy(c => c.First))
        {
            ArgumentNullException.ThrowIfNull(c, nameof(columns));
            if (c.First < 0 || c.Last < c.First || c.Last >= XlsxCellReference.MaxColumns)
                throw new ArgumentException($"The column range {c.First} to {c.Last} lies outside the sheet's columns.", nameof(columns));
            if (c.First <= last)
                throw new ArgumentException($"The column range {c.First} to {c.Last} overlaps another, which ends at {last}.", nameof(columns));
            if (c.StyleIndex < 0 || c.StyleIndex >= styles.CellFormats.Count)
                throw new ArgumentException($"The column range {c.First} to {c.Last} names the cell format {c.StyleIndex}, which the styles do not hold.", nameof(columns));
            last = c.Last;
        }
        current?.End();
        var index = sheets.Count;
        sheets.Add((name, visibility));
        var entry = zip.CreateEntry($"xl/worksheets/sheet{index + 1}.xml", CompressionLevel.Optimal);
        current = new XlsxSheetWriter(this, index, name, XmlWriter.Create(new CountedPart(this, entry.Open(), entry.FullName, name), Settings), columns, index == options.ActiveSheet);
        return current;
    }

    /// <summary>
    /// Ends the last worksheet, writes the parts that need every sheet, and hands what is left of the package to the
    /// stream. The workbook needs a sheet, and <see cref="XlsxWriteOptions.ActiveSheet"/> must name one of them.
    /// </summary>
    /// <param name="cancellationToken">Cancels the hand-off to the stream.</param>
    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        if (sheets.Count == 0)
            throw new InvalidOperationException("A workbook holds at least one sheet; add one before completing it.");
        if (options.ActiveSheet < 0 || options.ActiveSheet >= sheets.Count)
            throw new InvalidOperationException($"The active sheet {options.ActiveSheet} is not one of the workbook's {sheets.Count} sheets.");
        // Completed from here, whatever happens: nothing more is written. Should a part fail, the package is still open,
        // and DisposeAsync closes it.
        completed = true;
        current?.End();
        current = null;
        var dynamic = HasDynamicArrays;
        if (shared.Count > 0)
            Part("xl/sharedStrings.xml", WriteSharedStrings);
        Part("xl/styles.xml", w => StylesWriter.Write(w, styles));
        if (dynamic)
            Part("xl/metadata.xml", WriteMetadata);
        for (var i = 0; i < tables.Count; i++)
        {
            var (_, table) = tables[i];
            var id = i + 1;
            Part($"xl/tables/table{id}.xml", w => WriteTable(w, table, id));
        }
        foreach (var sheet in tables.Select(t => t.Sheet).Distinct())
            Part($"xl/worksheets/_rels/sheet{sheet + 1}.xml.rels", w => WriteSheetRelationships(w, sheet));
        Part("xl/workbook.xml", WriteWorkbook);
        Part("xl/_rels/workbook.xml.rels", w => WriteWorkbookRelationships(w, dynamic));
        Part("_rels/.rels", WriteRootRelationships);
        Part("[Content_Types].xml", w => WriteContentTypes(w, dynamic));
        ClosePackage();
        await buffer.DrainAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Abandons a workbook not completed, writing nothing more; the stream is left open, holding what was handed to it.</summary>
    public ValueTask DisposeAsync()
    {
        completed = true;
        if (!packageClosed)
        {
            current?.Abandon();
            current = null;
            ClosePackage();
        }
        return ValueTask.CompletedTask;
    }

    private void ClosePackage()
    {
        packageClosed = true;
        zip.Dispose();
    }

    /// <summary>Whether the package is closed: by <see cref="CompleteAsync"/>, or by <see cref="DisposeAsync"/>.</summary>
    internal bool PackageClosed => packageClosed;

    // ---- what the sheet writers share ----------------------------------------------------------

    internal XlsxStyles Styles => styles;

    internal bool HasDynamicArrays { get; set; }

    internal int Share(string text)
    {
        sharedReferences++;
        if (!sharedIndex.TryGetValue(text, out var at))
        {
            at = shared.Count;
            shared.Add(text);
            sharedIndex.Add(text, at);
        }
        return at;
    }

    internal ValueTask DrainAsync(CancellationToken cancellationToken) => buffer.DrainAsync(output, cancellationToken);

    /// <summary>
    /// Records a table on the sheet <paramref name="sheet"/>, checked against every other table and defined name of the
    /// workbook: the id of the relationship the sheet's <c>tablePart</c> names.
    /// </summary>
    internal string AddTable(int sheet, XlsxTable table)
    {
        CheckTable(table);
        if (tables.Any(t => string.Equals(t.Table.Name, table.Name, StringComparison.OrdinalIgnoreCase))
            || (options.DefinedNames ?? []).Any(n => string.Equals(n.Name, table.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"The workbook already has a table or a defined name named '{table.Name}', in some case.", nameof(table));
        var own = tables.Where(t => t.Sheet == sheet).Select(t => t.Table).ToList();
        if (own.FirstOrDefault(t => Overlaps(t, table)) is { } other)
            throw new ArgumentException($"The table '{table.Name}' ({table.Reference}) overlaps the table '{other.Name}' ({other.Reference}).", nameof(table));
        tables.Add((sheet, table));
        return "rId" + (own.Count + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static bool Overlaps(XlsxTable a, XlsxTable b) =>
        a.HeaderRow <= b.HeaderRow + b.DataRows && b.HeaderRow <= a.HeaderRow + a.DataRows
        && a.FirstColumn < b.FirstColumn + b.Columns.Count && b.FirstColumn < a.FirstColumn + a.Columns.Count;

    /// <summary>The uncompressed bytes the parts hold so far.</summary>
    private long uncompressed;

    /// <summary>Whether the parts went past <see cref="XlsxWriteOptions.MaxUncompressedBytes"/>: the package is abandoned.</summary>
    private bool tooLarge;

    /// <summary>Counts <paramref name="count"/> bytes more into a part, or refuses them past the limit; false once refused, and the bytes are dropped.</summary>
    internal bool Count(int count, string part, string? sheet)
    {
        if (tooLarge)
            return false;
        uncompressed += count;
        if (uncompressed <= options.MaxUncompressedBytes)
            return true;
        tooLarge = true;
        throw new XlsxException(XlsxRefusal.TooLarge, string.Create(CultureInfo.InvariantCulture,
            $"The workbook would hold more than the {options.MaxUncompressedBytes:N0} uncompressed bytes a file is written with (XlsxWriteOptions.MaxUncompressedBytes)."),
            part, sheet);
    }

    // ---- the parts -----------------------------------------------------------------------------

    private void Part(string name, Action<XmlWriter> write)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = XmlWriter.Create(new CountedPart(this, entry.Open(), name, null), Settings);
        writer.WriteStartDocument(standalone: true);
        write(writer);
        writer.WriteEndDocument();
    }

    private void WriteSharedStrings(XmlWriter w)
    {
        w.WriteStartElement("sst", Names.Main);
        w.WriteAttributeString("count", sharedReferences.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("uniqueCount", shared.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var text in shared)
        {
            w.WriteStartElement("si");
            WriteText(w, text);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    internal static void WriteText(XmlWriter w, string text)
    {
        w.WriteStartElement("t");
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains('\n') || text.Contains('\t')))
            w.WriteAttributeString("xml", "space", null, "preserve");
        w.WriteString(XString.Encode(text));
        w.WriteEndElement();
    }

    private void WriteMetadata(XmlWriter w)
    {
        const string xda = "http://schemas.microsoft.com/office/spreadsheetml/2017/dynamicarray";
        w.WriteStartElement("metadata", Names.Main);
        w.WriteAttributeString("xmlns", "xda", null, xda);
        w.WriteStartElement("metadataTypes");
        w.WriteAttributeString("count", "1");
        w.WriteStartElement("metadataType");
        foreach (var (name, value) in new[]
        {
            ("name", "XLDAPR"), ("minSupportedVersion", "120000"), ("copy", "1"), ("pasteAll", "1"), ("pasteValues", "1"),
            ("merge", "1"), ("splitFirst", "1"), ("rowColShift", "1"), ("clearFormats", "1"), ("clearComments", "1"),
            ("assign", "1"), ("coerce", "1"), ("cellMeta", "1"),
        })
        {
            w.WriteAttributeString(name, value);
        }
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteStartElement("futureMetadata");
        w.WriteAttributeString("name", "XLDAPR");
        w.WriteAttributeString("count", "1");
        w.WriteStartElement("bk");
        w.WriteStartElement("extLst");
        w.WriteStartElement("ext");
        w.WriteAttributeString("uri", Names.DynamicArrayExtension);
        w.WriteStartElement("xda", "dynamicArrayProperties", xda);
        w.WriteAttributeString("fDynamic", "1");
        w.WriteAttributeString("fCollapsed", "0");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteStartElement("cellMetadata");
        w.WriteAttributeString("count", "1");
        w.WriteStartElement("bk");
        w.WriteStartElement("rc");
        w.WriteAttributeString("t", "1");
        w.WriteAttributeString("v", "0");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>A table part (CT_Table): its range, an AutoFilter over it, its columns and Excel's default style.</summary>
    private static void WriteTable(XmlWriter w, XlsxTable table, int id)
    {
        w.WriteStartElement("table", Names.Main);
        w.WriteAttributeString("id", id.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("name", table.Name);
        w.WriteAttributeString("displayName", table.Name);
        w.WriteAttributeString("ref", table.Reference);
        w.WriteAttributeString("totalsRowShown", "0");
        if (table.AutoFilter)
        {
            w.WriteStartElement("autoFilter");
            w.WriteAttributeString("ref", table.Reference);
            w.WriteEndElement();
        }
        w.WriteStartElement("tableColumns");
        w.WriteAttributeString("count", table.Columns.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < table.Columns.Count; i++)
        {
            w.WriteStartElement("tableColumn");
            w.WriteAttributeString("id", (i + 1).ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("name", XString.Encode(table.Columns[i]));
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteStartElement("tableStyleInfo");
        w.WriteAttributeString("name", "TableStyleMedium2");
        w.WriteAttributeString("showFirstColumn", "0");
        w.WriteAttributeString("showLastColumn", "0");
        w.WriteAttributeString("showRowStripes", "1");
        w.WriteAttributeString("showColumnStripes", "0");
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>A worksheet's relationships: one to each of its tables, in the order they were added.</summary>
    private void WriteSheetRelationships(XmlWriter w, int sheet)
    {
        w.WriteStartElement("Relationships", Names.PackageRelationships);
        var id = 1;
        for (var i = 0; i < tables.Count; i++)
        {
            if (tables[i].Sheet != sheet)
                continue;
            w.WriteStartElement("Relationship");
            w.WriteAttributeString("Id", "rId" + id++.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("Type", Names.TableType);
            w.WriteAttributeString("Target", $"../tables/table{i + 1}.xml");
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private void WriteWorkbook(XmlWriter w)
    {
        w.WriteStartElement("workbook", Names.Main);
        w.WriteAttributeString("xmlns", "r", null, Names.Relationships);
        if (options.Date1904)
        {
            w.WriteStartElement("workbookPr");
            w.WriteAttributeString("date1904", "1");
            w.WriteEndElement();
        }
        w.WriteStartElement("bookViews");
        w.WriteStartElement("workbookView");
        if (options.ActiveSheet != 0)
            w.WriteAttributeString("activeTab", options.ActiveSheet.ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteStartElement("sheets");
        for (var i = 0; i < sheets.Count; i++)
        {
            w.WriteStartElement("sheet");
            w.WriteAttributeString("name", sheets[i].Name);
            w.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
            if (sheets[i].Visibility != XlsxSheetVisibility.Visible)
                w.WriteAttributeString("state", sheets[i].Visibility == XlsxSheetVisibility.Hidden ? "hidden" : "veryHidden");
            w.WriteAttributeString("id", Names.Relationships, "rId" + (i + 1).ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        w.WriteEndElement();
        if (options.DefinedNames is { Count: > 0 } names)
        {
            w.WriteStartElement("definedNames");
            foreach (var name in names)
            {
                w.WriteStartElement("definedName");
                w.WriteAttributeString("name", name.Name);
                if (name.LocalSheet is { } local)
                    w.WriteAttributeString("localSheetId", local.ToString(CultureInfo.InvariantCulture));
                if (name.Hidden)
                    w.WriteAttributeString("hidden", "1");
                w.WriteString(XString.Encode(name.Text));
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteStartElement("calcPr");
        w.WriteAttributeString("calcId", "191029");
        if (options.FullCalculationOnLoad)
            w.WriteAttributeString("fullCalcOnLoad", "1");
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private void WriteWorkbookRelationships(XmlWriter w, bool dynamic)
    {
        w.WriteStartElement("Relationships", Names.PackageRelationships);
        var id = 1;
        void Relationship(string type, string target)
        {
            w.WriteStartElement("Relationship");
            w.WriteAttributeString("Id", "rId" + id++.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("Type", type);
            w.WriteAttributeString("Target", target);
            w.WriteEndElement();
        }
        for (var i = 0; i < sheets.Count; i++)
            Relationship(Names.WorksheetType, $"worksheets/sheet{i + 1}.xml");
        Relationship(Names.StylesType, "styles.xml");
        if (shared.Count > 0)
            Relationship(Names.SharedStringsType, "sharedStrings.xml");
        if (dynamic)
            Relationship(Names.SheetMetadataType, "metadata.xml");
        w.WriteEndElement();
    }

    private static void WriteRootRelationships(XmlWriter w)
    {
        w.WriteStartElement("Relationships", Names.PackageRelationships);
        w.WriteStartElement("Relationship");
        w.WriteAttributeString("Id", "rId1");
        w.WriteAttributeString("Type", Names.OfficeDocumentType);
        w.WriteAttributeString("Target", "xl/workbook.xml");
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private void WriteContentTypes(XmlWriter w, bool dynamic)
    {
        w.WriteStartElement("Types", Names.ContentTypes);
        void Default(string extension, string type)
        {
            w.WriteStartElement("Default");
            w.WriteAttributeString("Extension", extension);
            w.WriteAttributeString("ContentType", type);
            w.WriteEndElement();
        }
        void Override(string part, string type)
        {
            w.WriteStartElement("Override");
            w.WriteAttributeString("PartName", part);
            w.WriteAttributeString("ContentType", type);
            w.WriteEndElement();
        }
        Default("rels", Names.RelationshipsContent);
        Default("xml", "application/xml");
        Override("/xl/workbook.xml", Names.WorkbookContent);
        for (var i = 0; i < sheets.Count; i++)
            Override($"/xl/worksheets/sheet{i + 1}.xml", Names.WorksheetContent);
        Override("/xl/styles.xml", Names.StylesContent);
        if (shared.Count > 0)
            Override("/xl/sharedStrings.xml", Names.SharedStringsContent);
        if (dynamic)
            Override("/xl/metadata.xml", Names.SheetMetadataContent);
        for (var i = 0; i < tables.Count; i++)
            Override($"/xl/tables/table{i + 1}.xml", Names.TableContent);
        w.WriteEndElement();
    }

    // ---- checks ----------------------------------------------------------------------------------

    private void CheckName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is 0 or > 31)
            throw new ArgumentException($"A sheet's name is 1 to 31 characters; '{name}' is {name.Length}.", nameof(name));
        if (!XString.Carries(name, encoded: false) || name.Any(c => c < 0x20))
            throw new ArgumentException("A sheet's name holds no control character and no half of a surrogate pair, which XML cannot carry.", nameof(name));
        if (name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0)
            throw new ArgumentException($"A sheet's name holds none of [ ] : * ? / \\; '{name}' does.", nameof(name));
        if (name[0] == '\'' || name[^1] == '\'')
            throw new ArgumentException($"A sheet's name does not begin or end with an apostrophe; '{name}' does.", nameof(name));
        if (sheets.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"The workbook already has a sheet named '{name}', in some case.", nameof(name));
    }

    /// <summary>A table's own shape: its name as Excel names a Table, its columns, and its range within a sheet.</summary>
    private static void CheckTable(XlsxTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!XlsxTable.IsValidName(table.Name))
        {
            throw new ArgumentException(
                $"'{table.Name}' is not a name a table can have: a letter, _ or \\, then letters, digits, _ and ., at most 255 characters, and not a cell's address.",
                nameof(table));
        }
        var name = table.Name;
        if (table.Columns is not { Count: > 0 } columns)
            throw new ArgumentException($"The table '{name}' has at least one column.", nameof(table));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (string.IsNullOrEmpty(column))
                throw new ArgumentException($"A column of the table '{name}' has no name.", nameof(table));
            if (!XString.Carries(column, encoded: true))
                throw new ArgumentException($"A column's name in the table '{name}' holds half of a surrogate pair, which XML cannot carry.", nameof(table));
            if (!seen.Add(column))
                throw new ArgumentException($"The table '{name}' names the column '{column}' twice, in some case.", nameof(table));
        }
        if (table.DataRows < 1)
            throw new ArgumentException($"The table '{name}' has at least one row of data; Excel holds a table of none as one empty row.", nameof(table));
        if (table.HeaderRow < 0 || table.FirstColumn < 0
            || (long)table.HeaderRow + table.DataRows >= XlsxCellReference.MaxRows
            || (long)table.FirstColumn + columns.Count > XlsxCellReference.MaxColumns)
            throw new ArgumentException($"The table '{name}' lies outside the sheet.", nameof(table));
    }

    private static void Validate(XlsxStyles styles)
    {
        if (styles.Fonts.Count == 0 || styles.Fills.Count < 2 || styles.Borders.Count == 0 || styles.CellFormats.Count == 0)
        {
            throw new ArgumentException(
                "A styles part has at least one font, two fills (none and gray125, which Excel reserves), one border and one cell format.",
                nameof(styles));
        }
        for (var i = 0; i < styles.CellFormats.Count; i++)
        {
            var f = styles.CellFormats[i];
            if (f.FontId < 0 || f.FontId >= styles.Fonts.Count || f.FillId < 0 || f.FillId >= styles.Fills.Count
                || f.BorderId < 0 || f.BorderId >= styles.Borders.Count || f.NumberFormatId < 0
                || (f.NumberFormatId >= StylesReader.FirstCustomNumberFormat && !styles.NumberFormats.ContainsKey(f.NumberFormatId)))
            {
                throw new ArgumentException($"The cell format {i} names a font, fill, border or Number Format the styles do not hold.", nameof(styles));
            }
        }
        // What the styles part writes as text is checked here, so that completing the workbook cannot fail on it.
        if (styles.NumberFormats.Values.Any(code => code is null || !XString.Carries(code, encoded: true))
            || styles.Fonts.Any(f => f.Name is { } name && !XString.Carries(name, encoded: false))
            || styles.Fills.Any(f => f.Pattern is { } pattern && !XString.Carries(pattern, encoded: false)))
        {
            throw new ArgumentException(
                "A Number Format, a font's name or a fill's pattern holds text XML cannot carry: a control character, or half of a surrogate pair.", nameof(styles));
        }
    }

    private static void Validate(IReadOnlyList<XlsxDefinedName>? names)
    {
        foreach (var name in names ?? [])
        {
            if (name?.Name is not { Length: > 0 } text || name.Text is null)
                throw new ArgumentException("A defined name has a name and a text.", nameof(names));
            if (!XString.Carries(text, encoded: false) || text.Any(c => c < 0x20) || !XString.Carries(name.Text, encoded: true))
            {
                throw new ArgumentException(
                    $"The defined name '{text}' holds a control character or half of a surrogate pair, which XML cannot carry.", nameof(names));
            }
        }
    }
}

/// <summary>
/// One worksheet being written (<see cref="XlsxWriter.AddSheet"/>): its cells, row by row and left to right. A cell out of
/// that order, or one written twice, is refused.
/// </summary>
public sealed class XlsxSheetWriter
{
    private readonly XlsxWriter owner;
    private readonly XmlWriter xml;
    private readonly int index;
    private readonly List<(XlsxTable Table, string RelationshipId, int[] Header)> tables = [];
    private bool rowOpen;
    private bool ended;
    private int row = -1;
    private int column = -1;

    internal XlsxSheetWriter(XlsxWriter owner, int index, string name, XmlWriter xml, IReadOnlyList<XlsxColumn>? columns, bool active)
    {
        this.owner = owner;
        this.xml = xml;
        this.index = index;
        Name = name;
        xml.WriteStartDocument(standalone: true);
        xml.WriteStartElement("worksheet", Names.Main);
        xml.WriteAttributeString("xmlns", "r", null, Names.Relationships);
        xml.WriteStartElement("sheetViews");
        xml.WriteStartElement("sheetView");
        if (active)
            xml.WriteAttributeString("tabSelected", "1");
        xml.WriteAttributeString("workbookViewId", "0");
        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteStartElement("sheetFormatPr");
        xml.WriteAttributeString("defaultRowHeight", "15");
        xml.WriteEndElement();
        if (columns is { Count: > 0 })
        {
            xml.WriteStartElement("cols");
            foreach (var c in columns.OrderBy(c => c.First))
            {
                xml.WriteStartElement("col");
                xml.WriteAttributeString("min", (c.First + 1).ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("max", (c.Last + 1).ToString(CultureInfo.InvariantCulture));
                if (c.Width is { } width)
                {
                    xml.WriteAttributeString("width", width.ToString("R", CultureInfo.InvariantCulture));
                    if (c.CustomWidth)
                        xml.WriteAttributeString("customWidth", "1");
                    if (c.BestFit)
                        xml.WriteAttributeString("bestFit", "1");
                }
                if (c.StyleIndex != 0)
                    xml.WriteAttributeString("style", c.StyleIndex.ToString(CultureInfo.InvariantCulture));
                if (c.Hidden)
                    xml.WriteAttributeString("hidden", "1");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        xml.WriteStartElement("sheetData");
    }

    /// <summary>The sheet's name.</summary>
    public string Name { get; }

    /// <summary>
    /// Adds an Excel Table over cells of this sheet (<c>tableParts</c>, and a table part of its own), before the first cell of
    /// its header's row is written. Each cell of its header must then be written, as text equal to its column's name
    /// (<see cref="XlsxCellType.SharedString"/> or <see cref="XlsxCellType.InlineString"/>, no Formula), or the cell, or the
    /// sheet's end, is refused; its rows of data hold whatever is written there. A table whose name is another table's or a
    /// defined name's in the workbook, in any case, or that overlaps another table of the sheet, is refused.
    /// </summary>
    /// <param name="table">The table.</param>
    public void AddTable(XlsxTable table)
    {
        if (ended)
            throw new InvalidOperationException($"The sheet '{Name}' is ended: another sheet was begun, or the workbook completed.");
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(table.Columns, nameof(table));
        // A copy of the columns, so that a list the caller changes afterwards cannot make the table part's names differ from
        // the header written.
        table = table with { Columns = [.. table.Columns] };
        if (table.HeaderRow <= row)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The table '{table.Name}' is added after row {row + 1} was begun: a table is added before its header's row."),
                nameof(table));
        }
        var id = owner.AddTable(index, table);
        tables.Add((table, id, new int[1]));
    }

    /// <summary>
    /// Begins <paramref name="row"/> with the cell format of its own that its empty cells take (<c>customFormat</c>), after
    /// every row written before it; the row's cells, if any, are then written as any others. A row written before, or one
    /// that already holds a cell, is refused.
    /// </summary>
    /// <param name="row">The row and its format.</param>
    public void WriteRow(XlsxRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (ended)
            throw new InvalidOperationException($"The sheet '{Name}' is ended: another sheet was begun, or the workbook completed.");
        if (row.Row < 0 || row.Row >= XlsxCellReference.MaxRows)
            throw new ArgumentException($"The row {row.Row} lies outside the sheet.", nameof(row));
        if (row.Row <= this.row)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The row {row.Row + 1} is begun after row {this.row + 1}: a sheet's rows are written in order, each once."),
                nameof(row));
        }
        if (row.StyleIndex < 0 || row.StyleIndex >= owner.Styles.CellFormats.Count)
            throw new ArgumentException($"The row {row.Row + 1} names the cell format {row.StyleIndex}, which the styles do not hold.", nameof(row));
        if (rowOpen)
            xml.WriteEndElement();
        xml.WriteStartElement("row");
        xml.WriteAttributeString("r", (row.Row + 1).ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("s", row.StyleIndex.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("customFormat", "1");
        rowOpen = true;
        this.row = row.Row;
        column = -1;
    }

    /// <summary>
    /// Writes <paramref name="cell"/>, after every cell written before it in the sheet's order. A
    /// <see cref="XlsxCellType.SharedString"/> is written from its <see cref="XlsxCell.Text"/> into the workbook's shared
    /// strings, whatever its <see cref="XlsxCell.Value"/> says.
    /// </summary>
    /// <param name="cell">The cell.</param>
    public void Write(in XlsxCell cell)
    {
        if (ended)
            throw new InvalidOperationException($"The sheet '{Name}' is ended: another sheet was begun, or the workbook completed.");
        if (cell.Row < 0 || cell.Row >= XlsxCellReference.MaxRows || cell.Column < 0 || cell.Column >= XlsxCellReference.MaxColumns)
            throw new ArgumentException($"The cell at row {cell.Row}, column {cell.Column} lies outside the sheet.", nameof(cell));
        if (cell.Row < row || (cell.Row == row && cell.Column <= column))
        {
            throw new ArgumentException(
                $"The cell {cell.Reference} is written after {(column < 0 ? string.Create(CultureInfo.InvariantCulture, $"row {row + 1}") : XlsxCellReference.Format(row, column))}: a sheet's cells are written row by row, left to right, each once.",
                nameof(cell));
        }
        if (cell.StyleIndex < 0 || cell.StyleIndex >= owner.Styles.CellFormats.Count)
            throw new ArgumentException($"The cell {cell.Reference} names the cell format {cell.StyleIndex}, which the styles do not hold.", nameof(cell));
        // Every argument is checked before anything is written, so that a refused cell leaves the sheet as it was.
        if (cell.Formula is { Kind: XlsxFormulaKind.Shared, SharedIndex: null })
            throw new ArgumentException($"The cell {cell.Reference}'s shared formula names no group.", nameof(cell));
        if (cell.Formula is { } f && (f.Text is null || !XString.Carries(f.Text, encoded: true) || (f.Reference is { } fr && !XString.Carries(fr, encoded: false))))
            throw new ArgumentException($"The cell {cell.Reference}'s Formula holds text XML cannot carry.", nameof(cell));
        var carried = cell.Type switch
        {
            XlsxCellType.SharedString or XlsxCellType.InlineString => cell.Text is null || XString.Carries(cell.Text, encoded: true),
            XlsxCellType.FormulaString => cell.Value is null || XString.Carries(cell.Value, encoded: true),
            _ => cell.Value is null || XString.Carries(cell.Value, encoded: false),
        };
        if (!carried)
            throw new ArgumentException($"The cell {cell.Reference} holds text XML cannot carry: a control character, or half of a surrogate pair.", nameof(cell));
        var header = HeaderOf(cell);
        if (cell.Row != row)
        {
            if (rowOpen)
                xml.WriteEndElement();
            xml.WriteStartElement("row");
            xml.WriteAttributeString("r", (cell.Row + 1).ToString(CultureInfo.InvariantCulture));
            rowOpen = true;
            row = cell.Row;
        }
        column = cell.Column;
        if (header is { } counted)
            counted[0]++;
        xml.WriteStartElement("c");
        xml.WriteAttributeString("r", cell.Reference);
        if (cell.StyleIndex != 0)
            xml.WriteAttributeString("s", cell.StyleIndex.ToString(CultureInfo.InvariantCulture));
        var type = cell.Type switch
        {
            XlsxCellType.SharedString => "s",
            XlsxCellType.InlineString => "inlineStr",
            XlsxCellType.FormulaString => "str",
            XlsxCellType.Boolean => "b",
            XlsxCellType.Error => "e",
            XlsxCellType.Date => "d",
            _ => null,
        };
        if (type is not null)
            xml.WriteAttributeString("t", type);
        if (cell.Formula is { IsDynamicArray: true })
        {
            xml.WriteAttributeString("cm", "1");
            owner.HasDynamicArrays = true;
        }
        if (cell.Formula is { } formula)
            WriteFormula(formula, cell);
        switch (cell.Type)
        {
            case XlsxCellType.SharedString:
                xml.WriteElementString("v", owner.Share(cell.Text ?? "").ToString(CultureInfo.InvariantCulture));
                break;
            case XlsxCellType.InlineString:
                xml.WriteStartElement("is");
                XlsxWriter.WriteText(xml, cell.Text ?? "");
                xml.WriteEndElement();
                break;
            case XlsxCellType.FormulaString when cell.Value is not null:
                xml.WriteElementString("v", XString.Encode(cell.Value));
                break;
            default:
                if (cell.Value is not null)
                    xml.WriteElementString("v", cell.Value);
                break;
        }
        xml.WriteEndElement();
    }

    private void WriteFormula(XlsxFormula formula, in XlsxCell cell)
    {
        xml.WriteStartElement("f");
        switch (formula.Kind)
        {
            case XlsxFormulaKind.Shared:
                xml.WriteAttributeString("t", "shared");
                break;
            case XlsxFormulaKind.Array:
                xml.WriteAttributeString("t", "array");
                break;
            case XlsxFormulaKind.DataTable:
                xml.WriteAttributeString("t", "dataTable");
                break;
        }
        if (formula.Reference is not null)
            xml.WriteAttributeString("ref", formula.Reference);
        else if (formula.Kind == XlsxFormulaKind.Array)
            xml.WriteAttributeString("ref", cell.Reference);
        if (formula.SharedIndex is { } si)
            xml.WriteAttributeString("si", si.ToString(CultureInfo.InvariantCulture));
        if (formula.Text.Length > 0)
            xml.WriteString(XString.Encode(formula.Text));
        xml.WriteEndElement();
    }

    /// <summary>Writes every cell of <paramref name="cells"/> in turn, handing the package to the stream between batches.</summary>
    /// <param name="cells">The cells, in the sheet's order.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async ValueTask WriteAsync(IEnumerable<XlsxCell> cells, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var n = 0;
        foreach (var cell in cells)
        {
            Write(cell);
            if (++n % XlsxSheetReader.BatchSize == 0)
                await FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Hands what the package holds so far to the stream, asynchronously. What is written stays in memory until then:
    /// call it between batches of a large sheet.
    /// </summary>
    /// <param name="cancellationToken">Cancels the hand-off.</param>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ended)
            xml.Flush();
        return owner.DrainAsync(cancellationToken);
    }

    /// <summary>
    /// The count of header cells written of the table whose header holds <paramref name="cell"/>, or null when none does; a
    /// header's cell that does not hold its column's name as text is refused.
    /// </summary>
    private int[]? HeaderOf(in XlsxCell cell)
    {
        foreach (var (table, _, written) in tables)
        {
            if (cell.Row != table.HeaderRow || cell.Column < table.FirstColumn || cell.Column >= table.FirstColumn + table.Columns.Count)
                continue;
            var name = table.Columns[cell.Column - table.FirstColumn];
            if (cell.Type is not (XlsxCellType.SharedString or XlsxCellType.InlineString) || cell.Formula is not null
                || !string.Equals(cell.Text, name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The cell {cell.Reference} is in the header of the table '{table.Name}', and holds its column's name, '{name}', as text, with no Formula.",
                    nameof(cell));
            }
            return written;
        }
        return null;
    }

    internal void End()
    {
        if (ended)
            return;
        // A table whose header was not written whole would be repaired by Excel: the sheet is not ended, and the caller
        // abandons the workbook (DisposeAsync).
        foreach (var (table, _, written) in tables)
        {
            if (written[0] != table.Columns.Count)
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"The sheet '{Name}' ends with {written[0]} of the {table.Columns.Count} cells of the header of the table '{table.Name}' written; each holds its column's name."));
            }
        }
        ended = true;
        if (rowOpen)
            xml.WriteEndElement();
        xml.WriteEndElement();
        if (tables.Count > 0)
        {
            xml.WriteStartElement("tableParts");
            xml.WriteAttributeString("count", tables.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var (_, id, _) in tables)
            {
                xml.WriteStartElement("tablePart");
                xml.WriteAttributeString("id", Names.Relationships, id);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
        xml.WriteEndDocument();
        xml.Dispose();
    }

    internal void Abandon()
    {
        ended = true;
        try
        {
            xml.Dispose();
        }
        catch (InvalidOperationException)
        {
            // A document left open: it is abandoned whole.
        }
    }
}

/// <summary>
/// A part's stream, counting what is written into it against <see cref="XlsxWriteOptions.MaxUncompressedBytes"/>. Past it,
/// the first write is refused by name and every later one dropped, so that closing the abandoned part throws nothing more.
/// </summary>
internal sealed class CountedPart(XlsxWriter owner, Stream inner, string part, string? sheet) : Stream
{
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (owner.Count(buffer.Length, part, sheet))
            inner.Write(buffer);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// Where the zip writes: memory, handed to the output stream asynchronously in <see cref="DrainAsync"/>. The archive writes
/// synchronously; this keeps every write to the caller's stream asynchronous.
/// </summary>
internal sealed class ForwardBuffer : Stream
{
    private MemoryStream pending = new();
    private long position;

    public async ValueTask DrainAsync(Stream output, CancellationToken cancellationToken)
    {
        if (pending.Length == 0)
            return;
        var full = pending;
        pending = new MemoryStream();
        // A memory stream holds less than 2 GiB, and the parts at most XlsxReadOptions.DefaultMaxUncompressedBytes (1 GiB)
        // together, which no compression enlarges past it: the length is an int.
        await output.WriteAsync(full.GetBuffer().AsMemory(0, (int)full.Length), cancellationToken).ConfigureAwait(false);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        pending.Write(buffer, offset, count);
        position += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        pending.Write(buffer);
        position += buffer.Length;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
