using System.Globalization;
using System.Xml;

namespace ExGrid.Data.Xlsx;

/// <summary>
/// One worksheet's cells read as a stream (ADR-0330 §9): <see cref="ReadAsync"/> reads the next batch into
/// <see cref="Cells"/>, row by row as the file holds them, and between batches the read reports its progress and yields
/// once a slice has spent its budget. The worksheet is never held as a document; only the batch at hand is.
/// </summary>
public sealed class XlsxSheetReader : IDisposable
{
    /// <summary>The cells a batch holds at most.</summary>
    public const int BatchSize = 4096;

    private readonly XlsxReader owner;
    private readonly int index;
    private readonly Stream stream;
    private readonly XmlReader xml;
    private readonly string part;
    private readonly List<XlsxCell> batch = new(BatchSize);
    private readonly List<XlsxColumn> columns = [];
    private readonly List<string> other = [];
    private State state;
    private int row = -1;
    private int column = -1;
    private long reported;

    private enum State { Start, Cells, Done }

    internal XlsxSheetReader(XlsxReader owner, int index, XlsxSheetInfo sheet, Stream stream)
    {
        this.owner = owner;
        this.index = index;
        this.stream = stream;
        Sheet = sheet;
        part = sheet.Part!;
        xml = XmlReader.Create(stream, Package.Settings);
    }

    /// <summary>The sheet as the workbook lists it.</summary>
    public XlsxSheetInfo Sheet { get; }

    /// <summary>The cells of the batch the last <see cref="ReadAsync"/> read, in the file's order.</summary>
    public IReadOnlyList<XlsxCell> Cells => batch;

    /// <summary>The worksheet's column widths, complete once the first <see cref="ReadAsync"/> has returned: they come before the cells.</summary>
    public IReadOnlyList<XlsxColumn> Columns => columns;

    /// <summary>
    /// The local names of what else the worksheet holds, beside its cells and columns, in the file's order, complete once
    /// <see cref="ReadAsync"/> has returned false: <c>mergeCells</c>, <c>conditionalFormatting</c>, <c>dataValidations</c>,
    /// <c>hyperlinks</c>, <c>drawing</c>, <c>legacyDrawing</c> (notes), <c>tableParts</c>, <c>sheetProtection</c>,
    /// <c>pageSetup</c> and the rest. None of it is read. What holds nothing a reader sees is not listed, though every file
    /// carries it: <c>pageMargins</c>, <c>phoneticPr</c>, <c>ignoredErrors</c>, <c>sheetCalcPr</c>, and <c>printOptions</c>,
    /// <c>pageSetup</c>, <c>headerFooter</c>, <c>rowBreaks</c> and <c>colBreaks</c> in their defaults or empty. <c>sheetPr</c>
    /// is listed for a tab colour, <c>pageSetUpPr</c> for a fit to pages, and <c>extLst</c> by the name of each extension it
    /// holds, such as <c>conditionalFormattings</c>.
    /// </summary>
    public IReadOnlyList<string> OtherElements => other;

    /// <summary>Whether a row read so far sets a height of its own.</summary>
    public bool CustomRowHeights { get; private set; }

    /// <summary>Whether a row read so far is hidden.</summary>
    public bool HiddenRows { get; private set; }

    /// <summary>Whether the sheet's view freezes panes; known once the first <see cref="ReadAsync"/> has returned.</summary>
    public bool FrozenPanes { get; private set; }

    /// <summary>
    /// Reads the next batch of cells into <see cref="Cells"/>; false when the worksheet has no more. Refuses the file by name
    /// (<see cref="XlsxException"/>) on a cell it cannot read, or past <see cref="XlsxReadOptions.MaxCells"/>.
    /// </summary>
    public async ValueTask<bool> ReadAsync()
    {
        batch.Clear();
        if (state == State.Done)
            return false;
        await owner.CheckpointAsync(index).ConfigureAwait(false);
        try
        {
            if (state == State.Start)
                ReadHead();
            if (state == State.Cells)
                ReadCells();
            if (state == State.Done)
                ReadTail();
        }
        catch (XmlException e)
        {
            throw owner.Package.Refuse(e, part, Sheet.Name);
        }
        finally
        {
            var now = stream.Position;
            owner.CountBytes(now - reported);
            reported = now;
        }
        if (state == State.Done)
            owner.Report(index);
        return batch.Count > 0 || state != State.Done;
    }

    /// <summary>Reads up to the cells: the root, and the children before <c>&lt;sheetData&gt;</c>.</summary>
    private void ReadHead()
    {
        xml.MoveToContent();
        if (xml.NamespaceURI.StartsWith(Names.StrictPrefix, StringComparison.Ordinal))
            throw new XlsxException(XlsxRefusal.StrictOpenXml, "The worksheet is saved as Strict Open XML, which is not read.", part, Sheet.Name);
        if (xml.LocalName != "worksheet")
            throw new XlsxException(XlsxRefusal.Malformed, $"The worksheet part's root is <{xml.LocalName}>, not <worksheet>.", part, Sheet.Name);
        if (xml.IsEmptyElement)
        {
            state = State.Done;
            return;
        }
        var depth = xml.Depth;
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
                case "sheetData":
                    if (xml.IsEmptyElement)
                    {
                        xml.Read();
                        state = State.Done;
                    }
                    else
                    {
                        xml.Read();
                        state = State.Cells;
                    }
                    return;
                case "cols":
                    ReadColumns();
                    break;
                case "sheetViews":
                    ReadViews();
                    break;
                case "dimension" or "sheetFormatPr":
                    xml.Skip();
                    break;
                default:
                    ReadOther();
                    break;
            }
        }
        state = State.Done;
    }

    private void ReadColumns()
    {
        if (xml.IsEmptyElement)
        {
            xml.Read();
            return;
        }
        var depth = xml.Depth;
        xml.Read();
        while (!xml.EOF && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "col")
            {
                var min = xml.GetAttribute("min") is { } a ? XlsxReader.Int(a, part, Sheet.Name) : 0;
                var max = xml.GetAttribute("max") is { } b ? XlsxReader.Int(b, part, Sheet.Name) : min;
                if (min < 1 || max < min || max > XlsxCellReference.MaxColumns)
                    throw new XlsxException(XlsxRefusal.Malformed, $"A column range {min} to {max} lies outside the sheet's columns.", part, Sheet.Name);
                double? width = xml.GetAttribute("width") is { } w ? XlsxReader.Double(w, part) : null;
                var hidden = XlsxReader.Bool(xml.GetAttribute("hidden"));
                var style = xml.GetAttribute("style") is { } s ? XlsxReader.Int(s, part, Sheet.Name) : 0;
                if (style < 0 || style >= owner.Styles.CellFormats.Count)
                {
                    throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture,
                        $"The columns {min} to {max} name the cell format {style}, which the styles do not hold."), part, Sheet.Name);
                }
                columns.Add(new XlsxColumn(min - 1, max - 1, width, hidden, style));
            }
            if (xml.NodeType == XmlNodeType.Element)
                xml.Skip();
            else
                xml.Read();
        }
        xml.Read();
    }

    private void ReadViews()
    {
        using (var sub = xml.ReadSubtree())
        {
            while (sub.Read())
            {
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "pane" && sub.GetAttribute("state") is "frozen" or "frozenSplit")
                    FrozenPanes = true;
            }
        }
        xml.Skip();
    }

    /// <summary>Reads cells until the batch is full or <c>&lt;sheetData&gt;</c> ends.</summary>
    private void ReadCells()
    {
        while (batch.Count < BatchSize)
        {
            if (xml.EOF)
                throw new XlsxException(XlsxRefusal.Malformed, "The worksheet ends inside its cells.", part, Sheet.Name);
            switch (xml.NodeType)
            {
                case XmlNodeType.Element when xml.LocalName == "row":
                    StartRow();
                    if (xml.IsEmptyElement)
                        xml.Skip();
                    else
                        xml.Read();
                    break;
                case XmlNodeType.Element when xml.LocalName == "c":
                    ReadCell();
                    break;
                case XmlNodeType.Element:
                    xml.Skip();
                    break;
                case XmlNodeType.EndElement when xml.LocalName == "sheetData":
                    xml.Read();
                    state = State.Done;
                    return;
                default:
                    xml.Read();
                    break;
            }
        }
    }

    private void StartRow()
    {
        var r = row + 1;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                switch (xml.LocalName)
                {
                    case "r":
                        r = XlsxReader.Int(xml.Value, part, Sheet.Name) - 1;
                        break;
                    case "customHeight" when XlsxReader.Bool(xml.Value):
                        CustomRowHeights = true;
                        break;
                    case "hidden" when XlsxReader.Bool(xml.Value):
                        HiddenRows = true;
                        break;
                }
            }
            while (xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        if (r < 0 || r >= XlsxCellReference.MaxRows)
            throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture, $"Row {r + 1} lies outside the sheet's rows."), part, Sheet.Name);
        if (r <= row)
        {
            throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture,
                $"Row {r + 1} comes after row {row + 1}: a worksheet's rows are in order, each once."), part, Sheet.Name);
        }
        row = r;
        column = -1;
    }

    private void ReadCell()
    {
        string? reference = null, type = null, style = null, metadata = null;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                switch (xml.LocalName)
                {
                    case "r":
                        reference = xml.Value;
                        break;
                    case "t":
                        type = xml.Value;
                        break;
                    case "s":
                        style = xml.Value;
                        break;
                    case "cm":
                        metadata = xml.Value;
                        break;
                }
            }
            while (xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        int r, c;
        if (reference is null)
        {
            r = row < 0 ? 0 : row;
            c = column + 1;
            if (c >= XlsxCellReference.MaxColumns)
                throw new XlsxException(XlsxRefusal.Malformed, "A cell lies past the sheet's last column.", part, Sheet.Name);
        }
        else if (!XlsxCellReference.TryParse(reference, out r, out c))
        {
            throw new XlsxException(XlsxRefusal.Malformed, $"'{reference}' is not a cell of the sheet.", part, Sheet.Name);
        }
        else if (row >= 0 && r != row)
        {
            throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture, $"The cell is written inside row {row + 1}, not its own."),
                part, Sheet.Name, reference);
        }
        else if (c <= column)
        {
            throw new XlsxException(XlsxRefusal.Malformed,
                $"The cell comes after {XlsxCellReference.Format(r, column)}: a row's cells are in order, each once.", part, Sheet.Name, reference);
        }
        row = r;
        column = c;
        owner.CountCell(Sheet, r, c);
        var cellType = type switch
        {
            null or "n" => XlsxCellType.Number,
            "s" => XlsxCellType.SharedString,
            "inlineStr" => XlsxCellType.InlineString,
            "str" => XlsxCellType.FormulaString,
            "b" => XlsxCellType.Boolean,
            "e" => XlsxCellType.Error,
            "d" => XlsxCellType.Date,
            _ => throw new XlsxException(XlsxRefusal.Malformed, $"'{type}' is not a cell type.", part, Sheet.Name, XlsxCellReference.Format(r, c)),
        };
        var styleIndex = style is null ? 0 : XlsxReader.Int(style, part, Sheet.Name, XlsxCellReference.Format(r, c));
        if (styleIndex < 0 || styleIndex >= owner.Styles.CellFormats.Count)
        {
            throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture,
                $"The cell names the cell format {styleIndex}, which the styles do not hold."), part, Sheet.Name, XlsxCellReference.Format(r, c));
        }
        string? value = null, text = null;
        XlsxFormula? formula = null;
        var rich = false;
        if (xml.IsEmptyElement)
        {
            xml.Read();
        }
        else
        {
            var depth = xml.Depth;
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
                    case "v":
                        value = xml.ReadElementContentAsString();
                        break;
                    case "f":
                        formula = ReadFormula(metadata, r, c);
                        break;
                    case "is":
                        (text, rich) = XlsxReader.ReadText(xml);
                        xml.Read();
                        break;
                    default:
                        xml.Skip();
                        break;
                }
            }
            xml.Read();
        }
        switch (cellType)
        {
            case XlsxCellType.SharedString:
                if (value is null || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var at) || at >= owner.SharedStrings.Count)
                {
                    throw new XlsxException(XlsxRefusal.Malformed, $"The cell names the shared string '{value}', which the workbook does not hold.",
                        part, Sheet.Name, XlsxCellReference.Format(r, c));
                }
                text = owner.SharedStrings[at];
                rich = owner.SharedStrings.IsRich(at);
                break;
            case XlsxCellType.InlineString:
                text ??= "";
                break;
            case XlsxCellType.FormulaString when value is not null:
                value = XString.Decode(value);
                break;
        }
        batch.Add(new XlsxCell(r, c, cellType, value, text, formula, styleIndex, rich));
    }

    private XlsxFormula ReadFormula(string? metadata, int r, int c)
    {
        string? kind = null, reference = null, shared = null;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                switch (xml.LocalName)
                {
                    case "t":
                        kind = xml.Value;
                        break;
                    case "ref":
                        reference = xml.Value;
                        break;
                    case "si":
                        shared = xml.Value;
                        break;
                }
            }
            while (xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        var formulaKind = kind switch
        {
            null or "normal" => XlsxFormulaKind.Normal,
            "shared" => XlsxFormulaKind.Shared,
            "array" => XlsxFormulaKind.Array,
            "dataTable" => XlsxFormulaKind.DataTable,
            _ => throw new XlsxException(XlsxRefusal.Malformed, $"'{kind}' is not a kind of Formula.", part, Sheet.Name, XlsxCellReference.Format(r, c)),
        };
        int? si = shared is null ? null : XlsxReader.Int(shared, part, Sheet.Name, XlsxCellReference.Format(r, c));
        if (formulaKind == XlsxFormulaKind.Shared && si is null)
            throw new XlsxException(XlsxRefusal.Malformed, "A shared formula names no group (si).", part, Sheet.Name, XlsxCellReference.Format(r, c));
        var dynamic = formulaKind == XlsxFormulaKind.Array && metadata is not null
            && int.TryParse(metadata, NumberStyles.None, CultureInfo.InvariantCulture, out var cm) && owner.IsDynamicArray(cm);
        var text = XString.Decode(xml.ReadElementContentAsString());
        return new XlsxFormula(text, formulaKind, reference, si, dynamic);
    }

    private void ReadTail()
    {
        while (!xml.EOF)
        {
            if (xml.NodeType == XmlNodeType.Element)
            {
                ReadOther();
            }
            else
            {
                xml.Read();
            }
        }
    }

    /// <summary>
    /// Lists an element of the worksheet other than its cells, columns and views in <see cref="OtherElements"/> when it
    /// holds something, and moves past it. Every file Excel writes carries some in a form that holds nothing a reader sees:
    /// those are not listed, so a list that is empty means a worksheet that holds nothing else.
    /// </summary>
    private void ReadOther()
    {
        var name = xml.LocalName;
        switch (name)
        {
            // Print margins, phonetic settings, dismissed error marks and calculation flags: nothing on a sheet, and in every file.
            case "pageMargins" or "phoneticPr" or "ignoredErrors" or "sheetCalcPr":
                xml.Skip();
                return;
            case "printOptions":
                if (AnyAttributeTrue("horizontalCentered", "verticalCentered", "headings", "gridLines")) other.Add(name);
                xml.Skip();
                return;
            case "pageSetup":
                if (PageSetupSetsAnything()) other.Add(name);
                xml.Skip();
                return;
            case "headerFooter":
            case "rowBreaks" or "colBreaks":
            case "sheetPr":
            case "extLst":
                ReadInside(name);
                return;
            default:
                other.Add(name);
                xml.Skip();
                return;
        }
    }

    private bool AnyAttributeTrue(params string[] names)
    {
        foreach (var attribute in names)
        {
            if (XlsxReader.Bool(xml.GetAttribute(attribute))) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether a <c>&lt;pageSetup&gt;</c> sets anything beyond Excel's defaults (ECMA-376 Part 1, §18.3.1.63). The paper
    /// size, the printer's resolution and its settings part are the printer's, written whenever one is installed, and are
    /// not counted.
    /// </summary>
    private bool PageSetupSetsAnything()
    {
        var sets = false;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                var value = xml.Value;
                sets |= xml.LocalName switch
                {
                    "paperSize" or "paperHeight" or "paperWidth" or "horizontalDpi" or "verticalDpi" or "id" or "usePrinterDefaults" => false,
                    "orientation" => value is not ("default" or "portrait"),
                    "scale" or "fitToWidth" or "fitToHeight" or "firstPageNumber" or "copies" => value != (xml.LocalName == "scale" ? "100" : "1"),
                    "pageOrder" => value != "downThenOver",
                    "cellComments" => value != "none",
                    "errors" => value != "displayed",
                    "blackAndWhite" or "draft" or "useFirstPageNumber" => XlsxReader.Bool(value),
                    _ => true,
                };
            }
            while (xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        return sets;
    }

    /// <summary>
    /// An element listed by what it holds: a header or footer with text, a page break, a tab colour (<c>sheetPr</c>) or a
    /// fit to pages (<c>pageSetUpPr</c>), and each extension of <c>extLst</c> by its own name, such as Excel 2010's
    /// <c>conditionalFormattings</c>, <c>dataValidations</c> or <c>sparklineGroups</c>.
    /// </summary>
    private void ReadInside(string name)
    {
        var found = new List<string>();
        using (var sub = xml.ReadSubtree())
        {
            sub.Read();
            var depth = sub.Depth;
            while (sub.Read())
            {
                if (sub.NodeType == XmlNodeType.Text && name == "headerFooter" && !string.IsNullOrWhiteSpace(sub.Value))
                    found.Add(name);
                if (sub.NodeType != XmlNodeType.Element) continue;
                switch (name)
                {
                    case "rowBreaks" or "colBreaks" when sub.LocalName == "brk":
                        found.Add(name);
                        break;
                    case "sheetPr" when sub.LocalName == "tabColor":
                        found.Add("sheetPr");
                        break;
                    case "sheetPr" when sub.LocalName == "pageSetUpPr" && XlsxReader.Bool(sub.GetAttribute("fitToPage")):
                        found.Add("pageSetUpPr");
                        break;
                    case "extLst" when sub.Depth == depth + 2:
                        found.Add(sub.LocalName);
                        break;
                }
            }
        }
        xml.Skip();
        foreach (var item in found.Distinct())
        {
            if (!other.Contains(item)) other.Add(item);
        }
    }

    /// <summary>Closes the worksheet's part.</summary>
    public void Dispose() => xml.Dispose();
}
