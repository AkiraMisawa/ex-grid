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
    /// array it is the whole row, as Microsoft documents. A kept blank (<see cref="Operand.KeptBlank"/>)
    /// stays marked in what <c>INDEX</c> picks of it, so its reader decides how to read it: Excel's
    /// <c>INDEX</c> took <c>SORT</c>'s out blank to <c>ISBLANK</c> (SORT-022) and <c>UNIQUE</c>'s out
    /// equal to empty text (UNIQUE-018); whether <c>UNIQUE</c>'s is blank UNIQUE-020 asks (ADR-0125).
    /// </remarks>
    private static Operand Index(FunctionCall call)
    {
        var source = call.KeepingBlanks(0);
        var picked = Pick(call, source);
        return source.KeptBlank ? picked.WithKeptBlank() : picked;
    }

    private static Operand Pick(FunctionCall call, Operand source)
    {
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
                    return source.SameSheet(new Area(area.Row1 + r1, area.Column1 + c1, area.Row1 + r2, area.Column1 + c2));
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
            OperandKind.Area => source.SameSheet(new Area(source.Area.Row1 + r, source.Area.Column1 + c, source.Area.Row1 + r, source.Area.Column1 + c)),
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
        if (IsArray(lookupOperand)) return Unlifted(call);
        if (call.Evaluator.ScalarOf(lookupOperand) is { IsError: true } lookupError) return Operand.Of(lookupError);
        var lookupArray = ArrayOrValue(call.Operand(1));
        if (!lookupArray.IsRange) return lookupArray.IsError ? lookupArray : Operand.Of(ErrorValue.Value);
        if (Vector.Of(lookupArray) is not { } vector) return Operand.Of(ErrorValue.Value);
        var outcome = Search(call, vector, modesAt: 2);
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
        return reference.SameSheet(new Area((int)top, (int)left, (int)bottom, (int)right));
    }

    /// <summary>
    /// INDIRECT (ADR-0124's note of 2026-10-10, a volatile function): the Reference its text names,
    /// to a cell or a range of this Sheet, with or without the Sheet's own qualifier. A Reference of
    /// several cells is a range for a function such as <c>SUM</c>, and spills where it is the result
    /// (ADR-0125). The text is text: an insertion or a deletion does not rewrite it.
    /// </summary>
    /// <remarks>
    /// The forms the seventeenth Windows run, the two area passes of 2026-10-12 and Windows run 18a and
    /// the oracle run of 2026-10-18 observed are read as Excel read them: A1 text; R1C1 text (<c>a1</c> FALSE, 0, a blank or left
    /// empty; <see cref="IndirectRowColumn"/>); a structured reference's text, <c>Positions[PV]</c>, read
    /// as the typed one is, in either style (INDIRECT-025, 039 to 041, 061); and a Spill Range,
    /// <c>A1#</c> or <c>R1C1#</c>, read as the typed <c>A1#</c> is (INDIRECT-026, 042, 060). Text that
    /// names no place is <c>#REF!</c>, as Excel's was observed to be: empty text, a number, a space before
    /// the Reference, a leading <c>=</c>, an intersection, a column past <c>XFD</c>, A1 text read as R1C1
    /// and R1C1 text read as A1, another Sheet's name, a table never declared, a column the table does not
    /// have, R1C1 parts joined that name no place, a row or column number 0 or past the Sheet's edge in
    /// R1C1 style, and in either style a <c>#</c> after a range, a row or a column (<c>A1:A2#</c>,
    /// <c>R3#</c>) or inside a join (<c>A1#:A2</c>, <c>R1C1#:R2C1</c>; INDIRECT-003, 007, 008, 016 to
    /// 022, 035, 040, 041, 058, 062, 073, 074, 077, 090 to 099, 104; ADR-0046, ADR-0049). <c>#REF!</c>
    /// too, by the rule those settle, which no case asks: other A1 text that goes on past its Reference,
    /// the engine's rule generalised from the intersection (INDIRECT-021); a row past 1048576, as
    /// <c>XFE1</c>'s column is (2026-10-12, decided with the user: A1 text past the Sheet's edge stays
    /// <c>#REF!</c>); and other text that is not R1C1 notation read in R1C1 style, as <c>B3</c> is
    /// (INDIRECT-035). Refused (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's, which no
    /// Formula can catch): the structured forms a Formula does not take (<c>#All</c>, <c>@</c>); and, by
    /// decision, in either style, a declared Linked Table's bare name, which Excel reads as the table's
    /// data (INDIRECT-043, 053 to 057), since a Linked Table is read by key, never by position (ADR-0049's
    /// rule 2; 2026-10-12, decided with the user: the bare name is refused, in either style). Relative
    /// R1C1 text in a Formula evaluated in no cell is <c>#VALUE!</c> too, as <c>ROW()</c> there is.
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

    /// <summary>INDIRECT's A1-style text: a Reference as a Formula writes one, <c>A1#</c>, or a Linked Table's text (<see cref="IndirectTableText"/>); a <c>#</c> after a range, a row or a column, or inside a join, refused.</summary>
    private static Operand IndirectA1(FunctionCall call, string text)
    {
        var cells = call.Evaluator.Cells;
        if (IndirectTableText(call, text) is { } table) return table;
        // A # inside a join, after its first part (A1#:A2, A1#:A2#, 1#:2), names no place: #REF!, as
        // the oracle run of 2026-10-18 read Excel (INDIRECT-098), whatever the Sheet.
        if (HashInsideJoin(text) is not null) return Operand.Of(ErrorValue.Ref);
        // A column past XFD names no place, as XFE1 was observed to (INDIRECT-017), and a row past 1048576
        // by the same rule (2026-10-12, decided with the user: A1 text past the Sheet's edge stays #REF!).
        // The reader reads no Reference there, and every reading of the text meets that place, so it is
        // #REF! whatever follows it.
        if (Lexer.ReadReference(text, 0, out var length) is not { } reference) return Operand.Of(ErrorValue.Ref);
        // A Sheet's name the Workbook does not have names no place, as Sheet2!B2 was observed to in a workbook
        // of one Sheet (INDIRECT-008); so too whatever follows the name, a # included, since every reading of
        // the rest names a place on a Sheet that is not there. A name it has reads that Sheet (ADR-0331).
        if (cells.Resolve(reference.SheetName) is not { } target) return Operand.Of(ErrorValue.Ref);
        if (length == text.Length - 1 && text[length] == '#')
        {
            // A1#: the Spill Range of the Formula in A1, read as the typed one is (INDIRECT-026, 042; ADR-0125).
            if (reference.Shape == ReferenceShape.Cell) return target.Spill(new CellAddress(reference.Row1, reference.Column1));
            // A # after a range, a whole row or a whole column names no place: #REF!, as the oracle run
            // of 2026-10-18 read Excel (INDIRECT-092, 096).
            return Operand.Of(ErrorValue.Ref);
        }
        // Text that goes on past the Reference is no Reference: the engine's rule, generalised from the
        // intersection Excel was observed to refuse (INDIRECT-021).
        if (length != text.Length) return Operand.Of(ErrorValue.Ref);
        return Operand.Of(reference.Area, target);
    }

    /// <summary>
    /// The join A1 text with a <c>#</c> inside it would be with that <c>#</c> taken out — a range, whole
    /// rows or whole columns, followed by nothing or by one more <c>#</c> — where the <c>#</c> stands
    /// right after the join's first part (<c>A1#:A2</c>, <c>Sheet1!1#:2</c>); otherwise
    /// <see langword="null"/>.
    /// </summary>
    private static Reference? HashInsideJoin(string text)
    {
        for (var hash = text.IndexOf("#:", StringComparison.Ordinal); hash >= 0; hash = text.IndexOf("#:", hash + 1, StringComparison.Ordinal))
        {
            var join = text.Remove(hash, 1);
            if (Lexer.MatchReference(join, 0) is not { } match) continue;
            // The : the # stood before is the join's, not one inside a quoted Sheet name.
            if (match.Groups["sheet"] is { Success: true } qualifier && hash < qualifier.Index + qualifier.Length) continue;
            if (match.Length != join.Length && (match.Length != join.Length - 1 || join[^1] != '#')) continue;
            if (Lexer.ReadReference(join, 0, out _) is { Shape: not ReferenceShape.Cell } reference) return reference;
        }
        return null;
    }

    /// <summary>
    /// A Linked Table's text, read alike whatever <c>a1</c> says (INDIRECT-025, INDIRECT-061), or
    /// <see langword="null"/> for text that is none: <c>Table[Column]</c> or <c>Table[[Column]]</c>, read
    /// as the typed one is (ADR-0049), so that a table still waiting for its data waits, and one never
    /// declared names no place, as a column it lacks does; any other text a structured reference begins
    /// as, a form a Formula does not take (<c>#All</c>, <c>@</c>), refused; and a declared table's bare
    /// name, refused by decision (ADR-0049's rule 2, ADR-0124; 2026-10-12, decided with the user: the bare
    /// name is refused, in either style): Excel reads it as the table's data, every column and every row
    /// without the header (INDIRECT-043, 053 to 057 record its answers), which a Formula could then read
    /// by position, and a Linked Table is read by key, never by position.
    /// </summary>
    private static Operand? IndirectTableText(FunctionCall call, string text)
    {
        var cells = call.Evaluator.Cells;
        if (Lexer.ReadStructuredReference(text, 0) is { } structured && structured.Length == text.Length)
        {
            var column = cells.TableColumn(structured.Table, structured.Column);
            return column.IsError && column.Scalar!.Value.Error == ErrorValue.Name ? Operand.Of(ErrorValue.Ref) : column;
        }
        if (StructuredText().IsMatch(text)) return call.Evaluator.Refuse();
        if (cells.IsLinkedTable(text)) return call.Evaluator.Refuse();
        return null;
    }

    /// <summary>
    /// INDIRECT's R1C1-style text (ADR-0124's notes of 2026-10-12, 2026-10-14 and 2026-10-18), with or
    /// without the Sheet's own qualifier, read as the seventeenth Windows run, the two area passes and
    /// Windows run 18a and the oracle run of 2026-10-18 read Excel: R and C each with a number counted from 1, a leading 0 included
    /// (<c>R3C2</c>, <c>R03C2</c>), and a number 0 or past the Sheet's edge naming no place, <c>#REF!</c>
    /// (INDIRECT-073, 074, 095); an offset in brackets from the Formula's own cell, <c>+</c>, 0 and
    /// <c>-0</c> included (<c>R[1]C[-1]</c>, <c>R[+1]C[-1]</c>, <c>R[0]C[-1]</c>, <c>R[-0]C[-1]</c>), which
    /// wraps round to the Sheet's far side past its edge (INDIRECT-052, 082 to 084); or no number, the
    /// Formula's own row or column (<c>RC[-1]</c>, <c>R[2]C</c>); a row part alone a whole row (<c>R3</c>,
    /// <c>R[1]</c>), a column part alone a whole column (<c>C2</c>, <c>C[-1]</c>); parts joined by
    /// <c>:</c>, two or three, joined left to right as <see cref="RowColumnSpan.Join"/> says
    /// (<c>R2C2:R5C2</c>, <c>R2C2:C3</c>, <c>R3:R2C2</c>, <c>R2:R3C2:C3</c>; INDIRECT-058, 062, 075 to 077,
    /// 101 to 105); one cell followed by <c>#</c>, its Spill Range (<c>R1C1#</c>), relative ones included,
    /// and <c>#REF!</c> where the cell does not spill, as <c>A1#</c> is (INDIRECT-078, 079), and any other
    /// <c>#</c> <c>#REF!</c> (INDIRECT-090, 091, 094, 097, 099); another Sheet's name <c>#REF!</c>,
    /// whatever follows it, as in A1 style (INDIRECT-093); and A1 text, which is not R1C1 notation,
    /// <c>#REF!</c> (<c>B3</c>, INDIRECT-035), as any other text that is not R1C1 notation is, by that
    /// rule. A Linked Table's text is read as in A1 style (<see cref="IndirectTableText"/>).
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
        // A Sheet's name the Workbook does not have names no place, as it was observed to in A1 style
        // (INDIRECT-008): read so in R1C1 style, which no run has asked (INDIRECT-093), whatever follows it,
        // since every reading of the rest names a place on a Sheet that is not there (ADR-0331).
        if (call.Evaluator.Cells.Resolve(sheet) is not { } target) return Operand.Of(ErrorValue.Ref);
        var inside = match.Groups["inside"].Success;
        var parts = new List<RowColumnPart> { RowColumnPart.Read(match.Groups["first"].Value) };
        if (match.Groups["second"].Success) parts.Add(RowColumnPart.Read(match.Groups["second"].Value));
        if (match.Groups["third"].Success) parts.Add(RowColumnPart.Read(match.Groups["third"].Value));
        // A # after one cell is its Spill Range, as A1#'s is (INDIRECT-060). After a range, a row or a
        // column, whatever the parts joined, and inside a join, after its first part, it names no place:
        // #REF!, as the oracle run of 2026-10-18 read Excel (INDIRECT-090, 091, 094, 097, 099).
        if (inside || spilled && (parts.Count > 1 || !parts[0].IsCell)) return Operand.Of(ErrorValue.Ref);
        // An offset counts from the Formula's own cell; a Formula evaluated in no cell has none, as ROW() has none.
        CellAddress own = default;
        if (parts.Exists(part => part.IsRelative))
        {
            if (call.Evaluator.Self is not { } self) return Operand.Of(ErrorValue.Value);
            own = self;
        }
        // Each part is placed on the Sheet first. A number 0, or a number past the Sheet's edge, names no
        // place, #REF! (INDIRECT-073, 074, 095); an offset past the edge wraps round to the far side
        // (INDIRECT-052, 082 to 084), as the oracle run of 2026-10-18 read Excel.
        var spans = new List<RowColumnSpan>(parts.Count);
        foreach (var part in parts)
        {
            if (RowColumnSpan.Place(part, own) is not { } span) return Operand.Of(ErrorValue.Ref);
            spans.Add(span);
        }
        // The parts are then joined left to right, each join the rectangle it spans (RowColumnSpan.Join):
        // R2:R3C2:C3 is B2:B3 joined to column C, B2:C3 (INDIRECT-105).
        var joined = spans[0];
        for (var i = 1; i < spans.Count; i++)
            if (RowColumnSpan.Join(joined, spans[i]) is { } next) joined = next;
            else return Operand.Of(ErrorValue.Ref);
        if (spilled) return target.Spill(new CellAddress(joined.Row1, joined.Column1));
        var (row1, row2) = joined.Rows ?? (0, Sheet.RowCount - 1);
        var (column1, column2) = joined.Columns ?? (0, Sheet.ColumnCount - 1);
        var reference = Reference.Rectangle(sheet, row1, column1, row2, column2, true, true, true, true);
        return Operand.Of(reference.Area, target);
    }

    /// <summary>
    /// A part of R1C1 text placed on the Sheet, or several joined: the rows and the columns it spans,
    /// each from 0 and in order, and each <see langword="null"/> where it spans the whole Sheet, a whole
    /// column or a whole row.
    /// </summary>
    private readonly record struct RowColumnSpan((int Low, int High)? Rows, (int Low, int High)? Columns)
    {
        public int Row1 => Rows?.Low ?? 0;

        public int Column1 => Columns?.Low ?? 0;

        /// <summary>The part placed from the Formula's own cell, or <see langword="null"/> where an axis names no place on the Sheet.</summary>
        public static RowColumnSpan? Place(RowColumnPart part, CellAddress own)
        {
            (int, int)? rows = null, columns = null;
            if (part.Row is { } row)
            {
                if (row.Place(own.Row, Sheet.RowCount) is not { } r) return null;
                rows = (r, r);
            }
            if (part.Column is { } column)
            {
                if (column.Place(own.Column, Sheet.ColumnCount) is not { } c) return null;
                columns = (c, c);
            }
            return new RowColumnSpan(rows, columns);
        }

        /// <summary>
        /// Two spans joined by <c>:</c>, as Windows run 18a and the oracle run of 2026-10-18 read Excel: two of the same kind, the
        /// rectangle they span (<c>R2C2:R5C2</c>, <c>R2:R3</c>, <c>C1:C2</c>); cells then a whole column,
        /// the cells' rows across the columns they span (<c>R2C2:C3</c>, B2:C2, INDIRECT-075, 101, 103);
        /// a whole row then cells, the rows they span down the cells' columns (<c>R3:R2C2</c>, B2:B3,
        /// INDIRECT-076, 102). Any other two name no place, <see langword="null"/>: cells then a whole row
        /// (INDIRECT-058), a whole column then cells (INDIRECT-104), and a whole row and a whole column in
        /// either order (INDIRECT-062, 077).
        /// </summary>
        public static RowColumnSpan? Join(RowColumnSpan first, RowColumnSpan second)
        {
            var firstCells = first.Rows.HasValue && first.Columns.HasValue;
            var secondCells = second.Rows.HasValue && second.Columns.HasValue;
            if (firstCells && second is { Rows: null, Columns: not null }) second = second with { Rows = first.Rows };
            else if (secondCells && first is { Rows: not null, Columns: null }) first = first with { Columns = second.Columns };
            if (first.Rows.HasValue != second.Rows.HasValue || first.Columns.HasValue != second.Columns.HasValue) return null;
            return new RowColumnSpan(Hull(first.Rows, second.Rows), Hull(first.Columns, second.Columns));
        }

        private static (int, int)? Hull((int Low, int High)? a, (int Low, int High)? b) =>
            a is { } x && b is { } y ? (Math.Min(x.Low, y.Low), Math.Max(x.High, y.High)) : null;
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

        /// <summary>
        /// The row or column from 0, or <see langword="null"/> where a number names no place: 0, or past
        /// the Sheet's edge (INDIRECT-073, 074, 095). An offset past the edge wraps round to the far side,
        /// as the oracle run of 2026-10-18 read Excel (INDIRECT-052, 082 to 084); an offset as long as the
        /// Sheet or longer, which Excel's own R1C1 notation cannot write and no run has asked, names no
        /// place, read by the rule a number past the edge settles.
        /// </summary>
        public int? Place(int own, int count)
        {
            if (Relative) return Math.Abs(Number) < count ? (int)((own + Number + count) % count) : null;
            return Number >= 1 && Number <= count ? (int)(Number - 1) : null;
        }
    }

    /// <summary>
    /// R1C1 notation, one part or two separated by <c>:</c>, after an optional Sheet qualifier: each
    /// part an R with a row (<c>3</c>, <c>[-1]</c>) or none, and a C with a column or none, or a C
    /// alone. Letters in either case. A <c>#</c> before the <c>:</c> is matched, as <c>inside</c>, so
    /// that it can be refused.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        """
        ^(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?
        (?<first>[Rr](?:[0-9]+|\[[+-]?[0-9]+\])?(?:[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)?|[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)
        (?:(?<inside>\#)?:(?<second>[Rr](?:[0-9]+|\[[+-]?[0-9]+\])?(?:[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)?|[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)
        (?::(?<third>[Rr](?:[0-9]+|\[[+-]?[0-9]+\])?(?:[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?)?|[Cc](?:[0-9]+|\[[+-]?[0-9]+\])?))?)?\z
        """,
        System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex RowColumnText();

    /// <summary>One R1C1 part: an R with a row or none, a C with a column or none, or both; each a number or an offset in brackets.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^(?:(?<r>[Rr])(?<row>[0-9]+|\[[+-]?[0-9]+\])?)?(?:(?<c>[Cc])(?<column>[0-9]+|\[[+-]?[0-9]+\])?)?\z",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex RowColumnPartText();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z_\\][A-Za-z0-9_.]*\[", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex StructuredText();
}
