# A cell's Fill may be split diagonally between two colours

*Proposed on 2026-11-07 in the project thread "Polish the XVA workspace page" of quant-workspace, which draws its
grids with `@ex-grid/mantine`. The user asked for the split the same day and agreed to this direction ("OK"), after
asking why ExGrid does not let a Consumer style a cell and refuse a style that would harm virtualisation. The details
below are for the user's review.*

## Context

quant-workspace's XVA what-if page shows three versions of each input. Original is what the sources gave and is
immutable. Before and After are each built from Original plus the user's changes. A value that differs from Original
on one side is highlighted in that side's colour. A value that differs on both sides is split diagonally: Before
above left, After below right. The grids that need it are editable ones: trades, netting sets, CSAs.

ExGrid draws the one-sided case already. `cellAppearance` (ADR-0050 item 15, ADR-0071) gives a cell a Font, a Fill
and Borders by (row, column). It is part of every grid's options, not only ExSheet's. It cannot draw the split,
because a Fill is one solid colour, and ADR-0071 left pattern and gradient fills out.

The user's question was the broader one. Refusing at run time would mean measuring what a style did to the cell,
which the grid never does (P4). What can be refused is a property, before anything is painted. `CellAppearance`'s
closed shape is that refusal: no field in it can change a cell's geometry or the widths `####` is judged by (bold is
judged by the bold widths, DC-58). ADR-0029's "no Class or Style parameters" stands. A further paint is a further
field.

## Decision

1. **`CellAppearance` gains `lowerRightFill`**, a colour as `0xRRGGBB` (C#: `CellAppearance.LowerRightFill`,
   `RgbColour?`). A cell that has it has a **Split Fill**:
   - `fill` paints the half above and left of the diagonal, which runs from the cell's top-right corner to its
     bottom-left corner;
   - `lowerRightFill` paints the half below and right of it.

   The diagonal joins the corners whatever the column's width. It is not drawn at 45°.
2. **`lowerRightFill` without `fill` is refused by name**, as a colour out of range is refused (`checkedAppearance`).
   Half a cell with no colour would show the ground, and would read as a one-sided highlight the Consumer did not
   ask for. Two equal colours paint as the plain Fill they are.
3. **A Split Fill is a Fill in everything but the split.**
   - It lies where a Fill lies among the cell's layers, and gives way where a Fill gives way.
   - It covers the gridlines at its edges as a Fill does. Each edge takes the colour of the half that meets it:
     `fill` at the top and left, `lowerRightFill` at the bottom and right.
   - The Cell Editor shows it as it shows a Fill: from the cell's own appearance, unless `editorAppearance` is given.
4. **It is interned as a Fill is.** Each pair of colours gets one generated class the first time it is asked for. So
   painting a split allocates nothing per cell on the render path (P5), and a row still repaints only when its values
   or its appearance change. Blazor carries it in the appearance it already sends to the browser; nothing per cell
   crosses to .NET.
5. **ExSheet does not offer it.** A Sheet's Fill stays Excel's one solid colour (ADR-0071, SH-38), because Excel has
   no Split Fill. A Sheet Document cannot record one, and `.xlsx` writing never meets one.
6. **Copy is unchanged.** The clipboard reads values, never a cell's appearance, so it carries no Fill today and no
   Split Fill.
7. **The colours are the Consumer's, as a Fill's are** (ADR-0071), not Visual Tokens. A Consumer whose colours follow
   the colour scheme answers with the scheme in force, and hands over a new `cellAppearance` when the scheme changes.
   The function's identity is the change signal (ADR-0006).
8. **A Split Fill is not announced, any more than a Fill is.** Colour is never the only signal: the Consumer says in
   words what its colours mean, for example in its `cellMessage`.

## Considered Options

- **A two-sided Cell State vocabulary (first, second, both) painted from tokens.** Rejected. ADR-0006 admits a
  vocabulary when two Consumers arrive at it independently, and here only one has. The paint it needs is already the
  Consumer's to give (a Fill), except for the split.
- **A per-cell Class or Style, refused when it would harm virtualisation.** Rejected, for ADR-0029's reasons and one
  more. The refusal cannot be made at run time without measuring (P4). An allowlist of CSS properties checked per
  cell puts a parse and a `style` attribute per cell on the render path (P5), and the allowlist ends up as this closed
  shape anyway.
- **Template Columns that draw the split themselves.** Rejected. A Template cell is not edited, overwritten or copied
  as an ordinary cell (ADR-0020, ADR-0037), and the grids that need the split are the editable ones.
- **General gradient or pattern fills.** Not taken. Nothing asks for them, and ADR-0071's exclusion stands for
  everything but the split.

## Consequences

- One optional field in TypeScript and in C#. It is additive: `core.api.md` and `PublicAPI.Unshipped.txt` gain it,
  and the implementation adds an ExGrid Change Note that "adds".
- `CONTEXT.md` gains **Split Fill**, and **Fill** says it may be split.
- The implementation adds to DC-58: a Split Fill is painted on that cell alone, with the halves of §1 and the
  gridline covers of §3, and P1 to P9 hold. Layer 1 checks the refusal of §2, layer 2 checks one class per pair, and
  layer 3 checks the painted pixels at 100% and 150% on the React host and both Blazor hosts.
- Before it merges, the implementation measures a Split Fill beside a plain one with a `spikes/render-bench` mode on
  the `/format` page, as ADR-0071 made a measurement the precondition for per-cell paint.
- The Docs Site's page on per-cell appearance gains the Split Fill and an Example that runs it.
- quant-workspace takes it in the release after it lands. Its ADR-0010 needs it by that page's third delivery step
  (Before-only and After-only changes). Until then every change there differs on both sides, and the cell wears
  `modified` instead.
