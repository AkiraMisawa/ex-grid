# A Header Group draws a toggle and notifies, and the Consumer holds its columns

*Decided with the user on 2026-10-27 in the project thread "展開できる Header Group を計画に入れる" ("Put
expandable Header Groups into the plan"), from the user's specification of the same day ("ExGrid:
expandable Header Groups spec"). The user asked for a group that expands when pressed and collapses when
pressed again, and for a value the user set to stay the user's while it is hidden. Asked about the
specification's open points, the user put it into 0.3.0 ("入れる", "put it in"), asked for the toggle's
and the mark's look to be changeable and to match each framework's design and theme
("切り替えのスタイルは変更可能にしたい。各フレームワークのデザイン・テーマに合うようなマークを使うのがいいでしょ。隠されマークしかり",
"I want the toggle's style to be changeable. A mark that fits each framework's design and theme is best,
and the same goes for the hidden mark"), asked for Excel's keys ("エクセルと同じ方がいいかな", "the same as
Excel would be better"), and agreed that a case of behaviour new to the Script Core need not go in on the
C# core first ("OK。というかその指示は見直しが必要そうだね", "OK. That instruction looks like it needs
revisiting anyway"). Excel has no key of its own for the step (section 4). Offered Alt+Shift+↓ and ↑,
Excel's key tips, or both, the user chose Alt+Shift+↓ and ↑; asked why Alt+Shift+→ and ← are not used,
the user accepted section 4's answer ("OK"). The specification's other open points (the toggle alone
takes the press, the mark covers the whole group, ExSheet and ExPivot later) are taken as it recommended
them.*

This amends [ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md) ("no
collapse"), [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md) (the
Focus across a change of columns), [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)
(classes and tokens) and [ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md)
section 5 (where a shared case goes in first).

## Context

The first Consumer prices trades whose legs carry many fields. A leg's first-level fields are always
shown; its second-level fields (a day count, a calendar, a stub) are wanted only now and then. The
Consumer wants a Header Group over a leg to open and close those columns in place, and wants a field the
user set to stay set while its column is closed, and to be seen to be set.

What ExGrid had on 2026-10-27 (`main` at `a134ae1`):

- **A Header Group paints and is a drag unit, nothing else.** ADR-0032 names "no collapse" among the data
  behaviours it refuses, so that the Column stays a flat runtime object and no tree enters the grid.
- **Columns are the Consumer's.** The column menu's Hide notifies `onColumnHidden(column)` and the
  Consumer drops the column; nothing in the grid hides one.
- **A change of the columns drops the Selection, the Anchor and the Focus** (ADR-0011), because a
  position no longer names the cell it named. A keyboard user who closes a group loses their place each
  time.
- **A Cell State** (ADR-0006) is how a Consumer shows that a cell is in an unusual state; the first
  Consumer marks a field the user set as `modified`.

Keeping a hidden value is the Consumer's: its edit log holds the edit whether a column is shown or not,
and closing a group must not turn the user's value back into a generated one. What the grid must add is
that a closed group which hides a user's value says so. Without a mark, a closed leg looks like a leg on
its defaults, which is the plausible-but-wrong picture the spine's first rule refuses.

## Decision

### 1. The grid draws the toggle and notifies; the Consumer holds the columns

`GridHeaderGroup` gains three members, and the grid options one callback:

```ts
export interface GridHeaderGroup {
    readonly columns: readonly string[];
    readonly label: string;
    readonly tier?: number;
    readonly tierSpan?: number;
    readonly id?: string;              // names the group in the notification
    readonly expanded?: boolean;       // given: the group draws its toggle, ▾ when true, ▸ when false
    readonly hiddenMark?: HiddenMark;  // given only while collapsed (section 6)
}

export interface HiddenMark {
    readonly state: Exclude<CellState, 'normal'>;  // the Cell State the hidden columns hold, e.g. 'modified'
    readonly reason: string;                       // e.g. "2 user-set fields in hidden columns"
}

// GridOptions
readonly onHeaderGroupToggled?: (toggle: HeaderGroupToggle) => void;

export interface HeaderGroupToggle {
    readonly id: string;
    readonly expanded: boolean;        // the state the press asks for
    readonly focusColumn?: string;     // the Focus's column when the press was taken, if there was a Focus
}
```

**The grid never adds or removes a column.** A press raises `onHeaderGroupToggled`, and the Consumer
answers by pushing the columns it wants, the group's members to match, and the group's new `expanded`.
Whether a group is open is View State, the Consumer's as the column order and the pinning are
(ADR-0011); a Consumer may keep it per user. The grid holds no expansion of its own: a group shows
whatever `expanded` it was last given, and a press the Consumer does not answer changes nothing on
screen.

Blazor's `HeaderGroup` record gains `string? Id`, `bool? Expanded` and `HiddenMark? HiddenMark`, and Blazor's
ExGrid `OnHeaderGroupToggled`, the same shapes. `Expanded` is nullable because "not given" (no toggle) and
`false` (collapsed) are different declarations, and a `bool` would declare every group collapsed. Blazor's ExGrid only carries them to the Script Core
([ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)); the C# core does
not take them (section 10).

### 2. Refused at declaration, by name

In ADR-0032's manner, a declaration the grid cannot honour is refused when it is given, naming the
group:

- `expanded` without `id`, and two groups with the same `id`.
- `expanded` on any group while `onHeaderGroupToggled` is not given. A toggle that does nothing when
  pressed is not drawn, as a column menu command is enabled only while it is heard.
- `hiddenMark` on a group whose `expanded` is not `false`. An open group's cells show their own Cell
  State, so the mark is for a closed group alone.
- A `hiddenMark` whose `reason` is empty. The mark must say what it hides.
- `expanded` on a group whose `label` is empty, a spacer. Its toggle would be named "Expand" with no
  group named (section 8), and a spacer is no group a reader opens.

**A collapsed group keeps at least one member shown.** The toggle stands on the group's rectangle, and a
group with no member is already refused (ADR-0032). A Consumer whose group would hide every column keeps
one shown, a summary column say; a Consumer that drops the group instead drops its toggle with it, and
nothing then reopens the group.

### 3. The toggle alone takes the press

The toggle is a button at the left of the group's rectangle (the grid lays out left to right only,
[ADR-0031](./0031-the-grid-lays-out-left-to-right-only.md)). A press on it notifies. A
press elsewhere on the rectangle stays what ADR-0032 made it, the start of the group's drag, and never
toggles: a press that might become a drag is not also a command, and a toggle that fires on a released
drag would make the misfire ADR-0032 refused for an overshoot.

- **Touch**: a tap on the toggle is a press. A swipe over the header scrolls, as ADR-0032's note of
  2026-10-19 has it.
- **Pointed At** ([ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md)): while the grid is pointed at, a press on the
  toggle is a press on the group's rectangle, which hands over nothing and tells the reason, as
  ADR-0058's table has it, and toggles nothing.
- **The Input Order** ([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)): the
  toggle is a control of the grid's own (`data-ex-claims`), so its press is numbered and taken in its
  turn by the click path that already exists. An open edit is settled by it as by a press on a column
  header.
- **The Hidden Mark's corner** (section 6) is the mark's own, as the toggle's box is the toggle's: it takes
  the pointer so that its reason shows as its tooltip, so a press on those few pixels neither drags nor
  toggles. *(Decided with the user, 2026-10-27, after the implementation's review found the corner outside
  the rule above: the tooltip stays where the eye goes, and the drag is lost from a 6px corner only.)*

### 4. The keys are Alt+Shift+↓ and Alt+Shift+↑

Excel has no key of its own for opening or closing an outline group. Its keyboard route is the ribbon's
key tips, Alt, then A, then J (Show Detail) or H (Hide Detail), pressed one after another, and its
outline keys proper are Alt+Shift+→ and Alt+Shift+←, which make and undo a group
([Exceljet](https://exceljet.net/videos/shortcuts-to-group-ungroup-and-outline),
[Sage City](https://communityhub.sage.com/site_help/b/excel-tips-tricks-list/posts/how-to-expand-and-collapse-grouped-columns-using-keyboard-shortcuts)).
In a browser the key-tip route cannot reach the grid: a released Alt gives the keyboard to the browser's
own menu, so the A and the J that follow never arrive. Google Sheets, beside Excel's Alt+Shift+→ and
Alt+Shift+←, expands with Alt+Shift+↓ and collapses with Alt+Shift+↑ (Option+Shift on a Mac,
[Google](https://support.google.com/docs/answer/181110?hl=en)). The grid takes those:

| Key (canonical form, ADR-0012) | Meaning |
|---|---|
| `Shift+Alt+ArrowDown` | Expand the lowest-tier collapsed group over the Focus's column |
| `Shift+Alt+ArrowUp` | Collapse the lowest-tier expanded group over the Focus's column |

- The Focus never enters the header (ADR-0012, ADR-0032), so the keys act from a cell: from any member
  column of the group, since a collapsed group's hidden columns have no cell to stand on.
- Neither key types a character on any platform, so neither takes a keystroke from Overwrite. The
  specification's first proposal, Alt+Shift+= and Alt+Shift+-, types ± and — on a Mac.
- The keys are the grid's only on a grid given `onHeaderGroupToggled`, as F8 is the grid's only on a
  grid given `findError` ([ADR-0381](./0381-a-rule-over-the-whole-collection-is-judged-after-apply-and-refused-at-the-consumers-save.md)).
  A grid without it leaves both to the page, and a Consumer that declares either on such a grid keeps it
  ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 14). On a grid given it, either among
  the declared keys is refused at declaration, naming this ADR. No Consumer that does not toggle breaks.
- On a grid given it, where no group over the Focus's column answers the key, the grid does nothing with
  it and lets it reach the page.
- While an editor is open the keys are the editor's, as every key is.
- Alt+Shift+→ and Alt+Shift+← are left unused. In Excel they make and undo a group, which in ExGrid is
  the Consumer's declaration, so the grid has nothing for them to do; to open and close with them would
  give an Excel user a different result for the same key.
- `Alt+ArrowDown` stays the column menu ([ADR-0044](./0044-alt-down-opens-one-popover-the-commands-above-the-filter.md)).

### 5. The notification asks for the opposite of what was shown

`expanded` in the notification is the opposite of the `expanded` the group showed when the press or the
key was taken in its turn. Two presses made before the Consumer answers raise the same notification
twice, and the Consumer that applies both lands where one press would: the result does not depend on
when the answer arrives (the spine's sixth rule). A key decides which group it acts on from the
groups as they were shown at its turn, never from a push still on its way.

### 6. The Hidden Mark

A collapsed group the Consumer gives a `hiddenMark` draws a **Hidden Mark** on its rectangle: the mark a
cell in that Cell State wears, painted with that state's token, so a reader who knows what a modified
cell looks like knows what the mark says. Its `reason` is the mark's accessible description and its
tooltip (`title`, as an action button's label is, ADR-0020).
*(2026-10-27: `missing`'s token is a cell's faint tint, which cannot be seen on a corner of the header, so the
mark paints that token's colour at full strength; the others are opaque already.)*

- **It covers the group, not a row.** It says that something in the hidden columns, in some row, holds
  that state. A Consumer that wants each row to say so marks the group's first shown column's cell with
  a Cell State whose message names the hidden field, which the grid already draws.
- The Consumer computes it, from whatever it knows of the hidden columns. The grid does not look into a
  column it was not given.

### 7. The look is the stylesheet's, so each design system draws its own

The user asked that the toggle and the mark match each framework's design and theme. They do so the
way the rest of the grid does, through classes and Visual Tokens
([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md), ADR-0029), and not through a
Chrome slot:

- **Classes.** `ex-header-group-toggle` on the button, which carries `aria-expanded`;
  `ex-hidden-mark` with `ex-hidden-mark-{state}` on the mark.
- **Visual Tokens.** `--ex-header-group-collapsed-icon` and `--ex-header-group-expanded-icon`, each a CSS
  image the stylesheet paints as a mask in `--ex-header-group-toggle-color` (default the header's
  colour); the mark's colour is its state's existing `--ex-state-*` token, and its shape the
  stylesheet's, Excel's corner triangle by default as a modified cell's is (ADR-0029's fourth
  correction).
- **Each Wrapper's stylesheet sets them from its design system**: `@ex-grid/mantine` the chevron
  Mantine's own Accordion draws, `@ex-grid/mui` and `ExGrid.MudBlazor` Material's chevrons, which their
  libraries draw, each in its theme's colours through the CSS variables ADR-0390 section 5 maps. A
  Consumer's own design system sets the same tokens, or restyles the classes.
- **Geometry stays the core's.** The toggle's width is a Grid Metric, emitted inline as
  `--ex-header-group-toggle-width` with the others, and the label's room is the rectangle less it. A
  group label still never feeds Auto width (ADR-0032).

A slot was considered and set aside. Everything a toggle does but its picture is the core's (the press,
`aria-expanded`, its label, its box), so a slot would hold a glyph, which a token holds without a mount
per group, without a render when the theme changes, and the same way under Blazor as under React.

### 8. Words and accessibility

The toggle's accessible name is the group's label in a template from the word tables, "Expand {0}" and
"Collapse {0}" (ids `header-group-expand` and `header-group-collapse`, in `japaneseGridWords` too), so a
Mantine Wrapper and a Consumer's label function word it as every other label (ADR-0370 section 10). The
group stays a `columnheader` cell ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md));
the toggle is a button inside it, out of the tab sequence like every control of the grid's own, since
the root is the grid's one tab stop.

### 9. The Focus survives the toggle

ADR-0011 drops the Selection, the Anchor and the Focus when the columns change. A toggle keeps the drop
for the Selection and the Anchor, and keeps the Focus: see ADR-0011's section "A Header Group's toggle
keeps the Focus". In short, the first columns push that shows the toggled group in the state the
notification asked for puts the Focus on the same row, in `focusColumn` when it is still there and
otherwise in the group's first member. The row is found by its Row Key, or by its position while the row
sequence version has not changed; when neither finds it, the Focus is dropped as before.

### 10. Where it is built, and where its cases go in first

In the Script Core (`@ex-grid/core`): the declaration and its refusals, the toggle, the keys, the
notification, the Focus kept and the mark. `@ex-grid/react` passes the members and the callback
through. The Mantine and MUI Wrappers, and `ExGrid.MudBlazor`, set the tokens. Blazor's ExGrid on the
Script Core carries the record and the callback.

**The C# core does not take it.** It is removed at ADR-0350 section 7's step 3, before 0.3.0 is tagged,
and a behaviour it never had is no move for a shared case to prove. ADR-0350 section 5's rule that a
case is green on the C# core first therefore holds for behaviour the C# core has: a case of behaviour new
to the Script Core goes in on the Script Core alone, in a file whose `about` says so, read by a kind no
C# runner reads (ADR-0350's note of 2026-10-27).

### 11. What it does not do

- The grid does not hide a column, hold an expansion, or know a column tree. Membership is still the
  Consumer's declaration (ADR-0032).
- No group sort and no aggregate.
- No per-row Hidden Mark, and no gathering of hidden cells' states into a shown cell.
- ExSheet and ExPivot do not take it yet. ExPivot's column Items collapse by their own rule; using this
  toggle for them is a decision of its own.
- No Consumer rebinding of the two keys on a grid that toggles, as no key the core answers can be rebound
  (ADR-0050, item 14).

## Considered options

- **A toggle outside the grid**, the Consumer swapping columns from its own button. It needs nothing of
  the grid, and is what the first Consumer runs until this lands; it still loses the Focus at every
  toggle, and the user's ask is a press on the group.
- **The grid hides the columns itself**, from a declared tree of levels. It puts a tree and a piece of
  View State into the grid, which ADR-0032 and ADR-0011 keep out, and a Consumer would have two places
  saying which columns are shown.
- **A press anywhere on the group's label toggles.** Nearer to "press it and it opens", but it is the
  same place a group's drag starts, and on touch the same place a swipe starts.
- **A Chrome slot for the toggle and the mark** (section 7).
- **Excel's key tips, Alt then A then J or H** (section 4).

## Consequences

- ADR-0032 is amended by a note: "no collapse" now reads that the grid collapses nothing and notifies.
- ADR-0011 gains the section on the Focus across a toggle.
- ADR-0029 gains the classes, the three Visual Tokens and the Geometry Token.
- ADR-0350 section 5 gains the note on cases of behaviour new to the Script Core, and CLAUDE.md,
  `src/npm/README.md` and `tests/cases/README.md` say the same.
- `CONTEXT.md` rewrites Header Group and adds **Hidden Mark**.
- The Definition of Done gains HG-18 to HG-24.
- Tests: layer 1 cases for the refusals, the notification's value, the group a key picks and the Focus
  kept; layer 2 for the pass-through in React and Blazor; layer 3 for only what a browser shows (a press
  on the toggle, the mark painted in its state's colour, the Focus kept after a key, and two quick
  presses over the Server host's latency), under the standing rule that a layer-3 test earns its run.
- The Docs Site's Header Group page gains an Example under the Built-in, Mantine and MUI Chromes and under
  Blazor, and a Change Note says what ExGrid adds.
