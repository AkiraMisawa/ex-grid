# A Consumer's Cell Class paints, and only paints

*(Decided with the user, 2026-10-03, in the grilling for the CDS marking sample,
`docs/specs/cds-marking/`. It reopens one sentence of
[ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md), "There are no
Class or Style parameters", and the matching row of
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md). Both carry a
note pointing here.)*

**A Column may declare a Cell Class: a function of the row that returns one class name of the
Consumer's own, or none.** The grid puts that class on the row's value cell in that column. The
class may change how the cell is painted. It may change the cell's weight up to 600. It cannot
change anything that moves a glyph or a box, because the grid's stylesheet holds those properties on
every classed cell.

## Why it was asked

The sample shows CDS quotes from a data provider. Each quote says whether it was observed in the
market, and the screen paints the quotes that were not observed faintly. That fact lives in the
row, not in the value, so Tone cannot express it
([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)). It is not a state in Cell State's
sense either: nothing is wrong with the value, and no theme or Wrapper has a meaning to paint for it.
The user compared ag-grid, where a column's `cellClassRules` returns classes the application styles
itself, and asked for the same.

## What ADR-0029 closed, and what this opens

ADR-0029 gave three reasons for having no Class parameter. Each is answered here, not set aside.

- **"A `Style` parameter is an unguarded door back into everything ADR-0027 closed."** Nothing here
  takes a style. A class can still carry geometry from CSS, so the grid's stylesheet holds, on every
  cell that carries a Cell Class, each property that moves a glyph or a box. It sets them
  `!important`, from the tokens the grid already reads:
  - `font-family`, `font-size`, `font-style` and `font-stretch`;
  - `letter-spacing`, `word-spacing` and `font-variant-numeric`;
  - `padding`, `line-height`, `height`, `box-sizing` and the border widths;
  - `white-space`, `overflow` and `text-overflow`.

  A Consumer rule that sets one of them has no effect. One that overrides them with `!important` of
  its own, at a higher specificity, is a defect of that Consumer, as editing the grid's stylesheet
  would be.
- **Weight is held at 600 or below.** A Consumer sets weight through a token the classed rule reads
  (`--ex-cell-font-weight`), and the rule clamps it to the weights the metrics are measured at. Weight
  600 is safe because
  [ADR-0016](./0016-column-width-and-overflow.md) charges each glyph class at its widest over every
  weight the grid paints, and group and total rows paint at 600. Anything wider would make
  `####` quietly wrong, and ADR-0016's note of 2026-10-03 records why the browser is not asked to
  decide `####` instead: it works, and it costs a fling 5× to 10× today's browser time, growing with
  every cell scrolled past.
- **"A `Class` parameter is an invitation to rules on arbitrary elements."** The class is on one kind
  of element, a value cell, and only on the cells the Consumer's function names. It is not offered
  on the root, a header, a row, or an Action, Template or Mark cell.
- **"A per-cell style string is an allocation in the render loop" (P5).** The grid interns one class
  string per distinct name, as `CellClasses` interns the closed enums. A Consumer that returns a
  small set of names pays nothing per render. One that returns a different name per row grows the
  table, and that cost is its own; the XML documentation says so.
- **Nothing in a classed cell transitions or animates** (P8). The classed rule sets
  `transition: none` and `animation: none`, `!important`. The Change Highlight stays the grid's own,
  static, mark ([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)).

What a class may set is therefore what paints: colour, background, text decoration, opacity, and
weight up to 600.

## The rules of the declaration

- **The function receives the row, and is asked when the row renders**, on the row's render path, as
  a Tone rule is. A class that depends on something outside the row changes when the Consumer hands
  over a new row instance ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)),
  and not otherwise.
- **It returns one class name or null.** A name with whitespace in it, or one that begins with `ex-`,
  is refused by name. The `ex-` prefix is the grid's
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md)), and a Consumer's class that
  borrowed it could restyle the grid's own cells.
- **The grid adds a marker class to a classed cell**, and the held properties apply through that
  marker only. A cell with no Cell Class paints exactly as before, including ExSheet's Cell Format,
  which is not a Cell Class.
- **A meaning the reader must not lose is not a Cell Class.** Under forced colors the browser
  replaces the class's colours, as it should. Cell State is restated in the forced-colors block
  (ADR-0006); a Cell Class is not. The sample's faint quotes are appearance, so a Cell Class fits
  them. An overridden quote is a state, so it is Modified.
- **A Wrapper does not paint it.** The class is the Consumer's, and so is its stylesheet. The
  MudBlazor Wrapper still refuses `CellClassFunc`: under MudBlazor that function may set anything,
  and accepting it while ignoring its font size would redefine a MudBlazor word quietly, which
  ADR-0030 refuses.
- **The selection overlay, the Focus outline and the Change Highlight paint over a classed cell** as
  over any other.
- **ExSheet does not offer it.** A Sheet's appearance is its Cell Format, which is document data
  ([ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)), and a
  Sheet's cell is not a row of the Consumer's data.

## Considered options

- **Add `Estimated` to Cell State** — the first proposal, rejected by the user. "Not observed" is a
  fact the data provider attaches to a quote, not a meaning every Consumer and Wrapper shares, and
  one Consumer is not ADR-0006's two.
- **Let the browser decide `####`, and open the class fully, as ag-grid does** — measured, and
  rejected on cost (ADR-0016, note of 2026-10-03).
- **A contract only, with the core trusting the Consumer** — what the research proposed: a class that
  changes glyph width owes `CellMetrics`, as a Theme does. Rejected in favour of holding the
  properties, because a Theme is written once by someone who knows the obligation, and a Cell Class
  is written per screen, where `font-size: 12px` in a highlight rule is an ordinary thing to write.
- **A Template Column for the faint quotes** — rejected. It would give up plain markup and the
  core's `####` decision on every value of the column
  ([ADR-0020](./0020-action-and-template-columns.md)).

## Consequences

- **The Definition of Done's §26 gains DC-67 to DC-69**, which gate ExGrid as every declaration
  there does.
- **Without the declaration nothing changes**: no call, no marker and no rule.
- **The cost is one delegate call per painted value cell of a row that renders.** It is measured in
  a `spikes/render-bench` mode before it ships, beside the Tone rule it resembles.
