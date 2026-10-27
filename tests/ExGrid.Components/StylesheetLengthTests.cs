using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// No pixel is only a stylesheet value (ADR-0027/0028; UX-1). Every length literal the core
/// stylesheet writes is one of three things, and this test says which:
/// <list type="bullet">
/// <item>the fallback of a Geometry Token C# emits inline on the instance root — and then it is the
/// number C# emits under the core default, Compact, so a fallback is never a second opinion;</item>
/// <item>the default of a Visual Token, named in <see cref="VisualTokenDefaults"/> with its reason;</item>
/// <item>a bare literal, named by its rule, property and value in <see cref="BareLengths"/> with its
/// reason — something painted that no arithmetic reads.</item>
/// </list>
/// Comments are not CSS and are not read. A length is a number with a length unit; zero, a
/// percentage and a resolution (<c>dppx</c>, in a media query's prelude) are not lengths.
/// </summary>
public class StylesheetLengthTests : GridTestContext
{
    private static readonly Regex Length = new(
        @"(?<![\w.#-])-?(?:\d+\.?\d*|\.\d+)(?:px|em|rem|ex|ch|lh|rlh|vh|vw|vmin|vmax|vi|vb|svh|lvh|dvh|svw|lvw|dvw|pt|pc|cm|mm|in|q)\b",
        RegexOptions.IgnoreCase);

    private const string Outline = "an outline is painted outside layout and moves no box the arithmetic reads (ADR-0028: painted, never laid out)";
    private const string Rule = "a rule's width is a Visual Token: a gradient or a shadow, painted, never laid out (ADR-0028/0029)";
    private const string DevicePixelSteps = "the --ex-dp fallback table, one Device Pixel per resolution step until the browser tells it (ADR-0090)";
    private const string Popover = "spacing inside a popover or menu, which stands outside the row flow and whose box no arithmetic reads (ADR-0018/0034)";
    private const string Strip = "the pager's and status line's padding: a strip outside the scroller, whose height no arithmetic reads (ADR-0015)";
    private const string Em = "a glyph's box in the em of the emitted font size (--ex-font-size), so it follows that Geometry Token";
    private const string Mark = "a painted mark on a pseudo-element or a placeholder bar, positioned over its box and laid out nowhere (ADR-0006/0013)";
    private const string VisuallyHidden = "the live region's visually-hidden clip, present to assistive technology only (ADR-0033)";
    private const string Scrollbar = "the grid's own scrollbar, which the browser lays out and reports as the Scrollbar Gutter (ADR-0013/0021/0029)";
    private const string ReferenceInset = "a Reference outline's painted inset inside a box C# places (ADR-0058)";

    /// <summary>The Visual Tokens whose default is a length, as (token, default) with the reason it
    /// is appearance and not geometry.</summary>
    private static readonly Dictionary<(string Token, string Literal), string> VisualTokenDefaults = new()
    {
        [("--ex-rule-width", "1px")] = Rule,
        [("--ex-rule-dp", "1px")] = Rule,
        [("--ex-editor-outline", "2px")] = Outline,
        [("--ex-grid-focus-outline", "1px")] = Outline,
        [("--ex-grid-focus-outline", "2px")] = Outline,
        [("--ex-size-tip-outline", "1px")] = Outline,
        [("--ex-scrollbar-width", "12px")] = Scrollbar,
        [("--ex-pasting-bar-height", "3px")] = "the bar a paste under way paints over the root's top edge, absolutely placed: a painted mark, never a box the arithmetic reads (ADR-0240 section 12)",
        [("--ex-dp", "1px")] = "the Device Pixel's own default before the browser's resolution is read: a painted line's width and inset, never a box the arithmetic reads (ADR-0090)",
    };

    /// <summary>The bare length literals, each by its rule's selectors, its property and its value,
    /// with the reason it is no geometry. <c>.ex-resize-grip</c>'s width is not here: it comes from
    /// Grid Metrics (UX-1).</summary>
    private static readonly Dictionary<(string Selectors, string Property, string Literal), string> BareLengths = new()
    {
        [(".ex-grid:focus-visible, .ex-grid[data-ex-focus-visible]", "outline-offset", "1px")] = Outline,
        [(".ex-formula-bar > .ex-editor:focus, .ex-formula-bar > .ex-editor:focus-within", "outline-offset", "-2px")] = Outline,
        [(".ex-focus, .ex-range, .ex-point, .ex-action-chosen", "outline-width", "2px")] = Outline,
        [(".ex-focus, .ex-range, .ex-point, .ex-action-chosen", "outline-offset", "-2px")] = Outline,
        [(".ex-fill-target", "outline", "1px")] = Outline,
        [(".ex-fill-target", "outline-offset", "-1px")] = Outline,
        [(".ex-reference-outline", "outline", "2px")] = Outline,
        [(".ex-reference-outline", "outline-offset", "-2px")] = Outline,
        [(".ex-point.ex-point-on-reference", "outline-offset", "-4px")] = Outline,
        [(".ex-point-dashes", "outline", "2px")] = Outline,
        [(".ex-point-dashes", "outline-offset", "-4px")] = Outline,
        [(".ex-message", "outline", "1px")] = Outline,
        [("[data-ex-notice]", "outline", "1px")] = Outline,
        [("[data-ex-notice]", "outline-offset", "-1px")] = Outline,
        [(".ex-size-tip", "outline-offset", "-1px")] = Outline,
        [(".ex-editor", "outline-offset", "-1px")] = Outline,
        [(".ex-grid:not(:focus-within) .ex-viewport .ex-editor", "outline-width", "1px")] = Outline,
        [(".ex-key-field.ex-key-field-composing", "outline-offset", "-1px")] = Outline,
        // The forced-colors block restates states and bands as outlines (ADR-0027).
        [(".ex-cell.ex-changed", "outline", "2px")] = Outline,
        [(".ex-cell.ex-changed", "outline-offset", "-2px")] = Outline,
        [(".ex-cell.ex-match-mark::before", "inset", "3px")] = Mark,
        [(".ex-cell.ex-match-mark::before", "border", "2px")] = Mark,
        [(".ex-header-cell.ex-match-mark::before, .ex-header-group.ex-match-mark::before", "inset", "3px")] = Mark,
        [(".ex-header-cell.ex-match-mark::before, .ex-header-group.ex-match-mark::before", "border", "2px")] = Mark,
        [(".ex-cell.ex-state-missing", "outline", "1px")] = Outline,
        [(".ex-cell.ex-state-missing", "outline-offset", "-2px")] = Outline,
        [(".ex-cell.ex-state-modified", "outline", "1px")] = Outline,
        [(".ex-cell.ex-state-modified", "outline-offset", "-2px")] = Outline,
        [(".ex-focus-row", "outline", "1px")] = Outline,
        [(".ex-focus-row", "outline-offset", "-1px")] = Outline,
        [(".ex-hover-row", "outline", "1px")] = Outline,
        [(".ex-hover-row", "outline-offset", "-1px")] = Outline,

        [(".ex-row-total, .ex-row-total .ex-cell", "--ex-tint", "1px")] = Rule,

        [(".ex-grid", "--ex-dp", "1px")] = DevicePixelSteps,
        [(".ex-grid", "--ex-dp", "0.8px")] = DevicePixelSteps,
        [(".ex-grid", "--ex-dp", "0.5px")] = DevicePixelSteps,
        [(".ex-grid", "--ex-dp", "0.4px")] = DevicePixelSteps,

        [(".ex-popover", "padding", "4px")] = Popover,
        [(".ex-popover", "gap", "4px")] = Popover,
        [(".ex-message", "padding", "4px")] = Popover,
        [(".ex-popover-filter", "gap", "4px")] = Popover,
        [(".ex-popover-filter", "padding-top", "4px")] = Popover,
        [(".ex-popover button", "padding", "2px")] = Popover,
        [(".ex-popover-list", "max-height", "240px")] = Popover,
        [(".ex-popover-list", "min-height", "48px")] = Popover,
        [(".ex-popover-actions", "gap", "4px")] = Popover,
        [(".ex-popover-join", "gap", "12px")] = Popover,
        [(".ex-popover-find-body", "gap", "4px")] = Popover,
        [(".ex-find-outcome", "min-height", "1.2em")] = Popover,
        [(".ex-select-all::before", "width", "1.2em")] = Popover,
        [(".ex-summary-menu", "min-width", "12em")] = Popover,
        [(".ex-summary-menu-item", "padding", "2px")] = Popover,
        [(".ex-summary-menu-tick", "width", "1.4em")] = Popover,

        [(".ex-pager, .ex-status", "padding", "4px")] = Strip,
        [(".ex-summary", "min-height", "1lh")] = Strip,

        [(".ex-mark", "width", "1em")] = Em,
        [(".ex-mark", "height", "1em")] = Em,

        [(".ex-header-group-toggle::before", "width", "1em")] = Em,
        [(".ex-header-group-toggle::before", "height", "1em")] = Em,
        [(".ex-hidden-mark", "width", "6px")] = Mark,
        [(".ex-hidden-mark", "height", "6px")] = Mark,
        [(".ex-cell.ex-state-modified::after", "width", "6px")] = Mark,
        [(".ex-cell.ex-state-modified::after", "height", "6px")] = Mark,
        [(".ex-placeholder::after", "border-radius", "2px")] = Mark,

        [(".ex-announce", "width", "1px")] = VisuallyHidden,
        [(".ex-announce", "height", "1px")] = VisuallyHidden,
        [(".ex-announce", "margin", "-1px")] = VisuallyHidden,

        [(".ex-scroller::-webkit-scrollbar-thumb", "border", "2px")] = Scrollbar,

        [(".ex-reference-outline", "padding", "3px")] = ReferenceInset,
    };

    /// <summary>The Geometry Tokens only the Script Core emits, by Compact's value there, for what the C# core
    /// never draws: this test reads the C# core's root, which does not write them.</summary>
    private static readonly Dictionary<string, string> ScriptCoreGeometryTokens = new()
    {
        // A Header Group's toggle (ADR-0400 section 10): GridMetrics.headerGroupToggleWidthPx under Compact.
        ["--ex-header-group-toggle-width"] = "20px",
    };

    private sealed record Found(string Selectors, string Property, string Literal, string? FallbackOf)
    {
        public override string ToString()
            => $"{Selectors} {{ {Property}: … {Literal} … }}" + (FallbackOf is null ? "" : $" (fallback of {FallbackOf})");
    }

    /// <summary>Every non-zero length in the core stylesheet's declarations, with the token whose
    /// fallback it stands in, when it stands in one.</summary>
    private static List<Found> Lengths(string css)
    {
        var found = new List<Found>();
        foreach (Match rule in Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}"))
        {
            var selectors = Regex.Replace(rule.Groups["selectors"].Value, @"\s+", " ").Trim();
            foreach (var (property, value) in ShippedStylesheetTests.Declarations(rule.Groups["body"].Value))
            {
                var flat = Regex.Replace(value, @"\s+", " ");
                foreach (Match length in Length.Matches(flat))
                {
                    if (double.Parse(Regex.Match(length.Value, @"^-?[\d.]+").Value, System.Globalization.CultureInfo.InvariantCulture) == 0)
                        continue;
                    found.Add(new Found(selectors, property, length.Value, FallbackOf(flat, length.Index)));
                }
            }
        }
        return found;
    }

    /// <summary>The token of the innermost <c>var()</c> whose fallback holds the position, or null.</summary>
    private static string? FallbackOf(string value, int position)
    {
        string? innermost = null;
        foreach (Match read in Regex.Matches(value, @"var\(\s*(?<token>--[\w-]+)\s*,"))
        {
            var depth = 1;
            var end = read.Index + read.Length;
            for (; end < value.Length && depth > 0; end++)
            {
                if (value[end] == '(')
                    depth++;
                else if (value[end] == ')')
                    depth--;
            }
            if (position >= read.Index + read.Length && position < end)
                innermost = read.Groups["token"].Value;
        }
        return innermost;
    }

    /// <summary>The Geometry Tokens an untouched grid writes inline on its root, Compact's.</summary>
    private Dictionary<string, string> EmittedGeometryTokens()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(5))
            .Add(g => g.Columns, TestRows.Wide(3))
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ViewportHeight, 200));
        return Regex.Matches(cut.Find(".ex-grid").GetAttribute("style")!, @"(?<token>--ex-[\w-]+)\s*:\s*(?<value>[^;]+)")
            .ToDictionary(token => token.Groups["token"].Value, token => token.Groups["value"].Value.Trim());
    }

    [Fact] // ADR-0027/0028 / UX-1: no pixel is only a stylesheet value — every length in ex-grid.css is a Geometry Token's own fallback, a Visual Token's default, or a named painted value
    public void UX1_every_length_in_the_core_stylesheet_is_accounted_for()
    {
        var (css, _) = ShippedStylesheetTests.CoreStylesheet();
        var lengths = Lengths(css);
        var emitted = EmittedGeometryTokens();
        foreach (var (token, value) in ScriptCoreGeometryTokens)
            Assert.True(emitted.TryAdd(token, value), $"{token} is emitted by the C# core too: it is no longer the Script Core's alone");

        // Nothing escapes the reading: a length outside a declaration — in a selector, or an
        // at-rule's prelude — would be counted here and not above.
        Assert.Equal(
            Length.Matches(Regex.Replace(css, @"\s+", " ")).Count(m => !Regex.IsMatch(m.Value, @"^-?[0.]+[a-z]+$", RegexOptions.IgnoreCase)),
            lengths.Count);

        var offenders = new List<string>();
        foreach (var length in lengths)
        {
            if (length.FallbackOf is { } token && emitted.TryGetValue(token, out var value))
            {
                // A Geometry Token's fallback stands only where the root's inline value does not
                // reach; it may only repeat that value, never hold a number of its own.
                if (value != length.Literal)
                    offenders.Add($"{length}: the root emits {token}: {value}");
            }
            else if (length.FallbackOf is { } visual)
            {
                if (!VisualTokenDefaults.ContainsKey((visual, length.Literal)))
                    offenders.Add($"{length}: a length default of a token neither emitted by C# nor a listed Visual Token");
            }
            else if (!BareLengths.ContainsKey((length.Selectors, length.Property, length.Literal)))
            {
                offenders.Add($"{length}: a bare length — geometry comes from Grid Metrics (ADR-0027/0028)");
            }
        }
        Assert.Empty(offenders);

        // An allowance nothing uses any more is removed, so the list stays what the sheet holds.
        var unusedBare = BareLengths.Keys.Where(key => !lengths.Any(l => l.FallbackOf is null && (l.Selectors, l.Property, l.Literal) == key)).ToList();
        var unusedDefaults = VisualTokenDefaults.Keys.Where(key => !lengths.Any(l => (l.FallbackOf, l.Literal) == key)).ToList();
        Assert.Empty(unusedBare);
        Assert.Empty(unusedDefaults);
    }

    [Fact] // ADR-0027 / UX-1: the check above refuses the grip's old bare width, and a geometry fallback that differs from what C# emits
    public void UX1_a_bare_grip_width_or_a_second_opinion_in_a_fallback_is_refused()
    {
        var emitted = EmittedGeometryTokens();
        Assert.Equal("5px", emitted["--ex-resize-grip-width"]);
        Assert.Equal("2px", emitted["--ex-column-guide-width"]);

        var bare = Assert.Single(Lengths(".ex-resize-grip { position: absolute; width: 5px; }"));
        Assert.Null(bare.FallbackOf);
        Assert.False(BareLengths.ContainsKey((bare.Selectors, bare.Property, bare.Literal)));

        var fallback = Assert.Single(Lengths(".ex-resize-grip { width: var(--ex-resize-grip-width, 6px); }"));
        Assert.Equal("--ex-resize-grip-width", fallback.FallbackOf);
        Assert.NotEqual(emitted[fallback.FallbackOf!], fallback.Literal);

        // A length in a nested function inside the fallback is still that token's fallback.
        Assert.Equal("--ex-outline-width",
            Assert.Single(Lengths(".x { top: calc(var(--ex-outline-width, calc(2px + 0px)) * 2); }")).FallbackOf);
    }
}
