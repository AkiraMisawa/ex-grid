# Change Highlight: the Consumer says when a cell changed, and the grid marks it without animating

*(Decided with the user, 2026-09-30, in the ExPivot grilling — Q59 with its parts a to d, and Q60.
The user asked for it in the first version, and in ExGrid rather than only in ExPivot. It is an
opt-in declaration, shaped like the ones [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md)
gave ExSheet: without it, nothing changes.)*

A trading screen marks a value the moment it changes, so the eye finds what moved. **ExGrid gains
an opt-in declaration for this, the Change Highlight.**

- The Consumer says when a cell's shown value last changed.
- The grid marks the cell for a short time, and then takes the mark away.

## It does not animate, and P8 stands

Rows are recycled. The element that painted row 400 paints row 460 after a scroll, which is why
[ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)'s P8 and the Definition of
Done's UX-6 let nothing inside `.ex-viewport` transition or animate. A fade would run from the
previous row's colour to this one's, so every scroll would send a wave of colour across the grid.

**The mark is therefore static (Q59a).**

- **It is a class on the cell** for as long as the mark lasts, and it is removed in one step when
  the mark ends.
- **It is keyed by row and column, never by the element.** A recycled element asks again for the
  row it now paints, so the mark stays on the cell and does not travel with the element.
- **A user who asks for reduced motion sees the same thing**, because there is no motion to reduce.

Rejected: **rewriting P8 to allow a one-off fade on a changed cell** (Q59a, option b). A fade needs
rules against recycling that no measurement supports, and it would weaken a criterion that holds
the whole Viewport.

## The Consumer knows; the grid asks

(Q59b) **`CellChangedAt` is asked by (row, column), as Cell State is**
([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)). It answers with the time the
cell's shown value last changed, or null.

**The grid never compares values itself** (principle 3). A comparison would need a key to tell rows
apart across Windows, and the grid has none: Row Identity is a reference
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)).

- **The delegate's identity is the change signal**, as it is for Cell State. When the Consumer hands
  over a new delegate, the painted rows ask again. Rewriting what an unchanged delegate answers
  leaves the marks as they were.
- **A cell is marked while the current time is before its change time plus
  `ChangeHighlightDuration`.** The duration defaults to 1 s, and the Consumer may set it.
- **The grid takes the mark away itself.** It keeps one timer, for the earliest end among the marks
  it has painted, and re-renders only the rows whose marks end. No other row renders, and nothing
  is read from the page.
- **The grid reads the time from its `Clock`**, a `TimeProvider` that defaults to the system's.
  A test hands in its own.
- **The delegate is asked of value cells only**, as a per-cell kind is. An Action, Template or Mark
  cell paints no value that could change.
- **ExPivot answers the delegate by comparing the text it paints**
  ([ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)).
- **A plain Consumer answers from its own knowledge.** For example, a server's notice that trade
  T100123's P&L changed is enough, and this is where a server does the telling (Q59b's follow-up).
  The `/grid-live` demo shows it.

## One colour

(Q59c) The mark is one colour, whichever way the value moved: `--ex-change-highlight-background`.

- **Its default is a 40% tint of the system colour `Mark`** over the cell's own ground
  ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)). See
  "Refined while building it" below for why it is a tint and not `Mark` itself.
- **The MudBlazor Wrapper maps the token onto its palette.**
- **The forced-colors block restates the mark** in system colours, as it restates every state.

Rejected: **green for a rise and red for a fall.**

- Red and green are the Tone's colours (ADR-0006), so a loss and a fall would look the same.
- For a risk measure, a rise is not good news.
- A direction, if one is ever wanted, is a small arrow, not a colour.

## Seen, not announced

The mark is visual only. **No live region announces it.**

- A screen that changes four times a second would be unusable read aloud.
- The grid's one live region, which is `polite`
  ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)), keeps its
  two writers and nothing more.

A Consumer that wants an audible alert on one value writes its own, from the knowledge it answers
the delegate with.

## Refined while building it

*(2026-10-01, when the declaration was built.)*

- **The default was `Mark` itself, and that was wrong on a dark page.** The decision above said
  `Mark` "so the bare grid follows the host's colour scheme". Measured in Chromium, it does not
  follow it: `Mark` stays pure yellow (255, 255, 0) under `color-scheme: dark`, while the cell's
  text, `CanvasText`, turns white. A marked value on a dark host would have been white on yellow,
  which cannot be read. The default is now `color-mix(in srgb, Mark 40%, transparent)`, painted
  over the cell's ground: about 5:1 against the white text on Chromium's dark `Canvas`, and 20:1
  against black text on a light page. It is still `Mark`'s colour, and still one token a theme
  replaces; the MudBlazor Wrapper maps it to its warning colour at 25%.
- **A `Clock` left null is the grid's own clock**: the `TimeProvider` the host registers, or the
  system's when it registers none. The grid's other timers already resolve their clock this way,
  so a host or a test that registers one gets one clock throughout.
- **A change time still to come is taken as given**: the cell is marked until that time plus the
  duration, which is the rule above as written. The grid asks again on every render, so treating
  it as now would paint the same marks and only add renders.
- **A negative duration is refused by name.** A zero duration marks only change times still to
  come.
- **A state outranks the mark.** In the forced-colors block a Missing or Modified outline wins a
  cell that is both marked and in that state, and under the Wrapper a Missing cell's opaque ground
  hides the mark. The mark lasts a second; the state is what the reader must not lose (ADR-0006).

## Consequences

- **§26 of the Definition of Done gains DC-60 to DC-62**, which gate ExGrid as every declaration
  there does.
- **Without the declaration nothing changes**: no timer, no class and no call.
- **The cost is that of Cell State**: one delegate call per painted value cell of a row that renders,
  plus one timer while any mark is showing.
- **ExSheet and plain Consumers can use the same declaration later.** For ExSheet, the obvious case
  is a cell whose value a recalculation changed.
