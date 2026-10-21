using System.Globalization;
using System.Xml;

namespace ExGrid.Data.Xlsx;

/// <summary>
/// An <c>.xlsx</c> workbook opened for reading (ADR-0330 §1, §9). Opening reads the package's parts that are small and
/// whole — the workbook part (its sheets in order, the one shown, the date system, its defined names), the shared
/// strings, the styles and the theme — and refuses by name a file that is not a plain <c>.xlsx</c>, or that passes a
/// limit. Each worksheet's cells are then read as a stream, a batch at a time, through <see cref="OpenSheet"/>; no
/// worksheet is ever held as a document.
/// </summary>
/// <remarks>
/// The model is the file's own: a number is its text, a Formula its stored text with its prefixes, a style an index.
/// Mapping it to a Workbook is ExSheet.Xlsx's.
/// </remarks>
public sealed class XlsxReader : IDisposable
{
    private readonly Package package;
    private readonly HashSet<int> dynamicArrayMetadata;
    private readonly Pacer pacer;
    private readonly string? sharedStringsPart;
    private long cells;
    private long sheetBytesRead;

    private XlsxReader(Package package, XlsxReadOptions options, CancellationToken cancellationToken)
    {
        this.package = package;
        Options = options;
        pacer = new Pacer(options, cancellationToken);
        var workbookPart = WorkbookPart(package);
        var rels = Relationships(package, workbookPart);
        var (sheets, active, date1904, names) = ReadWorkbook(package, workbookPart, rels);
        Sheets = sheets;
        ActiveSheet = active;
        Date1904 = date1904;
        DefinedNames = names;
        WorkbookPartName = workbookPart;
        string? Target(string type) => rels.Values.FirstOrDefault(r => r.Type == type).Target;
        sharedStringsPart = Target(Names.SharedStringsType);
        Styles = Target(Names.StylesType) is { } styles ? StylesReader.Read(package, styles) : XlsxStyles.Default;
        var themePart = Target(Names.ThemeType);
        Theme = themePart is null ? XlsxTheme.Office : ReadTheme(package, themePart);
        HasTheme = themePart is not null;
        dynamicArrayMetadata = Target(Names.SheetMetadataType) is { } metadata ? ReadDynamicArrayMetadata(package, metadata) : [];
        SheetBytes = sheets.Where(s => s.Part is not null).Sum(s => package.LengthOf(s.Part!));
    }

    /// <summary>
    /// Opens the workbook in <paramref name="stream"/>, reading its workbook part, shared strings, styles and theme, or
    /// refuses it by name (<see cref="XlsxException"/>). A stream that cannot seek — a browser's file, a request body — is
    /// copied into memory first, as far as <see cref="XlsxReadOptions.MaxUncompressedBytes"/> allows. The reader owns the
    /// stream from here, and disposes it.
    /// </summary>
    /// <param name="stream">The file, from its current position.</param>
    /// <param name="options">The limits, progress and pacing; null for the defaults.</param>
    /// <param name="cancellationToken">Cancels the read; it is looked at between slices.</param>
    public static async ValueTask<XlsxReader> OpenAsync(Stream stream, XlsxReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new XlsxReadOptions();
        cancellationToken.ThrowIfCancellationRequested();
        var head = new byte[8];
        Stream seekable = stream;
        try
        {
            var start = stream.CanSeek ? stream.Position : 0;
            var got = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            if (Sniff.IsOle(head.AsSpan(0, got)))
            {
                // Only the compound file's header, the FAT sectors its directory's chain passes through and the directory
                // are read: a stream that seeks is read where they lie, and one that cannot is read only as far as the
                // last of them, never past the size limit.
                var file = stream.CanSeek ? OleFile.Seeking(stream, start) : OleFile.Forward(stream, head.AsMemory(0, got), options.MaxUncompressedBytes);
                throw await Sniff.ClassifyOleAsync(file, cancellationToken).ConfigureAwait(false);
            }
            if (!Sniff.IsZip(head.AsSpan(0, got)))
                throw new XlsxException(XlsxRefusal.NotAZip, got == 0 ? "The file is empty." : "The file is not a zip package, so it is not an .xlsx.");
            if (stream.CanSeek)
            {
                stream.Position = start;
            }
            else
            {
                seekable = await CopyAsync(stream, head.AsMemory(0, got), options.MaxUncompressedBytes, cancellationToken).ConfigureAwait(false);
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            var package = Package.Open(seekable, options.MaxUncompressedBytes);
            try
            {
                var reader = new XlsxReader(package, options, cancellationToken);
                await reader.ReadSharedStringsAsync().ConfigureAwait(false);
                return reader;
            }
            catch
            {
                package.Dispose();
                throw;
            }
        }
        catch
        {
            await seekable.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The limits, progress and pacing the read was opened with.</summary>
    public XlsxReadOptions Options { get; }

    /// <summary>The workbook part's name, as <c>xl/workbook.xml</c>.</summary>
    public string WorkbookPartName { get; }

    /// <summary>The workbook's sheets, in its order: the order of its tabs.</summary>
    public IReadOnlyList<XlsxSheetInfo> Sheets { get; }

    /// <summary>The index into <see cref="Sheets"/> of the sheet the workbook shows (<c>activeTab</c>), 0 when it names none.</summary>
    public int ActiveSheet { get; }

    /// <summary>Whether the workbook counts dates from 1904 (<c>date1904</c>), whose serials are 1,462 less than 1900's for the same day.</summary>
    public bool Date1904 { get; }

    /// <summary>The workbook's defined names.</summary>
    public IReadOnlyList<XlsxDefinedName> DefinedNames { get; }

    /// <summary>The shared strings, held whole.</summary>
    public XlsxSharedStrings SharedStrings { get; private set; } = XlsxSharedStrings.Empty;

    /// <summary>The styles, as far as ADR-0330 §4 carries them; <see cref="XlsxStyles.Default"/> when the package has no styles part.</summary>
    public XlsxStyles Styles { get; }

    /// <summary>The theme's colours; <see cref="XlsxTheme.Office"/> when the package has no theme part (<see cref="HasTheme"/>).</summary>
    public XlsxTheme Theme { get; }

    /// <summary>Whether the package has a theme part of its own.</summary>
    public bool HasTheme { get; }

    /// <summary>Every part the package holds, by name, as <c>xl/comments1.xml</c>: what else it holds than what is read.</summary>
    public IEnumerable<string> Parts => package.PartNames;

    /// <summary>The cells read so far, every worksheet counted.</summary>
    public long CellsRead => cells;

    /// <summary>The worksheets' uncompressed bytes, as the zip declares them.</summary>
    public long SheetBytes { get; }

    /// <summary>
    /// Opens the worksheet at <paramref name="index"/> for reading its cells as a stream. One worksheet is read at a time;
    /// dispose the reader before opening the next.
    /// </summary>
    /// <param name="index">The sheet's index into <see cref="Sheets"/>.</param>
    public XlsxSheetReader OpenSheet(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Sheets.Count);
        var sheet = Sheets[index];
        if (sheet.Part is null)
            throw new InvalidOperationException($"The sheet '{sheet.Name}' is not a worksheet and holds no cells.");
        var stream = package.OpenPart(sheet.Part, sheet.Name)
            ?? throw new XlsxException(XlsxRefusal.Malformed, "The workbook names a worksheet part the package does not hold.", sheet.Part, sheet.Name);
        return new XlsxSheetReader(this, index, sheet, stream);
    }

    /// <summary>
    /// Reads the worksheet at <paramref name="index"/> whole: its columns, its cells and what else it holds. For a sheet
    /// that is small, or a test; a large one is read a batch at a time through <see cref="OpenSheet"/>.
    /// </summary>
    /// <param name="index">The sheet's index into <see cref="Sheets"/>.</param>
    public async ValueTask<XlsxWorksheet> ReadSheetAsync(int index)
    {
        using var reader = OpenSheet(index);
        var all = new List<XlsxCell>();
        var rows = new List<XlsxRow>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            all.AddRange(reader.Cells);
            rows.AddRange(reader.Rows);
        }
        return new XlsxWorksheet(reader.Sheet, reader.Columns, all, reader.OtherElements, reader.CustomRowHeights, reader.HiddenRows, reader.FrozenPanes,
            rows.Count > 0 ? rows : null);
    }

    /// <summary>Disposes the package and its stream.</summary>
    public void Dispose() => package.Dispose();

    // ---- what the sheet readers share --------------------------------------------------------

    internal Package Package => package;

    internal bool IsDynamicArray(int cellMetadata) => dynamicArrayMetadata.Contains(cellMetadata);

    internal void CountCell(XlsxSheetInfo sheet, int row, int column)
    {
        if (++cells > Options.MaxCells)
        {
            throw new XlsxException(XlsxRefusal.TooManyCells, string.Create(CultureInfo.InvariantCulture,
                $"The workbook holds more than the {Options.MaxCells:N0} cells a read is allowed (XlsxReadOptions.MaxCells)."),
                sheet.Part, sheet.Name, XlsxCellReference.Format(row, column));
        }
    }

    internal void CountBytes(long bytes) => sheetBytesRead += bytes;

    internal ValueTask CheckpointAsync(int sheet)
        => pacer.CheckpointAsync(new XlsxProgress(sheet, Sheets.Count, cells, sheetBytesRead, SheetBytes));

    internal void Report(int sheet) => pacer.Report(new XlsxProgress(sheet, Sheets.Count, cells, sheetBytesRead, SheetBytes));

    // ---- the package -------------------------------------------------------------------------

    private static async ValueTask<MemoryStream> CopyAsync(Stream stream, ReadOnlyMemory<byte> head, long limit, CancellationToken cancellationToken)
    {
        var memory = new MemoryStream();
        memory.Write(head.Span);
        var buffer = new byte[1 << 20];
        while (true)
        {
            var n = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (n == 0)
                break;
            if (memory.Length + n > limit)
            {
                throw new XlsxException(XlsxRefusal.TooLarge, string.Create(CultureInfo.InvariantCulture,
                    $"The file is larger than the {limit:N0} uncompressed bytes a read is allowed (XlsxReadOptions.MaxUncompressedBytes)."));
            }
            memory.Write(buffer, 0, n);
        }
        memory.Position = 0;
        return memory;
    }

    private readonly record struct Relationship(string Type, string Target);

    /// <summary>The workbook part, from the content types and the package's relationships; refused by name when the
    /// package is not a plain transitional workbook.</summary>
    private static string WorkbookPart(Package package)
    {
        var (defaults, overrides) = ReadContentTypes(package);
        var root = Relationships(package, "");
        var office = root.Values.FirstOrDefault(r => r.Type.EndsWith("/officeDocument", StringComparison.Ordinal));
        if (office.Type is null)
        {
            if (root.Values.Any(r => r.Type.StartsWith(Names.StrictPrefix, StringComparison.Ordinal)))
                throw Strict();
            throw new XlsxException(XlsxRefusal.NotAWorkbook, "The zip package holds no document, so it is not an .xlsx.");
        }
        if (office.Type.StartsWith(Names.StrictPrefix, StringComparison.Ordinal))
            throw Strict();
        var part = office.Target;
        var contentType = overrides.TryGetValue(part, out var o) ? o
            : defaults.TryGetValue(Path.GetExtension(part).TrimStart('.'), out var d) ? d : "";
        if (contentType == Names.BinaryContent || part.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            throw new XlsxException(XlsxRefusal.Binary,
                "The file is an Excel Binary Workbook (.xlsb), not an .xlsx. Save it from Excel as an Excel Workbook (.xlsx).", part);
        }
        if (contentType.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)
            || overrides.Values.Concat(defaults.Values).Any(t => t == Names.VbaProjectContent))
        {
            throw new XlsxException(XlsxRefusal.MacroEnabled,
                "The file is a workbook with macros (.xlsm), which is not read. Save it from Excel as an Excel Workbook (.xlsx), which leaves the macros out.", part);
        }
        if (contentType is not (Names.WorkbookContent or Names.TemplateContent))
        {
            throw new XlsxException(XlsxRefusal.NotAWorkbook, string.IsNullOrEmpty(contentType)
                ? "The package's document has no content type, so it is not an .xlsx."
                : $"The package's document is '{contentType}', not a workbook: the file is not an .xlsx.", part);
        }
        if (!package.Has(part))
            throw new XlsxException(XlsxRefusal.Malformed, "The package names a workbook part it does not hold.", part);
        return part;
    }

    private static XlsxException Strict()
        => new(XlsxRefusal.StrictOpenXml,
            "The workbook is saved as Strict Open XML, which is not read. Save it from Excel as an Excel Workbook (.xlsx).");

    private static (Dictionary<string, string> Defaults, Dictionary<string, string> Overrides) ReadContentTypes(Package package)
    {
        const string part = "[Content_Types].xml";
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var xml = package.OpenXml(part)
            ?? throw new XlsxException(XlsxRefusal.NotAWorkbook, "The zip package has no [Content_Types].xml, so it is not an Office document.");
        try
        {
            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Element)
                    continue;
                if (xml.LocalName == "Default" && xml.GetAttribute("Extension") is { } ext && xml.GetAttribute("ContentType") is { } t1)
                    defaults[ext] = t1;
                else if (xml.LocalName == "Override" && xml.GetAttribute("PartName") is { } name && xml.GetAttribute("ContentType") is { } t2)
                    overrides[name.TrimStart('/')] = t2;
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        return (defaults, overrides);
    }

    /// <summary>The relationships of <paramref name="source"/> (the package itself when empty), by id, each target
    /// resolved to a part name.</summary>
    private static Dictionary<string, Relationship> Relationships(Package package, string source)
    {
        var dir = source.Length == 0 ? "" : Path.GetDirectoryName(source)!.Replace('\\', '/');
        var part = (dir.Length == 0 ? "" : dir + "/") + "_rels/" + Path.GetFileName(source) + ".rels";
        var found = new Dictionary<string, Relationship>(StringComparer.Ordinal);
        using var xml = package.OpenXml(part);
        if (xml is null)
            return found;
        try
        {
            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "Relationship")
                    continue;
                var id = xml.GetAttribute("Id");
                var type = xml.GetAttribute("Type");
                var target = xml.GetAttribute("Target");
                if (id is null || type is null || target is null || xml.GetAttribute("TargetMode") == "External")
                    continue;
                found[id] = new Relationship(type, Resolve(dir, target));
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        return found;
    }

    private static string Resolve(string dir, string target)
    {
        target = Uri.UnescapeDataString(target.Replace('\\', '/'));
        var path = target.StartsWith('/') ? target.TrimStart('/') : (dir.Length == 0 ? target : dir + "/" + target);
        var parts = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment is "" or ".")
                continue;
            if (segment == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(segment);
        }
        return string.Join('/', parts);
    }

    private static (List<XlsxSheetInfo> Sheets, int Active, bool Date1904, List<XlsxDefinedName> Names) ReadWorkbook(
        Package package, string part, Dictionary<string, Relationship> rels)
    {
        var sheets = new List<XlsxSheetInfo>();
        var names = new List<XlsxDefinedName>();
        var active = 0;
        var date1904 = false;
        using var xml = package.OpenXml(part)!;
        try
        {
            xml.MoveToContent();
            if (xml.NamespaceURI.StartsWith(Names.StrictPrefix, StringComparison.Ordinal))
                throw Strict();
            if (xml.LocalName != "workbook")
                throw new XlsxException(XlsxRefusal.Malformed, $"The workbook part's root is <{xml.LocalName}>, not <workbook>.", part);
            // A defined name's text is read with ReadElementContentAsString, which leaves the reader on the node after it:
            // that node is looked at before reading on.
            var read = xml.Read();
            while (read)
            {
                if (xml.NodeType != XmlNodeType.Element)
                {
                    read = xml.Read();
                    continue;
                }
                var advanced = false;
                switch (xml.LocalName)
                {
                    case "workbookPr":
                        date1904 = Bool(xml.GetAttribute("date1904"));
                        break;
                    case "workbookView":
                        if (xml.GetAttribute("activeTab") is { } tab)
                            active = Int(tab, part);
                        break;
                    case "sheet":
                        var name = xml.GetAttribute("name") ?? throw new XlsxException(XlsxRefusal.Malformed, "A sheet has no name.", part);
                        var id = xml.GetAttribute("id", Names.Relationships);
                        var visibility = xml.GetAttribute("state") switch
                        {
                            "hidden" => XlsxSheetVisibility.Hidden,
                            "veryHidden" => XlsxSheetVisibility.VeryHidden,
                            _ => XlsxSheetVisibility.Visible,
                        };
                        var sheetId = xml.GetAttribute("sheetId") is { } sid ? Int(sid, part) : sheets.Count + 1;
                        if (id is not null && rels.TryGetValue(id, out var rel) && rel.Type == Names.WorksheetType)
                            sheets.Add(new XlsxSheetInfo(name, sheetId, visibility, rel.Target));
                        else
                            sheets.Add(new XlsxSheetInfo(name, sheetId, visibility, null, XlsxSheetKind.Other));
                        break;
                    case "definedName":
                        var defined = xml.GetAttribute("name") ?? "";
                        int? local = xml.GetAttribute("localSheetId") is { } l ? Int(l, part) : null;
                        var hidden = Bool(xml.GetAttribute("hidden"));
                        names.Add(new XlsxDefinedName(defined, XString.Decode(xml.ReadElementContentAsString()), local, hidden));
                        advanced = true;
                        break;
                }
                read = advanced ? !xml.EOF : xml.Read();
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        if (sheets.Count == 0)
            throw new XlsxException(XlsxRefusal.Malformed, "The workbook lists no sheets.", part);
        if (active < 0 || active >= sheets.Count)
            active = 0;
        return (sheets, active, date1904, names);
    }

    /// <summary>
    /// Reads the shared strings, the one part held whole, in slices as a worksheet is read (ADR-0330 §9): cancellation is
    /// looked at, progress reported and the read yields between them, so a browser keeps painting while a large part is
    /// read. Its progress names no worksheet (<see cref="XlsxProgress.Sheet"/> is -1) and counts the part's bytes.
    /// </summary>
    private async ValueTask ReadSharedStringsAsync()
    {
        if (sharedStringsPart is not { } part)
            return;
        var texts = new List<string>();
        var rich = new HashSet<int>();
        var total = package.LengthOf(part);
        using var stream = package.OpenPart(part);
        if (stream is null)
            return;
        using var xml = XmlReader.Create(stream, Package.Settings);
        XlsxProgress Progress() => new(-1, Sheets.Count, 0, stream.Position, total);
        try
        {
            xml.MoveToContent();
            if (xml.GetAttribute("uniqueCount") is { } unique && int.TryParse(unique, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                texts.Capacity = Math.Min(count, 1 << 20);
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "si")
                {
                    var (text, isRich) = ReadText(xml);
                    if (isRich)
                        rich.Add(texts.Count);
                    texts.Add(text);
                    if (texts.Count % SharedStringSlice == 0)
                        await pacer.CheckpointAsync(Progress()).ConfigureAwait(false);
                }
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        pacer.Report(Progress());
        SharedStrings = new XlsxSharedStrings([.. texts], rich);
    }

    /// <summary>The shared strings read between two looks at the clock: a few microseconds' work.</summary>
    internal const int SharedStringSlice = 256;

    /// <summary>The text of a string item (<c>&lt;si&gt;</c> or <c>&lt;is&gt;</c>), the reader on its start: its <c>&lt;t&gt;</c>, or
    /// its runs' <c>&lt;t&gt;</c>s in order, a phonetic guide left out. Leaves the reader on its end.</summary>
    internal static (string Text, bool Rich) ReadText(XmlReader xml)
    {
        if (xml.IsEmptyElement)
            return ("", false);
        var depth = xml.Depth;
        string? single = null;
        System.Text.StringBuilder? more = null;
        var rich = false;
        xml.Read();
        while (!xml.EOF && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.NodeType != XmlNodeType.Element)
            {
                xml.Read();
                continue;
            }
            switch (xml.LocalName)
            {
                case "t":
                    var text = xml.ReadElementContentAsString();
                    if (single is null)
                        single = text;
                    else
                        (more ??= new System.Text.StringBuilder(single)).Append(text);
                    break;
                case "r":
                    rich = true;
                    if (xml.IsEmptyElement)
                        xml.Skip();
                    else
                        xml.Read();
                    break;
                default:
                    // A run's properties, a phonetic guide and its properties: not text.
                    xml.Skip();
                    break;
            }
        }
        var whole = more?.ToString() ?? single ?? "";
        return (XString.Decode(whole), rich);
    }

    private static XlsxTheme ReadTheme(Package package, string part)
    {
        string[] order = ["dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink"];
        var colors = XlsxTheme.Office.SchemeColors.ToArray();
        using var xml = package.OpenXml(part);
        if (xml is null)
            return XlsxTheme.Office;
        try
        {
            if (!xml.ReadToFollowing("clrScheme", Names.Drawing))
                return XlsxTheme.Office;
            var depth = xml.Depth;
            var slot = -1;
            while (xml.Read() && xml.Depth > depth)
            {
                if (xml.NodeType != XmlNodeType.Element)
                    continue;
                if (xml.Depth == depth + 1)
                {
                    slot = Array.IndexOf(order, xml.LocalName);
                    continue;
                }
                if (slot < 0)
                    continue;
                var value = xml.LocalName switch
                {
                    "srgbClr" => xml.GetAttribute("val"),
                    "sysClr" => xml.GetAttribute("lastClr"),
                    _ => null,
                };
                if (value is not null && uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
                    colors[slot] = 0xFF000000 | (rgb & 0xFFFFFF);
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        return new XlsxTheme(colors);
    }

    /// <summary>The cell metadata indexes (<c>cm</c>, from one) that mark a dynamic array: a <c>cellMetadata</c> block whose
    /// record points at an <c>XLDAPR</c> future-metadata block with <c>fDynamic="1"</c>.</summary>
    private static HashSet<int> ReadDynamicArrayMetadata(Package package, string part)
    {
        var dynamic = new HashSet<int>();
        using var xml = package.OpenXml(part);
        if (xml is null)
            return dynamic;
        var types = new List<string>();
        var futures = new Dictionary<string, List<bool>>(StringComparer.Ordinal);
        var cellBlocks = new List<(int Type, int Value)>();
        try
        {
            string? future = null;
            var inCellMetadata = false;
            var pendingRecord = false;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement)
                {
                    if (xml.LocalName == "futureMetadata")
                        future = null;
                    else if (xml.LocalName == "cellMetadata")
                        inCellMetadata = false;
                    continue;
                }
                if (xml.NodeType != XmlNodeType.Element)
                    continue;
                switch (xml.LocalName)
                {
                    case "metadataType":
                        types.Add(xml.GetAttribute("name") ?? "");
                        break;
                    case "futureMetadata":
                        future = xml.GetAttribute("name") ?? "";
                        futures[future] = [];
                        if (xml.IsEmptyElement)
                            future = null;
                        break;
                    case "bk" when future is not null:
                        futures[future].Add(false);
                        break;
                    case "dynamicArrayProperties" when future is not null && futures[future].Count > 0:
                        futures[future][^1] = Bool(xml.GetAttribute("fDynamic"));
                        break;
                    case "cellMetadata":
                        inCellMetadata = !xml.IsEmptyElement;
                        break;
                    case "bk" when inCellMetadata:
                        pendingRecord = true;
                        cellBlocks.Add((-1, -1));
                        break;
                    case "rc" when inCellMetadata && pendingRecord:
                        pendingRecord = false;
                        cellBlocks[^1] = (Int(xml.GetAttribute("t") ?? "0", part), Int(xml.GetAttribute("v") ?? "0", part));
                        break;
                }
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        for (var i = 0; i < cellBlocks.Count; i++)
        {
            var (type, value) = cellBlocks[i];
            if (type < 1 || type > types.Count || types[type - 1] != "XLDAPR")
                continue;
            if (futures.TryGetValue("XLDAPR", out var blocks) && value >= 0 && value < blocks.Count && blocks[value])
                dynamic.Add(i + 1);
        }
        return dynamic;
    }

    internal static bool Bool(string? value) => value is "1" or "true";

    internal static int Int(string value, string part, string? sheet = null, string? cell = null)
        => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n
            : throw new XlsxException(XlsxRefusal.Malformed, $"'{value}' is not a whole number where the part needs one.", part, sheet, cell);

    internal static double Double(string value, string part)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n
            : throw new XlsxException(XlsxRefusal.Malformed, $"'{value}' is not a number where the part needs one.", part);
}

/// <summary>A worksheet read whole (<see cref="XlsxReader.ReadSheetAsync"/>).</summary>
/// <param name="Sheet">The sheet as the workbook lists it.</param>
/// <param name="Columns">Its column widths.</param>
/// <param name="Cells">Its cells, in the file's order: row by row, left to right.</param>
/// <param name="OtherElements">What else it holds, as <see cref="XlsxSheetReader.OtherElements"/> lists it.</param>
/// <param name="CustomRowHeights">Whether a row sets a height of its own.</param>
/// <param name="HiddenRows">Whether a row is hidden.</param>
/// <param name="FrozenPanes">Whether the sheet's view freezes panes.</param>
/// <param name="Rows">The rows that record a cell format of their own, in order, as <see cref="XlsxSheetReader.Rows"/> gives them;
/// null for none.</param>
public sealed record XlsxWorksheet(
    XlsxSheetInfo Sheet,
    IReadOnlyList<XlsxColumn> Columns,
    IReadOnlyList<XlsxCell> Cells,
    IReadOnlyList<XlsxElement> OtherElements,
    bool CustomRowHeights = false,
    bool HiddenRows = false,
    bool FrozenPanes = false,
    IReadOnlyList<XlsxRow>? Rows = null);
