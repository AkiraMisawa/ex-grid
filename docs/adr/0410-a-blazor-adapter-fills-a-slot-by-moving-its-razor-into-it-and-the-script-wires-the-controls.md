# A Blazor adapter fills a slot by moving its Razor into it, and the script wires the controls

*Proposed on 2026-10-27 in the project thread "ExPivotのBlazor包みを仕上げる" ("Finish ExPivot's Blazor
wrapper"), as every product's Blazor adapter reached its Chrome at once, and decided by the user on
2026-10-28: option A, sections 1 to 6 below.*

This fills in [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)
section 1's "fills Chrome and Template slots with Razor components", which says what a Blazor adapter
does but not how, and amends [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)'s C# Chrome
contracts for the Script Core.

## Context

On 2026-10-27 (`main` at `90c1036`):

- **The Script Core's slots are mount functions.** A Chrome slot is `(slot, context) => cleanup`
  (ADR-0350 section 5): the core makes the slot element where the part stands, hands a context of data
  and functions, and calls the cleanup when the part goes. ExGrid has eleven slots, ExPivot ten, ExSheet
  and ExChart their own.
- **React fills a slot through a portal, and a hook wires its controls.** `useMenuItems`,
  `useFilterPanel`, `useCellEditor` and the rest hand the contents props to spread: the role, the
  `data-ex-claims` an input is numbered by (ADR-0240), and handlers that ask the core what a key means
  (`resolveKey`), take the focus a request asks for (`takeFocus`, ADR-0204) and report a popup of their
  own. The core reads a text field's value from the field drawn, never from the framework's state.
- **The Blazor adapters refuse every slot.** `ExGrid.Script.ExGrid` refuses a Template column by name and
  takes no Chrome; `ExPivot.Script.ExPivot` takes no Pivot Chrome. `ExGrid.MudBlazor`,
  `ExPivot.MudBlazor`, `ExSheet.MudBlazor` and `ExChart.MudBlazor` still run only over the C# core.
- **The C# Chrome contracts were written for a core in .NET.** `IGridChrome`'s and `IPivotChrome`'s
  contexts call into the core synchronously: `ColumnMenuContext.ResolveKey` answers a key at once,
  `CellEditorContext.TextChanged` reports each text before the Enter that commits it arrives on the same
  circuit. Over the Script Core both cross the circuit, and on the Server host a key resolved in .NET or a
  text sent from .NET reaches the core a round trip after the Enter the browser has already handed it:
  the edit would commit the text before the last keys, which is spine rule 6 broken.
- **The ExSheet, ExChart and ExBoard Blazor threads wait on this**, so it is decided once, here.

## Options considered

| | A. Move the Razor into the slot (portal) | B. A Blazor root component per slot | C. The MudBlazor Wrappers become a theme only |
|---|---|---|---|
| How | The adapter renders each standing slot's fragment in its own tree, beside the root; its script moves the element into the slot | The script adds a root component on the slot (`Blazor.rootComponents.add`) | The Script Core's built-in markup is drawn, coloured by a `MudTheme` mapped to Visual Tokens |
| MudBlazor's components in the grid | Yes, as today | Yes | No: the built-in menus, panels and editor, in MudBlazor's colours |
| Cascading values, the Consumer's services and component state | Kept: the fragment renders in the Consumer's tree | Lost: a root component has no parent, so `MudThemeProvider`'s and a page's cascading values do not reach it | Nothing to keep |
| What a Consumer adds to `Program.cs` | Nothing | `RegisterForJavaScript` for every component a slot may hold; Blazor Server's `MaxJSRootComponents` bounds the slots open at once | Nothing |
| A Consumer's own Razor Chrome, and Template cells | Both | Both, each registered | Neither Chrome; Template cells still need A's mechanism |
| Cost | Every C# context rewritten over the Script Core's, and each MudBlazor Wrapper rewritten against it | A's, plus the registration and the lost context worked around | The least: each MudBlazor Wrapper shrinks to a theme mapping, Cell Metrics and a density |

**A is chosen** (recommended, and the user's choice). It is what ADR-0370 section 1 already says the Wrappers do ("fills the Chrome seams
with MudBlazor's components"), it keeps the MudBlazor Wrappers in step with the Mantine and MUI ones, which
fill the same slots with their kits' components (ADR-0390), and a Template cell needs its mechanism
whatever is chosen. B costs A's work and loses what A keeps. C is the cheapest and changes what a
MudBlazor Wrapper is: a Blazor Consumer would get MudBlazor's colours but none of its controls inside the
grid, and could write no Chrome of its own.

## Decision

### 1. The Razor renders in the adapter's tree and the script moves it into the slot

The component renders, beside its root and never inside it, one holder per standing slot: a hidden
element whose only child is the element the slot's fragment is drawn in. That child never gains siblings,
so Blazor never inserts beside it, and its own children are Blazor's to change as they are anywhere.
When the core mounts a slot, the adapter's script asks .NET for the fragment; once .NET has rendered it,
the script moves the child into the slot. At the cleanup the script moves it back into its holder,
synchronously and before the core takes the slot away, and .NET removes the holder at its next render.

Blazor keeps the element's identity, its delegated events (heard on the document, ADR-0240) and the Razor
component's state while it stands in the slot, and the fragment renders under the Consumer's cascading
values and services, `MudThemeProvider`'s and `MudPopoverProvider`'s among them.

### 2. Razor draws; the script wires

What a slot's controls do on a key, a press, a focus request or a popup of their own is wired by the
adapter's script on the moved markup, from attributes the contents write: a menu item's index, the panel's
first, last and search controls, the value list, the text field the core reads. The wiring is the logic
of React's hooks (`useMenuItems`, `useFilterPanel`, `useFindPanel`, `useCellEditor`, the Formula Bar's and
the Name Box's), moved into plain DOM functions in the Script Core's packages that React's hooks and
Blazor's script both call, so the two adapters cannot drift.

So a key on a MudBlazor menu item is resolved in the browser against the core's place for the menu, a
focus request is taken in the browser, and the text the core commits is read from the field drawn, never
from .NET. Nothing a key means waits on the circuit.

*Note of 2026-10-28, as the filter panel was built:* a choice is read in the browser too, from a control
the browser hears, never from a component's .NET handler. A value's checkbox, the search box, an
operator's `select`, a condition's value and the join each carry an attribute
(`ScriptSlotAttributes.Filter…`), and the script hands the change to the core as the browser fires it.
A checkbox names its entry by index, read against the view the contents were drawn from, whose number the
element they are drawn in carries (`data-ex-version`): the value the user saw, though the core's list has
moved since. A text field is drawn with no value bound to the view, which arrives a round trip behind
the typing (SRV-7); a value field is given the operand it opens with, and nothing after.

### 3. What crosses the circuit

- **To .NET:** the slot's context as data: words, commands' ids and whether each is enabled, the column,
  the type, and a part's view (the filter panel's, the find panel's, the editor's) each time the core
  changes it, as React's hooks re-render on the context's `subscribe`.
- **To the browser:** what the contents' own handlers do that the attributes do not carry: a choice
  ticked in a MudBlazor checkbox, an operator chosen in a `MudSelect`, a command invoked from a component's
  `OnClick`. Each calls the core's function for it. An input that commits or moves the keyboard is numbered
  in the Input Order by the `data-ex-claims` the contents write, as under the C# core (ADR-0240).
  *Note of 2026-10-28:* this path is not used for a choice that a commit reads. `MudSelect` tells .NET
  of a choice only after awaiting the close of its list, so on a circuit the choice reached the core after
  an Enter typed straight after it, which had applied the panel without it: an outcome that depended on
  the round trip (spine rule 6). `MudGridChrome`'s filter panel therefore draws its operators as native
  selects and a condition's value as text for every type, as the MUI Wrapper's does, and section 2's
  note reads each in the browser.
- **Never:** a member that must answer synchronously. `ResolveKey`, `TextChanged` and the pull for
  distinct values are not carried; their work is section 2's or the view's.

### 4. The C# Chrome contracts are rewritten over the Script Core's

`IGridChrome` and `IPivotChrome` keep their names and their slots; their contexts become views of the
Script Core's slot contexts (section 3), with the attributes of section 2 named as constants. A Chrome
written against the C# core's contexts is rewritten; each product's Change Note says so (ADR-0190). The
MudBlazor Wrappers are rewritten against the new contexts, keeping their components and their look.

*Note of 2026-10-28, as the first slice was built on ExGrid:* while the C# core still draws the
grid (ADR-0350 section 7), the views stand beside its contexts rather than replacing them, so a Chrome
answers both cores. `IGridChrome.ScriptSlots` names the slots a Chrome fills over the Script Core, and
each is asked for through an overload that takes a view (`ColumnMenu(GridMenuView)`,
`ContextMenu(GridMenuView)`, `LoadingIndicator(GridLoadingView)`); the members have defaults, so no
Chrome breaks before the C# core goes, and the C# contexts go with it. A slot a Chrome does not name
stays the Script Core's own. `ScriptSlotAttributes` holds section 2's attributes, `MenuItem` first.

*Note of 2026-10-28, as the find panel was built:* a product that draws a part of its own in a grid
slot keeps it where a Wrapper forwards the Grid Chrome's slots. ExSheet's find panel is its own, with
Within (ADR-0332 section 5), and a grid's panel drawn in its place would search the Sheet shown alone, so
`MudSheetChrome` forwards every slot `MudGridChrome` names except `FindPanel`; a Sheet Chrome's own find
slot over the Script Core is ExSheet's to add.

### 5. A slot's contents arrive a render later, and the grid is settled only once they stand

On the Server host the contents arrive a round trip after the slot. Meanwhile the core already owes the
opening focus (ADR-0204), numbers the inputs that arrive (ADR-0240), and answers a menu's keys against the
place it keeps, which needs no drawn item: a key typed on the grid while a menu's contents still owe
the opening focus is the menu's, and a move it makes renews the focus request, which names the item
the place stands on (`focusItem`), so the contents take the keyboard there when they arrive (note of
2026-10-28: the first build answered such a key as the grid's, and a Server-host test of End and
Enter typed as the menu opened caught it). The Settled Mark (ADR-0220) is not raised while a mounted
slot's contents have not been moved in, so a test reads a part only once it stands, and a reading never
depends on the round trip's length (spine rule 6).

*Note of 2026-10-28, as the filter panel was built:* keys typed before a panel's contents arrive have no
place the core keeps to be answered against (a Space on a value, letters in the search box), so the
Blazor adapter holds every input made after a part that takes the keyboard is mounted, in its order
(ADR-0240's `inOrder`), until that part's contents stand and have taken the keyboard, and the core then
takes each where the keyboard is, its default done as the held inputs' are. A menu's keys reach its
drawn items this way on Blazor; the core's answer against the menu's place stays for an adapter that
holds nothing. A loading indicator, which takes no keyboard, holds no input.

### 6. Template cells use the same mechanism, and ExGrid takes it first

A Template cell (ADR-0020) is a slot like any other, drawn from the row .NET holds. The mechanism is built
on ExGrid first, `ExGrid.MudBlazor` over `ExGrid.Script.ExGrid`, and each product's Blazor adapter then
uses it for its own slots.

## Consequences

- A Blazor Consumer keeps MudBlazor's components inside the grid, and a Chrome of its own, with nothing
  added to `Program.cs`.
- Every Chrome written against the C# core's contexts, the four MudBlazor Wrappers included, is rewritten
  once. The contexts lose their synchronous members.
- React's Chrome hooks become thin calls of DOM functions the Blazor adapters share.
- The adapter renders holders beside its root, so the component's markup is the root and its holders. A
  Consumer's CSS that selects the component's element by position (`:first-child` of a wrapper) still
  finds the root first.
- A layer-3 spec that judges a Razor-filled slot is a Blazor spec (ADR-0370 section 11), on both Blazor
  hosts.
