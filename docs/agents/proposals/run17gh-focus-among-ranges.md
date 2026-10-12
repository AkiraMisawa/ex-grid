# Run 17 G-H, the Focus among several ranges: what the orchestrator writes

Branch `claude/run17gh-focus-among-ranges`, rebased on `main` at `2265cda6` (round 3). The edits below are for
the files a background agent does not edit: ADR-0008, ADR-0029 and ADR-0071, two Definition of Done rows, and
three Docs Site pages. Each is an exact old -> new block of whole lines, numbered in the order they apply, checked
against this branch's tip. Nothing here touches `CONTEXT.md`.

The two decisions this branch builds, as the edits write them:

- *(2026-10-12, decided with the user: in a Selection of several ranges ExGrid as a whole follows Excel, with no
  range outline and no fill handle, and the Focus cell takes a thin border of one Device Pixel in a Visual Token
  of its own, `--ex-focus-among-ranges-outline`; a single range keeps today's look.)* Taken from the seventeenth
  Windows run's h2, with a default of `CanvasText` and black on the Paper.
- *(2026-10-12, decided with the user: on the Paper the border is `#217346`, as the readings of 2026-10-12
  measured (R4); outside the Paper, `--ex-focus-among-ranges-outline`'s default follows the Focus's own outline
  colour, `--ex-focus-outline`, ExGrid's accent, so under the MudBlazor Wrappers it follows their primary with no
  mapping of its own, and not `CanvasText`.)*

Everything else in the edits is this branch's, and none of it is credited to the user: the border's place inside
the cell, the cell's own ground between it and the gridline, the class the core marks the Focus with, and forced
colours drawing the Focus outline in its place.

## Stop: two parts of R4 that the Paper does not paint, for the user

R4 read four short lines across Excel's active cell (`records/r4.jsonl` on
`claude/exsheet-windows-readings-2026-10-12`). Of the parts it measured, the Paper now agrees with the border
(`#217346`, one device pixel, one in from the cell's edges), the white line between the border and the gridline,
and the `#C7C7C7` shade beyond it; layer 3 holds those (`UX-19/SH-54` in `sheet-paper.spec.mjs`, at 100% and
at a real 150%). Two parts differ, and making either agree changes a decided rule, so neither is built.

**How it was compared.** An untracked probe (`zz-probe-r4.spec.mjs`, not committed) made R4's three selections on
`/sheet?case=13` (a blank Sheet, nothing pinned, as R4's workbook) under both Chromes, at 100% and at a real 150%
(`--force-device-scale-factor=1.5`), WebAssembly host, Playwright's Chromium 141, and read the same four lines
across the active cell, from 6 device pixels outside to 10 inside, plus whole rows and columns through the areas.
R4's own picture `shots/r4-z100-1-a1-b2-then-ctrl-d4-e6-cells.png` (a crop at 2×, nearest neighbour) was read the
same way, at row 1, row 4 and columns A, D and E.

| Line, case (1), the active cell D4 | Excel (R4, 150% zoom) | ExSheet, the Paper (150%) |
|---|---|---|
| left, outside in | `#FFFFFF` `#AFAFAF` `#FFFFFF` `#217346` `#FFFFFF` | `#FFFFFF` `#E0E0E0` `#FFFFFF` `#217346` `#FFFFFF` |
| right, inside out | `#FFFFFF` `#217346` `#FFFFFF` `#AFAFAF` `#C7C7C7` | `#FFFFFF` `#217346` `#FFFFFF` `#E0E0E0` `#C7C7C7` |
| top, outside in | `#FFFFFF` `#AFAFAF` `#FFFFFF` `#217346` `#FFFFFF` | `#FFFFFF` `#E0E0E0` `#FFFFFF` `#217346` `#FFFFFF` |
| bottom, inside out | `#FFFFFF` `#217346` `#FFFFFF` `#AFAFAF` `#C7C7C7` | `#FFFFFF` `#217346` `#FFFFFF` `#E0E0E0` `#C7C7C7` |

Cases (2) and (3), and 100%, read the same way: the border, the white line and what lies past the gridline (the
shade in the area, the Paper outside it) agree, and the gridline's pixel is `#AFAFAF` in Excel and `#E0E0E0` on the
Paper on all four sides, except at A1 in case (2). The probe read the Sheet where the page laid it, a fraction of a
device pixel off the grid, so at 150% the shade's first row past the bottom gridline read blended (`#D5D5D5`); the
committed test reads with the page moved onto the grid (`onDevicePixels`) and finds the shade exact there.

**(a) The gridline round the active cell, and each area's frame.** R4 read the gridline's pixel round the active
cell as `#AFAFAF` on every side in all three cases, at both zooms, where no Heading stands. That is what `#E0E0E0`,
Excel's gridline in the same picture, gives under the shade's ×0.78 (224 × 0.78 = 175): a reading of the pixels,
since no run looked with the gridlines hidden, which would tell a shaded gridline from a grey line drawn over it.
Its pictures show more than its lines, as R4's picture read at row 1, row 4 and columns A, D and E says: every edge
of both areas is a line of `#AFAFAF` on the gridline (outside the area on its top and left, on its own last gridline
on its right and bottom) with a white device pixel just inside it, then the shade; where an area meets the
Headings, their green stands in place of the grey line. Across A1:B2 at row 1: `#217346` ×2 (the Heading),
`#FFFFFF`, `#C7C7C7` ×94, `#AFAFAF`, `#C7C7C7` ×94, `#FFFFFF`, `#AFAFAF`, `#FFFFFF` ×95. Past E4's right at row 4: `#C7C7C7` ×94, `#FFFFFF`, `#AFAFAF`. The Paper
paints its own gridline there: unshaded on the active cell's four sides (the hole takes the Focus cell's own right
and bottom gridline out of the shade, and the top and left ones are the neighbours', outside the range), unshaded
on each area's top and left (the neighbours'), shaded `#AFAFAF` on its right and bottom (its own), and no white
line inside the area anywhere. Across A1:B2 at row 1 on the Paper: `#ABABAB` (the Heading's edge), `#C7C7C7` ×98,
`#AFAFAF`, `#C7C7C7` ×98, `#AFAFAF`, `#FFFFFF`.

Options:

1. **Leave it**, and record the difference in ADR-0008's several-ranges bullet as observed and not followed.
2. **Draw Excel's frame round each area among several ranges**: one Device Pixel on the gridline round it in the
   shaded gridline's colour, a white Device Pixel inside it, and the active cell's own gridlines shaded. That
   changes the first decision of 2026-10-12 ("no range outline", on the Paper at least, or for ExGrid as a whole),
   ADR-0008's hole (the tint would cover the gridline the Focus cell holds, against UX-19's "the selection's tint
   never covers the Focus cell"), and ADR-0071 and ADR-0206 on what lies over a Border line (whether the frame is a
   multiplied shade or a line drawn over the lines is unobserved: R4 read only plain cells).
3. **Only the ring round the active cell**: the Focus among several ranges draws its four gridlines in `#AFAFAF` on
   the Paper. That makes R4's four lines agree in all three cases, but R4's pictures show the same line along every
   area edge, so the Paper would then show it round the Focus and nowhere else, which no picture of Excel shows.
   This branch would not choose it.

**(b) The Headings beside A1.** R4 read, at A1's left and top in case (2), the Headings' 2 device pixels of
`#217346` where the gridline would be, then the white line and the border: `#E1E1E1` ×3, `#217346` ×2, `#FFFFFF`,
`#217346`. R4's pictures show every selected row's and column's Heading that way, a shaded ground with that line on
its edge toward the cells and its label in the green, for each area. The Paper draws nothing of the Selection on
the Headings, for any selection: at A1 it reads `#ABABAB` (the Row Headings' edge, ADR-0071's Part C), `#FFFFFF`,
`#217346`, and on top the header's rule. Options:

1. **Leave it**: the Headings frame the Paper and follow the scheme (ADR-0071), and no ADR asks them to show the
   Selection.
2. **Excel's Heading mark for every selection**, not only several ranges: the selected rows' and columns' Headings
   shaded, with a line of 2 Device Pixels in the outline's colour on the edge toward the cells (and, by R4's
   pictures, the label in it). A new decision: new classes or tokens (ADR-0029), drawn as an overlay in the
   Heading bands so that no row learns the Selection (ADR-0008, "Selection state never has to enter the row's
   parameters"; the Row Headings are painted in each row, ADR-0050), and on which products (ExSheet's Paper, or
   every grid with Headings).

Neither part changes what this branch built; either option 2 is a later change on top of it.

## Edits

### 1. docs/adr/0008-selection-is-painted-by-an-overlay.md

The several-ranges bullet of "Excel's look for the Focus and a single range". This answers the verifier's finding
on round 2: h2 and R4 were at Excel's 100% and 150% zoom on the 150% display, so what is still unobserved is a
display at 100%, a cell with a Fill, and the active cell while it is edited.

Old:

```
- **Several ranges are left as they are until Excel has been observed**: each range tinted with
  no outline, the Focus's cell untinted (the hole above) and outlined. How Excel outlines a
  selection made with Ctrl+click, and whether its active cell carries a border there, is on the
  next Windows run's list, and this bullet is replaced by what it finds.
```

New:

```
- **Several ranges: no outline, no fill handle, and a thin border on the Focus** *(2026-10-12,
  decided with the user: in a Selection of several ranges ExGrid as a whole follows Excel, with no
  range outline and no fill handle, and the Focus cell takes a thin border of one Device Pixel in a
  Visual Token of its own, `--ex-focus-among-ranges-outline`; a single range keeps the look
  above)*. The decision followed the seventeenth Windows run's h2
  (`verification/2026-10-11-windows-17/report.md`, Part H). This bullet first left several ranges
  as they were, each tinted and the Focus outlined, until Excel had been observed.
  - No range carries an outline, and no fill handle is shown
    ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 5). Each range is tinted, with
    the hole where the Focus is, as before.
  - The border's colour *(2026-10-12, decided with the user: where nothing names
    `--ex-focus-among-ranges-outline`, the border is the Focus outline's colour,
    `--ex-focus-outline`, so a Theme or a Wrapper that colours the Focus colours it too, with no
    mapping of its own, and under the MudBlazor Wrappers it is their primary; on a Sheet's Paper it
    is `#217346`)*. Earlier the same day it had been decided as `CanvasText`, and black on the
    Paper, from h2's report. The readings of 2026-10-12 then measured Excel's border as `#217346`
    (R4): h2's black was its picture read at 1×.
  - It lies inside the cell, one Device Pixel in from its top and left edges and from the gridline
    the cell holds on its right and bottom, with the cell's own ground between it and the
    gridline. That is where the readings of 2026-10-12 measured Excel's active cell among several
    areas (R4), at Excel's 100% and 150% zoom on a 150% display, in each of three selections: one
    device pixel of `#217346` on every edge, one device pixel inside the cell, and white between it
    and the gridline. Nothing above the selection layer covers a side of it (UX-18), and every side
    lies on Device Pixels wherever the cell's edges do
    ([ADR-0090](./0090-the-grid-is-told-its-device-pixel-and-puts-column-edges-on-it.md)).
  - The core marks the Focus among several ranges with a class, decided from the Selection: a
    layer may hold the Focus and no range's box, so the stylesheet cannot tell from the Focus's
    siblings.
  - Under forced colors the border is not drawn, and the Focus is outlined there as it is
    everywhere else under forced colors, beside ranges that are each outlined.
  - *Not observed:* a display at 100% (R4 read the 150% display at both zooms, so one device pixel
    at a 100% display is by generalisation); a cell with a Fill (whether the line between the
    border and the gridline is the Fill, as built, or white); and the active cell while it is
    edited (ExGrid's Cell Editor covers the cell, and the border inside it).
```

### 2. docs/adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md

The Selection row of the token table.

Old:

```
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(the border Excel draws around the range's perimeter, reserved here and taken up by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-09-29; it defaults to `--ex-focus-outline`)*, `--ex-focus-outline` *(exists)*, `--ex-grid-focus-outline` *(exists)* |
```

New:

```
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(the border Excel draws around the range's perimeter, reserved here and taken up by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-09-29; it defaults to `--ex-focus-outline`)*, `--ex-focus-outline` *(exists)*, `--ex-focus-among-ranges-outline` *(the thin border of the Focus among several ranges, added by [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) on 2026-10-12; (2026-10-12, decided with the user: it defaults to `--ex-focus-outline`))*, `--ex-grid-focus-outline` *(exists)* |
```

### 3. docs/adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md

A section at the end of the ADR, after "Added by ADR-0170".

Old:

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

## Added by ADR-0008's note of 2026-10-12

- **Internal class**, which may change without notice: `ex-focus-among-ranges`. It joins `ex-focus`
  on the Focus's element while the Selection has several ranges. The core decides it from the
  Selection, because a layer may hold the Focus and no range's box. UX-4's walk names it.
- **Visual Token**: `--ex-focus-among-ranges-outline`, the colour of that Focus's border of one
  Device Pixel *(2026-10-12, decided with the user: where nothing names it, it is the Focus
  outline's colour, `--ex-focus-outline`, so a Theme or a Wrapper that colours the Focus colours it
  too and no Wrapper maps it; on a Sheet's Paper it is `#217346`, as the Focus outline is there)*.
  Its width is one Device Pixel, `--ex-dp`, and is not a token.
- **Forced colours** do not draw the border, and restate the Focus by its outline, as everywhere
  there.
```

### 4. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

"The Selection on the Paper (case 11)", its first bullet: the verifier's finding on round 2 that it is false for
several ranges.

Old:

```
- **The outline is 2 Device Pixels of `#217346`, on the gridline and one Device Pixel outside it,
  with a white line of one Device Pixel inside it.** Today it is 2 CSS px in the Ink. The single range's
  outline and the Focus's outline are drawn alike.
```

New:

```
- **The outline is 2 Device Pixels of `#217346`, on the gridline and one Device Pixel outside it,
  with a white line of one Device Pixel inside it.** Today it is 2 CSS px in the Ink. The single range's
  outline and the Focus's outline are drawn alike.
  - *Several ranges* *(2026-10-12, decided with the user: among several ranges no range carries
    this outline and no fill handle is shown, and the Focus takes, in place of its outline, a border
    of one Device Pixel inside its cell, in the Focus outline's colour, `#217346` on the Paper;
    [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*. It has no white line of its own:
    the cell's ground lies between it and the gridline, the Paper's white on a cell with no Fill,
    which is the white the readings of 2026-10-12 measured there (R4). Under `ExSheet.MudBlazor` it
    is the palette's primary, as the Focus outline is.
```

### 5. docs/definition-of-done.md

UX-19.

Old:

```
| **UX-19** | MUST | Excel's look for the Focus and a single range: the selection's tint never covers the Focus cell (the Focus band and the hover band are other overlays and keep theirs); a one-cell selection shows the Focus outline and no tint; a single range shows one outline around the whole range and no outline around the Focus inside it; several ranges are each tinted with no outline, and the Focus cell among them is untinted and outlined (ADR-0008, 2026-09-29) | Layer 2 for which rectangles are painted and the hole's geometry; Layer 3 by pixel under both Chromes | as stated, in each of the four cases |
```

New:

```
| **UX-19** | MUST | Excel's look for the Focus, a single range and several ranges: the selection's tint never covers the Focus cell (the Focus band and the hover band are other overlays and keep theirs); a one-cell selection shows the Focus outline and no tint; a single range shows one outline around the whole range and no outline around the Focus inside it; several ranges are each tinted and the Focus cell among them is untinted *(2026-10-12, decided with the user: among several ranges no range is outlined and no fill handle is shown, and the Focus takes a border of one Device Pixel in `--ex-focus-among-ranges-outline`, which is the Focus outline's colour unless set)*, lying inside the cell a Device Pixel in from its edges and its gridline, as the readings of 2026-10-12 measured Excel's (R4), and on Device Pixels at every scale (ADR-0008, 2026-09-29 and 2026-10-12) | Layer 2 for which rectangles are painted, the hole's geometry, the Focus's class among several ranges and the border's token; Layer 3 by pixel under both Chromes, the border among several ranges read in device pixels at 100% and at a real 150% on `/wide`, on the Paper and under `ExGrid.MudBlazor` | as stated, in each of the four cases |
```

### 6. docs/definition-of-done.md

SH-54: the verifier's finding on round 2 that it is false for several ranges.

Old:

```
| **SH-54** | MUST | On a Sheet's Paper, the Selection's outline and the Focus's are 2 Device Pixels on the gridline and one outside it, with a white line of one Device Pixel inside that is not drawn while an edit is open; under the built-in Chrome the outline is `#217346` and the shade `#C7C7C7` over white; a black line inside the shade stays `#000000`; the Focus cell is unshaded; under `ExSheet.MudBlazor` the outline is the palette's primary and the shade the primary mixed into white (ADR-0071, 2026-10-02) *(2026-10-11, [ADR-0206](adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md): also in a Pinned Column and across the pinned boundary, under both Chromes; it had been checked only where nothing is pinned.)* | Layer 3 pixels at 100% and 150% under both Chromes | as stated |
```

New:

```
| **SH-54** | MUST | On a Sheet's Paper, a single range's outline and the Focus's when one cell is selected are 2 Device Pixels on the gridline and one outside it, with a white line of one Device Pixel inside that is not drawn while an edit is open; under the built-in Chrome the outline is `#217346` and the shade `#C7C7C7` over white; a black line inside the shade stays `#000000`; the Focus cell is unshaded; under `ExSheet.MudBlazor` the outline is the palette's primary and the shade the primary mixed into white (ADR-0071, 2026-10-02) *(2026-10-11, [ADR-0206](adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md): also in a Pinned Column and across the pinned boundary, under both Chromes; it had been checked only where nothing is pinned.)* *(2026-10-12, decided with the user: among several ranges no range is outlined, and the Focus takes a border of one Device Pixel inside its cell in the outline's colour, `#217346` under the built-in Chrome and the palette's primary under `ExSheet.MudBlazor`; ADR-0008.)* It has no white line of its own: between it and the gridline lies the cell's ground, white on white Paper, as the readings of 2026-10-12 measured (R4) | Layer 3 pixels at 100% and 150% under both Chromes | as stated |
```

### 7. samples/ExGrid.Docs/Pages/Grid/GridAppearance.razor

The token table's Selection row.

Old:

```
                <tr><td><code>--ex-selection-fill</code>, <code>--ex-selection-outline</code>, <code>--ex-focus-outline</code></td><td>The selection and the Focus cell.</td></tr>
```

New:

```
                <tr><td><code>--ex-selection-fill</code>, <code>--ex-selection-outline</code>, <code>--ex-focus-outline</code>, <code>--ex-focus-among-ranges-outline</code></td><td>The selection and the Focus cell. The last is the Focus's thin border among several ranges, and is the Focus outline's colour unless you set it.</td></tr>
```

### 8. samples/ExGrid.Docs/Pages/Grid/GridAppearance.razor

After the shade's paragraph.

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
            unshaded. Among several ranges, made with Ctrl+click, no range carries that outline and no fill handle is
            shown, and the Focus cell takes a thin border of one device pixel inside it, as Excel's active cell does, in
            <code>--ex-focus-among-ranges-outline</code>. Unset, that is the Focus outline's colour, so a theme that
            colours <code>--ex-focus-outline</code> colours this border too, and under ExGrid.MudBlazor it is the
            palette's primary. The theme in "A theme, tones and a stale price" below sets only
            <code>--ex-focus-outline</code>: Ctrl+click in it.
        </p>
```

### 9. samples/ExGrid.Docs/Pages/Grid/GridSelectionAndKeys.razor

The Focus is not "outlined inside the selection" in a single range (since 2026-09-29), and among several ranges it
has a thin border.

Old:

```
            A selection is one or more rectangles of cells. The <strong>Focus</strong> is Excel's active cell: the
            cell typing goes into, outlined inside the selection. The <strong>Extent</strong> is the corner that
```

New:

```
            A selection is one or more rectangles of cells. The <strong>Focus</strong> is Excel's active cell: the
            cell typing goes into, outlined when it is selected alone, left unshaded inside a selected range, and
            given a thin border of its own among several ranges. The <strong>Extent</strong> is the corner that
```

### 10. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

The Selection's look on the Paper, which ExSheet's page describes (ExSheet shows the change too).

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
            Among several ranges, made with Ctrl+click, no range carries the green outline and there is no fill handle,
            and the active cell takes a thin border of one screen pixel inside it, in the outline's green, as Excel's
            is. Under ExSheet.MudBlazor it is the palette's primary, as the outline is.
            <code>--ex-focus-among-ranges-outline</code> colours it.
        </p>
        <DocsExample Builtin="typeof(BordersUnderSelection)" Mud="typeof(BordersUnderSelectionMud)" Files="@(["Examples/Sheet/QuarterlyPnl.cs"])">
```

## Not a page: the Themed grid Example

`samples/ExGrid.Docs/Examples/Grid/ThemedGrid.razor` is an Example component, not a page (it has no `@page`), so
this branch fixed it in code: `--ex-focus-outline: 2px solid #2f81f7;` became `--ex-focus-outline: #2f81f7;`. The
old value is not a colour, so the Focus outline's `border: … solid var(--ex-focus-outline, CanvasText)` was invalid
at computed-value time and the Example drew no Focus outline and no range outline. With the decided default, the
Focus among several ranges in that Example is the same blue with no token of its own, which edit 8's last sentence
invites the reader to try.

## Change Notes

- `docs/releases/unreleased/run17gh-focus-among-ranges-exgrid.md`: ExGrid, Behaviour, ADR 0008 and 0029.
- `docs/releases/unreleased/run17gh-focus-among-ranges-exsheet.md`: ExSheet, Behaviour, ADR 0008 and 0071.

## Questions for the next Windows run

Each is a reading until a run has observed it; none is written as Excel's.

1. **A display at 100%.** R4's three selections at Excel's 100% zoom on a display at 100%, if the desk has one:
   the border's width and place in device pixels, as R4 read them.
2. **A cell with a Fill.** D4 filled yellow through COM before R4's first selection; the four lines again. What
   decides: whether the device pixel between the border and the gridline is the Fill (as built) or white (a line of
   its own, which the Paper would then draw).
3. **The active cell while it is edited.** R4's first selection, then F2 on D4, the four lines again. What decides:
   ExGrid's Cell Editor covers the cell and the border inside it; if Excel keeps a border round the edited cell, the
   border has to stand over the editor, or the editor inside it.
