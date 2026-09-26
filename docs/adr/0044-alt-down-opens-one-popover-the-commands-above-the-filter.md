# Alt+↓ opens one popover: the column's commands above its filter, as Excel's drop-down

*(Decided with the user, 2026-09-26, after comparing Alt+↓ with Excel's AutoFilter drop-down.)*

Excel opens one drop-down from a filtered header. It holds the sort commands, "Clear Filter From",
the condition filters, a search box, the checkbox list of the column's distinct values, and OK and
Cancel. The grid opened two things in turn: the column menu
([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) — sort, Filter, hide, pin, size to
fit — and, from its "Filter" item, the filter panel
([ADR-0009](./0009-filter-panel-contract.md)) as a second popover. Every piece Excel shows was
there, one step further away than someone coming from Excel expects to find it. No ADR had
decided the split. It was how the two seams happened to be wired.

## Decision

**Alt+↓, and the header's ▾, open one popover.** The column's commands stand at the top, and
below them, where the column can be filtered, stands its filter: the search, the value list or
the condition, and OK and Cancel. The "Filter" command goes, because what it opened is now
already open. Hide, pin and Size to fit stay in the command list. Excel keeps those on the
header's own menu, and this grid has only the one menu to put them in. A column that cannot be
filtered shows the commands alone.

**The two seams stay two seams.** `IGridChrome` still supplies the column menu and the filter
panel separately, each with its own context (ADR-0009/0010), and the core stacks what the two
return inside the one popover it places ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)).
A substituted Chrome, `ExGrid.MudBlazor`'s included, changes what each half looks like and nothing
about where they stand or when they close. OK, Cancel, a command run, Escape or a dismissing press
closes the whole popover, and the keyboard goes back to the root
([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)).

**The keyboard is ADR-0039's, over both halves.** The popover opens with DOM focus on its first
enabled command. ↑ / ↓ move among the commands as they did. Tab leaves the commands for the
filter's first control, moves through the filter's controls, and wraps back to the first command,
so focus never leaves the popover by Tab. Which letters jump where is decided below.

Rejected:
- **Keeping the two steps.** Nothing is lost by it, but it is the one place a user coming from
  Excel has to learn a different route to something they reach for daily.
- **A filter-only drop-down with the commands elsewhere.** Sorting from the same drop-down is
  half of what Excel's is used for.

### The letters are Excel's — decided

*(Decided with the user, 2026-09-26.)* **S sorts ascending, O sorts descending, C clears the
column's filter, and E moves to the search box**, as in Excel's drop-down. The letters are fixed,
whatever language the labels are in: a Consumer translating "Sort ascending" does not move its
key, just as Japanese Excel keeps S beside 昇順. They act while focus is on a command or on the
value list. In a text field a letter is text. The built-in Chrome shows each one the way
Excel does: underlined where the label contains the letter, as English Excel does, and appended
as "(S)" where it does not, as Japanese Excel does. The commands Excel's drop-down does not have
— hide, pin, unpin, Size to fit — get no letter. Borrowing one would give a letter a meaning an
Excel user does not expect.

### "Clear filter" is a command — decided

*(Decided with the user, 2026-09-26.)* As Excel's "Clear Filter From", **clearing the column's
filter is a command in the list**, key C, enabled only while the column has a filter. The filter
half keeps OK and Cancel and loses its own Clear button. `FilterPanelContext.Clear` stays, for a
substituted panel shown on its own.

The filter half's own decisions — "Add current selection to filter", and a second condition —
are recorded in [ADR-0009](./0009-filter-panel-contract.md).

## Not taken

*(Both confirmed with the user, 2026-09-26.)*

- **Sort by colour and Filter by colour.** Excel's colours are the cell's formatting. This grid's
  colours are Cell State and tone, which are the Consumer's meaning. Filtering by them is
  filtering by the Consumer's own data, which the Consumer can already offer as a column.
- **Named condition submenus ("Number Filters ▸", Top 10, Above Average).** The two-condition form
  covers the named comparisons. Top 10 and the averages are computed over the whole result, which
  the grid neither holds nor executes ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)).

## Reserved

- **Excel's year ▸ month ▸ day tree over a date column's value list.** It would be presentation
  only: the distinct dates are already in hand, and what applies stays an `In` list. It waits for
  its trigger, a Consumer declaring a value list on a date column. A date column with many
  distinct dates falls back to the search form anyway (ADR-0009's TooMany), and the tree adds a
  level of keyboard handling to a popover that has just been rebuilt.

## As implemented

*(2026-09-26, both Chromes. What the decision left open, settled while building it.)*

- **The roles.** A column's popover is `role="dialog"`, named by the column's header, and holds
  the commands' `role="menu"`, named the same. A column that cannot be filtered shows the menu
  alone, and the popover around it carries no role of its own.
- **Tab is the core's at both ends.** On a command, Tab and Shift+Tab are menu keys, and
  `MenuKeys.ResolveInColumnMenu` answers them `FilterFirst` / `FilterLast`: the core sends the
  keyboard to the filter's first or last control. Off either end of the filter, Tab lands on one
  of two sentinels the core renders around the filter half, which hand the keyboard back to the
  **first** command. The menu's place goes back with it, so Enter there runs the first command,
  not the one ↓ had chosen before Tab left. The panels draw no sentinels and no Clear of their
  own.
- **The filter is asked, not opened.** `FilterPanelContext.FocusRequest` starts from zero at each
  opening, where nothing is asked, and counts Tab and E; `FocusLastRequest` counts Shift+Tab.
  `ColumnMenuContext.FocusRequest` counts the opening and each return to the commands. A
  substituted panel written against the old contract — focus on the first render — would take
  the keyboard from the commands, which is why the contract says zero is no request.
- **The value list's letters go through the core.** The panel hands a key on its value list to
  `FilterPanelContext.ValueListKey`, and the core answers it with `MenuKeys.Letter`, the table the
  commands use. A text field hands nothing over, so a letter there is text.
- **A key on its way is held, not lost** *(corrected the same day, by review)*. On a circuit, E
  moves the keyboard to the search box a round trip later, and the letters typed after it still
  land on the command or the value list being left. The first build dropped them, which
  contradicted [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md): a change of who
  holds the keyboard holds the keys after it and hands them on in order. "E apple" searched for
  "ple". Now the key listener treats Tab, Shift+Tab and E on a command, E on the value list, and
  any key landing on a wrap sentinel as that change, and holds what follows until DOM focus has
  moved. In a text field a held key is typed at the caret, as in the Cell Editor, and a held
  Enter submits the field's form. The core no longer guards anything itself. *(Widened after the
  second review, decided with the user.)* A key that closes the popover — a command run, Enter
  in the filter's field — holds the keys behind it too, until the popover is gone
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). E held on a checkbox outside
  the value list ("Add current selection to filter") holds nothing: only the Chrome's element
  marked `ex-value-list` answers letters.
- **E is not typed where it sends the keyboard.** On WebAssembly the core moves DOM focus inside
  E's own keydown, and the browser typed the E into the search box it had just reached. The
  listener cancels that key's default.
- **What a held key cannot do** *(decided with the user, 2026-09-26)*. A dispatched key has no
  default action, so a held Tab handed to one of the filter's own controls cannot move DOM
  focus, nor a held Space tick a checkbox, and moving focus from script is what
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) keeps out. That key and every
  key held behind it are dropped (ADR-0010): the typing stops short rather than landing in the
  wrong field. Only typing faster than a round trip reaches it, so only a Server circuit.
- **The letters' marks are the built-in Chrome's.** `MenuKeys.SplitAtLetter` splits a label at
  its letter, or appends "(S)". `ExGrid.MudBlazor`'s menu marks none: a Material menu shows no
  mnemonics, and the letters act the same under it. The Context Menu answers no letter, so it
  marks none either, even on a Consumer's command that shares a sort's id.
- **E where no value list stands** *(decided with the user, 2026-09-26)*. The condition form has
  no search box, so E goes to the field a search is typed into there: the condition's value,
  not its operator — typed letters in a select would choose an operator instead. It is asked
  through its own count, `FilterPanelContext.SearchRequest`, and `MenuKeys` answers E with
  `MenuKeyKind.FilterSearch`. While the operator takes no value (is blank), E goes to the
  operator.
- **Opening costs a value-list query.** The built-in filter asks for the column's distinct values
  when the popover opens, where it used to ask when Filter was chosen: once per opening, as
  ADR-0009 has it, and now also when the popover was opened only to sort. A substituted panel
  pulls the list itself, as before.

## Consequences

- **The Definition of Done's menu rows change with it**: the built-in menu's item list (FN-18)
  loses "Filter", and KB-29 to KB-31 are read over the one popover.
- **ADR-0036's Context Menu is unaffected.** It is the command list over a selection, with no
  filter under it.
