# A filtered column's ▾ is a funnel in the standard header too

*Decided with the user on 2026-11-10, in the project thread "Add a compact display for the header".
[ADR-0500](./0500-a-header-may-be-drawn-compact-with-one-box-for-the-menu-and-the-columns-state.md) left
the standard header as it was and put the question in front of the user with its other open points: no
mark and no change; the ▾ becoming a funnel while the column is filtered, at the widths that stand; or
a mark of its own with room charged for it. The user chose the second, and took ADR-0500's other points
as it had them (recorded there).*

This amends ADR-0500 (section 1's "the DOM an earlier version drew", section 4 and its Consequences),
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) and
[ADR-0044](./0044-alt-down-opens-one-popover-the-commands-above-the-filter.md) (the ▾ and what it shows)
and [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md) (the state
hooks).

## Context

The standard header never said that a column is filtered. The only sign was inside the column menu:
*Clear filter* enabled, and the panel showing what was chosen. A reader of a table of money who did not
open each menu could not tell a short table from a filtered one by the header, and the spine's first
rule is about exactly that reader.

ADR-0500 gave the compact header a mark for it, because its specification asked for state visible at
rest, and left the standard header alone, because a mark there reaches every grid in use and nobody had
asked for that. With both displays in one product, the same filtered column was then marked under one
and not under the other, which reads as the Filter being gone when a Consumer switches back.

What a mark may cost was already bounded by ADR-0500's findings. The standard header has two boxes
beside its label, both reserved at every moment: the ▾'s band (22px) and the sort mark's box (20px). A
third box would be a third charge on every column of every grid, the opposite of what the same thread
had asked for. The ▾'s own box is the one place a mark can stand at no charge.

## Decision

### 1. A filtered column's ▾ is painted as a funnel, under either Header Display

While the Filter holds a spec for a column, the column menu's button in its header shows a funnel in
place of the ▾: the same picture the compact header's Header Box shows, in the header's colour and at
full strength, where the ▾ is faint. It is told from the ▾ by shape and by weight, never by colour and
never by hover.

- **The standard header keeps its two boxes.** The sort mark stays where it was, in its own box after
  the label, as ▲ or ▼. A column that is sorted and filtered shows both, each in its box. The combined
  pictures (an arrow and a funnel in one box) remain the compact header's, which has one box.
- **The button is the button.** It stands where it stood, at the size it had; a press opens the column
  menu and Alt+↓ opens the Focus's, as before. Only what it paints changes.
- **It says so in words.** The button is named *Column menu, filtered* while its column is filtered, in
  both displays, by the label ADR-0500 added. A filter has no attribute of ARIA's, so the name is how
  assistive technology hears it. The Sort is the cell's `aria-sort`, as before.
- **It follows the Filter the grid is given**, as the compact header's mark does: the column names of
  `filter.columns`. The grid neither holds nor executes the Filter (the spine's third rule); it paints
  what the Consumer hands it. A Consumer that narrows its rows by some other means has told the grid
  nothing, and no header is marked.

### 2. No width changes, and nothing is measured

`headerRequiredPx` is what it was under both displays. The funnel is painted in the box the ▾ had, so a
Filter set or cleared moves no label, cuts no label and changes no Auto width, and nothing is reported.
The Filter repaints the header band, where before this decision it repainted nothing of a standard
header. No layout is read and no listener is added.

### 3. A standard header with no column menu shows no mark

With `hideColumnMenu` the standard header has no ▾ and no box for one: its charge is the label, the
slack and the sort mark's box. A funnel there would need room charged to every such column, which is the
third option, not the one chosen. So a Consumer that hides the column menu and filters its rows has, in
the standard display, the header it had before: no mark. The compact header does mark it, in the Header
Box it keeps without the button (ADR-0500 section 2), and the page that documents `hideColumnMenu` says
so. This is a limit stated, not a mark quietly dropped.

### 4. What is written to the DOM

- A header cell wears `data-ex-filtered` while the Filter holds a spec for its column, **under either
  display**. The stylesheet paints the funnel from it.
- `data-ex-header-display="compact"` stays the compact header's alone.
- A grid with no Filter draws, to the attribute, the standard header an earlier version drew. A filtered
  column's header differs from that version's by the attribute and the button's name. ADR-0500's
  sentence that the standard header's DOM is unchanged now holds of a grid that is not filtered.

## Considered

- **A mark of its own, with room charged.** A third box beside the label, or a wider band holding the ▾
  and the funnel together. It keeps the ▾ in sight while the column is filtered. It widens every Auto
  column and every fit, on every grid in use, to mark a state most columns are not in; and the compact
  header had just shown that one box can say both. Refused for the width.
- **The funnel beside the ▾ inside the box it has.** A ▾ and a funnel sharing the button's 16px, as the
  compact header's arrow and funnel do. In the compact header's box the ▾ is what stands there while the
  column holds nothing, and a picture takes its place when it holds something. A standard header that
  kept the ▾ beside the funnel would give the same box a second grammar under the other display, for a
  ▾ half its size. Refused: one box reads one way.
- **Colour, or weight alone, on the ▾.** A ▾ drawn darker or in an accent. Colour alone is what the
  specification refused, and weight alone is not read without a second ▾ to compare it with.
- **Leave the standard header as it was.** ADR-0500's first position. It costs nothing and changes no
  grid in use, and it leaves the product marking a filter under one display and not under the other.

## Consequences

- **Every grid in use that shows the column menu and is given a Filter changes its look**: the ▾ of a
  filtered column becomes a funnel. No width moves. This is a behaviour change with its own Change Note.
- A test or a script of a Consumer's that finds the button of a **filtered** column by its exact name
  *Column menu* no longer finds it: the name is *Column menu, filtered*. The class `ex-menu-button`, the
  button's place and what a press does are unchanged.
- The README's blotter recording filters by desk and shows that column's header, so it is recorded
  again.
- ExSheet and ExPivot draw their grids without the column menu, so their headers are unchanged.
- The Wrappers set nothing for it: the picture is a mask in the header's colour, in the box the Geometry
  Tokens already size, and under forced colours it is painted in `CanvasText`.
