using System.Text.RegularExpressions;

namespace ExSheet.Engine.Formulas;

/// <summary>INDEX (its array form), XMATCH, CHOOSE, ROW, COLUMN, OFFSET and INDIRECT, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// INDEX: the cell at <c>row_num</c> and <c>column_num</c> of the range, both counted from 1
    /// and truncated to whole numbers. Over a range of one row given <c>row_num</c> alone, that
    /// number runs along the row. Below 0 is <c>#VALUE!</c>, and past the range's edge is
    /// <c>#REF!</c>. The result is the cell itself, so a blank one reads as blank.
    /// </summary>
    /// <remarks>
    /// A row or a column of 0 names the whole column or row, which spills where it is more than one
    /// cell (ADR-0125), and is a range a function such as <c>SUM</c> reads. Over a Reference of
    /// several rows and several columns, <c>column_num</c> cannot be left out: <c>INDEX(G1:H5,2)</c>
    /// is <c>#REF!</c>, as Excel's is (INDEX-012, the seventeenth Windows run), and so is
    /// <c>INDEX(A1#,2)</c> over such a Spill Range, which is a Reference as <c>G1:H5</c> is. Over an
    /// array it is the whole row, as Microsoft documents.
    /// </remarks>
    private static Operand Index(FunctionCall call)
    {
        var source = call.Operand(0);
        if (source.IsError) return source;
        var (rows, columns) = source.Kind switch
        {
            OperandKind.Area => (source.Area.Rows, source.Area.Columns),
            OperandKind.Column => (source.Column!.Count, 1),
            OperandKind.Array => (source.Array!.Rows, source.Array.Columns),
            _ => (1, 1),
        };

        var row = 0.0;
        if (call.Has(1) && !TryNumber(call, 1, out row, out var failure)) return failure;
        var column = 0.0;
        if (call.Count < 3)
        {
            if (rows == 1 && columns > 1) (row, column) = (1, row);
        }
        else if (call.Has(2) && !TryNumber(call, 2, out column, out failure))
        {
            return failure;
        }
        row = Math.Truncate(row);
        column = Math.Truncate(column);
        if (row < 0 || column < 0) return Operand.Of(ErrorValue.Value);
        if (call.Count < 3 && source.IsReference && rows > 1 && columns > 1) return Operand.Of(ErrorValue.Ref);
        if (row > rows || column > columns) return Operand.Of(ErrorValue.Ref);
        if ((row == 0 && rows > 1) || (column == 0 && columns > 1))
        {
            // The whole row, column or range: rows r1..r2 and columns c1..c2, from 0.
            var (r1, r2) = row == 0 ? (0, rows - 1) : ((int)row - 1, (int)row - 1);
            var (c1, c2) = column == 0 ? (0, columns - 1) : ((int)column - 1, (int)column - 1);
            switch (source.Kind)
            {
                case OperandKind.Area:
                    var area = source.Area;
                    return Operand.Of(new Area(area.Row1 + r1, area.Column1 + c1, area.Row1 + r2, area.Column1 + c2));
                case OperandKind.Column:
                    return Operand.Of(Slice(call.Evaluator.ToArray(source), r1, r2, c1, c2));
                case OperandKind.Array:
                    return Operand.Of(Slice(source.Array!, r1, r2, c1, c2));
            }
        }

        var r = Math.Max((int)row, 1) - 1;
        var c = Math.Max((int)column, 1) - 1;
        return source.Kind switch
        {
            OperandKind.Area => Operand.Of(new Area(source.Area.Row1 + r, source.Area.Column1 + c, source.Area.Row1 + r, source.Area.Column1 + c)),
            OperandKind.Column => source.Column![r] is { } value ? Operand.Of(value) : Operand.Blank,
            OperandKind.Array => source.Array![r, c] is { } item ? Operand.Of(item) : Operand.Blank,
            _ => source,
        };
    }

    /// <summary>Rows <paramref name="r1"/>..<paramref name="r2"/> and columns <paramref name="c1"/>..<paramref name="c2"/> of an array, from 0.</summary>
    private static ValueArray Slice(ValueArray array, int r1, int r2, int c1, int c2)
    {
        var slice = new ValueArray(r2 - r1 + 1, c2 - c1 + 1);
        for (var r = r1; r <= r2; r++)
        {
            for (var c = c1; c <= c2; c++) slice[r - r1, c - c1] = array[r, c];
        }
        return slice;
    }

    /// <summary>
    /// XMATCH: the position, from 1, at which XLOOKUP's search finds the lookup value, its modes
    /// and its refusals XLOOKUP's (<see cref="Search"/>); <c>#N/A</c> when nothing matches.
    /// </summary>
    private static Operand XMatch(FunctionCall call)
    {
        var lookupOperand = call.Operand(0);
        if (IsArray(lookupOperand)) return Operand.Of(ErrorValue.Value);
        if (call.Evaluator.ScalarOf(lookupOperand) is { IsError: true } lookupError) return Operand.Of(lookupError);
        var lookupArray = ArrayOrValue(call.Operand(1));
        if (!lookupArray.IsRange) return lookupArray.IsError ? lookupArray : Operand.Of(ErrorValue.Value);
        if (Vector.Of(lookupArray) is not { } vector) return Operand.Of(ErrorValue.Value);
        if (Search(call, vector, modesAt: 2) is not { } outcome) return Operand.Of(ErrorValue.Value);
        if (outcome.Failure is { } failure) return failure;
        return outcome.Found is { } index ? Operand.Of(Value.FromNumber(index + 1)) : Operand.Of(ErrorValue.NA);
    }

    /// <summary>
    /// CHOOSE: the value at <c>index_num</c>, from 1, truncated; below 1 or past the values,
    /// <c>#VALUE!</c>. Only the chosen value is evaluated, and a Reference stays a Reference.
    /// </summary>
    private static Operand Choose(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var index, out var failure)) return failure;
        index = Math.Truncate(index);
        if (index < 1 || index > call.Count - 1) return Operand.Of(ErrorValue.Value);
        var at = (int)index;
        return call.Has(at) ? call.Operand(at) : Operand.Blank;
    }

    private static Operand Row(FunctionCall call) => Place(call, row: true);

    private static Operand Column(FunctionCall call) => Place(call, row: false);

    /// <summary>
    /// ROW and COLUMN: the number, from 1, of the Reference's row or column, or of the Formula's own
    /// cell when it is left out (a move recalculates it: <c>Sheet.ReadsOwnPlace</c>). A Reference of
    /// several rows to ROW gives each row's number, down a column, and of several columns to COLUMN
    /// each column's, along a row; it spills (ADR-0125). A Linked Table's column has no place on the
    /// Sheet: <c>#VALUE!</c>.
    /// </summary>
    private static Operand Place(FunctionCall call, bool row)
    {
        if (!call.Has(0))
        {
            if (call.Evaluator.Self is not { } self) return Operand.Of(ErrorValue.Value);
            return Operand.Of(Value.FromNumber((row ? self.Row : self.Column) + 1));
        }
        var reference = call.Operand(0);
        if (reference.Kind != OperandKind.Area) return reference.IsError ? reference : Operand.Of(ErrorValue.Value);
        var area = reference.Area;
        var count = row ? area.Rows : area.Columns;
        var places = row ? new ValueArray(count, 1) : new ValueArray(1, count);
        for (var k = 0; k < count; k++)
        {
            var place = Value.FromNumber((row ? area.Row1 : area.Column1) + k + 1);
            if (row) places[k, 0] = place;
            else places[0, k] = place;
        }
        return Operand.Of(places);
    }

    private static Operand Rows(FunctionCall call) => Extent(call, rows: true);

    private static Operand Columns(FunctionCall call) => Extent(call, rows: false);

    /// <summary>
    /// ROWS and COLUMNS: how many rows or columns the range spans; a Linked Table's column is one column; a single Value, 1.
    /// They read the extent alone, never a Value (<see cref="FunctionCall.Measured"/>), so they count a blank in
    /// <c>UNIQUE</c>'s array as Excel's do (UNIQUE-011).
    /// </summary>
    private static Operand Extent(FunctionCall call, bool rows)
    {
        var operand = call.Measured(0);
        if (operand.IsError) return operand;
        var count = operand.Kind switch
        {
            OperandKind.Area => rows ? operand.Area.Rows : operand.Area.Columns,
            OperandKind.Column => rows ? operand.Column!.Count : 1,
            OperandKind.Array => rows ? operand.Array!.Rows : operand.Array!.Columns,
            _ => 1,
        };
        return Operand.Of(Value.FromNumber(count));
    }

    /// <summary>
    /// OFFSET (ADR-0124, a volatile function): the Reference <c>rows</c> below and <c>cols</c> right
    /// of <c>reference</c>, <c>height</c> rows by <c>width</c> columns, each truncated and the size the
    /// reference's own when left out. A size below 1, or a Reference past the Sheet's edge, is
    /// <c>#REF!</c>; a Linked Table's column has no place to move from: <c>#VALUE!</c>. A Reference of
    /// several cells is a range for a function such as <c>SUM</c>, and spills where it is the
    /// result (ADR-0125).
    /// </summary>
    private static Operand Offset(FunctionCall call)
    {
        var reference = call.Operand(0);
        if (reference.Kind != OperandKind.Area) return reference.IsError ? reference : Operand.Of(ErrorValue.Value);
        var area = reference.Area;
        if (!TryNumber(call, 1, out var rows, out var failure)) return failure;
        if (!TryNumber(call, 2, out var columns, out failure)) return failure;
        double height = area.Rows;
        double width = area.Columns;
        if (call.Has(3) && !TryNumber(call, 3, out height, out failure)) return failure;
        if (call.Has(4) && !TryNumber(call, 4, out width, out failure)) return failure;
        rows = Math.Truncate(rows);
        columns = Math.Truncate(columns);
        height = Math.Truncate(height);
        width = Math.Truncate(width);
        if (height < 1 || width < 1) return Operand.Of(ErrorValue.Ref);
        var top = area.Row1 + rows;
        var left = area.Column1 + columns;
        var bottom = top + height - 1;
        var right = left + width - 1;
        if (top < 0 || left < 0 || bottom >= Sheet.RowCount || right >= Sheet.ColumnCount) return Operand.Of(ErrorValue.Ref);
        return Operand.Of(new Area((int)top, (int)left, (int)bottom, (int)right));
    }

    /// <summary>
    /// INDIRECT (ADR-0124's note of 2026-10-10, a volatile function): the Reference its text names,
    /// to a cell or a range of this Sheet, with or without the Sheet's own qualifier. A Reference of
    /// several cells is a range for a function such as <c>SUM</c>, and spills where it is the result
    /// (ADR-0125). The text is text: an insertion or a deletion does not rewrite it.
    /// </summary>
    /// <remarks>
    /// The forms Excel was observed to read are read as it reads them, and so are those that follow
    /// from them by the rule its answers settle: R1C1 text (<c>a1</c> FALSE, 0, a blank or left empty;
    /// the seventeenth Windows run and the two area passes of 2026-10-12, INDIRECT-023, 036 to 038, 044
    /// and 051 to 066; <see cref="IndirectRowColumn"/>); a structured reference's text,
    /// <c>Positions[PV]</c>, read as the typed one is, in either style (INDIRECT-025, 061); and a Spill
    /// Range, <c>A1#</c> or <c>R1C1#</c>, read as the typed <c>A1#</c> is (INDIRECT-026, 060). Still
    /// <c>#VALUE!</c>, forms no run has observed and the observed ones leave open: in R1C1 style, a row
    /// or column number 0 (<c>R0C2</c>) and an offset past the Sheet's edge (<c>R[-5]C[-1]</c> from C2,
    /// which Excel reads as a place, INDIRECT-052, though no run has told whether it wraps round to the
    /// far side or stops at the edge); in either style, a declared Linked Table's bare name and the
    /// structured forms a Formula does not take (<c>#All</c>, <c>@</c>). Relative R1C1 text in a Formula
    /// evaluated in no cell is <c>#VALUE!</c> too, as <c>ROW()</c> there is. Text that names no place is
    /// <c>#REF!</c>, as in Excel: another Sheet's name, a table never declared, a column the table does
    /// not have, R1C1 parts of two kinds joined by <c>:</c> (INDIRECT-058, 062), and a number past the
    /// Sheet's edge, as <c>XFE1</c> is (INDIRECT-017), whatever an offset beside it reads (ADR-0046,
    /// ADR-0049).
    /// </remarks>
    private static Operand Indirect(FunctionCall call)
    {
        if (!TryScalar(call, 0, out var value, out var failure)) return failure;
        // a1: TRUE, or left out, is A1 style; FALSE, 0, a blank or an empty argument is R1C1's.
        var isA1 = true;
        if (call.Count > 1)
        {
            if (!call.Has(1)) isA1 = false;
            else
            {
                if (!TryScalar(call, 1, out var style, out failure)) return failure;
                if (style is not { } a1) isA1 = false;
                else if (!TryTruth(a1, out isA1)) return Operand.Of(ErrorValue.Value);
            }
        }
        var text = call.Evaluator.ToText(value);
        return isA1 ? IndirectA1(call, text) : IndirectRowColumn(call, text);
    }

    /// <summary>INDIRECT's A1-style text: a Reference as a Formula writes one, <c>A1#</c>, or a Linked Table's text (<see cref="IndirectTableText"/>).</summary>
    private static Operand IndirectA1(FunctionCall call, string text)
    {
        var cells = call.Evaluator.Cells;
        if (IndirectTableText(call, text) is { } table) return table;
        if (Lexer.ReadReference(text, 0, out var length) is not { } reference) return Operand.Of(ErrorValue.Ref);
        // A1#: the Spill Range of the Formula in A1, read as the typed one is (ADR-0125).
        if (reference.Shape == ReferenceShape.Cell && length == text.Length - 1 && text[length] == '#')
        {
            if (!cells.IsLocal(reference)) return Operand.Of(ErrorValue.Ref);
            return cells.Spill(new CellAddress(reference.Row1, reference.Column1));
        }
        if (length != text.Length || !cells.IsLocal(reference)) return Operand.Of(ErrorValue.Ref);
        return Operand.Of(reference.Area);
    }

    /// <summary>
    /// A Linked Table's text, read alike whatever <c>a1</c> says (INDIRECT-025, INDIRECT-061), or
    /// <see langword="null"/> for text that is none: <c>Table[Column]</c> or <c>Table[[Column]]</c>, read
    /// as the typed one is (ADR-0049), so that a table still waiting for its data waits, and one never
    /// declared names no place, as a column it lacks does; any other text a structured reference begins
    /// as, a form a Formula does not take (<c>#All</c>, <c>@</c>), refused; and a declared table's bare
    /// name, which Excel reads as a structured reference to the table's data, refused until a run
    /// observes what it reads (ADR-0124's note of 2026-10-10; INDIRECT-043, 053 to 057).
    /// </summary>
    private static Operand? IndirectTableText(FunctionCall call, string text)
    {
        var cells = call.Evaluator.Cells;
        if (Lexer.ReadStructuredReference(text, 0) is { } structured && structured.Length == text.Length)
        {
            var column = cells.TableColumn(structured.Table, structured.Column);
            return column.IsError && column.Scalar!.Value.Error == ErrorValue.Name ? Operand.Of(ErrorValue.Ref) : column;
        }
        if (StructuredText().IsMatch(text)) return Operand.Of(ErrorValue.Value);
        if (cells.IsLinkedTable(text)) return Operand.Of(ErrorValue.Value);
        return null;
    }

    /// <summary>
    /// INDIRECT's R1C1-style text (ADR-0124's notes of 2026-10-12), with or without the Sheet's own
    /// qualifier, read as the two area passes observed Excel read it and by the rule its answers
    /// settle. R and C each take a number, the row or column counted from 1, leading 0s and all
    /// (<c>R3C2</c>, <c>R03C2</c>); an offset in brackets from the Formula's own cell, signed or not and
    /// 0 included (<c>R[1]C[-1]</c>, <c>R[+1]</c>, <c>R[0]</c>, <c>R[-0]</c>); or nothing, the Formula's
    /// own row or column, as an offset of 0 (<c>RC[-1]</c>, <c>R[2]C</c>). A row part alone is a whole
    /// row (<c>R3</c>, <c>R[1]</c>), a column part alone a whole column (<c>C2</c>), two parts of the
    /// same kind joined by <c>:</c> name the rectangle they span (<c>R2:R3</c>,
    /// <c>RC[-1]:R[1]C[-1]</c>), and a cell followed by <c>#</c> names its Spill Range, as <c>A1#</c>
    /// does (<c>R1C1#</c>). A Linked Table's text is read as in A1 style
    /// (<see cref="IndirectTableText"/>). <c>#REF!</c>, text that names no place: parts of two kinds
    /// joined (<c>R2C2:R3</c>, <c>R3:C2</c>), a <c>#</c> after anything but one cell, as after a range
    /// in A1 style, a number past the Sheet's edge, another Sheet's name, and text that is not R1C1
    /// notation. <c>#VALUE!</c> where Excel's answer is still open: a row or column number 0, and an
    /// offset past the Sheet's edge, which Excel reads as a place (INDIRECT-052 is 0, a blank cell, not
    /// <c>#REF!</c>), though no run has told whether it wraps round to the Sheet's far side or stops at
    /// the edge (INDIRECT-082 to 084 ask it).
    /// </summary>
    private static Operand IndirectRowColumn(FunctionCall call, string text)
    {
        var spilled = text.EndsWith('#');
        var match = RowColumnText().Match(spilled ? text[..^1] : text);
        if (!match.Success) return IndirectTableText(call, text) ?? Operand.Of(ErrorValue.Ref);
        string? sheet = null;
        if (match.Groups["sheet"].Success)
        {
            var raw = match.Groups["sheet"].Value;
            sheet = raw[0] == '\'' ? raw[1..^1].Replace("''", "'", StringComparison.Ordinal) : raw;
        }
        var joined = match.Groups["second"].Success;
        var first = RowColumnPart.Read(match.Groups["first"].Value);
        var second = joined ? RowColumnPart.Read(match.Groups["second"].Value) : first;
        // Parts of two kinds joined are no Reference: a cell and a whole row (INDIRECT-058), a whole row
        // and a whole column (INDIRECT-062), and so a cell and a column, whichever comes first.
        if (!first.IsShapedAs(second)) return Operand.Of(ErrorValue.Ref);
        // A # follows one cell, as in A1# (INDIRECT-060); after a range, a row or a column it names no
        // place, as it does after one in A1 style.
        if (spilled && (joined || !first.IsCell)) return Operand.Of(ErrorValue.Ref);
        // A number past the Sheet's edge names no place, as XFE1 does (INDIRECT-017), and so does another
        // Sheet's name: #REF!, whatever an offset past the edge beside them reads.
        if (first.HasNumberPastEdge || second.HasNumberPastEdge) return Operand.Of(ErrorValue.Ref);
        if (!call.Evaluator.Cells.IsLocal(Reference.Rectangle(sheet, 0, 0, 0, 0, true, true, true, true))) return Operand.Of(ErrorValue.Ref);
        // An offset counts from the Formula's own cell; a Formula evaluated in no cell has none, as ROW() has none.
        CellAddress own = default;
        if (first.IsRelative || second.IsRelative)
        {
            if (call.Evaluator.Self is not { } self) return Operand.Of(ErrorValue.Value);
            own = self;
        }
        var (row1, row2) = first.Row is { } r1 && second.Row is { } r2 ? (r1.Place(own.Row, Sheet.RowCount), r2.Place(own.Row, Sheet.RowCount)) : (0, Sheet.RowCount - 1);
        var (column1, column2) = first.Column is { } c1 && second.Column is { } c2 ? (c1.Place(own.Column, Sheet.ColumnCount), c2.Place(own.Column, Sheet.ColumnCount)) : (0, Sheet.ColumnCount - 1);
        // What is left to fall off the Sheet is a number 0, which no run has asked, or an offset past its
        // edge, which Excel reads as a place no run has yet told (INDIRECT-052): refused.
        if (row1 < 0 || row2 < 0 || column1 < 0 || column2 < 0) return Operand.Of(ErrorValue.Value);
        if (spilled) return call.Evaluator.Cells.Spill(new CellAddress(row1, column1));
        var reference = Reference.Rectangle(sheet, Math.Min(row1, row2), Math.Min(column1, column2), Math.Max(row1, row2), Math.Max(column1, column2), true, true, true, true);
        return Operand.Of(reference.Area);
    }

    /// <summary>
    /// One part of R1C1 text: its row and its column, each <see langword="null"/> where the part has no
    /// R or no C, a whole column or a whole row.
    /// </summary>
    private readonly record struct RowColumnPart(RowColumnAxis? Row, RowColumnAxis? Column)
    {
        public bool IsRelative => Row is { Relative: true } || Column is { Relative: true };

        /// <summary>Whether the part names one cell: it has both an R and a C.</summary>
        public bool IsCell => Row.HasValue && Column.HasValue;

        /// <summary>Whether a number, not an offset, names a row or a column past the Sheet's far edge.</summary>
        public bool HasNumberPastEdge =>
            Row is { Relative: false, Number: > Sheet.RowCount } || Column is { Relative: false, Number: > Sheet.ColumnCount };

        /// <summary>Whether both are cells, both whole rows or both whole columns: parts of two kinds joined name no place (INDIRECT-058, 062).</summary>
        public bool IsShapedAs(RowColumnPart other) => Row.HasValue == other.Row.HasValue && Column.HasValue == other.Column.HasValue;

        /// <summary>A part <see cref="RowColumnText"/> has matched.</summary>
        public static RowColumnPart Read(string part)
        {
            var match = RowColumnPartText().Match(part);
            return new RowColumnPart(
                match.Groups["r"].Success ? RowColumnAxis.Read(match.Groups["row"].Value) : null,
                match.Groups["c"].Success ? RowColumnAxis.Read(match.Groups["column"].Value) : null);
        }
    }

    /// <summary>One axis of an R1C1 part: a row or a column counted from 1, or an offset from the Formula's own.</summary>
    private readonly record struct RowColumnAxis(bool Relative, long Number)
    {
        /// <summary>
        /// <c>3</c> and <c>03</c> are absolute (INDIRECT-059); <c>[-1]</c>, <c>[+1]</c>, <c>[0]</c> and
        /// <c>[-0]</c> are offsets (INDIRECT-063 to 065); nothing written is an offset of 0, the Formula's
        /// own row or column (INDIRECT-044, 066). A number too long for any Sheet is past its edge.
        /// </summary>
        public static RowColumnAxis Read(string written)
        {
            if (written.Length == 0) return new RowColumnAxis(true, 0);
            var relative = written[0] == '[';
            var digits = relative ? written[1..^1] : written;
            var negative = digits.StartsWith('-');
            if (negative || digits.StartsWith('+')) digits = digits[1..];
            digits = digits.TrimStart('0');
            var number = digits.Length == 0 ? 0 : digits.Length > 9 ? 1_000_000_000L : long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            return new RowColumnAxis(relative, negative ? -number : number);
        }

        /// <summary>The row or column from 0, or −1 where the Sheet has none there: a number 0, or an offset past its edge.</summary>
        public int Place(int own, int count)
        {
            var place = Relative ? own + Number : Number - 1;
            return place >= 0 && place < count ? (int)place : -1;
        }
    }

    /// <summary>
    /// R1C1 notation, one part or two separated by <c>:</c>, after an optional Sheet qualifier: each
    /// part an R with a row (<c>3</c>, <c>[-1]</c>) or none, and a C with a column or none, or a C
    /// alone. Letters in either case.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        """
        ^(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?
        (?<first>[Rr](?:[0-9]+|\[[+-]?[0-9]+\])?(?:[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)?|[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)
        (?::(?<second>[Rr](?:[0-9]+|\[[+-]?[0-9]+\])?(?:[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)?|[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?))?$
        """,
        System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex RowColumnText();

    /// <summary>One R1C1 part: an R with a row or none, a C with a column or none, or both; each a number or an offset in brackets.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^(?:(?<r>[Rr])(?<row>[0-9]+|\[[+-]?[0-9]+\])?)?(?:(?<c>[Cc])(?<column>[0-9]+|\[[+-]?[0-9]+\])?)?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex RowColumnPartText();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z_\\][A-Za-z0-9_.]*\[", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex StructuredText();
}
