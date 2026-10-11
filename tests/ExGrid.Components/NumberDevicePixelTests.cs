using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A cell that shows a number may use one Device Pixel of its right padding (ADR-0090, decided
/// 2026-10-11; VZ-19). Its column's edges lie on Device Pixels, so it is painted up to one narrower
/// than declared, while the <c>####</c> decision reads the declared width: with that pixel its content
/// box is never narrower than the declared content. Text is not given it. Read from the inline
/// geometry — each cell's painted width, and the root's padding and Device Pixel — through the cascade
/// of the shipped stylesheet and of the grid's generated rules, worked out in CSS pixels.
/// </summary>
public class NumberDevicePixelTests : GridTestContext
{
    // Compact's padding, which the root writes inline as --ex-cell-padding-x.
    private static readonly double Padding = ExGrid<TestRow>.DefaultCellMetrics.CellHorizontalPaddingPx;

    // SH-41's column: after a lead column, eight of these put their edges at eight offsets from the
    // Device Pixels, at every ratio below, and some of them round narrower than declared.
    private const double Declared = 49.87;
    private const double Lead = 41.3;

    private static ColumnWidthSpec Fixed(double px) => new(ColumnWidth.Fixed(px));

    [Theory] // ADR-0090 (2026-10-11) / VZ-19: a number's content box is never narrower than its declared content, however its column's edges round, at every scale and zoom; a text's is the painted width less its padding, as before
    [InlineData(0.5)]
    [InlineData(0.67)]
    [InlineData(0.8)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.25)]
    [InlineData(3.0)]
    public async Task A_numbers_content_box_is_never_narrower_than_its_declared_content(double ratio)
    {
        GridColumn<TestRow>[] columns =
        [
            new("Lead", ColumnType.Text, r => r.Book, width: Fixed(Lead)),
            .. Enumerable.Range(0, 8).Select(i => new GridColumn<TestRow>($"N{i}", ColumnType.Number, r => r.Amount, width: Fixed(Declared))),
            .. Enumerable.Range(0, 8).Select(i => new GridColumn<TestRow>($"T{i}", ColumnType.Text, r => r.Book, width: Fixed(Declared))),
        ];
        var cut = RenderGrid(columns);

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(ratio));

        var rules = Rules(cut);
        var cells = FirstRow(cut);
        var numbers = Enumerable.Range(1, 8).Select(c => Box(cut, cells[c], rules)).ToList();
        foreach (var number in numbers)
        {
            Assert.Equal(Padding - (1 / ratio), number.Right, 9);
            Assert.True(number.Content >= Declared - (2 * Padding) - 1e-9,
                $"painted {number.Painted}px leaves {number.Content}px of content, under the declared {Declared - (2 * Padding)}px");
        }
        // The case is reached: some are painted narrower than declared, where the pixel is needed.
        Assert.Contains(numbers, number => number.Painted < Declared);
        foreach (var text in Enumerable.Range(9, 8).Select(c => Box(cut, cells[c], rules)))
        {
            Assert.Equal(Padding, text.Right, 9);
            Assert.Equal(text.Painted - (2 * Padding), text.Content, 9);
        }
    }

    [Fact] // ADR-0090 (2026-10-11) / VZ-19: untold, the stylesheet's Device Pixel stands, and a number is given it over its declared width
    public void Untold_a_number_is_given_the_stylesheets_device_pixel()
    {
        var cut = RenderGrid([
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed(Declared)),
            new("Book", ColumnType.Text, r => r.Book, width: Fixed(Declared)),
        ]);

        var rules = Rules(cut);
        var cells = FirstRow(cut);
        Assert.Equal(Declared - (2 * Padding) + 1, Box(cut, cells[0], rules).Content, 9);
        Assert.Equal(Declared - (2 * Padding), Box(cut, cells[1], rules).Content, 9);
    }

    [Fact] // ADR-0090 (2026-10-11) / VZ-19: the pixel goes with the numeric presentation — a number aligned left, a date, a pinned number and a group or total row's number take it; text aligned right, a Boolean and every header do not
    public async Task The_pixel_goes_with_the_numeric_presentation_alone()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount),
            new("Book", ColumnType.Text, r => r.Book, align: CellAlign.Right),
            new("AsOf", ColumnType.Date, r => r.AsOf),
            new("Active", ColumnType.Boolean, r => r.Active),
            new("Left", ColumnType.Number, r => r.Amount, align: CellAlign.Left),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(3))
            .Add(g => g.Columns, columns)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.RowKind, (Func<TestRow, RowKind>)(row => row.Amount switch
            {
                1 => RowKind.Group,
                2 => RowKind.Total,
                _ => RowKind.Detail,
            }))
            .Add(g => g.ViewportWidth, 1000));

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(2));

        var rules = Rules(cut);
        var rows = cut.FindAll(".ex-viewport [role=row]");
        Assert.Contains("ex-row-group", rows[1].ClassList);
        Assert.Contains("ex-row-total", rows[2].ClassList);
        double[] given = [Padding - 0.5, Padding, Padding - 0.5, Padding, Padding - 0.5];
        foreach (var row in rows)
        {
            Assert.Equal(given, row.QuerySelectorAll(".ex-cell").Select(cell => Box(cut, cell, rules).Right));
        }
        Assert.All(cut.FindAll(".ex-header-cell"), header => Assert.True(Box(cut, header, rules).Right >= Padding));
    }

    [Fact] // ADR-0090 (2026-10-11) / VZ-19 / ADR-0071: a right line gives its width back out of the padding, and a number keeps its Device Pixel out of what is left; text under the same line is given none
    public async Task A_number_with_a_right_line_keeps_its_device_pixel()
    {
        var line = new CellAppearance { Right = new Border(BorderStyle.Thick, RgbColour.FromRgb(0xFF0000)) };
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(3))
            .Add(g => g.Columns, [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed(120)),
                new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed(Declared)),
            ])
            .Add(g => g.CellAppearance, (CellAppearanceOf<TestRow>)((_, _) => line)));

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(1.5));

        var rules = Rules(cut);
        var cells = FirstRow(cut);
        var (text, number) = (Box(cut, cells[0], rules), Box(cut, cells[1], rules));
        // Thick is two Device Pixels on and left of the gridline: the cell's own right border.
        Assert.Equal(2 / 1.5, number.Border, 9);
        Assert.Equal(2 / 1.5, text.Border, 9);
        Assert.Equal(number.Painted - (2 * Padding) + (1 / 1.5), number.Content, 9);
        Assert.Equal(text.Painted - (2 * Padding), text.Content, 9);
    }

    [Theory] // ADR-0090 (2026-10-11) / VZ-19: no #### decision moves — a number a hundredth over its estimate is shown, and a hundredth under it is ####, at every ratio, judged at the declared width
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.25)]
    public async Task No_hash_decision_moves(double ratio)
    {
        var row = new TestRow { Book = "Alpha", Amount = 1234.5m };
        var amount = new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount);
        var shown = amount.Info.TextOf(row);
        var edge = ExGrid<TestRow>.DefaultCellMetrics.EstimatePx(shown);
        // Over and under in turn, so each falls at four offsets from the Device Pixels.
        GridColumn<TestRow>[] columns =
        [
            new("Lead", ColumnType.Text, r => r.Book, width: Fixed(Lead)),
            .. Enumerable.Range(0, 8).Select(i => new GridColumn<TestRow>(
                $"C{i}", ColumnType.Number, r => r.Amount, width: Fixed(i % 2 == 0 ? edge + 0.01 : edge - 0.01))),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, [row])
            .Add(g => g.Columns, columns)
            .Add(g => g.ViewportWidth, 1000));

        await cut.InvokeAsync(() => cut.Instance.OnDevicePixelAsync(ratio));

        var cells = FirstRow(cut);
        for (var c = 1; c <= 8; c++)
        {
            if (c % 2 == 1)
            {
                Assert.Equal(shown, cells[c].TextContent);
                Assert.Empty(cells[c].QuerySelectorAll(".ex-hashes"));
            }
            else
            {
                Assert.Single(cells[c].QuerySelectorAll(".ex-hashes"));
                Assert.Equal(shown, cells[c].GetAttribute("aria-label"));
            }
        }
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(GridColumn<TestRow>[] columns)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(3))
            .Add(g => g.Columns, columns)
            .Add(g => g.ViewportWidth, 1000));

    private static IHtmlCollection<IElement> FirstRow(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-viewport [role=row]")[0].QuerySelectorAll(".ex-cell");

    /// <summary>A cell's box as laid out: its painted width, its left and right padding, its right
    /// border, and the content box the text is laid out in, which border-box sizing leaves.</summary>
    private readonly record struct CellBox(double Painted, double Left, double Right, double Border)
    {
        public double Content => Painted - Left - Right - Border;
    }

    private static CellBox Box(IRenderedComponent<ExGrid<TestRow>> cut, IElement cell, IReadOnlyList<(string[] Selectors, string Body)> rules)
    {
        var root = cut.Find(".ex-grid");
        double Px(string value) => Calc(Substitute(value, name => Custom(name, cell, root, rules)));
        var painted = double.Parse(
            Regex.Match(cell.GetAttribute("style")!, @"width: (?<px>[-\d.E+]+)px").Groups["px"].Value, CultureInfo.InvariantCulture);
        return new CellBox(
            painted,
            Px(Side(ShippedStylesheetTests.Winning(cell, rules, "padding", "padding-left"), left: true)),
            Px(Side(ShippedStylesheetTests.Winning(cell, rules, "padding", "padding-right"), left: false)),
            ShippedStylesheetTests.Winning(cell, rules, "border-right") is { } border ? Px(Split(border.Value, ' ')[0]) : 0);
    }

    /// <summary>The shipped stylesheet's unconditional rules, then the grid's generated ones, which the
    /// cascade meets after them: the grid's own &lt;style&gt; stands in the page after the package's.</summary>
    private static List<(string[] Selectors, string Body)> Rules(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var rules = ShippedStylesheetTests.UnconditionalRules().ToList();
        foreach (var style in cut.FindAll(".ex-grid > style"))
        {
            rules.AddRange(Regex.Matches(style.TextContent, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")
                .Select(rule => (Split(rule.Groups["selectors"].Value, ','), rule.Groups["body"].Value)));
        }
        return rules;
    }

    /// <summary>A custom property as the cell inherits it: from its own rules, else the root's inline
    /// Geometry Tokens, else the stylesheet's default on the root.</summary>
    private static string? Custom(string name, IElement cell, IElement root, IReadOnlyList<(string[] Selectors, string Body)> rules)
        => ShippedStylesheetTests.Winning(cell, rules, name)?.Value
           ?? Regex.Match(root.GetAttribute("style") ?? "", $@"(?:^|;)\s*{Regex.Escape(name)}:\s*(?<value>[^;]+)") switch
           {
               { Success: true } inline => inline.Groups["value"].Value.Trim(),
               _ => ShippedStylesheetTests.Winning(root, rules, name)?.Value,
           };

    /// <summary>One side of a padding: a longhand as it is, or the side a shorthand of one to four
    /// lengths gives it.</summary>
    private static string Side((string Property, string Value)? declared, bool left)
    {
        var (property, value) = declared ?? throw new InvalidOperationException("no padding is declared");
        if (property != "padding")
            return value;
        var lengths = Split(value, ' ');
        return lengths[left ? lengths.Length switch { 1 => 0, 2 or 3 => 1, _ => 3 } : Math.Min(1, lengths.Length - 1)];
    }

    /// <summary>Splits at <paramref name="separator"/> outside parentheses, trimmed, empties dropped.</summary>
    private static string[] Split(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
                depth--;
            else if (depth == 0 && (separator == ' ' ? char.IsWhiteSpace(text[i]) : text[i] == separator))
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }
        parts.Add(text[start..]);
        return [.. parts.Select(part => part.Trim()).Where(part => part.Length > 0)];
    }

    /// <summary>Puts each <c>var()</c> in parentheses as its value resolves, or its fallback.</summary>
    private static string Substitute(string value, Func<string, string?> lookup)
    {
        var start = value.IndexOf("var(", StringComparison.Ordinal);
        if (start < 0)
            return value;
        var end = start + 4;
        var comma = -1;
        for (var depth = 1; ; end++)
        {
            if (value[end] == '(')
                depth++;
            else if (value[end] == ')' && --depth == 0)
                break;
            else if (value[end] == ',' && depth == 1 && comma < 0)
                comma = end;
        }
        var name = value[(start + 4)..(comma < 0 ? end : comma)].Trim();
        var resolved = lookup(name) ?? (comma < 0 ? null : value[(comma + 1)..end].Trim())
            ?? throw new InvalidOperationException($"{name} is not set, and {value} gives no fallback");
        return Substitute($"{value[..start]}({Substitute(resolved, lookup)}){value[(end + 1)..]}", lookup);
    }

    /// <summary>Works out a length in CSS pixels: numbers in px or none, calc() and parentheses, + − × ÷.</summary>
    private static double Calc(string expression)
    {
        var text = expression.Replace("calc(", "(", StringComparison.Ordinal);
        var at = 0;
        var result = Sum();
        Blank();
        Assert.True(at == text.Length, $"not a length: {expression}");
        return result;

        void Blank()
        {
            while (at < text.Length && char.IsWhiteSpace(text[at]))
                at++;
        }

        double Sum()
        {
            var value = Product();
            for (Blank(); at < text.Length && text[at] is '+' or '-'; Blank())
                value = text[at++] == '+' ? value + Product() : value - Product();
            return value;
        }

        double Product()
        {
            var value = Factor();
            for (Blank(); at < text.Length && text[at] is '*' or '/'; Blank())
                value = text[at++] == '*' ? value * Factor() : value / Factor();
            return value;
        }

        double Factor()
        {
            Blank();
            if (text[at] == '(')
            {
                at++;
                var inner = Sum();
                Blank();
                Assert.Equal(')', text[at++]);
                return inner;
            }
            var number = Regex.Match(text[at..], @"^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?(px)?");
            Assert.True(number.Success, $"not a length at {at}: {expression}");
            at += number.Length;
            return double.Parse(number.Value.Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
        }
    }
}
