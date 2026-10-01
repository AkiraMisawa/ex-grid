using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ExGrid.Cells;

namespace ExGrid.Components;

/// <summary>
/// The classes a per-cell appearance is painted with, and the stylesheet that gives them their
/// meaning (ADR-0050, item 15; ADR-0071, "What the measurement chose"). One grid's, so its strings
/// and rules go with it.
///
/// <para><b>Interned per part, never per combination of parts.</b> Each distinct Font, each Fill
/// colour, and each line on each side gets one class and one rule, the first time it is asked for.
/// A cell names the classes of its parts; the combination is interned here as a string (P5), but the
/// stylesheet grows only with the parts. A class's name says what it paints — <c>ex-fill-ffff00</c>
/// is that yellow in every grid — so two grids on one page that write the same rule write the same
/// meaning (ADR-0018).</para>
///
/// <para><b>Lines are drawn inside each cell</b>, each cell painting its own share of Excel's centred
/// line (the eleventh Windows run, case 9): the upper (left) cell the gridline's pixel and the
/// pixels above it (left of it), the lower (right) cell what lies below (right of) it, which only
/// thick and double have. A solid share on the gridline is the cell's own bottom or right border —
/// ticket 47 measured this hybrid beside layers alone, and it drew the same pixels for a little less.
/// Every other share — dashes, double, the pixel past the gridline, a neighbour's Fill over it — is a
/// background layer over the Fill and under the text, read by one static rule in
/// <c>ex-grid.css</c> (<c>.ex-lined</c>) from the custom properties a part's class sets. Lengths are
/// device pixels (<c>--ex-dp</c>), as Excel's are.</para>
/// </summary>
internal sealed class AppearanceStyles
{
    private readonly Dictionary<FontKey, string> _fonts = [];
    private readonly Dictionary<int, string> _fills = [];
    private readonly Dictionary<PartKey, string> _parts = [];
    private readonly Dictionary<CellKey, string?> _cells = [];
    private readonly Dictionary<(string Base, string Appearance), string> _joined = new(ByReference.Instance);
    private readonly Dictionary<(FontKey Font, int Fill), string?> _editors = [];
    private readonly HashSet<string> _editorRules = [];
    private readonly StringBuilder _rules = new();
    private string _css = "";

    /// <summary>Moves whenever a rule is added; the grid repaints its stylesheet when it does, and
    /// at no other time.</summary>
    public int Version { get; private set; }

    /// <summary>Every rule asked for so far, as the grid's generated <c>&lt;style&gt;</c> holds them.</summary>
    public string Css
    {
        get
        {
            if (_css.Length != _rules.Length)
                _css = _rules.ToString();
            return _css;
        }
    }

    /// <summary>
    /// The class attribute's appearance half for one cell, interned: its Font, its Fill, its four
    /// shares, and the Fill over each gridline it holds that is not its own — <paramref name="rightCover"/>
    /// and <paramref name="bottomCover"/>, beneath any line there — or null when it paints none of
    /// them. The base half — kind, alignment, state — is the row's to compose, and <see cref="Join"/>
    /// puts the two together.
    /// </summary>
    public string? ClassFor(in CellAppearance own, Share top, Share right, Share bottom, Share left, RgbColour? rightCover, RgbColour? bottomCover)
    {
        var font = new FontKey(own.FontColour?.Rgb ?? -1, own.Bold, own.Italic, own.Underline, own.Strikethrough);
        var key = new CellKey(font, own.Fill?.Rgb ?? -1, top, right, bottom, left, rightCover?.Rgb ?? -1, bottomCover?.Rgb ?? -1);
        if (_cells.TryGetValue(key, out var cached))
            return cached;

        var parts = new List<string>(9);
        if (font.Rgb >= 0 || font.Bold || font.Italic || font.Underline || font.Strikethrough)
            parts.Add(FontClass(font));
        if (key.Fill >= 0)
            parts.Add(FillClass(key.Fill));
        if (!top.IsNone || !right.IsNone || !bottom.IsNone || !left.IsNone || rightCover is not null || bottomCover is not null)
        {
            parts.Add("ex-lined");
            AddPart(parts, 't', top);
            AddPart(parts, 'r', right);
            AddPart(parts, 'b', bottom);
            AddPart(parts, 'l', left);
            if (rightCover is { } coverRight)
                AddPart(parts, 'r', Share.Cover(coverRight));
            if (bottomCover is { } coverBottom)
                AddPart(parts, 'b', Share.Cover(coverBottom));
        }

        var composed = parts.Count == 0 ? null : string.Join(' ', parts);
        _cells[key] = composed;
        return composed;
    }

    /// <summary>
    /// The Cell Editor's appearance half over a cell that looks like <paramref name="own"/>
    /// (ADR-0050, item 15), interned: the cell's own Font and Fill classes, or null for neither. The
    /// first time an editor names one, the class gains a rule for the editor's surfaces — its field,
    /// the box a Chrome's control stands in, and the coloured text beneath them (ADR-0057) — through
    /// the editor's own tokens: the Fill is <c>--ex-editor-background</c>, the Font's colour
    /// <c>--ex-editor-color</c>. A field that turns see-through over the coloured text therefore
    /// stays so, and the text is read on the Fill. Paint only: the box and its padding are the
    /// cell's, as before. The Borders stay the cell's.
    /// </summary>
    public string? EditorClassFor(in CellAppearance own)
    {
        var font = new FontKey(own.FontColour?.Rgb ?? -1, own.Bold, own.Italic, own.Underline, own.Strikethrough);
        var key = (font, own.Fill?.Rgb ?? -1);
        if (_editors.TryGetValue(key, out var cached))
            return cached;

        string? fontClass = null;
        if (font.Rgb >= 0 || font.Bold || font.Italic || font.Underline || font.Strikethrough)
        {
            fontClass = FontClass(font);
            if (_editorRules.Add(fontClass))
            {
                // The field and the coloured text take the Font as the cell's text does. A Chrome's
                // control and the coloured text it places stand in the box, and inherit its colour
                // and weight; a decoration reaches neither a control nor an absolutely placed layer,
                // so they are named too. No child combinator: the text is a <style>'s, which a
                // prerender would write with the '>' escaped.
                _rules.Append(".ex-viewport .ex-editor.").Append(fontClass)
                    .Append(",.ex-viewport .ex-editor.").Append(fontClass).Append(" :is(input,textarea,.ex-reference-text)")
                    .Append(",.ex-viewport .ex-reference-text-cell.").Append(fontClass).Append('{');
                AppendFont(font, "--ex-editor-color");
                _rules.Append("}\n");
                Version++;
            }
        }
        string? fillClass = null;
        if (key.Item2 >= 0)
        {
            fillClass = FillClass(key.Item2);
            if (_editorRules.Add(fillClass))
            {
                _rules.Append(".ex-viewport .ex-editor.").Append(fillClass)
                    .Append(",.ex-viewport .ex-reference-text-cell.").Append(fillClass)
                    .Append("{--ex-editor-background:#").Append(Hex(key.Item2)).Append("}\n");
                Version++;
            }
        }

        var composed = fontClass is null ? fillClass : fillClass is null ? fontClass : string.Concat(fontClass, " ", fillClass);
        _editors[key] = composed;
        return composed;
    }

    /// <summary>The whole class attribute of a cell: its base classes and its appearance's,
    /// interned per pair (P5). Both halves are interned strings, so the pair is compared by
    /// reference.</summary>
    public string Join(string baseClass, string appearanceClass)
    {
        if (_joined.TryGetValue((baseClass, appearanceClass), out var joined))
            return joined;
        joined = string.Concat(baseClass, " ", appearanceClass);
        _joined[(baseClass, appearanceClass)] = joined;
        return joined;
    }

    private void AddPart(List<string> parts, char side, Share share)
    {
        if (share.IsNone)
            return;
        var key = new PartKey(side, share);
        if (!_parts.TryGetValue(key, out var name))
        {
            name = share.Kind == ShareKind.Cover
                ? $"ex-l{side}-cover-{Hex(share.Rgb)}"
                : $"ex-l{side}-{StyleName(share.Style)}-{Hex(share.Rgb)}";
            _parts[key] = name;
            AppendPartRule(name, side, share);
        }
        parts.Add(name);
    }

    private string FontClass(FontKey font)
    {
        if (_fonts.TryGetValue(font, out var name))
            return name;
        var flags = string.Concat(font.Bold ? "b" : "", font.Italic ? "i" : "", font.Underline ? "u" : "", font.Strikethrough ? "s" : "");
        name = $"ex-font-{(font.Rgb >= 0 ? Hex(font.Rgb) : "x")}{flags}";
        _fonts[font] = name;

        // A Stale or Error Cell State keeps its own look: the state is never the one that
        // disappears (ADR-0006). Over a tone the Font wins, being the cell's own.
        _rules.Append(".ex-cell:not(.ex-state-stale, .ex-state-error).").Append(name).Append('{');
        AppendFont(font, "color");
        _rules.Append("}\n");
        Version++;
        return name;
    }

    /// <summary>A Font's declarations, its colour as <paramref name="colour"/>.</summary>
    private void AppendFont(FontKey font, string colour)
    {
        if (font.Rgb >= 0)
            _rules.Append(colour).Append(":#").Append(Hex(font.Rgb)).Append(';');
        if (font.Bold)
            _rules.Append("font-weight:700;");
        if (font.Italic)
            _rules.Append("font-style:italic;");
        if (font.Underline || font.Strikethrough)
        {
            _rules.Append("text-decoration-line:")
                .Append(font.Underline && font.Strikethrough ? "underline line-through" : font.Underline ? "underline" : "line-through")
                .Append(';');
        }
    }

    private string FillClass(int rgb)
    {
        if (_fills.TryGetValue(rgb, out var name))
            return name;
        name = $"ex-fill-{Hex(rgb)}";
        _fills[rgb] = name;
        // The Fill covers the gridlines at its edges (ADR-0071; the eleventh run, cases 4–6): its own
        // colour covers the row's rule beneath it, a Pinned Column's cell paints that rule no more,
        // and the column rule on its right edge goes. The gridlines its neighbours paint are
        // covered by their shares (Share.Cover), and so are its own where the cell below (right)
        // has a Fill of its own (case 16 of the fourteenth). --ex-fill-color is what a double
        // line's middle pixel shows on a gridline no neighbour's Fill covers.
        _rules.Append(".ex-cell.").Append(name).Append("{background-color:#").Append(Hex(rgb))
            .Append(";--ex-fill-color:#").Append(Hex(rgb))
            .Append(";--ex-column-rule-color:transparent;--ex-row-rule:none}\n");
        Version++;
        return name;
    }

    private void AppendPartRule(string name, char side, Share share)
    {
        // t and l are the shares a line reaches into the lower (right) cell with; b and r the
        // gridline's pixel and what lies above (left of) it.
        var horizontal = side is 't' or 'b';
        var fromFar = side is 'b' or 'r';
        var toward = horizontal ? (fromFar ? "top" : "bottom") : (fromFar ? "left" : "right");
        var along = horizontal ? "right" : "bottom";
        var colour = "#" + Hex(share.Rgb);
        var property = "--ex-line-" + side;

        if (share.Kind == ShareKind.Cover)
        {
            // A neighbour's Fill over the gridline this cell holds: a layer of its own, beneath every
            // line, so a line along the other edge keeps its corner pixel (lines lie above Fills),
            // and the gaps of a dashed line on this gridline show it. Its colour is named as well,
            // for a double line's middle pixel.
            _rules.Append('.').Append(name).Append("{--ex-cover-").Append(side)
                .Append(":linear-gradient(to ").Append(toward).Append(',').Append(colour)
                .Append(" 0 var(--ex-rule-width, 1px),transparent 0);--ex-cover-").Append(side)
                .Append("-color:").Append(colour).Append("}\n");
            Version++;
            return;
        }

        if (fromFar && Dashes(share.Style) is null && share.Style != BorderStyle.Double)
        {
            // A solid line on and above (left of) the gridline is the cell's own border (ticket 47's
            // measurement: it draws case 9's pixels, as the layer does, and costs a little less). A
            // right border gives its width back out of the right padding, so no text moves and the
            // content box the #### decision assumes stays where it was (ADR-0016).
            var width = Device(UpPixels(share.Style));
            _rules.Append(".ex-cell.").Append(name).Append('{');
            if (horizontal)
            {
                _rules.Append("border-bottom:").Append(width).Append(" solid ").Append(colour);
            }
            else
            {
                _rules.Append("border-right:").Append(width).Append(" solid ").Append(colour)
                    .Append(";padding-right:calc(var(--ex-cell-padding-x, 8px) - ").Append(width).Append(')');
            }
            _rules.Append("}\n");
            Version++;
            return;
        }

        string image, size, at;
        if (!fromFar)
        {
            // The pixel past the gridline: thick's and double's only.
            image = $"linear-gradient(to {toward},{colour} 0 var(--ex-dp),transparent 0)";
            size = "100% 100%";
            at = "0 0";
        }
        else if (Dashes(share.Style) is { } rows)
        {
            // Each row of the line its own tile, as high as the row and as long as the cell, so a
            // pattern starts at the cell's edge. Slanted dash-dot's two rows differ; every other
            // pattern is the same on both.
            var images = new List<string>(rows.Length);
            var sizes = new List<string>(rows.Length);
            var ats = new List<string>(rows.Length);
            for (var r = 0; r < rows.Length; r++)
            {
                images.Add(Repeating(along, colour, rows[r].Pattern));
                var thick = rows[r].Height == 1 ? "var(--ex-dp)" : $"calc({rows[r].Height} * var(--ex-dp))";
                sizes.Add(horizontal ? $"100% {thick}" : $"{thick} 100%");
                var offset = rows[r].Offset == 0 ? "0px" : "var(--ex-dp)";
                ats.Add(horizontal ? $"left 0 bottom {offset}" : $"right {offset} top 0");
            }
            image = string.Join(',', images);
            size = string.Join(',', sizes);
            at = string.Join(',', ats);
        }
        else
        {
            // Double: a line above (left of) the gridline, whose own pixel shows what the gridline
            // beneath it would: the lower (right) cell's Fill, else this cell's, else the ground (the
            // fourteenth Windows run, case 17; the eleventh, case 9). Named, not left transparent:
            // beneath a gridline no Fill covers lies the grid's rule, which Excel does not show there.
            var under = $"var(--ex-cover-{side}-color,var(--ex-fill-color,var(--ex-background, Canvas)))";
            image = $"linear-gradient(to {toward},{under} 0 var(--ex-dp),{colour} 0 calc(2 * var(--ex-dp)),transparent 0)";
            size = "100% 100%";
            at = "0 0";
        }

        _rules.Append('.').Append(name).Append('{')
            .Append(property).Append(':').Append(image).Append(';')
            .Append(property).Append("-size:").Append(size).Append(';')
            .Append(property).Append("-at:").Append(at).Append("}\n");
        Version++;
    }

    /// <summary>How many device pixels a line takes on its gridline and above it (left of it):
    /// one for the 1-px styles, two for medium, thick, double and the medium dashes (case 9).</summary>
    internal static int UpPixels(BorderStyle style) => style switch
    {
        BorderStyle.Medium or BorderStyle.Thick or BorderStyle.Double or BorderStyle.MediumDashed
            or BorderStyle.MediumDashDot or BorderStyle.MediumDashDotDot or BorderStyle.SlantedDashDot => 2,
        _ => 1,
    };

    /// <summary>Whether a line reaches one pixel past its gridline into the lower (right) cell:
    /// thick and double (case 9).</summary>
    internal static bool ReachesPast(BorderStyle style) => style is BorderStyle.Thick or BorderStyle.Double;

    /// <summary>Excel's long dash: 9 device pixels at every scale, as every other length of a pattern
    /// is the same number of device pixels at every scale (the fourteenth Windows run, case 18).</summary>
    private const int Dash = 9;

    private readonly record struct DashRow(int Height, int Offset, int[] Pattern);

    /// <summary>The dash rows of a patterned style, nearest the gridline last, or null for a solid
    /// one. Each pattern alternates on and off, starting on.</summary>
    private static DashRow[]? Dashes(BorderStyle style) => style switch
    {
        BorderStyle.Hair => [new(1, 0, [1, 1])],
        BorderStyle.Dotted => [new(1, 0, [2, 2])],
        BorderStyle.Dashed => [new(1, 0, [3, 1])],
        BorderStyle.DashDot => [new(1, 0, [Dash, 3, 3, 3])],
        BorderStyle.DashDotDot => [new(1, 0, [Dash, 3, 3, 3, 3, 3])],
        BorderStyle.MediumDashed => [new(2, 0, [Dash, 3])],
        BorderStyle.MediumDashDot => [new(2, 0, [Dash, 3, 3, 3])],
        BorderStyle.MediumDashDotDot => [new(2, 0, [Dash, 3, 3, 3, 3, 3])],
        // Excel offsets the two rows of a slanted dash-dot: 11 on, 1 off, 5 on, 1 off above the
        // gridline, and on the gridline the same period shifted a pixel and narrower (case 9, at
        // both zooms).
        BorderStyle.SlantedDashDot => [new(1, 1, [11, 1, 5, 1]), new(1, 0, [9, 2, 4, 2, 1, 0])],
        _ => null,
    };

    private static string Repeating(string along, string colour, int[] pattern)
    {
        var css = new StringBuilder("repeating-linear-gradient(to ").Append(along);
        var at = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var end = at + pattern[i];
            css.Append(',').Append(i % 2 == 0 ? colour : "transparent").Append(' ').Append(Length(at)).Append(' ').Append(Length(end));
            at = end;
        }
        return css.Append(')').ToString();

        // A stop is one calc() of device pixels from the tile's start, so it never rounds on the way.
        static string Length(int pixels) => pixels == 0 ? "0px" : Device(pixels);
    }

    private static string Device(int pixels) => pixels == 1 ? "var(--ex-dp)" : $"calc({pixels} * var(--ex-dp))";

    private static string StyleName(BorderStyle style) => style switch
    {
        BorderStyle.Hair => "hair",
        BorderStyle.Thin => "thin",
        BorderStyle.Medium => "medium",
        BorderStyle.Thick => "thick",
        BorderStyle.Double => "double",
        BorderStyle.Dotted => "dotted",
        BorderStyle.Dashed => "dashed",
        BorderStyle.DashDot => "dashdot",
        BorderStyle.DashDotDot => "dashdotdot",
        BorderStyle.MediumDashed => "mediumdashed",
        BorderStyle.MediumDashDot => "mediumdashdot",
        BorderStyle.MediumDashDotDot => "mediumdashdotdot",
        BorderStyle.SlantedDashDot => "slanteddashdot",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };

    private static string Hex(int rgb) => rgb.ToString("x6", CultureInfo.InvariantCulture);

    private readonly record struct FontKey(int Rgb, bool Bold, bool Italic, bool Underline, bool Strikethrough);

    private readonly record struct PartKey(char Side, Share Share);

    private readonly record struct CellKey(FontKey Font, int Fill, Share Top, Share Right, Share Bottom, Share Left, int RightCover, int BottomCover);

    /// <summary>Compares the interned halves of a class attribute by reference.</summary>
    private sealed class ByReference : IEqualityComparer<(string Base, string Appearance)>
    {
        public static readonly ByReference Instance = new();

        public bool Equals((string Base, string Appearance) x, (string Base, string Appearance) y)
            => ReferenceEquals(x.Base, y.Base) && ReferenceEquals(x.Appearance, y.Appearance);

        public int GetHashCode((string Base, string Appearance) obj)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Base), RuntimeHelpers.GetHashCode(obj.Appearance));
    }
}

/// <summary>What a share of one edge paints in a cell: nothing, the cell's share of a line, or the
/// neighbour's Fill over the gridline this cell holds.</summary>
internal enum ShareKind : byte
{
    None = 0,
    Line,
    Cover,
}

/// <summary>
/// One cell's share of one edge, resolved from both cells' records (ADR-0050, item 15). A bottom or
/// right share is the gridline's pixel and what lies above (left of) it; a top or left share is the
/// pixel a thick or double line reaches past the gridline into this cell.
/// </summary>
internal readonly record struct Share(ShareKind Kind, BorderStyle Style, int Rgb)
{
    public static Share None => default;

    public bool IsNone => Kind == ShareKind.None;

    public static Share Line(Border line) => line.IsNone ? default : new(ShareKind.Line, line.Style, line.Colour.Rgb);

    public static Share Cover(RgbColour fill) => new(ShareKind.Cover, BorderStyle.None, fill.Rgb);
}
