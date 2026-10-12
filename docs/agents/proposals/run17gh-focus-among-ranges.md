# Run 17 G-H, the Focus among several ranges: what the orchestrator writes

Branch `claude/run17gh-focus-among-ranges`, from `main` at `a533de01`. Decision (1) of 2026-10-12, decided with the
user: in a Selection of several ranges ExGrid as a whole follows Excel, with no range outline and no fill handle, and
the Focus cell takes a thin border of one Device Pixel in a new token (`--ex-focus-among-ranges-outline`, default
`CanvasText`; ExSheet's Paper sets it black); a single range keeps today's look. It follows the seventeenth Windows
run's h2 (`verification/2026-10-11-windows-17/report.md` on `claude/exsheet-windows-verify-17`, Part H;
`part-h/shots/h-h2-2-ctrl-d4-e6-cells.png` for Excel, `h-h2-2-ctrl-d4-e6-chrome-sheet.png` for ExSheet).

Nothing here changes an ADR, `CONTEXT.md`, the Definition of Done or the Docs Site: the text for each is below, as
exact old -> new edits.

## Stop: the run's own picture reads the border green, not black

**For the user.** h2's report says "a thin **black** border". Its picture says otherwise. Read in device pixels
(the display at 150%, Excel at 100% zoom: a standard column is 96 pixels wide), across D4 at y = 160 and down it at
x = 370:

| Side | Pixels, from outside the cell inward |
|---|---|
| left | `#AFAFAF` (the gridline, shaded), `#FFFFFF`, **`#217346`**, then `#FFFFFF` |
| top | `#AFAFAF`, `#FFFFFF`, **`#217346`**, then `#FFFFFF` |
| right | `#AFAFAF` (D4's own gridline), `#FFFFFF`, **`#217346`**, then `#FFFFFF` |
| bottom | `#AFAFAF`, `#FFFFFF`, **`#217346`**, then `#FFFFFF` |

So at that scale the border is:

- **one device pixel wide**, as decided;
- **`#217346`**, Excel's green: the colour of the single range's outline in h1 and in h2's first picture
  (`h-h2-1-a1-b2-cells.png`, two device pixels of `#217346`). A one-pixel dark green line reads as black by eye;
- **inside the cell**, one device pixel in from each gridline, with the cell's own ground between.

What this branch did with it:

- **The colour is the decision's: black on the Paper.** `ex-sheet.css` falls back to `#000`, and nothing written
  calls it Excel's. If the user takes the picture's colour, these are the changes:
  - `src/ExSheet/Assets/ex-sheet.css`: `var(--ex-focus-among-ranges-outline, #000)` becomes
    `var(--ex-focus-among-ranges-outline, var(--ex-focus-outline, #217346))`, then the minify;
  - `tests/ExSheet.MudBlazor.Tests/PaperStylesheetTests.cs` and the Paper's layer-3 test (`BLACK`) follow;
  - the ExSheet Change Note says green instead of black.

  ExGrid's own default, `CanvasText`, is unaffected: it is the decision's, and the Focus outline's default too.
- **The place is the picture's**, which the decision did not state. The border lies inside the cell, one Device
  Pixel in from its top and left edges and from the gridline the cell holds on its right and bottom.
  - Inside, nothing above the selection layer covers a side, so it needs none of the sides UX-18 marks.
  - Every side is whole Device Pixels from an edge that lies on one (ADR-0090).
  - The other reading, on the gridline as the Focus outline lies, is not what the picture shows.
  - The 100% zoom is the next run's (question 1).

**Seen in the same picture, not acted on.** A range among several is drawn differently from ExSheet's:

- In Excel, the gridline outside the range's top and left edges is shaded too: `#AFAFAF`, which is `#E1E1E1` under
  the 22% shade.
- In Excel, a white device pixel runs just inside every edge of the range. At D6's left: `#AFAFAF`, `#FFFFFF`,
  `#C7C7C7`; past E5's right: `#FFFFFF`, `#AFAFAF`.
- ExSheet shades only inside the range and draws no white line. At D6's left in its picture: `#E0E0E0`, `#C7C7C7`.

That is not decision (1), and it is a reading of one picture. It is question 3 below.

**Also for the user: the Wrappers.** `ExGrid.MudBlazor` sets `--ex-focus-outline` to the palette's primary and leaves
the new token alone, as the decision gives it no Wrapper value. So under ExGrid.MudBlazor the Focus among several
ranges is `CanvasText`, beside a Focus outline in the primary everywhere else. Under ExSheet.MudBlazor it is black on
the Paper. Excel draws both in one colour (h1, h2). Whether a Wrapper maps the new token to its primary, as it maps
the Focus outline, is not decided here, and this branch maps nothing.

## What was built

- **The core marks the Focus among several ranges.** `ExGrid.razor` gives the Focus's element the class
  `ex-focus ex-focus-among-ranges` while the Selection has more than one range, in whichever layer holds it
  (`FocusClass`, interned beside `RangeClass`). It is decided from the Selection, not from the layer's siblings: a
  layer may hold the Focus and no range's box (a range in the Pinned Columns, the Focus added beyond them).
- **The stylesheet draws the thin border** (`src/ExGrid/Assets/ex-grid.css`, `.ex-focus.ex-focus-among-ranges::after`):
  - one `var(--ex-dp)` wide;
  - in `var(--ex-focus-among-ranges-outline, CanvasText)`;
  - `top` and `left` one `--ex-dp`, `right` and `bottom` `--ex-rule-dp` plus one `--ex-dp`.

  Paint only: nothing in C# reads its width.
- **No range outline, no fill handle.** Both already held among several ranges, by `RangeClass` (`ex-range-single`
  for one range only) and `FillRules.HandleRange` (one range only); both are now asserted beside the border.
- **Forced colours.** The core's forced-colors block already sets `content: none` on `.ex-focus::after`, and outlines
  `.ex-focus` and every `.ex-range`. So the thin border is not drawn there, and the Focus among several ranges is
  outlined as the Focus is everywhere there, beside ranges that are each outlined. The block's comment says so.
- **The Paper** (`src/ExSheet/Assets/ex-sheet.css`). `.ex-sheet .ex-focus.ex-focus-among-ranges::after` sets the
  border black where no token names it, with no white line. Three classes, so it outranks the Focus outline's green
  and white line (two classes) in either load order.
- **CI's chrome-150 project** runs the two layer-3 tests that read the border at the display's scale, ExGrid's own on
  `/wide` and the Paper's, at a real 150% (`playwright.config.mjs`'s grep gains `UX-19\/SH-39` and
  `UX-19 at the display's scale`). An emulated scale is not read, for the reason `device-pixel-225.spec.mjs` gives
  for a Border line:
  - through the DevTools protocol, the screenshot is not taken at the scale (a device pixel read twice at 1.5);
  - through Playwright's own `deviceScaleFactor` at 2.25, the border read one device pixel off on two sides and
    blended on a third.

  A real scale reads it exact. Besides the committed 100% and 150%, an untracked probe ran the bundled Chromium with
  `--force-device-scale-factor` 2 and 2.25, on both hosts, on `/wide` and on the Paper. Each read the border one
  device pixel wide on all four sides, a device pixel in from the top and left edges and from the two-pixel gridline
  (`left [1], top [1], right [3], bottom [3]`). CI has no project at those scales, so the probe is not committed.

## ADR-0008: the several-ranges bullet replaced

In "Excel's look for the Focus and a single range *(decided with the user, 2026-09-29)*".

Old:

```
- **Several ranges are left as they are until Excel has been observed**: each range tinted with
  no outline, the Focus's cell untinted (the hole above) and outlined. How Excel outlines a
  selection made with Ctrl+click, and whether its active cell carries a border there, is on the
  next Windows run's list, and this bullet is replaced by what it finds.
```

New:

```
- **Several ranges look as Excel's did in the seventeenth Windows run** *(decided with the user,
  2026-10-12; `verification/2026-10-11-windows-17/report.md`, Part H, h2)*. This bullet first left
  them as they were, each range tinted and the Focus outlined, until Excel had been observed.
  - No range carries an outline, and no fill handle is shown
    ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 5). Each range is tinted, with
    the hole where the Focus is, as before.
  - The Focus cell takes a thin border of one Device Pixel, in a token of its own,
    `--ex-focus-among-ranges-outline`
    ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)). Its
    default is `CanvasText`, the Focus outline's default, and it does not follow
    `--ex-focus-outline`. ExSheet's Paper sets it black.
  - It lies inside the cell, one Device Pixel in from its top and left edges and from the gridline
    the cell holds on its right and bottom, where the run's picture at 150% shows Excel's. Nothing
    above the selection layer covers a side of it (UX-18), and every side lies on Device Pixels
    wherever the cell's edges do
    ([ADR-0090](./0090-the-grid-is-told-its-device-pixel-and-puts-column-edges-on-it.md)).
  - The core marks the Focus among several ranges with a class. A layer may hold the Focus and no
    range's box, so the stylesheet cannot tell from the Focus's siblings.
  - Under forced colors the border is not drawn, and the Focus is outlined there as it is everywhere
    else under forced colors, beside ranges that are each outlined.
  - A single range and a single cell keep the look above.
  - *Read, not observed:* the border at Excel's 100% zoom, and its colour. The run's picture reads
    `#217346`, Excel's green, where its report says black. Both are on the next Windows run's list.
```

If the user takes the picture's colour, the sixth line of the second sub-bullet reads instead:

```
    `--ex-focus-outline`. ExSheet's Paper sets it to the Focus outline's colour, Excel's green.
```

and the last sub-bullet drops "and its colour. The run's picture reads `#217346`, Excel's green, where its report
says black. Both are" for "It is".

## ADR-0029: the token, and the class UX-4's walk names

The Selection row of the token table.

Old:

```
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(the border Excel draws around the range's perimeter, reserved here and taken up by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-09-29; it defaults to `--ex-focus-outline`)*, `--ex-focus-outline` *(exists)*, `--ex-grid-focus-outline` *(exists)* |
```

New:

```
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(the border Excel draws around the range's perimeter, reserved here and taken up by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-09-29; it defaults to `--ex-focus-outline`)*, `--ex-focus-outline` *(exists)*, `--ex-focus-among-ranges-outline` *(the thin border of the Focus among several ranges, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) of 2026-10-12; it defaults to `CanvasText` and does not follow `--ex-focus-outline`)*, `--ex-grid-focus-outline` *(exists)* |
```

A section at the end of the ADR.

Old (its last lines):

```
- **No new token.** The dashes take `--ex-focus-outline`, as ADR-0058's do, over a band of
  `--ex-background`, so they read on the Selection's own outline; `--ex-selection-outline` stays
  the range outline's alone. Forced colours restate them in `Highlight` over `Canvas`.
```

New:

```
- **No new token.** The dashes take `--ex-focus-outline`, as ADR-0058's do, over a band of
  `--ex-background`, so they read on the Selection's own outline; `--ex-selection-outline` stays
  the range outline's alone. Forced colours restate them in `Highlight` over `Canvas`.

## Added by ADR-0008's decision of 2026-10-12 *(decided with the user)*

- **Internal class**, which may change without notice: `ex-focus-among-ranges`. It joins `ex-focus`
  on the Focus's element while the Selection has several ranges. The core decides it from the
  Selection, because a layer may hold the Focus and no range's box. UX-4's walk names it.
- **Visual Token**: `--ex-focus-among-ranges-outline`, the colour of that Focus's thin border.
  - It defaults to `CanvasText`, as `--ex-focus-outline` does, and does not read it: a Theme that
    colours the Focus outline sets this one too.
  - ExSheet's Paper falls back to black.
  - Its width is one Device Pixel, `--ex-dp`, and is not a token.
- **Forced colours** do not draw the border, and restate the Focus by its outline, as everywhere
  there.
```

## The Definition of Done: UX-19 restated

Old:

```
| **UX-19** | MUST | Excel's look for the Focus and a single range: the selection's tint never covers the Focus cell (the Focus band and the hover band are other overlays and keep theirs); a one-cell selection shows the Focus outline and no tint; a single range shows one outline around the whole range and no outline around the Focus inside it; several ranges are each tinted with no outline, and the Focus cell among them is untinted and outlined (ADR-0008, 2026-09-29) | Layer 2 for which rectangles are painted and the hole's geometry; Layer 3 by pixel under both Chromes | as stated, in each of the four cases |
```

New:

```
| **UX-19** | MUST | Excel's look for the Focus, a single range and several ranges: the selection's tint never covers the Focus cell (the Focus band and the hover band are other overlays and keep theirs); a one-cell selection shows the Focus outline and no tint; a single range shows one outline around the whole range and no outline around the Focus inside it; several ranges are each tinted with no outline and no fill handle, and the Focus cell among them is untinted and takes a thin border of one Device Pixel inside the cell, in `--ex-focus-among-ranges-outline`, lying on Device Pixels at every scale (ADR-0008, 2026-09-29 and 2026-10-12) | Layer 2 for which rectangles are painted, the hole's geometry and the Focus's mark among several ranges; Layer 3 by pixel under both Chromes, the thin border read in device pixels at 100% and at a real 150%, ExGrid's own and the Paper's | as stated, in each of the four cases |
```

## The Docs Site

`samples/ExGrid.Docs/Pages/Grid/GridAppearance.razor`, the token table's Selection row.

Old:

```
                <tr><td><code>--ex-selection-fill</code>, <code>--ex-selection-outline</code>, <code>--ex-focus-outline</code></td><td>The selection and the Focus cell.</td></tr>
```

New:

```
                <tr><td><code>--ex-selection-fill</code>, <code>--ex-selection-outline</code>, <code>--ex-focus-outline</code>, <code>--ex-focus-among-ranges-outline</code></td><td>The selection and the Focus cell; the last is the Focus's thin border among several ranges.</td></tr>
```

The same page, after the shade's paragraph.

Old:

```
        <p>
            The selection's shade is set by <code>--ex-selection-fill</code> alone. It is painted by an element of
            the grid's own, beneath each selected range's box, so a rule on <code>ex-range</code>, the box that draws
            the outline, or on anything inside it, does not reach the shade.
        </p>
```

New:

```
        <p>
            The selection's shade is set by <code>--ex-selection-fill</code> alone. It is painted by an element of
            the grid's own, beneath each selected range's box, so a rule on <code>ex-range</code>, the box that draws
            the outline, or on anything inside it, does not reach the shade.
        </p>
        <p>
            A single range is outlined in <code>--ex-selection-outline</code>, and the Focus inside it is only left
            unshaded. Among several ranges, made with Ctrl+click, no range is outlined and no fill handle is shown, as
            in Excel, and the Focus cell takes a thin border of one device pixel inside it, in
            <code>--ex-focus-among-ranges-outline</code>: <code>CanvasText</code> unless you set it. It does not follow
            <code>--ex-focus-outline</code>, so a theme that colours the Focus outline sets this one too.
        </p>
```

`samples/ExGrid.Docs/Examples/Grid/ThemedGrid.razor`, the Example the page runs its tokens in. **A defect found while
reading it, by reading and not run.** It sets `--ex-focus-outline: 2px solid #2f81f7;`, which is not a colour. The
Focus outline is `border: … solid var(--ex-focus-outline, CanvasText)`, so the declaration is invalid at
computed-value time and draws no border. The Example's Focus, and its single range, are therefore drawn with no
outline at all. With the fix, the Example shows the new token too: Ctrl+click in it.

Old:

```
    --ex-focus-outline: 2px solid #2f81f7;
```

New:

```
    --ex-focus-outline: #2f81f7;
    --ex-focus-among-ranges-outline: #2f81f7;
```

`samples/ExGrid.Docs/Pages/Grid/GridSelectionAndKeys.razor`: the Focus is no longer "outlined inside the selection"
in a single range (since 2026-09-29), and among several ranges it now has a thin border.

Old:

```
            A selection is one or more rectangles of cells. The <strong>Focus</strong> is Excel's active cell: the
            cell typing goes into, outlined inside the selection. The <strong>Extent</strong> is the corner that
```

New:

```
            A selection is one or more rectangles of cells. The <strong>Focus</strong> is Excel's active cell: the
            cell typing goes into, left unshaded inside the selection, with a thin border of its own when the
            selection has several ranges. The <strong>Extent</strong> is the corner that
```

`samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor`, ExSheet's page that describes the Selection's look on the
Paper (ExSheet shows the change too).

Old:

```
            in the same way.
        </p>
        <DocsExample Builtin="typeof(BordersUnderSelection)" Mud="typeof(BordersUnderSelectionMud)" Files="@(["Examples/Sheet/QuarterlyPnl.cs"])">
```

New:

```
            in the same way.
        </p>
        <p>
            Among several ranges, made with Ctrl+click, no range is outlined and there is no fill handle, and the
            active cell takes a thin black border inside it. <code>--ex-focus-among-ranges-outline</code> colours it.
        </p>
        <DocsExample Builtin="typeof(BordersUnderSelection)" Mud="typeof(BordersUnderSelectionMud)" Files="@(["Examples/Sheet/QuarterlyPnl.cs"])">
```

(If the user takes the picture's colour: "a thin border inside it, in the outline's green".)

## Change Notes written

- `docs/releases/unreleased/run17gh-focus-among-ranges-exgrid.md`: ExGrid, Behaviour, ADR 0008 and 0029.
- `docs/releases/unreleased/run17gh-focus-among-ranges-exsheet.md`: ExSheet, Behaviour, ADR 0008.

## Questions for the next Windows run

Each is a reading until a run has observed it; none is written as Excel's.

1. **The border's width, colour and place at Excel's 100% and 150% zoom, in device pixels** (asked by the user with
   the decision).
   - *Procedure.* A new workbook. A1:B2 dragged, then Ctrl+drag D4:E6, as h2 did; the active cell D4. Do it at
     Excel's zoom 100%, then 150% (View, Zoom). Use the run's display at 150%, and a display at 100% if the desk has
     one. Cut a picture to the cells at each. Then fill D4 yellow through COM before the selection, and take the
     same pictures again.
   - *What to read.* Across D4's left and right edges at its middle row, and down its top and bottom edges at its
     middle column, in device pixels. Start at the gridline and go inward to the cell's ground, and give each
     pixel's colour in hex.
   - *What decides.*
     - The width: one device pixel, or two at 150% zoom. Two makes it scale with the zoom, and ADR-0008's bullet
       says so.
     - The colour: `#217346`, as h2's picture reads, or black, as its report says. Anything but black puts the
       Paper's default to the user again.
     - The gap: one device pixel of the cell's ground between the gridline and the border, as h2's picture shows,
       or none. None moves the border onto the gridline.
     - The gap over the yellow Fill: yellow (the cell's ground, as built) or white (a white line of its own, which
       the Paper would then draw).
2. **What marks the active cell among several ranges while it is edited.**
   - *Procedure.* The same selection, then F2 on D4, at 100% zoom.
   - *What to read.* The pixels across D4's edges, as in question 1, with the edit open.
   - *What decides.* ExGrid's Cell Editor covers the cell, and with it the thin border inside the cell. If Excel
     keeps a border round the edited cell, the border has to stand over the editor, or the editor inside it.
3. **A range among several: a white line inside its edges, and the gridline outside its top and left shaded?**
   - *Procedure.* As question 1, at 100% and 150% zoom.
   - *What to read.* Across E5's right edge and D6's left edge, and down E4's top edge and E6's bottom edge (cells of
     D4:E6 away from the active cell), from two pixels outside the range to three inside.
   - *What decides.* h2's picture at 150% shows both: a `#FFFFFF` device pixel between the shaded gridline and the
     shade, on every side, and the gridline outside the top and left edges at `#AFAFAF` (`#E1E1E1` under the 22%
     shade). ExSheet draws neither. Either one, confirmed, is a new decision about the Paper's look, which this
     branch did not take.

## Seen while reading, for another area

ADR-0008's single-range bullet still says "*(Read, not observed: that Excel draws no separate outline around the
active cell inside a single range. It is on the next Windows run's list.)*". Run 17's h1 observed it: A1 untinted,
with no outline of its own (`h-h1-1-a1-zoom.png`). That is the readings area's to record, so it is not proposed here.
