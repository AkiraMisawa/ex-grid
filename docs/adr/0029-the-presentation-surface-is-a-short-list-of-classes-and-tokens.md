# The presentation surface is a short list of classes and tokens. Everything else in the DOM is implementation detail

[ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) split what travels in
CSS from what travels in C#. This ADR names **what, exactly, a Wrapper or a Consumer may write
CSS against** — and, as importantly, what it may not, so the internals can keep changing without
an ADR.

## The stable classes

A class in this table is a contract: it is not renamed, and what it marks does not change
meaning, without an ADR. It marks a **meaning**, never a mechanism.

| Class | Marks |
|---|---|
| `ex-grid` | the instance root; `ex-loading` joins it while an answer is in flight, and while the grid is **Prerendered** (added 2026-09-25) |
| `ex-header`, `ex-header-cell` | the header band and its cells |
| `ex-header-group` | a Header Group's rectangle ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)) |
| `ex-align-left`, `ex-align-center`, `ex-align-right` | an explicit alignment (`Auto` adds nothing — ADR-0016) |
| `ex-row` | one row; `ex-placeholder` joins it for a row not painted with real data ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)) |
| `ex-row-group`, `ex-row-total` | Row Kind ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)) |
| `ex-row-stripe` | a Row Stripe: a row at an odd position in the whole result, while stripes are on ([ADR-0038](./0038-row-stripes-are-painted-from-the-rows-absolute-position.md)) |
| `ex-cell` | one cell; `ex-cell-numeric` joins it on the numeric presentation ([ADR-0016](./0016-column-width-and-overflow.md)) |
| `ex-pinned` | a Pinned Column's cell, body or header |
| `ex-state-stale / -missing / -error / -modified` | Cell State ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)) |
| `ex-tone-positive / -negative` | the Column's tone rule's answer about the value — a gain, a loss ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)); a meaning, never a colour, and `None` adds nothing |
| `ex-range`, `ex-focus` | a selection rectangle and the Focus outline ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)) |
| `ex-action`, `ex-interactive` | the grid's own action button; a Consumer's control that takes its own pointer events ([ADR-0020](./0020-action-and-template-columns.md)) |
| `ex-action-chosen` | the one action Space will fire, while a cell with several actions is Interactive — on that button alone, and only then ([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)) |
| `ex-editor` | the floating Cell Editor; `ex-editing` joins the root while it stands ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) |
| `ex-resize-guide` | the vertical guide a column resize drags, applied on release ([ADR-0016](./0016-column-width-and-overflow.md)) |
| `ex-drop-indicator` | where a dragged header will land; it stops at the pinned boundary rather than promising a drop that is refused ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) |

## The internal classes — and why the line sits exactly there

`ex-scroller`, `ex-spacer`, `ex-viewport`, `ex-gap`, `ex-selection`, `ex-selection-pinned`,
`ex-header-groups-pinned`, `ex-focus-row`, `ex-pager`, `ex-status`, `ex-resize-grip`,
`ex-menu-button`, `ex-popover`, `ex-popover-list`, `ex-popover-actions`, `ex-editor-pinned`,
`ex-announce` are
**implementation detail**. They may be renamed, merged or removed by any commit — the Focus
band's contract is its token (`--ex-focus-row-fill`) and the parameter that turns it on, never
the element; the pager and status line are the minimal built-in chrome
([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)), replaceable when the Chrome
seams land ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)).

`ex-announce` — the visually hidden live region that states the selection extent
([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)) — is internal
for a different reason from the rest, and the reason is worth the line: it carries no box that the
virtualisation depends on, but **any style reaching it can silence it**. `display: none` or
`visibility: hidden` removes it from the accessibility tree entirely, and making it visible puts a
sentence of English in the middle of someone's design. ADR-0030 already forbids a Wrapper to touch
ARIA; this keeps the element that carries the announcement out of reach by the same logic.

The line is not arbitrary: the internal list is precisely **the elements whose box, `position`,
`overflow` or `transform` carries the virtualisation**. A selector that can reach them is a
selector that can break P1–P6
([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)) from a stylesheet,
which is why the prohibition in
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) is expressible
mechanically: *styling a class not in the stable table is unsupported.* The stable classes, by
contrast, mark meanings — a row, a state, a role — and every load-bearing property they do carry
(`height`, `padding`, the pointer-events scheme) is emitted inline or token-fed by the core,
where a stylesheet cannot displace it (ADR-0027).

The one thing a Wrapper legitimately wants from an internal element is the scrollbar's look. It
gets tokens instead of the element: `--ex-scrollbar-width` and `--ex-scrollbar-color`, applied by
the core's own stylesheet to the scroller. Narrowing the scrollbar changes the gutter, the
browser reports it, and the geometry follows — the chain
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) already built.

## The tokens

Geometry tokens (written inline by C#, read-only — ADR-0027/0028):

```
--ex-row-height   --ex-header-height   --ex-font-size   --ex-cell-padding-x
--ex-action-padding-x   --ex-action-border-width   --ex-action-gap
--ex-menu-button-width   --ex-menu-button-inset   --ex-sort-mark-width
```

*(The menu-button pair was added when review found the ▾'s 16px/6px living as a CSS
literal beside a C# estimate that ignored it — the exact pairing defect ADR-0027/0028
dissolved, re-entering through a new element. `--ex-sort-mark-width` was added for the same
reason on 2026-09-26: the header estimate began charging the sort mark
([ADR-0016](./0016-column-width-and-overflow.md)), and the mark's `" ▲"` was a stylesheet
literal.)*

Visual tokens (defaults in `ex-grid.css`, overridden on any ancestor). The existing eleven are
kept with their names and defaults; the vocabulary this ADR fixes is:

| Area | Tokens |
|---|---|
| Ground | `--ex-background`, `--ex-color`, `--ex-font-family`, `--ex-font-weight` |
| Header | `--ex-header-background` *(exists)*, `--ex-header-color`, `--ex-header-font-weight`, `--ex-header-rule-color` |
| Rules | `--ex-row-rule-color`, `--ex-column-rule-color`, `--ex-rule-width` — painted as gradients/inset shadows, **never borders** ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)) |
| Hover | `--ex-row-hover-background` *(unimplementable as stated — see the correction below)* |
| Pinned | `--ex-pinned-background` *(exists)* |
| Selection | `--ex-selection-fill` *(exists)*, `--ex-focus-row-fill` *(the Focus band, [ADR-0008](./0008-selection-is-painted-by-an-overlay.md))*, `--ex-selection-outline` *(reserved — the border Excel draws around the range's perimeter; decided with the selection paint polish)*, `--ex-focus-outline` *(exists)*, `--ex-grid-focus-outline` *(exists)* |
| Cell State | the six `--ex-state-*` *(exist)* |
| Tone | `--ex-tone-positive-color`, `--ex-tone-negative-color` — default `inherit`, so a declared tone paints nothing until a theme says what colour it is ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)) |
| Row Kind | the four `--ex-row-group/total-*` *(exist)* |
| Row Stripe | `--ex-row-stripe-background` — a faint neutral by default, so turning stripes on shows them; unpainted under `forced-colors`, because a stripe carries no meaning ([ADR-0038](./0038-row-stripes-are-painted-from-the-rows-absolute-position.md)) |
| Placeholder / loading | `--ex-placeholder-background`, `--ex-loading-opacity` |
| Editor | `--ex-editor-background`, `--ex-editor-color`, `--ex-editor-outline` |
| Column gestures | `--ex-resize-guide-color`, `--ex-drop-indicator-color` — the guide and the indicator are painted, not laid out, so neither is metrics-bearing (ADR-0011/0016) |
| Scrollbar | `--ex-scrollbar-width`, `--ex-scrollbar-color` |

Defaults stay on system colours (`Canvas`, `Highlight`, `currentColor`) so the bare grid follows
the host's colour scheme and forced-colors settings (ADR-0027). `--ex-font-family` and
`--ex-font-weight` are the **metrics-bearing** pair: setting either owes the core new
`CellMetrics` (ADR-0027).

## There are no Class or Style parameters

Deliberately — on the root, on a column, on a cell, anywhere. A `Style` parameter is an unguarded
door back into everything ADR-0027 closed (`style="height: 40px"` on a cell is geometry from
CSS), a `Class` parameter is an invitation to rules on arbitrary elements, and a per-cell style
string is an allocation in the render loop (P5). The escape hatches are exactly three, each with
its price on the label: **Visual Tokens** for instance-wide appearance, **closed enums painted as
interned classes** (Cell State, Row Kind, alignment, tone) for per-column and per-cell meaning, and a
**Template Column** for arbitrary content, paid for per column
([ADR-0020](./0020-action-and-template-columns.md)).

## State hooks: how each state reaches CSS

| State | Mechanism | Why this one |
|---|---|---|
| hover | `:hover` | the browser alone; no event, no render, no wire round trip on Server |
| selected / focused cell | **the overlay only** | per-cell classes were measured and refused ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md): max 19.7ms vs 2.1ms). A Wrapper styles the fill and outline tokens; "restyle the selected cell's text" is not offered, and that is a recorded limitation, not an oversight |
| grid holds the keyboard | `:focus-visible` on the root | [ADR-0018](./0018-multiple-instances-must-be-independent.md) requires the live grid to be tellable |
| editing | `ex-editor` on the floating input; `ex-editing` on the root | one element exists; no row knows |
| the chosen action, inside a cell | `ex-action-chosen` on one button, outlined with `--ex-focus-outline` | the overlay cannot draw it — a button's box is the browser's layout of a label, not arithmetic the core holds — and the keyboard never leaves the root, so `:focus-visible` has nothing to match. One button in one row, on a discrete keypress ([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)) |
| error / stale / missing / modified | Cell State classes | the closed vocabulary below |
| a gain / a loss | tone classes, from the Column's rule | the Consumer says when; the theme says what colour; the grid derives nothing from a sign on its own ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)) |
| pending / dirty / disabled / anything else | **mapped onto Cell State by the Consumer or Wrapper** | ADR-0006: the Consumer's vocabulary does not enter the grid — a design system's does not either. `dirty` is `modified`; `pending` is `stale` or `missing`; a state that maps to none of the five is a Template Column or a case for amending ADR-0006, not a per-wrapper class |
| loading | `ex-loading` + Placeholder rows | one mechanism ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)) |
| Prerendered | `ex-loading` on the root | *(added 2026-09-25, with the Blazor Server host.)* A prerendered grid is painted before anything can reach it — no key, no pointer, no scroll. It is waiting for the same thing a loading grid waits for, an answer that has not arrived, so it wears the same state rather than a second one a Wrapper would have to style twice. [ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md) says what it tells assistive technology |
| row role | Row Kind classes | |
| row stripe | `ex-row-stripe`, from the row's absolute position | the position is a parameter every row already re-renders on (ADR-0033), so the class costs no render; `:nth-child()` would count painted siblings and crawl ([ADR-0038](./0038-row-stripes-are-painted-from-the-rows-absolute-position.md)) |

**Classes, not `data-*` attributes, and never ARIA.** The class strings are composed once and
interned (`CellClasses` / `RowClasses`), so painting a state allocates nothing on the render path
— P5. A parallel `data-state` attribute would carry the same information at the cost of one more
attribute per cell and a second vocabulary to keep in step, and buys nothing CSS can use that a
class cannot. ARIA attributes are refused as styling hooks from the other direction: they are
**semantics**, their shape will be decided by the accessibility work
([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)), and a
Wrapper styling `[aria-selected]` would weld the two surfaces together — besides which selection
deliberately has no per-cell attribute to select on at all.

**No transitions inside the row Viewport** — invariant P8 (ADR-0027). Rows are recycled, so a
transition animates from another row's value. The tokens above set colours, not `transition`s.

## Consequences

- **The stable list is the test surface.** The bUnit layer already selects `.ex-state-*`,
  `.ex-action`, `.ex-placeholder`; those selectors are now contractual rather than convenient.
  Internal classes appearing in a test outside the component's own suite is a smell the review
  can name.
- **`ex-grid.css` needs a small diff, not a rewrite**: the ground/hover/rule/scrollbar tokens and
  the `:hover` rule are additive; the literals move to tokens per ADR-0028. Nothing visible
  changes by default.
- **A forced-colors block is required work** (ADR-0027): every state above restated in system
  colours, still painted rather than laid out.

## Open

- ~~The ARIA surface~~ — resolved twice over, in agreement. The split decided here — structural
  ARIA now, interaction ARIA inside the editor/Interactive-mode task (ADR-0010/0020), because
  ARIA describing interaction that does not exist yet would be a lying accessibility tree — and
  the structural half is designed in
  [ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md): the root
  owns the surface, counts and indices state the totals the virtualised DOM cannot, and
  **selection is described by the root, never attributed per cell** — the one real decision,
  taken to keep ADR-0008's memoisation. Ownership stays the core's, never a Wrapper's
  ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).
- **The editor's classes and tokens were reserved with a named trigger, and the trigger fired**:
  the Cell Editor exists (ADR-0010), `ex-editor` marks it, `ex-editing` joins the root while it
  stands, and the three `--ex-editor-*` tokens carry its look — exactly the namespace fixed here
  in advance, with the geometry half needing nothing because the editor's box is the cell's
  (ADR-0028). `--ex-selection-outline` remains reserved, settled with the selection paint
  polish; the Focus band half of that polish has landed (`--ex-focus-row-fill`,
  [ADR-0008](./0008-selection-is-painted-by-an-overlay.md)).

## A correction found in implementation: the hover token could not work as declared — and how it now does

`--ex-row-hover-background` was specified as "via `:hover`; costs no C#". **`:hover` never
matches a row**: every element under the row Viewport is `pointer-events: none` — that is the
delegated hit-test the mouse machinery is built on (ADR-0004/0008) — and an element that takes
no pointer events takes no hover either. The token was therefore not implemented, and the row
above stayed in the table marked *(unimplementable as stated)* while the two honest options
stood: (a) drop it, or (b) a C#-driven hover class from a permanent `mousemove`, which
contradicts "costs no C#" and, on a Blazor Server host, ADR-0008's reason for attaching the
move handler only during a drag.

**Decided 2026-09-01, when two callers arrived at once**: the `ExGrid.MudBlazor` survey
([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)) found
`Hover` on every MudBlazor table and the first Consumer expects a pointer-row highlight; and
[ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md) gave the
validation popover a hover trigger. Neither (a) nor (b): the cell under the pointer is
**reported by JavaScript only when it changes** — the fifth allowlist entry of
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), which holds the whole argument
— and the hover row is then painted **as a band by the overlay**, exactly like the Focus band
(`--ex-focus-row-fill`, ADR-0008): one element per layer, no class on any row, no row
re-rendered. The token keeps its name and becomes the band's fill. "Costs no C#" was wrong and
is withdrawn: hover costs one render per row the pointer crosses, which is the Focus band's
price and not the per-frame price (b) would have paid. The state-hooks table above still says
`:hover` in its hover row; read it as *the overlay band, driven by the fifth entry* — the
mechanism column was written before this correction and is left so the correction stays visible.

## A second correction: what "read-only" can and cannot mean for a Geometry Token

Measured while writing the browser suite: the inline Geometry Tokens on the instance root beat
an ancestor's value and beat a stylesheet rule targeting `.ex-grid` — the two override routes
this ADR supports — but **CSS offers two routes past an inline declaration that no emission
strategy on the root can close**: an `!important` stylesheet declaration, and a rule that
re-declares the token on a *descendant* (`.ex-row { --ex-row-height: … }`), where inheritance
from the root is simply replaced. Both are already unsupported by this ADR's own rule —
geometry from CSS is the defect class ADR-0027 exists to remove, and `.ex-row`'s stable-class
contract licenses styling its *appearance*, not re-declaring the core's tokens — but the
earlier claim that a stylesheet "cannot displace" a token-fed property was stronger than CSS
permits. The contract is stated precisely now: **the supported override routes cannot move
geometry; a stylesheet that reaches for the unsupported ones is writing outside the contract,
and what breaks is on it.** The browser suite pins the supported routes.
