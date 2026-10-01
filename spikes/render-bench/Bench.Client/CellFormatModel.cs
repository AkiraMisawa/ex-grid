using System.Globalization;
using System.Text;

namespace Bench.Client;

/// <summary>
/// The per-cell appearance modes (ticket 44, ADR-0063 "How it is painted: measured first"). Every one
/// is built on RowComponent: the row is the memoisation boundary and cells are plain markup.
/// </summary>
public enum CellFormatMode
{
    /// <summary>The same row with no Cell Format: the baseline every other mode is read against.</summary>
    RowComponent,
    /// <summary>Font and Fill as an inline <c>style</c> on each formatted cell, one interned string per format.</summary>
    CellFormatInline,
    /// <summary>Font and Fill as per-cell custom properties, read by one static rule on every cell.</summary>
    CellFormatVars,
    /// <summary>Font and Fill as interned classes, one per distinct format, written into a generated &lt;style&gt;.</summary>
    CellFormatClasses,
    /// <summary>Borders drawn by a layer over the rows: one element per run of the same line along a
    /// painted row or column, as ADR-0008's overlay draws ranges. The rows know nothing of borders.</summary>
    BorderLayer,
    /// <summary>Borders drawn inside each cell's box: each cell paints its resolved bottom and right
    /// edges through interned classes, in place of its gridlines.</summary>
    BorderInCell,
    /// <summary>Not one of the ticket's two: BorderLayer split into one strip per painted row, keyed
    /// and memoised as the rows are. Measured because BorderLayer renders every run on every scroll
    /// step.</summary>
    BorderLayerPerRow,
    /// <summary>Not one of the ticket's two: each cell paints its own share of Excel's line on its four
    /// edges (the eleventh run's case 9 geometry), as background layers above its Fill, so the lines
    /// are Excel's to the pixel and nothing is painted outside the row.</summary>
    BorderInCellExcel,
    /// <summary>BorderInCellExcel's pixels without custom properties or per-cell gradients: solid
    /// lines are the cell's own borders, the parts past the gridline and double's inner row are inset
    /// shadows, and a dash pattern is a pseudo-element whose rule is the line's own.</summary>
    BorderInCellExcelBox,
}

/// <summary>
/// The distinct Cell Formats of one run. Each carries a Font colour, a Fill and bold, as the ticket
/// asks. The string each mechanism paints is interned once, when the format is created, and never
/// composed in the render loop (P5).
/// </summary>
public sealed class FormatTable
{
    /// <summary>Per format id, the inline style. Id 0 is "no format": null, which Blazor omits.</summary>
    public readonly List<string?> Inline = [null];
    /// <summary>Per format id, the custom properties the static rule reads.</summary>
    public readonly List<string?> Vars = [null];
    /// <summary>Per format id, the whole class attribute.</summary>
    public readonly List<string> Classes = ["c num"];

    private readonly StringBuilder _rules = new();

    /// <summary>The generated stylesheet of CellFormatClasses: one rule per format.</summary>
    public string Rules => _rules.ToString();

    public ushort Add()
    {
        var id = Inline.Count;
        var ink = Colour(id, 0x20, 8);
        var fill = Colour(id, 0xC0, 4);
        Inline.Add($"color:{ink};background-color:{fill};font-weight:700");
        Vars.Add($"--fc:{ink};--fb:{fill};--fw:700");
        Classes.Add($"c num f{id}");
        _rules.Append($".fx .f{id}{{color:{ink};background-color:{fill};font-weight:700}}\n");
        return (ushort)id;
    }

    /// <summary>Distinct for the first 4096 ids: each 4-bit digit of the id moves one channel.</summary>
    internal static string Colour(int id, int floor, int stride) =>
        $"#{floor + (id & 15) * stride:x2}{floor + ((id >> 4) & 15) * stride:x2}{floor + ((id >> 8) & 15) * stride:x2}";
}

/// <summary>One row's format ids, one per column (0 = none). Immutable: a change hands the row a new
/// instance, which is its change signal.</summary>
public sealed class RowFormat(ushort[] ids)
{
    public readonly ushort[] Ids = ids;
}

/// <summary>
/// The distinct lines of one run: one of Excel's thirteen line styles and a colour, cycled through by
/// id, so every border mode draws the same edges. Each mechanism's classes are interned once, here.
/// </summary>
public sealed class BorderTable
{
    /// <summary>
    /// Excel's line styles as the eleventh Windows run drew them at 100% (case 9,
    /// <c>verification/2026-10-01-windows-excel-11/cell-format.md</c>, group 3). <c>Up</c> is the pixels
    /// on the gridline and above it (left of it, for a vertical edge); <c>Down</c> the pixels below it
    /// (right of it); <c>Dashes</c> the lengths along the line, on first. The other modes draw the
    /// nearest CSS border: <c>CssWidth</c> and <c>CssStyle</c>.
    /// </summary>
    private static readonly (string Name, int Up, int Down, int[]? Dashes, int CssWidth, string CssStyle)[] Styles =
    [
        ("thin", 1, 0, null, 1, "solid"),
        ("hair", 1, 0, [1, 1], 1, "dotted"),
        ("medium", 2, 0, null, 2, "solid"),
        ("thick", 2, 1, null, 3, "solid"),
        ("double", 2, 1, null, 3, "double"),
        ("dotted", 1, 0, [2, 2], 1, "dotted"),
        ("dashed", 1, 0, [3, 1], 1, "dashed"),
        ("dash-dot", 1, 0, [8, 3, 3, 3], 1, "dashed"),
        ("dash-dot-dot", 1, 0, [8, 3, 3, 3, 3, 3], 1, "dashed"),
        ("medium dashed", 2, 0, [8, 3], 2, "dashed"),
        ("medium dash-dot", 2, 0, [8, 3, 3, 3], 2, "dashed"),
        ("medium dash-dot-dot", 2, 0, [8, 3, 3, 3, 3, 3], 2, "dashed"),
        // Excel offsets its two rows; one pattern over both prices it closely enough.
        ("slanted dash-dot", 2, 0, [11, 1, 5, 1], 2, "dashed"),
    ];

    /// <summary>Per line id, the class of a horizontal run in the layer.</summary>
    public readonly List<string> Horizontal = [""];
    /// <summary>Per line id, the class of a vertical run in the layer.</summary>
    public readonly List<string> Vertical = [""];

    private readonly List<int> _style = [0];
    private readonly StringBuilder _layerRules = new();
    private readonly StringBuilder _cellRules = new();
    private readonly StringBuilder _excelRules = new();
    private readonly Dictionary<(ushort Bottom, ushort Right), string> _cellClasses = new();
    private readonly Dictionary<(ushort Top, ushort Right, ushort Bottom, ushort Left), string> _excelClasses = new();
    private readonly Dictionary<(ushort Top, ushort Left, ushort Bottom, ushort Right), string> _shadowClasses = new();
    private readonly Dictionary<(ushort Top, ushort Right, ushort Bottom, ushort Left), string> _boxClasses = new();
    private readonly List<string> _colour = [""];
    private readonly StringBuilder _boxBase = new();
    private readonly StringBuilder _boxScaled = new();

    /// <summary>Bumped whenever a rule is added, including the shadow rules BorderInCellExcelBox adds
    /// as new combinations appear, so the page knows to rewrite the generated sheet.</summary>
    public int Version { get; private set; }

    /// <summary>BorderInCellExcelBox's resolutions: its lengths are written out in CSS pixels for each,
    /// with no custom property for a cell to resolve. The bench writes the two it checks.</summary>
    private static readonly double[] Scales = [1, 1.5];

    /// <summary>The generated stylesheet of BorderLayer and BorderLayerPerRow.</summary>
    public string LayerRules => _layerRules.ToString();
    /// <summary>The generated stylesheet of BorderInCell.</summary>
    public string CellRules => _cellRules.ToString();
    /// <summary>The generated stylesheet of BorderInCellExcel.</summary>
    public string ExcelRules => _excelRules.ToString();
    /// <summary>The generated stylesheet of BorderInCellExcelBox.</summary>
    public string BoxRules => $"{_boxBase}@media (resolution: 1.5dppx) {{\n{_boxScaled}}}\n";

    public ushort Add(string? colour = null)
    {
        var id = Horizontal.Count;
        var style = (id - 1) % Styles.Length;
        var s = Styles[style];
        colour ??= id == 1 ? "#000000" : FormatTable.Colour(id, 0x10, 8);
        _style.Add(style);

        // The layer: a run sits on the gridline's pixel, and a line of two or three pixels backs up by
        // one, so medium takes the gridline and the pixel above it, and thick and double the gridline
        // and one pixel either side, as Excel's do (case 9). Only the dash patterns are CSS's own.
        var line = $"{s.CssWidth}px {s.CssStyle} {colour}";
        var back = s.CssWidth >= 2 ? 1 : 0;
        Horizontal.Add($"bh k{id}");
        Vertical.Add($"bv k{id}");
        _layerRules.Append($".fx .bh.k{id}{{border-top:{line};margin-top:-{back}px}}\n");
        _layerRules.Append($".fx .bv.k{id}{{border-left:{line};margin-left:-{back}px}}\n");

        // In the cell's box: the whole line inside the upper (left) cell, ending on its gridline.
        _cellRules.Append($".fx .c.bb{id}{{border-bottom:{line}}}\n");
        _cellRules.Append($".fx .c.br{id}{{border-right:{line}}}\n");

        // Excel's pixels, each cell painting its own share: the upper (left) cell the gridline and what
        // lies above (left of) it, the lower (right) cell what lies below (right of) it. Background
        // layers, positioned against the border box, over the Fill and under the text; the gridline's
        // border turns transparent where a line lies, so the layer shows through it. Every length is
        // in device pixels (--dp), because Excel's are: at 150% thin is still 1px and thick 3 (case 9).
        string up(bool horizontal) => s.Name == "double"
            ? $"linear-gradient(to {(horizontal ? "bottom" : "right")},{colour} 0 {Dp(1)},#fff {Dp(1)} {Dp(2)})"
            : Dashed(colour, s.Dashes, horizontal);
        _excelRules.Append($".fx .eb{id}{{--eb:{up(true)};--ebs:100% {Dp(s.Up)};border-bottom-color:transparent}}\n");
        _excelRules.Append($".fx .er{id}{{--er:{up(false)};--ers:{Dp(s.Up)} 100%;border-right-color:transparent}}\n");
        if (s.Down > 0)
        {
            _excelRules.Append($".fx .et{id}{{--et:linear-gradient({colour},{colour});--ets:100% {Dp(s.Down)}}}\n");
            _excelRules.Append($".fx .el{id}{{--el:linear-gradient({colour},{colour});--els:{Dp(s.Down)} 100%}}\n");
        }

        // BorderInCellExcelBox: the same pixels from plain declarations. The cell keeps 7px between
        // its text and its right edge (1px gridline and 6px padding), so a wider or missing right
        // border moves no text.
        _colour.Add(colour);
        foreach (var scale in Scales)
        {
            var dp = 1 / scale;
            var rules = scale == 1 ? _boxBase : _boxScaled;
            if (s.Dashes is null && s.Name != "double")
            {
                rules.Append($".fx .c.xb{id}{{border-bottom:{Px(s.Up * dp)} solid {colour}}}\n");
                rules.Append($".fx .c.xr{id}{{border-right:{Px(s.Up * dp)} solid {colour};padding-right:{Px(6 + dp - s.Up * dp)}}}\n");
            }
            else if (s.Name == "double")
            {
                // The gridline's pixel is white; the dark row inside it is an inset shadow.
                rules.Append($".fx .c.xb{id}{{border-bottom-color:#fff}}\n");
                rules.Append($".fx .c.xr{id}{{border-right-color:#fff}}\n");
            }
            else
            {
                rules.Append($".fx .c.xb{id}{{border-bottom-width:0;position:relative}}\n");
                rules.Append($".fx .c.xb{id}::after{{content:\"\";position:absolute;left:0;right:0;bottom:0;height:{Px(s.Up * dp)};background:{Pattern(colour, s.Dashes!, true, dp)}}}\n");
                rules.Append($".fx .c.xr{id}{{border-right-width:0;padding-right:{Px(6 + dp)};position:relative}}\n");
                rules.Append($".fx .c.xr{id}::before{{content:\"\";position:absolute;top:0;bottom:0;right:0;width:{Px(s.Up * dp)};background:{Pattern(colour, s.Dashes!, false, dp)}}}\n");
            }
        }
        Version++;
        return (ushort)id;
    }

    private static string Dashed(string colour, int[]? dashes, bool horizontal)
    {
        if (dashes is null) return $"linear-gradient({colour},{colour})";
        var css = new StringBuilder($"repeating-linear-gradient(to {(horizontal ? "right" : "bottom")}");
        var at = 0;
        for (var i = 0; i < dashes.Length; i++)
        {
            css.Append($",{(i % 2 == 0 ? colour : "transparent")} {Dp(at)} {Dp(at + dashes[i])}");
            at += dashes[i];
        }
        return css.Append(')').ToString();
    }

    private static string Px(double px) => px.ToString("0.####", CultureInfo.InvariantCulture) + "px";

    private static string Pattern(string colour, int[] dashes, bool horizontal, double dp)
    {
        var css = new StringBuilder($"repeating-linear-gradient(to {(horizontal ? "right" : "bottom")}");
        var at = 0;
        for (var i = 0; i < dashes.Length; i++)
        {
            css.Append($",{(i % 2 == 0 ? colour : "transparent")} {Px(at * dp)} {Px((at + dashes[i]) * dp)}");
            at += dashes[i];
        }
        return css.Append(')').ToString();
    }

    /// <summary>BorderInCellExcelBox's class attribute for a cell's four resolved edges, interned. The
    /// parts drawn as inset shadows (the pixel past the gridline of a thick or double top or left edge,
    /// and double's inner row on the bottom or right) share one box-shadow, so their combination is a
    /// class of its own, added to the sheet the first time it appears.</summary>
    public string BoxClass(ushort top, ushort right, ushort bottom, ushort left)
    {
        if (top != 0 && Styles[_style[top]].Down == 0) top = 0;
        if (left != 0 && Styles[_style[left]].Down == 0) left = 0;
        if (_boxClasses.TryGetValue((top, right, bottom, left), out var cls)) return cls;

        var css = new StringBuilder("c num");
        if (bottom != 0) css.Append($" xb{bottom}");
        if (right != 0) css.Append($" xr{right}");
        var darkBottom = bottom != 0 && Styles[_style[bottom]].Name == "double" ? bottom : (ushort)0;
        var darkRight = right != 0 && Styles[_style[right]].Name == "double" ? right : (ushort)0;
        if (top != 0 || left != 0 || darkBottom != 0 || darkRight != 0)
            css.Append(' ').Append(ShadowClass(top, left, darkBottom, darkRight));
        cls = css.ToString();
        _boxClasses[(top, right, bottom, left)] = cls;
        return cls;
    }

    private string ShadowClass(ushort top, ushort left, ushort bottom, ushort right)
    {
        if (_shadowClasses.TryGetValue((top, left, bottom, right), out var cls)) return cls;
        cls = $"xs{_shadowClasses.Count + 1}";
        _shadowClasses[(top, left, bottom, right)] = cls;
        foreach (var scale in Scales)
        {
            var dp = Px(1 / scale);
            var shadows = new List<string>();
            if (top != 0) shadows.Add($"inset 0 {dp} 0 {_colour[top]}");
            if (left != 0) shadows.Add($"inset {dp} 0 0 {_colour[left]}");
            if (bottom != 0) shadows.Add($"inset 0 -{dp} 0 {_colour[bottom]}");
            if (right != 0) shadows.Add($"inset -{dp} 0 0 {_colour[right]}");
            (scale == 1 ? _boxBase : _boxScaled).Append($".fx .c.{cls}{{box-shadow:{string.Join(",", shadows)}}}\n");
        }
        Version++;
        return cls;
    }

    /// <summary>A length of <paramref name="n"/> device pixels, from the --dp token the stylesheet sets
    /// per resolution.</summary>
    private static string Dp(int n) => n switch
    {
        0 => "0px",
        1 => "var(--dp)",
        _ => $"calc({n}*var(--dp))",
    };

    /// <summary>The in-cell class attribute for a cell's resolved edges, interned.</summary>
    public string CellClass(ushort bottom, ushort right)
    {
        if (!_cellClasses.TryGetValue((bottom, right), out var cls))
        {
            cls = (bottom, right) switch
            {
                (0, 0) => "c num",
                (_, 0) => $"c num bb{bottom}",
                (0, _) => $"c num br{right}",
                _ => $"c num bb{bottom} br{right}",
            };
            _cellClasses[(bottom, right)] = cls;
        }
        return cls;
    }

    /// <summary>BorderInCellExcel's class attribute for a cell's four resolved edges, interned. A top or
    /// left edge enters it only when that line reaches past the gridline into this cell.</summary>
    public string ExcelClass(ushort top, ushort right, ushort bottom, ushort left)
    {
        if (top != 0 && Styles[_style[top]].Down == 0) top = 0;
        if (left != 0 && Styles[_style[left]].Down == 0) left = 0;
        if (!_excelClasses.TryGetValue((top, right, bottom, left), out var cls))
        {
            var css = new StringBuilder("c num");
            if (bottom != 0) css.Append($" eb{bottom}");
            if (right != 0) css.Append($" er{right}");
            if (top != 0) css.Append($" et{top}");
            if (left != 0) css.Append($" el{left}");
            cls = css.ToString();
            _excelClasses[(top, right, bottom, left)] = cls;
        }
        return cls;
    }
}

/// <summary>
/// One row's resolved edges: the line on each cell's bottom and right edge, after the rule for an edge
/// recorded on both sides has been applied. Immutable, so it is the row's change signal.
/// </summary>
public sealed class RowBorders
{
    public required ushort[] Bottom { get; init; }
    public required ushort[] Right { get; init; }
    /// <summary>The in-cell modes' class per cell, composed here, once per change (P5); null for the
    /// layers.</summary>
    public string[]? CellClass { get; init; }
}

/// <summary>Which class, if any, a <see cref="BorderSheet"/> composes per cell.</summary>
public enum BorderPaint { Layer, InCell, InCellExcel, InCellExcelBox }

/// <summary>
/// The Borders of the sheet. Each formatted cell records the same line on its four sides, as Excel's
/// "All Borders" does. Resolution couples neighbours: a cell's bottom edge is also the top of the cell
/// below, so a change re-resolves the rows either side as well, and a row is replaced only when what
/// it paints from actually changed.
/// </summary>
public sealed class BorderSheet
{
    /// <summary>The line each cell records on its four sides (0 = none).</summary>
    public readonly ushort[][] Recorded;
    /// <summary>The resolved edges, per row.</summary>
    public readonly RowBorders[] Rows;

    private readonly BorderTable _table;
    private readonly BorderPaint _paint;

    public BorderSheet(ushort[][] recorded, BorderTable table, BorderPaint paint)
    {
        Recorded = recorded;
        Rows = new RowBorders[recorded.Length];
        _table = table;
        _paint = paint;
        for (var r = 0; r < recorded.Length; r++) Resolve(r);
    }

    /// <summary>Re-resolves row <paramref name="r"/>; replaces it only when an edge it paints changed.</summary>
    public bool Resolve(int r)
    {
        var n = Recorded.Length;
        r = (r % n + n) % n;
        var own = Recorded[r];
        var above = Recorded[(r + n - 1) % n];
        var below = Recorded[(r + 1) % n];
        var cols = own.Length;
        var bottom = new ushort[cols];
        var right = new ushort[cols];
        for (var c = 0; c < cols; c++)
        {
            // An edge recorded on both sides: the upper (left) cell's line wins. Excel draws the later
            // one set (case 7); which side wins does not change the cost.
            bottom[c] = own[c] != 0 ? own[c] : below[c];
            right[c] = own[c] != 0 ? own[c] : c + 1 < cols ? own[c + 1] : (ushort)0;
        }

        string[]? classes = null;
        if (_paint == BorderPaint.InCell)
        {
            classes = new string[cols];
            for (var c = 0; c < cols; c++) classes[c] = _table.CellClass(bottom[c], right[c]);
        }
        else if (_paint is BorderPaint.InCellExcel or BorderPaint.InCellExcelBox)
        {
            // This cell also paints the part of its top and left edges that lies inside it: the top
            // edge is the row above's bottom edge, which is why a border change can repaint the row
            // below.
            classes = new string[cols];
            for (var c = 0; c < cols; c++)
            {
                var top = above[c] != 0 ? above[c] : own[c];
                var left = c == 0 ? (ushort)0 : right[c - 1];
                classes[c] = _paint == BorderPaint.InCellExcel
                    ? _table.ExcelClass(top, right[c], bottom[c], left)
                    : _table.BoxClass(top, right[c], bottom[c], left);
            }
        }

        var old = Rows[r];
        if (old is not null
            && old.Bottom.AsSpan().SequenceEqual(bottom)
            && old.Right.AsSpan().SequenceEqual(right)
            && SameClasses(old.CellClass, classes))
        {
            return false;
        }
        Rows[r] = new RowBorders { Bottom = bottom, Right = right, CellClass = classes };
        return true;
    }

    // The classes are interned, so a reference comparison is an equality one.
    private static bool SameClasses(string[]? a, string[]? b)
    {
        if (a is null || b is null) return a is null && b is null;
        for (var i = 0; i < a.Length; i++)
            if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }
}

/// <summary>One measured phase of a run: a scroll, a column change, or a render with nothing changed.</summary>
public sealed class PhaseResult
{
    public required int Steps { get; init; }
    /// <summary>Blazor's render plus the DOM update, measured as the existing modes are.</summary>
    public required Stats Render { get; init; }
    /// <summary>The style recalculation and layout that render caused, forced synchronously right after it.</summary>
    public required Stats Layout { get; init; }
    /// <summary>The next frame's main-thread paint and commit, from its animation frame to the task after it.</summary>
    public required Stats Paint { get; init; }
    /// <summary>Render + Layout + Paint of the same step: the frame's cost on the main thread.</summary>
    public required Stats Frame { get; init; }
    public required double RowRendersPerStep { get; init; }
    public required int MaxRowRenders { get; init; }
    /// <summary>Renders of the border layer: BorderOverlay, or the strips of BorderLayerPerRow.</summary>
    public required double LayerRendersPerStep { get; init; }
    public required int MaxLayerRenders { get; init; }
    public required double StyleSheetRendersPerStep { get; init; }
    /// <summary>The border layers only: the layer's run elements after each step, averaged.</summary>
    public required double LayerElements { get; init; }
}

public sealed class FormatRunResult
{
    public required string Mode { get; init; }
    public required int SharePct { get; init; }
    public required int Distinct { get; init; }
    public required int VisibleRows { get; init; }
    public required int VisibleCols { get; init; }
    public required int Cells { get; init; }
    public required int Step { get; init; }
    /// <summary>Scrolling by <see cref="Step"/> rows: every painted row is new.</summary>
    public required PhaseResult Fling { get; init; }
    /// <summary>Scrolling by one row: one row enters, the others must skip.</summary>
    public required PhaseResult Slow { get; init; }
    /// <summary>One format change over a whole column (a new format, so a generated sheet grows).</summary>
    public required PhaseResult ColumnChange { get; init; }
    /// <summary>A render of the page with nothing changed: no row may render.</summary>
    public required PhaseResult NoChange { get; init; }
    /// <summary>One cell's format changed: only the rows whose appearance changed may render.</summary>
    public required PhaseResult OneCell { get; init; }
    public required bool RowsSkip { get; init; }
    public string? SkipNote { get; init; }
}

public sealed class FormatReport
{
    public required string TimestampLocal { get; init; }
    public required Dictionary<string, object> Environment { get; init; }
    public required List<FormatRunResult> Runs { get; init; }
    public Stats? ManualScrollFrames { get; init; }
    public string? Note { get; init; }
}
