# Run 17, Parts C to F: the toolbar under MudBlazor, and Format Cells' letters

Branch `claude/run17cf-toolbar-mud`, 2026-10-12 and 2026-10-13, four rounds. Findings D-mud-lists, D-mud-console
and D-letter-O of the seventeenth Windows run (`verification/2026-10-11-windows-17/report.md`, Part D); readings R2
(a) and (b) of the Windows readings of 2026-10-12 (`verification/2026-10-12-windows-readings/report.md` on
`claude/exsheet-windows-readings-2026-10-12`); the user's decisions of 2026-10-12 and 2026-10-13 listed below; the
verifier's six findings on round 2 and its findings on round 3. This file holds the text for the files a background
agent does not edit — ADR-0100, ADR-0050, ADR-0044, ADR-0021, ADR-0061, ADR-0070, ADR-0071, the Definition of Done
and the Docs Site — as numbered edits of whole lines under "Edits", each old text occurring once at the branch tip
and the list applying in order; and what the fourth round stopped on.

## The user's decisions this file carries

- (2026-10-12, decided with the user, decision (B): a Chrome whose list is its own closes it when `CloseRequest`
  changes and does not call `ChoicesClosed` for that close; `MudToolbarItem` closes its MudMenu and drops a focus
  not yet landed.)
- (2026-10-12, decided with the user, decision (C): the letters typed after the release of the Alt key alone wait
  for the KeyTips, so that Alt, H, 1 typed at once sets bold rather than opening an edit holding "h1".)
- (2026-10-12, decided with the user: keys typed while the keyboard is in flight back from a closing popover are
  held and handed on in order to where the keyboard lands, ExGrid-wide: any popover the grid or a Chrome shows —
  the toolbar's lists and palettes, filter panels, Format Cells' and other dialogs' lists — closed by Escape or by
  a choice, under the built-in Chrome and under MudBlazor; the MudBlazor Chrome gets a seam member that hands on
  the keys typed into a list while it closes; the outcome is the same at any speed on both hosts.)

- (2026-10-13, decided with the user: an Alt pressed alone while keys are held — the grid's script hears Alt's
  `keyup` on the root and holds it in order with the keys. It shares ED-30's root `keyup` listener: no listener is
  added. This reverses ADR-0100's "rather than adding a listener to the grid" and ADR-0050 item 14's "No listener
  is added to the grid", and widens what ADR-0021's seventh entry's listener is for.)
- (2026-10-13, decided with the user: keys typed on the toolbar's band while a list opens go to the list, through
  a second seam member, by the same rule as the close direction.)
- (2026-10-13, decided with the user: under `MudSheetChrome` the toolbar item's lists take no Tab, keeping SH-50's
  one Tab stop.)
- (2026-10-13, decided with the user: the hold is family-wide — ExSheet's and ExPivot's own dialogs, menus and
  panels outside the grid's root too, `ExSheet.MudBlazor`'s Format Cells and ExPivot's Show Details dialog among
  them. As for the toolbar, a key typed as the keyboard goes back is held and delivered in order, never dropped, on
  both hosts.)

The user also said that round 2's stop 1 stands. How it reads now is my reading of decision (B), below.

## What the fourth round built (2026-10-13)

The user decided round 3's four stops on 2026-10-13 (above). What each decision built, and what holds it:

- **Alt pressed alone while keys are held (stop 1).** The grid's script hears the Alt key's release on the root, in
  the capture phase, through the one `keyup` listener ED-30 added for the Keyboard Field (ADR-0021's seventh entry,
  `onRootKeyUp`): no listener is added, and MEM-4's count stands. Where the Consumer declares `Alt+Alt`:
  - While the gate holds keys, the release is kept from the Consumer's handler and held in its place. Its press,
    held too, is no longer skipped as a modifier's: the drain hands the press to the core in its turn, then the
    release, as the keyup it was, where the keyboard is then (the root, or the frame or control the keys went to),
    and then asks the empty question (`Ping`) whose answer travels behind it.
  - Not held, the release reaches the Consumer, and the keys after it wait for the same question, asked in the
    task after the release's dispatch rather than inferred from the next key: `afterAltAlone`, `altGoesFirst` and
    the marks they left are gone.
  - Whether an Alt went down alone is the gate's to tell, since it hears every key between the press and the
    release in order: a chord's release, or one whose press it did not hear, never reaches the Consumer. ExSheet no
    longer reads other keys' releases, which, typed while keys are held, reach it ahead of the held Alt's; a key
    typed on the band forgets an Alt press whose release the gate kept. Alt released before the ↓ of Alt+↓ no
    longer shows the KeyTips (ADR-0100 had left that case "rather than adding a listener to the grid").
  - The toolbar's band holds an Alt's release typed on it while the keyboard is in flight, with the keys, and the
    Sheet's handler, which hears it next, leaves it. A closing MudBlazor list passes its Alt release on with its
    keys.
  - Layer 2: `An_alt_released_on_the_band_while_the_keyboard_goes_back_is_held_with_its_press` and
    `Whether_the_alt_was_alone_is_the_gates_to_tell` (SheetToolbarKeyTipTests),
    `The_alt_keys_release_is_held_in_order_by_the_roots_one_keyup_listener` (ShippedStylesheetTests). Layer 3
    (sheet-toolbar.spec.mjs, both Chromes, 80 ms on the Server host and WebAssembly): Enter on an edit, then Alt, H,
    1, typed at once; Alt, Alt, H, 1 typed slowly, then at once; three Escapes in a list a KeyTip opened, then Alt,
    H, H, all at once.
- **Keys typed on the band while a list opens (stop 2).** From the toolbar's request for a list (`OpenRequest`),
  the band holds the keys typed on it.
  - A Chrome whose list is its own calls the seam's second member, `ToolbarItemContext.ChoicesOpened(true)`, once
    its list holds the keyboard, and answers the keys it returns as typed in the list, ahead of any typed there
    since; `ChoicesOpened(false)`, for a list that did not open, gives them back to the band. `MudSheetChrome` does
    both.
  - Under the built-in Chrome the list is the grid's popover. As it opens, the grid takes the keyboard from the
    band (`ReturnKeyboardAsync(from, typedOnTheWay)`) with the keys held; a keyboard already in the grid's popover
    stays there, and the grid's hold hands them to the list once it holds the keyboard.
  - Escape typed on the band before the list has the keyboard is one of those keys: handed to the list, it goes
    back a level there (R2 (b)). The band no longer closes the list through `CloseRequest` for it; `CloseRequest`
    still closes a list on its way when the toolbar gives the keyboard back for any other reason.
  - Layer 2: `Keys_typed_on_the_toolbar_while_a_keytips_list_opens_go_to_the_list`,
    `A_chromes_own_list_is_handed_the_keys_typed_while_it_opened` and `A_list_that_did_not_open_gives_its_keys_to_the_band`
    (SheetToolbarKeyTipTests), `The_keys_held_while_the_list_opened_are_answered_in_it` (MudToolbarTests). Layer 3:
    Alt, H, H, then ↓, →, Enter, typed at once, fill the cell from the palette, both Chromes.
- **One Tab stop under `MudSheetChrome` (stop 3).** `MudToolbarItem` draws Number Format's button and the split
  controls' arrows itself, `tabindex="-1"`, and opens a MudMenu that has no Label, Icon or ActivatorContent, and so
  draws no button: MudBlazor's own takes no tabindex a Chrome can set, and the wrapper its ActivatorContent is drawn
  in carries `tabindex="0"`. The menu stands, empty, at the item's foot, where its list opens. Layer 2:
  `No_control_inside_the_band_takes_tab` (MudToolbarTests); layer 3: the Tab case finds the band the one stop under
  either Chrome.
- **The hold, family-wide (stop 4).**
  - `ExSheet.MudBlazor`'s Format Cells: Escape (now the dialog's own: MudBlazor's `CloseOnEscapeKey` is off), OK
    and Cancel close through the frame. OK sets what was touched as it is pressed; Cancel ends Format Cells; the
    frame's element comes back and takes the keyboard out of the dialog; the dialog closes; and the core takes the
    keyboard back from the element (`FormatCellsContext.ReturnKeyboardFrom`, new) with the keys typed in the dialog
    from the closing key on — the dialog's form and the box around its buttons pass them to the frame — and on the
    element. ExSheet keeps a Chrome's frame on the page after OK or Cancel until it has given the keyboard back. The
    ×, the backdrop and an Escape an open dropdown's list takes are as they were.
  - ExPivot's Show Details dialog: its records' grid passes the keys after the leaving Escape out to the dialog's
    frame (`ExGrid.PassKeysOutAsync`, new, called from `OnLeave` before its first await; while a Consumer listens to
    `OnLeave`, the keys after a plain Escape on the root wait for its answer). However the dialog closes, the frame
    holds the keys typed in it from then on; a render takes the report's `inert` away while the dialog stands, the
    report's grid takes the keyboard back from the dialog with those keys, and then the dialog goes.
  - ExPivot's menus and panels, the Field List's and the Pivot Toolbar's, closed from inside with DOM focus in them:
    they stand until the keyboard has left them for their button (the frame's focusout), their commands and OK doing
    nothing meanwhile, and the keys typed in them are the button's, in order. Enter or Space presses it; Tab is
    dropped with every key after it (ADR-0010); anything else means nothing on a button. Closed with the keyboard
    outside them — their button pressed again, a press elsewhere — they go at once, as before.
  - Layer 2: `Keys_typed_after_escape_go_to_the_sheet_with_the_keyboard` and `Ok_sets_what_was_touched_as_it_is_pressed`
    (MudFormatCellsTests), `A_frame_gives_the_keyboard_back_from_its_element_with_the_keys_typed_on_the_way`
    (FormatCellsTests), `Keys_typed_while_the_dialog_closes_go_to_the_report` (KeyboardBackTests),
    `Keys_typed_while_a_menu_closes_are_its_buttons` and `A_menu_closed_from_its_button_goes_at_once`
    (FieldListTests), `The_gate_hears_of_onleave_and_the_keys_after_it_go_out_when_told`
    (LeaveAndReturnKeyboardTests), `The_keys_after_a_leaving_escape_go_out_to_the_element_around_the_root`
    (ShippedStylesheetTests). Layer 3, 80 ms on the Server host and WebAssembly: in MudBlazor's Format Cells,
    Escape then ↓, ↓, and OK then ↓ (format-cells-mud.spec.mjs); under both of ExPivot's Chromes, Escape in the
    dialog's grid then ↓, Escape on its Close then ↓, ↓, and Escape in a field's menu then Enter (pivot.spec.mjs).
    Each gives the outcome typed slowly.
- **The verifier's round-3 texts.** ADR-0100's decision (C) bullet and the SheetFormatting sentence claim only the
  cases layer 3 types at once, each with the outcome typed slowly, on either host; what no case reaches is said.

## What the third round built

- **No keyboard trap (the verifier's finding 1).** The Sheet Toolbar held every key typed on a control inside its
  band while the band itself lacked DOM focus, on the premise that no Toolbar Item is a tab stop; MudMenu's own
  buttons (Number Format, and the Font Colour, Fill and Borders arrows) are, and a key typed on one was held for
  ever. The band now holds a key only while a hand-back or a list's close is under way, and answers its own keys
  only: a key typed on a control inside it that holds the keyboard is that control's, and presses nothing.
  Layer 2 under both Chromes (the round-2 test that pinned the trap is replaced); layer 3 Tabs forward through every
  control of the toolbar and back under both Chromes, typing on each, and the band's Escape then gives the keyboard
  to the Sheet.
- **Keys typed while a popover closes go where the keyboard lands (decision of 2026-10-12, above).**
  - The grid's key gate holds the keys typed after Escape that closes one of its popovers until the popover has
    gone and the keyboard has landed, as it already did after Enter on a command or a choice; a press on a
    `menuitemradio` now holds as a press on a `menuitem` did. Escape that an Inner Popup answers holds nothing.
  - `ExGrid.OpenPopoverAsync(content, label, closedByEscape)` tells a Consumer that Escape closed its popover,
    before the grid takes the keyboard back, so a hand-off it tells there is where the held keys go.
  - `ToolbarItemContext.ChoicesClosing` is the seam member: a Chrome whose list is its own calls it as the list
    begins to close, with the key that closes it, and passes the keys typed in the list from then on to the action
    it returns; the toolbar holds them with its band's and hands them on with the keyboard. It is a member of a
    Chrome's seam, ADR-0201's item 2 ("the Chrome's seams: … the contexts its signatures carry"), beside
    `ChoicesClosed`. `MudSheetChrome` uses it; `ChoicesClosed` is now called once the list has gone, after a choice
    too.
  - Format Cells' and ExPivot's MudSelect lists keep the keyboard on the select while they stand (MudBlazor's own
    keys), so no key is in flight when they close; nothing was added for them. This is read off MudBlazor's
    MudSelect, which keeps DOM focus on its input; no case of ours types into one as it closes.
  - *(Round 3, continued; a reading of mine, which was stop 4.)* A panel's OK, Apply and Cancel close its popover
    too, a round trip later on a circuit, and a key typed on the button meanwhile was the button's: ↓ was lost,
    and Enter pressed it again. ADR-0010's rule decided on 2026-09-26 ("so is closing a popover by a key … The
    keys behind a closing key are held until the popover is gone") already covers them, and the decision of
    2026-10-12 says "closed by Escape or by a choice"; I read OK and Apply as the panel's choice and Cancel as
    its Escape. Each Chrome marks such a button `ex-popover-closes`, as it marks its value list `ex-value-list`:
    the built-in filter panel's OK and Cancel, `ExGrid.MudBlazor`'s Apply and Cancel, and the built-in Format
    Cells' OK and Cancel. Enter or Space on one, a press on one, and Enter in a field of a form whose OK is one
    hold the keys after it until the popover has gone, as Enter on a command does; the find panel's Previous
    and Next close nothing and are not marked. `GridChrome`'s filter-panel and Consumer-popover docs say so.
    Layer 3: Enter on OK, Space on Cancel and a press on OK, then ↓ typed at once, under both Chromes on both
    hosts (popovers.spec.mjs), and Enter on Format Cells' OK under the built-in Chrome (format-cells.spec.mjs).
    A panel that refuses what OK submits stands, and the keys behind it wait out the hold's two seconds, as
    behind Enter in the filter's field today.
- **R2 (b), Escape goes back a level.** Observed (the readings of 2026-10-12, R2 (b)): after Alt, H, F, C one Escape
  showed the Home tab's KeyTips with the keyboard not back in the cell, a second the tabs', a third none. ExSheet's
  levels map one to one: the list a KeyTip opened, its Toolbar Row's KeyTips, the rows' KeyTips, none. Under both
  Chromes, typed slowly and at once (80 ms on Server, and WebAssembly). Escape typed on the band before the list has
  the keyboard does the same. Letters that narrow a level (Alt, H, O) are a level of their own, as Excel's Format
  menu is: Escape shows that level's every letter again, so three Escapes after Alt, H, O leave the Sheet's keyboard
  on the Sheet, as three left Excel Ready (observed, R2 (a)). That an F typed for FC is such a level in Excel too is
  by generalisation; no run read Alt, H, F, Escape.
- **R2 (a), D-letter-O settled.** Alt, H, O opens Excel's Format menu and its KeyTip for Format Cells… is E,
  observed (R2 (a)). The comment on `FormatCellsItem.DefaultKeyTip` and the tests say so.
- **The verifier's finding 3.** format-cells-mud.spec.mjs's Alt, H, O, E case types at machine speed, waiting for
  nothing, and holds that no letter reached the cell.
- **Finding 4.** The texts below claim only what a test ran; what depends on the stop below is narrowed. The claims
  that had no case got one: F10, H, 2 typed at once (the F10 of "one Alt, or F10, then letters"), and a ↓ typed in a
  list as it closes (the arrow of "a letter, an arrow, Enter or Escape"), under both Chromes, at 80 ms on Server and
  on WebAssembly. Every text that states what Excel does says it was read on Windows, or by generalisation.
- **Finding 5.** The Change Note `run17cf-toolbar-mud-alt-release-hold.md` and the ADR-0050 note say that a
  Consumer's `keyup` handler tells the gate before its first `await`: the gate waits only for `Ping`'s answer.
- **Finding 6.** The case "a colour chosen in a list, then Enter and ↓ typed in the list as it closes" types the
  second Enter and the ↓ at once under both Chromes, and on the Server host holds that all three keydowns landed in
  the list, which can close only a round trip later: the keys are provably typed while the hold is in flight, and
  without it the second Enter chose again or was lost, the ↓ moved in the list, and the Focus stayed on B3. On
  WebAssembly the list closes inside the first Enter's dispatch and nothing is in flight; the case holds the outcome
  only.
- **Merged `main` (ADR-0201's baseline):** the new public members are in the packages' `PublicAPI.Unshipped.txt`.

## My reading of decision (B): round 2's stop 1

When the toolbar asks a MudBlazor list to close (`CloseRequest`) and the list's request for the keyboard had already
been sent, the list holds the keyboard when the toolbar's hand-back lands, and the hand-back is declined. Decision
(B) says such a close calls no `ChoicesClosed`; read literally, the keyboard would be left in a list that has gone.
I read (B) as covering the list that never took the keyboard, and build the other case as a close of the list's
own: it leaves through its element inside the band, calls `ChoicesClosing` (with no key) and `ChoicesClosed`, and
the toolbar takes the keyboard on as it stands — to the Sheet when it was handing the keyboard back, to the band when
the close came from Escape typed on the band (R2 (b)). The `CloseRequest` XML doc says so. Round 2's stop 1 put
it as "closes as if Escape were pressed in it, and calls `ChoicesClosed`"; that stands, read as: it closes through
the path Escape in the list takes, and calls `ChoicesClosed`. It passes no key to `ChoicesClosing`, because since
R2 (b) Escape in a list a KeyTip opened means going back one level, and here the toolbar, which asked for the close,
already knows where the keyboard goes. Layer 2 pins it
(`A_close_request_closes_a_list_that_holds_the_keyboard_back_to_the_keytips`); layer 3 does not reach the window,
which is a round trip wide.

## Round 3's stops, decided

Round 3 stopped on four questions: the Alt-alone hold, keys typed on the band while a list opens, MudMenu's own
buttons as Tab stops, and the popovers and dialogs outside the grid's root. The user decided all four on 2026-10-13
(above), and the fourth round built them. Each stop's case, evidence and options are in this file at c313c369.

## Stops, for the user

### 1. Keys typed while one of ExPivot's menus or panels opens

Not covered by the decisions: the decision of 2026-10-13 on keys typed while a list opens was made for the Sheet
Toolbar, and ExPivot's openings were not built. A Field List entry or a Pivot Toolbar button opens its menu or panel
in a render, and the menu takes the keyboard a round trip later on a circuit: a key typed on the button meanwhile is
the button's (Enter pressed it again and closed the menu). The closing direction now holds the keys typed as the
keyboard goes back and hands them to the button; a held Enter that presses the button opens the menu again, and
the keys held behind that Enter, which belong to the menu now opening, are dropped — against the decision's
"never dropped", which they cannot keep without the opening held too. Read off the code; no run typed them.
Options: (a) leave, and let ADR-0061 say so; (b) hold the keys typed on the button while its menu or panel opens,
and hand them to it once it holds the keyboard, as the Sheet Toolbar's band does (`ChoicesOpened`'s rule).

## Choices of mine

- The seam member's shape: `Func<KeyboardEventArgs?, Action<KeyboardEventArgs>> ChoicesClosing`, a positional
  parameter after `ChoicesClosed`, called with the closing key and answering where the list's keys go.
- The core's word on Escape: an overload `OpenPopoverAsync(content, label, closedByEscape)`, raised only for Escape.
- The press hold extended to `menuitemradio` in the existing `mousedown` listener, rather than anything new.
- The band answers its own keys only (`_hasFocus`), so a Chrome control inside it keeps its keys.
- ExSheet's levels for R2 (b), and narrowing letters as a level of their own.
- A hold the band finds standing when it takes DOM focus, from a Chrome that began closing a list and never said it
  had, is dropped rather than handed on.
- "Closed by a choice" read as a choice made in a list, a menu or a palette, and a panel's OK and Apply (its
  choice) with Cancel as its Escape: built, under ADR-0010's rule for a key that closes a popover, rather than
  left as a stop. The mark is a class, `ex-popover-closes`, as `ex-value-list` is, on the buttons and nowhere
  else; Enter in a field of a form whose submit button carries it holds too, in the fields the browser submits
  from (text, search, url, tel, password, email, number).
- ExPivot's frames and MudBlazor's Format Cells dialog left to stop 4 rather than built (round 3; the user decided
  stop 4 on 2026-10-13, and round 4 built them).
- The F10 and the ↓ cases, rather than narrowing the texts that name them.
- Stop 1's options (d) and (h), and the recommendation of (a).
- *(Round 4.)* The answer to a release not held is asked in the task after the release's dispatch, a timer of no
  delay: that orders it behind every listener of the keyup, Blazor's among them, which sends the release to the
  Consumer later in the same dispatch. It waits for no time, and no outcome depends on how long it takes.
- *(Round 4.)* Whether the Alt went down alone told by the gate, and ExSheet reading no other key's release; a key
  typed on the band forgetting an Alt press the Sheet heard.
- *(Round 4.)* `ChoicesOpened`'s shape: `Func<bool, IReadOnlyList<KeyboardEventArgs>>`, a positional parameter after
  `ChoicesClosing`, answering the keys held for the list.
- *(Round 4.)* Under the built-in Chrome, the band's keys for an opening list handed to the grid with the keyboard,
  through the core's hand-back from a frame, rather than a new member.
- *(Round 4.)* The MudBlazor lists' buttons drawn by the item, and a MudMenu with no activator, rather than a
  MudPopover of the Chrome's own (round 3's option (b)).
- *(Round 4.)* `FormatCellsContext.ReturnKeyboardFrom` as an init-only member, not a positional one; OK set as it is
  pressed; ExSheet keeping a Chrome's Format Cells frame after OK or Cancel until it gives the keyboard back. A
  Chrome of a Consumer's own that never calls `ReturnKeyboard` sees its fragment stay rendered (a Change Note says
  so).
- *(Round 4.)* `ExGrid.PassKeysOutAsync`, a new public member of the core (ADR-0201), and the hold behind a plain
  Escape on the root of a grid whose Consumer listens to `OnLeave`: the keys after it are passed out as the keydowns
  they were, dispatched on the root's parent. It is not ADR-0070's rejected "the grid lets Escape bubble": the
  Escape itself is the grid's as before, and only the keys after it go out, and only when the Consumer asks.
- *(Round 4.)* ExPivot's menus and panels standing until the frame's focusout says the keyboard has left them, and
  the keys held then answered as the button's.

## Edits

### 1. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Old:

```
- **Escape goes back one level, and from the top it ends.** Any other key that matches nothing ends
  it and does nothing, as in Excel. After a command, the keyboard stays on the Sheet. The Chrome draws
  the letters.
```

New:

```
- **Escape goes back one level, and from the top it ends.** Any other key that matches nothing ends
  it and does nothing, as in Excel. After a command, the keyboard stays on the Sheet. The Chrome draws
  the letters.
  - *(2026-10-12, observed: the Windows readings of that day, R2 (b).)* After Alt, H, F, C one Escape
    showed the Home tab's KeyTips with the keyboard not back in the cell, a second the tabs', a third
    none. A list a KeyTip opened is therefore a level: Escape in it, or on the toolbar before it has the
    keyboard, closes it and shows its row's KeyTips with the keyboard on the toolbar.
  - *(2026-10-12.)* Letters that narrow a level are a level of their own, as Excel's Format menu is
    behind O (three Escapes after Alt, H, O left Excel Ready, observed, R2 (a)): Escape shows that
    level's every letter again. That Excel's F of FC is such a level is by generalisation.
```

### 2. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Old:

```
- **Excel's Home tab letters.** Microsoft's page confirms H for Fill, B for Borders and AC for
  Center. The rest, and the free letters, are read off Excel by a Windows run.
```

New:

```
- **Excel's Home tab letters.** Microsoft's page confirms H for Fill, B for Borders and AC for
  Center. The rest, and the free letters, are read off Excel by a Windows run. *(Read: the seventeenth
  run read the Home tab's letters (d1), and the readings of 2026-10-12 read the Format menu's, R2 (a):
  "Read on Windows" below.)*
```

### 3. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Old:

```
  - This is the way `ExSheet.MudBlazor`'s Format Cells leaves its dialog (ADR-0071). No script is
    added.
```

New:

```
  - This is the way `ExSheet.MudBlazor`'s Format Cells leaves its dialog (ADR-0071). No script is
    added.
  - *(2026-10-12, the seventeenth Windows run, Part D.)* The element is not `aria-hidden`. It was, and on
    the Server host, where it holds focus for a round trip, Chromium logged "Blocked aria-hidden on an
    element because its descendant retained focus" whenever its accessibility tree was on: under a
    screen reader, or the DevTools protocol. Format Cells' element under MudBlazor's Chrome did the same.
    A focused element cannot be hidden from assistive technology, as the Keyboard Field cannot
    (ADR-0080), so each is empty, unnamed and not hidden. Layer 3 reads the tree once over the DevTools
    protocol, which keeps it up to date from then on (`Accessibility.enable` alone does not), holds the
    round trip at 80 ms on the Server host, and holds the console to no such warning.
  - *(Replaced the same day.)* The element held the keyboard about a round trip and a half on a circuit,
    and a key typed then reached the toolbar: Enter pressed the last item again. The core now takes the
    keyboard back from the band itself (ADR-0050 item 16's note of 2026-10-12), and the toolbar has no
    element of its own for it. `ExSheet.MudBlazor`'s list still leaves through one, inside the band;
    Format Cells' dialog still leaves through its own.
```

### 4. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Old:

```
- **A KeyTip on a split control opens its list,** as Excel's Alt, H, H opens the Fill's palette. A
  toggle's KeyTip sets it and gives the keyboard back.

## Considered options
```

New:

```
- **A KeyTip on a split control opens its list,** as Excel's Alt, H, H opens the Fill's palette. A
  toggle's KeyTip sets it and gives the keyboard back.

### Read on Windows *(2026-10-12)*

- **Format Cells' KeyTip is O, E.** Alt, H, O opens Excel's Format menu, and that menu's KeyTip for
  Format Cells… is E: observed (the readings of 2026-10-12, R2 (a)). The seventeenth run read the Home
  tab's O (d1). ExSheet's item answered O alone, so Alt, H, O opened Format Cells at once, and the E that
  Excel's hands type next reached the dialog.
  - The item's letters are now both, `OE`. O narrows the KeyTips to it, as A narrows them to AL, AC and
    AR, and E runs it. Excel's hands carry over unchanged.
  - ExSheet draws no Format menu: nothing else on it (row height, column width, …) is a Toolbar Item. If
    one becomes an item, O becomes a menu as Excel's is, with Format Cells its E.
  - A Consumer's O beside `FormatCellsItem` collides with `OE` and is refused by name, as it was with O.
- **The other letters agree with Excel's** (d1): 1, 2, 3, FC, H, B, AL, AC, AR, N, P and K. 4 is free on
  Excel's Home tab, as Strikethrough's own letter needs it to be; the free letters are G, Q, R, Z, 4, 7
  and 8, and Y where Acrobat's add-in is not installed.
- **Escape goes back a level from a list a KeyTip opened** (R2 (b), observed): the KeyTips paragraph's
  notes above.
  - ExSheet's levels are Excel's one for one: the list a KeyTip opened, its Toolbar Row's KeyTips (the
    Home tab's), the rows' KeyTips (the tabs'), none.
  - Two things on Excel's levels ExSheet does not have: the palette's own KeyTips (H for its
    high-contrast switch, M for More Colours…), since ExSheet's lists are moved through with the arrows;
    and, among the tabs' KeyTips, the Quick Access Toolbar's and the other ribbon items' (1 to 4, B, ZC,
    …), since a Sheet Toolbar has only its rows.
- **Under MudBlazor's Chrome, a list opened from the keys takes the keyboard** (D-mud-lists). The run found
  the keyboard left on the toolbar: `MudMenu`, opened by its own `OpenMenuAsync`, takes no keyboard. Escape
  then reached the toolbar, which gave the keyboard back to the Sheet and left the list standing, and the
  next letter's list opened beside it, four at once.
  - The Chrome now puts the keyboard on the choice the Focus cell has, else the first that can be made,
    once MudBlazor's popover provider has drawn the list, with Blazor's `FocusAsync`. No script is added.
  - The list's keys are the built-in list's, not MudMenu's: ↑ and ↓ (ten across in a palette, with ← and
    →), Home, End, Enter or Space to choose, and Escape to close. Tab keeps the keyboard on the choice, as
    the built-in popover's wrap does. MudMenu's own Escape and Tab, and its items' `AutoClose`, hand the
    keyboard to the menu's button, which the core's focus function does not take it back from.
  - A list closes through an empty element of the item's own, inside the toolbar's band, from which the
    toolbar takes the keyboard on. MudBlazor's provider removes the list in a render of its own, outside
    the band.
  - *(As built, 2026-10-12; replaced 2026-10-13, decided with the user: the MudBlazor toolbar item's lists
    take no Tab, keeping SH-50's one Tab stop.)* MudMenu drew its own button for Number Format and for each
    split control's arrow, and Tab reached them. The item now draws those buttons itself, out of the Tab
    order, and its MudMenu, given no Label, Icon or ActivatorContent, draws none: under either Chrome the
    toolbar is one Tab stop.

### The toolbar's keys, held in order *(2026-10-12)*

The seventeenth run's Part D, layer 3 at an 80 ms round trip, and the verifier's two rounds found places
where what a key did on the toolbar depended on when it was typed (the spine's sixth principle).

- *(2026-10-12, decided with the user, decision (C): the letters typed after the release of the Alt key
  alone wait for the KeyTips.)* On a circuit the KeyTips take the keyboard a round trip after Alt's
  release, and A4 pressed, Alt, H, 1 typed at machine speed opened an edit in A4 holding `h1` (the
    seventeenth run's Part D, layer 3 at an 80 ms round trip).
  - The grid's key gate holds the keys typed after the release of a declared Alt that went down alone,
    until the Sheet has answered the release (ADR-0050 item 16's note of 2026-10-12). The Sheet, starting
    the KeyTips, tells the gate the keyboard is going to a frame of its own, the toolbar, before it first
    waits, and gives the band the keyboard through the core, which gives it in turn, with the held keys,
    in order. A Tab among them is dropped with the keys behind it. F10 takes the same way.
  - *(2026-10-13, decided with the user: the grid's script hears Alt's `keyup` on the root and holds it in
    order with the keys, sharing ED-30's root `keyup` listener.)* The release reaches the Sheet through the
    gate: held in its place while keys are held, and handed on after the press it releases. Whether the Alt
    went down alone is the gate's to tell: a chord's release never reaches the Sheet, which reads no other
    key's release. ADR-0021's seventh entry, widened; no listener is added.
  - *(As built.)* Layer 3 types these at once, under both Chromes, on the Server host at an 80 ms round
    trip and on WebAssembly, and each has the outcome it has typed slowly: Alt, H, 1; F10, H, 2; Enter on
    an edit, then Alt, H, 1; Alt, Alt, H, 1, which is also typed slowly; Alt, H, F, C and three Escapes;
    three Escapes in a list a KeyTip opened, then Alt, H, H. No case types at a speed between the two.
- *(2026-10-12, decided with the user, decision (B): a Chrome whose list is its own closes it when
  `CloseRequest` changes and does not call `ChoicesClosed` for that close; `MudToolbarItem` closes its
  MudMenu and drops a focus not yet landed.)* Escape typed on the toolbar before a list a KeyTip opened
  had the keyboard gave the keyboard back to the Sheet, and then the list arrived and took it.
  - Whenever the toolbar takes the keyboard back, or goes back a level, while an item's list may be open
    from the keyboard, the item's `CloseRequest` changes, to the value its `OpenRequest` had. An opening
    asked for before the close, and not yet answered, is not answered.
  - `ExSheet.MudBlazor` closes its MudMenu. The built-in Chrome closes the grid's popover, if it still shows
    that list, and the core stops holding keys for contents that will not take the keyboard.
  - *(As built, a reading of (B).)* A MudBlazor list whose request for the keyboard had already been sent
    holds the keyboard by the time the close is answered. It closes through its own element as any close
    of its own does, calls `ChoicesClosing` and `ChoicesClosed`, and the toolbar takes the keyboard on as
    it stands: to the Sheet, or, after Escape on the toolbar, to the KeyTips.
  - *(Replaced 2026-10-13, decided with the user: keys typed on the band while a list opens go to the
    list.)* Escape typed on the toolbar before the list has the keyboard is now one of the keys held for the
    list, and goes back a level there. `CloseRequest` still closes a list on its way when the toolbar gives
    the keyboard back for any other reason.
- *(2026-10-12, decided with the user: keys typed while the keyboard is in flight back from a closing
  popover are held and handed on in order to where the keyboard lands; the MudBlazor Chrome gets a seam
  member that hands on the keys typed into a list while it closes.)*
  - The core takes the keyboard back from the band itself (ADR-0050 item 16's note of 2026-10-12). From the
    moment the toolbar asks, it holds the keys typed on the band, and the core hands them to the grid, in
    order, ahead of any typed after. Only while a hand-back or a list's close is under way: a key typed on
    a control inside the band that holds the keyboard is that control's.
  - Under the built-in Chrome a list is the grid's popover, whose closing the grid holds keys behind
    (ADR-0044's note of 2026-10-12). Escape there is told to the Sheet before the grid takes the keyboard
    back (`OpenPopoverAsync`'s `closedByEscape`), so after a list a KeyTip opened the band takes it, with
    the keys typed after the Escape.
  - Under `ExSheet.MudBlazor` the list is MudBlazor's. As it begins to close, from a choice, Escape or
    MudBlazor's own means with the keyboard in it, the Chrome tells the toolbar
    (`ToolbarItemContext.ChoicesClosing`, with the closing key) and passes it the keys typed in the list
    until it has gone; the keys typed on its element inside the band reach the band. The toolbar holds
    them all and hands them on with the keyboard: to the Sheet, or, after Escape in a list a KeyTip opened,
    to its KeyTips once the band holds the keyboard.
  - *(2026-10-13, decided with the user: keys typed on the band while a list opens go to the list, by the
    same rule as the close direction.)* From the toolbar's request for a list (`OpenRequest`) the band holds
    the keys typed on it. A Chrome whose list is its own calls `ToolbarItemContext.ChoicesOpened(true)` once
    the list holds the keyboard, and answers the keys returned as typed in the list, ahead of any typed there
    since; with false, for a list that did not open, the band takes them back. Under the built-in Chrome the
    grid takes the keyboard from the band as its popover opens, with those keys, and its hold hands them to
    the list (a keyboard already in the grid's popover stays there). Layer 3: Alt, H, H, ↓, →, Enter typed
    at once fill the cell, under both Chromes, on both hosts.
  - Format Cells' item gives the keyboard back after its press from the keys, as every item does. It
    declined that, because the hand-back would have taken the keyboard from the built-in popover. The
    core's hand-back from a frame now leaves a keyboard already inside the grid where it is, and the grid
    holds the keys typed meanwhile for Format Cells: Escape straight after Alt, H, O, E closes it.

## Considered options
```

### 5. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Old:

```
  - A dynamic call was chosen over a fixed flag on the command, because the same command opens the
    core's popover under one Chrome and a frame of its own under another.

## A Formula Bar text that is not the cell's own *(2026-10-03, with ADR-0125)*
```

New:

```
  - A dynamic call was chosen over a fixed flag on the command, because the same command opens the
    core's popover under one Chrome and a frame of its own under another.
- *(2026-10-12; ExSheet's Sheet Toolbar, ADR-0100.)* **The hold covers the keyboard's way to and from a
  frame of the Consumer's own, a popover of the Consumer's closing, and the release of the Alt key.**
  - *(2026-10-12, decided with the user, decision (C): the letters typed after the release of the Alt key
    alone wait for the KeyTips.)* **After the release of a declared Alt that went down alone, the keys wait
    for the Consumer.** The release is the Consumer's to answer (ExSheet's `keyup`, ADR-0100). *(Until
    2026-10-13 the gate heard no keyup, and knew the release by the key after it: one that does not carry
    Alt, or Alt pressed again.)* The gate hears it on the root (ADR-0021's seventh entry, widened 2026-10-13),
    and every key after it is held until an empty question to the core (`Ping`), asked once the release has
    been dispatched, travels behind it and is answered. The gate waits for nothing else, so the Consumer's
    handler tells it before its first `await`: `HandKeyboardToFrameAsync`, then `HandKeyboardToAsync` for
    the frame's control. Handed off, the keys go to the frame, or are dropped with every key behind one it
    cannot take, a Tab; told nothing, they are the grid's. A press on the grid and DOM focus leaving it
    forget an Alt that is down.
  - *(2026-10-13, decided with the user: the grid's script hears Alt's `keyup` on the root and holds it in
    order with the keys.)* **While keys are held, the release is held in its place**, kept from the
    Consumer's handler, and handed on in its turn, as the keyup it was, where the keyboard is then — the
    root, or the frame or control the keys went to — after its press, which the hold hands to the core in
    its turn rather than skip as a modifier's; the question is asked after it. Whether the Alt went down
    alone is the gate's to tell, since it hears every key between the press and the release in order: a
    chord's release (a key carrying Alt came between), or one whose press it did not hear, never reaches
    the Consumer, and the Consumer reads no other key's release, one typed while keys are held reaching it
    out of turn. A frame's band holds an Alt's release typed on it while the keyboard is in flight, with the
    keys. *(Until then an Alt pressed alone again while keys were held was forwarded at once or held as a
    modifier, and its press and its release could reach the Consumer in either order.)*
  - **A hand-off told while the hold drains keeps the hold until the keyboard arrives,** so the keys typed
    meanwhile go where the keyboard is going, and none is gated against the root.
  - **A frame gives the keyboard back with the keys typed on it on the way:**
    `ReturnKeyboardAsync(ElementReference from, Func<IReadOnlyList<KeyboardEventArgs>> typedOnTheWay)`.
    It is granted where the core's focus function is, and also while DOM focus is still on the frame, so
    the frame need not first give the keyboard up to nothing, where a key reaches no grid. The Consumer
    holds the keys typed on the frame from the moment it asks; the gate, once it has the keyboard, asks
    for them, behind every key the frame heard, and holds them ahead of any typed on the root since.
    Declined, it still asks, and drops them. A keyboard already inside the root — in a popover a command
    of the frame opened — is the grid's, and stays where it is.
  - **A Consumer's popover closed before its contents took the keyboard** withdraws the gate's wait for
    them: the keys typed meanwhile are the grid's, rather than held for the hold's fallback and dropped.
  - *(2026-10-12, decided with the user: keys typed while the keyboard is in flight back from a closing
    popover are held and handed on in order to where the keyboard lands.)* **A Consumer's popover closed
    by Escape tells the Consumer that asked** (`OpenPopoverAsync(content, label, closedByEscape)`), before
    the grid takes the keyboard back. The keys typed after the Escape are held until the popover is gone
    and the keyboard has landed (ADR-0044's note of 2026-10-12), and a hand-off the Consumer tells before
    its first `await` is where they land. It is not told of any other close.
  - **A control handed the keyboard while it is on its way to a frame takes it in turn.**
    `HandKeyboardToAsync` called while a frame hand-off is pending and the gate holds keys gives the
    control the keyboard when the hold reaches it, and hands it the held keys in the same task: a key
    typed straight on the control can no longer overtake one still held (Alt, H, O typed at once, the O
    on the band before the held H). ExSheet's KeyTips give the band the keyboard this way.

## A Formula Bar text that is not the cell's own *(2026-10-03, with ADR-0125)*
```

### 6. docs/adr/0044-alt-down-opens-one-popover-the-commands-above-the-filter.md

Old:

```
  second review, decided with the user.)* A key that closes the popover — a command run, Enter
  in the filter's field — holds the keys behind it too, until the popover is gone
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). E held on a checkbox outside
```

New:

```
  second review, decided with the user.)* A key that closes the popover — a command run, Enter
  in the filter's field — holds the keys behind it too, until the popover is gone
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). *(2026-10-12, decided with the
  user: keys typed while the keyboard is in flight back from a closing popover are held and handed on
  in order to where the keyboard lands, ExGrid-wide, closed by Escape or by a choice.)* So do Escape in
  any of the grid's popovers, unless a popup of its contents' own answers it (ADR-0039), Enter on a
  `menuitemradio` — one choice among others, which ARIA's menu pattern closes the menu on — and a
  press on one, as a press on a `menuitem` already did: the keys wait until the popover has gone and
  go where the keyboard lands, on the root or where the core or the Consumer handed it on. On the
  Server host at an 80 ms round trip, ↓ typed straight after Escape in a menu moved in the closing
  menu and was lost, and a second Enter after a choice chose again. *(As built, a reading of that
  decision and of ADR-0010's rule for a key that closes a popover.)* So do a panel's OK, Apply and
  Cancel, which each Chrome marks `ex-popover-closes` (ADR-0010's note of 2026-10-12): pressed, or by
  Enter or Space, and Enter in a field of a form whose OK is one. ↓ typed straight after Enter on a
  filter panel's OK was lost on the closing button. *(2026-10-13, decided with the user: the hold is
  family-wide — ExSheet's and ExPivot's own dialogs, menus and panels outside the root too.)* Outside the
  root the frame holds the keys itself and hands them on with the keyboard: ADR-0071's note of 2026-10-13
  for `ExSheet.MudBlazor`'s Format Cells, ADR-0061's and ADR-0070's for ExPivot's menus, panels and Show
  Details' dialog. E held on a checkbox outside
```

### 7. docs/adr/0010-chrome-seams-column-menu-editor-loading.md

Old:

```
(`data-ex-letters`), and a Chrome marks its value list `ex-value-list`, so the listener mirrors
nothing but Tab, Shift+Tab and E. The two seconds count from the start of the hold, however
many moves it holds behind.
```

New:

```
(`data-ex-letters`), and a Chrome marks its value list `ex-value-list`, so the listener mirrors
nothing but Tab, Shift+Tab and E. The two seconds count from the start of the hold, however
many moves it holds behind. *(2026-10-12, as built: a reading of this rule and of the decision of
that day, ADR-0044's note.)* A button that closes the popover — a panel's OK, Apply or Cancel —
closes it by a key too, Enter or Space, and by a press: a Chrome marks such a button
`ex-popover-closes`, as it marks its value list, and the contents of a Consumer's popover mark
theirs. Enter in a field of a form whose OK is so marked submits it, and holds as Enter in the
filter's field does. A panel that refuses what OK submits stands, and the hold behind it ends at
its two seconds.
```

### 8. docs/adr/0021-javascript-is-allowlisted-not-minimised.md

Old:

```
independence cannot be kept on a circuit otherwise.)* *(No longer the only one: see the note of
2026-09-29 at the end.)*
```

New:

```
independence cannot be kept on a circuit otherwise.)* *(No longer the only one: see the note of
2026-09-29 at the end.)* *(Widened 2026-10-12, with ADR-0050 item 16's note of that day: the
hand-back is also granted while DOM focus is still on a frame of the Consumer's own that gives the
keyboard back, which the core's call names — ExSheet's Sheet Toolbar — so the frame need not give it
up to nothing first. It is the same decision with a wider condition, still a read of
`document.activeElement` and no layout. And the keyboard handed on to a control of the Consumer's is
given in turn by the key hold while the keyboard is on its way to that control's frame, with the keys
held for it.)*
```

### 9. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
      - No script is involved. A Chrome that owns its frame is responsible for leaving the keyboard
        where the core will take it back.
```

New:

```
      - No script is involved. A Chrome that owns its frame is responsible for leaving the keyboard
        where the core will take it back.
      - *(2026-10-12.)* The element is not `aria-hidden`: it holds focus, and Chromium refuses to hide
        a focused element and says so in the console (ADR-0100's note of 2026-10-12).
      - *(2026-10-13, decided with the user: the keys typed as the keyboard goes back are held and handed
        on in order, `ExSheet.MudBlazor`'s Format Cells among the frames.)* On a circuit the dialog stands a
        round trip after Escape, OK or Cancel, and once MudBlazor removed it DOM focus was on nothing: ↓
        typed straight after Escape was lost. Escape is now the dialog's own, not MudBlazor's
        `CloseOnEscapeKey`, and those three close through the frame: OK sets what was touched as it is
        pressed, so nothing typed after it changes what it sets; the frame's element takes the keyboard out
        of the dialog before it closes; and the core takes the keyboard back from the element with the keys
        typed in the dialog from the closing key on and on the element
        (`FormatCellsContext.ReturnKeyboardFrom`, ADR-0050 item 16's note of 2026-10-12). ExSheet keeps a
        Chrome's frame on the page after OK or Cancel until it has given the keyboard back. The ×, the
        backdrop and an Escape an open dropdown's list takes are as they were. Layer 3: Escape then ↓, ↓,
        and OK then ↓, typed at once on both hosts.
```

### 10. docs/definition-of-done.md

Old:

```
| **SH-52** | MUST | While the Sheet holds the keyboard, its Sheet Toolbar is shown and no edit is open, Alt (Option on macOS) released alone, or F10, shows a KeyTip per Toolbar Row (the formatting row's is H); a row's letter shows its items' letters, Excel's Home tab letters where Excel has one and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses); a letter runs its item and leaves the keyboard on the Sheet; Escape goes back one level and ends from the top; an unmatched key ends it doing nothing; Alt with another key stays a chord (Alt+↓, Alt+Enter); a Consumer's letters are only those declared, and two colliding at one level are refused at render naming both; while an edit is open they start nothing, and with the toolbar hidden Alt and F10 stay the browser's; on Windows the browser's menu does not open from a Sheet whose toolbar is shown (ADR-0100, ADR-0021) | Layer 2; Layer 3; beside Excel and by hand on Windows for the browser's keys (a Windows run) | as Excel, where Excel has the letter |
```

New:

```
| **SH-52** | MUST | While the Sheet holds the keyboard, its Sheet Toolbar is shown and no edit is open, Alt (Option on macOS) released alone, or F10, shows a KeyTip per Toolbar Row (the formatting row's is H); a row's letter shows its items' letters, Excel's Home tab letters where Excel has one, Format Cells' through Excel's Format menu (O, E), and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses); a letter runs its item and leaves the keyboard on the Sheet; Escape goes back one level and ends from the top — from a list a KeyTip opened to its row's KeyTips, from letters that narrow a level to that level's (*2026-10-12, the readings' R2 (a) and (b), observed*); an unmatched key ends it doing nothing; Alt with another key stays a chord (Alt+↓, Alt+Enter); an Alt pressed alone, and the letters after it, typed at once while the grid still holds keys, have the outcome they have typed slowly (*2026-10-13, decided with the user*); a Consumer's letters are only those declared, and two colliding at one level are refused at render naming both; while an edit is open they start nothing, and with the toolbar hidden Alt and F10 stay the browser's; on Windows the browser's menu does not open from a Sheet whose toolbar is shown (ADR-0100, ADR-0021) | Layer 2; Layer 3, the keys at once on the Server host at an 80 ms round trip and on WebAssembly; beside Excel and by hand on Windows for the browser's keys (a Windows run) | as Excel, where Excel has the letter |
```

### 11. docs/definition-of-done.md

Old:

```
| **KB-32** | MUST | However a popover closes — Escape, its ▾, a pointer-down elsewhere in the instance, a command run (Clear filter among them, ADR-0044), Apply, Cancel — DOM focus returns to the root, **including from inside an Inner Popup**; a dismissing pointer-down keeps its own meaning (ADR-0039/0010) | Layer 3 under both Chromes | `document.activeElement` is the root and the next arrow moves the Focus |
```

New:

```
| **KB-32** | MUST | However a popover closes — Escape, its ▾, a pointer-down elsewhere in the instance, a command run (Clear filter among them, ADR-0044), Apply, Cancel — DOM focus returns to the root, **including from inside an Inner Popup**; a dismissing pointer-down keeps its own meaning; *(2026-10-12, decided with the user: keys typed while the keyboard is in flight back from a closing popover are held and handed on in order to where the keyboard lands)* the keys typed after Escape or a choice that closes it reach where the keyboard lands, in order, and *(as built, a reading)* after a panel's OK, Apply or Cancel (ADR-0039/0010/0044) | Layer 3 under both Chromes, on the Server host at an 80 ms round trip with the keys typed at once | `document.activeElement` is the root and the next arrow moves the Focus; ↓, ↓ typed straight after Escape in a column's menu, or after Enter on a filter panel's OK, move the Focus two rows |
```

### 12. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

Old:

```
                <b>KeyTips.</b> Release Alt on its own, or press F10, and each row shows its letter: ExSheet's row is H,
                as Excel's Home tab is, and its items carry Excel's letters, so Alt, H, 1 sets bold. Your rows and
                buttons have a KeyTip where you set <code>KeyTip</code>; two letters that collide at one level are
                refused when the toolbar renders.
            </li>
            <li>
                <b>The look is the Chrome's.</b> The built-in Chrome draws plain buttons with inline-SVG pictures;
                <code>MudSheetChrome</code> draws MudBlazor's controls.
            </li>
```

New:

```
                <b>KeyTips.</b> Release Alt on its own, or press F10, and each row shows its letter: ExSheet's row is H,
                as Excel's Home tab is, and its items carry Excel's letters, so Alt, H, 1 sets bold and Alt, H, O, E
                opens Format Cells, as Excel's Format menu reaches it (read on Windows). A list a KeyTip opens takes
                the keyboard under either Chrome: the arrows move among its choices and Enter makes one. Escape goes
                back one level, as Excel's did when read on Windows: from a list to its row's letters, then to the
                rows', then to the Sheet. The keys can be typed at once, over Blazor Server too: the letters straight
                after Alt or F10 wait for the KeyTips, and the keys typed while a list opens or closes, or while the
                keyboard goes back to the Sheet, wait for the keyboard and go where it lands, so Alt, H, 1 typed straight
                off sets bold, and Alt, H, H, ↓, →, Enter fills the cell from the palette, as typed one at a time. Your
                rows and buttons have a KeyTip where you set <code>KeyTip</code>; two letters that collide at one level
                are refused when the toolbar renders.
            </li>
            <li>
                <b>The look is the Chrome's.</b> The built-in Chrome draws plain buttons with inline-SVG pictures;
                <code>MudSheetChrome</code> draws MudBlazor's controls; under either the toolbar is one Tab stop. A Chrome
                of your own draws each item from its <code>ToolbarItemContext</code>: a list of its own opens when
                <code>OpenRequest</code> changes, and once it holds the keyboard it calls <code>ChoicesOpened(true)</code>
                and answers the keys that returns, typed on the toolbar while the list was on its way; it closes when
                <code>CloseRequest</code> changes; as it begins to close from a key or a choice it calls
                <code>ChoicesClosing</code> with that key and passes on every key typed in it, and once it has gone it
                calls <code>ChoicesClosed</code>, and the toolbar takes the keyboard on from there.
            </li>
```

### 13. samples/ExGrid.Docs/Pages/KeyboardByHand.cs

Old:

```
            "Show the KeyTips: each row's letter, then each item's, so that [Alt], [H], [1] sets bold.",
```

New:

```
            "Show the KeyTips: each row's letter, then each item's, so that [Alt], [H], [1] sets bold and [Alt], [H], [O], [E] opens Format Cells. [Escape] goes back one level.",
```

### 14. samples/ExGrid.Docs/Pages/Grid/GridSelectionAndKeys.razor

Old:

```
        <p>
            Every key of the three products, each with the page that explains it, is in the
            <a href="reference/keyboard">Keyboard reference</a>.
        </p>
```

New:

```
        <p>
            <strong>Keys typed at once arrive in order.</strong> Over Blazor Server a popover opens or closes a round
            trip after the key or the press that asked, and the keys typed meanwhile wait and then go where the
            keyboard lands: ↓, ↓ typed straight after Escape in a column's menu, or after Enter on a filter panel's OK,
            move the Focus two rows, rather than moving in a menu that is going or being lost on a button.
        </p>
        <p>
            Every key of the three products, each with the page that explains it, is in the
            <a href="reference/keyboard">Keyboard reference</a>.
        </p>
```

### 15. samples/ExGrid.Docs/Pages/Grid/GridAppearance.razor

Old:

```
            announced in, by the ids in <code>RefusalLabelIds</code>; answering <code>null</code> leaves the
            built-in English (<a href="exgrid/clipboard#what-a-refusal-sounds-like">what a refusal sounds like</a>).
        </p>
```

New:

```
            announced in, by the ids in <code>RefusalLabelIds</code>; answering <code>null</code> leaves the
            built-in English (<a href="exgrid/clipboard#what-a-refusal-sounds-like">what a refusal sounds like</a>).
            A third: a button that closes its panel — OK, Apply, Cancel — carries the class
            <code>ex-popover-closes</code>, as the built-in and MudBlazor panels' do, so that the keys typed while
            the panel goes wait for the keyboard to land rather than reach a button that is going.
        </p>
        <p>
            <strong>A frame of your own</strong> outside the grid — a dialog, a toolbar — takes the keyboard through
            <code>HandKeyboardToFrameAsync</code> and gives it back through <code>ReturnKeyboardAsync</code>, with the
            frame's element and the keys it heard on the way. A popover of yours in the grid's frame
            (<code>OpenPopoverAsync</code>) can ask to hear Escape close it, and hand the keyboard on from there. Over
            Blazor Server each of these lands a round trip late, and the grid holds the keys typed meanwhile and
            hands them on, in order, to where the keyboard lands. ExSheet's Sheet Toolbar is built this way. A dialog
            of yours with a grid inside, closing on the Escape that raises the grid's <code>OnLeave</code>, calls
            <code>PassKeysOutAsync</code> there: the keys typed after the Escape go out to your dialog, for it to hand
            on with the keyboard, as ExPivot's Show Details dialog does.
        </p>
```

### 16. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Old:

```
  - The release is the Consumer's to hear, from a `keyup` that reaches it from the grid's root.
    No listener is added to the grid.
```

New:

```
  - The release is the Consumer's to hear, from a `keyup` that reaches it from the grid's root.
    No listener is added to the grid.
    *(Reversed 2026-10-13, decided with the user: the grid's script hears Alt's `keyup` on the root and
    holds it in order with the keys, sharing ED-30's root `keyup` listener, ADR-0021's seventh entry; no
    listener is added. Item 16's note of 2026-10-12 says how.)*
```

### 17. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Old:

```
- **The Alt key's release is heard from a `keyup` on the Sheet's own element.** That listener is
  there only while the toolbar is shown. Another key's release in between makes the Alt a chord's.
  - The grid stops the keys it claims at keydown, so a chord such as Alt+↓ is seen only by its
    release.
  - Alt released before the ↓ of Alt+↓ therefore shows the KeyTips. Escape takes them away. This
    is left as it is, rather than adding a listener to the grid.
```

New:

```
- **The Alt key's release is heard from a `keyup` on the Sheet's own element.** That listener is
  there only while the toolbar is shown. Another key's release in between makes the Alt a chord's.
  - The grid stops the keys it claims at keydown, so a chord such as Alt+↓ is seen only by its
    release.
  - Alt released before the ↓ of Alt+↓ therefore shows the KeyTips. Escape takes them away. This
    is left as it is, rather than adding a listener to the grid.
  - *(2026-10-13, decided with the user: the grid's script hears Alt's `keyup` on the root and holds it
    in order with the keys, sharing ED-30's root `keyup` listener.)* The Sheet's listener stays, and hears
    the release through the grid: held in order with the keys while the grid holds any, and never a
    chord's, which the grid's key gate tells by the keys it hears between the press and the release. So
    another key's release no longer makes the Alt a chord's in the Sheet, and Alt released before the ↓
    of Alt+↓ no longer shows the KeyTips. This reverses "rather than adding a listener to the grid" above:
    the grid's own `keyup` listener, ED-30's, is widened (ADR-0021's seventh entry), and none is added.
```

### 18. docs/adr/0021-javascript-is-allowlisted-not-minimised.md

Old:

```
| **The Keyboard Field's composition and focus, on the grid root** | Added 2026-10-02 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)). An IME starts only in an element that is editable, and no Blazor API gives the root, a `div`, an input context. So on a grid that edits a text field of the grid's own holds the keyboard. Its composition's end has to take its place among the held keys and presses, which exist only in the browser (the first entry's hold), so `compositionstart` and `compositionend` on the root are heard there, always on. The root's own `focus` is passed on to the field at once: Blazor's `@onfocus` and `FocusAsync` land a round trip later on a circuit, and a key typed in between was read as a descendant's (ADR-0033's scroller, 2026-09-26). The root's `focusout` empties the field as it is left, and ends the release of Tab when DOM focus leaves the grid (ADR-0012). No layout is read. *(`keyup` added 2026-10-12 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)), decided with the user: the root's `keyup` says that a key the IME took in the field has been told whole. The editor's request for the keyboard waits for it, so that it is granted in order with the IME's events, where the task after a composition's end missed a start the browser told a moment later. A key the IME took is told from its keydown, which the first entry already leaves to the IME, to the next keyup heard on the root, or until the keyboard leaves the root, which the root's `focusout` tells: Alt+Tab mid-composition sends its keyup to the other window. Nothing is measured, and no timer is set.)* |
```

New:

```
| **The Keyboard Field's composition and focus, on the grid root** | Added 2026-10-02 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)). An IME starts only in an element that is editable, and no Blazor API gives the root, a `div`, an input context. So on a grid that edits a text field of the grid's own holds the keyboard. Its composition's end has to take its place among the held keys and presses, which exist only in the browser (the first entry's hold), so `compositionstart` and `compositionend` on the root are heard there, always on. The root's own `focus` is passed on to the field at once: Blazor's `@onfocus` and `FocusAsync` land a round trip later on a circuit, and a key typed in between was read as a descendant's (ADR-0033's scroller, 2026-09-26). The root's `focusout` empties the field as it is left, and ends the release of Tab when DOM focus leaves the grid (ADR-0012). No layout is read. *(`keyup` added 2026-10-12 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)), decided with the user: the root's `keyup` says that a key the IME took in the field has been told whole. The editor's request for the keyboard waits for it, so that it is granted in order with the IME's events, where the task after a composition's end missed a start the browser told a moment later. A key the IME took is told from its keydown, which the first entry already leaves to the IME, to the next keyup heard on the root, or until the keyboard leaves the root, which the root's `focusout` tells: Alt+Tab mid-composition sends its keyup to the other window. Nothing is measured, and no timer is set.)* *(Widened 2026-10-13, decided with the user: the same `keyup` listener hears the Alt key's release where the Consumer declares the Alt key alone (ADR-0050 item 14, ADR-0100's KeyTips). While the first entry's hold holds keys, the release is kept from the Consumer and held in its place, and handed on, in its turn, as the keyup it was; a chord's release is kept from the Consumer. No listener is added, and the count MEM-4 reads is unchanged. Nothing is measured.)* |
```

### 19. docs/adr/0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md

Old:

```
- **DC-61 says what the method does, and PV-41 what ExPivot does with it.** Layer 2 checks that each
  request goes through the report's grid, under both Chromes. Layer 3 puts DOM focus in the records
  in the task that draws them, where a press there would put it, before the control's request can
  land. With either fix taken out, every run of those tests failed on the Server host; with both,
  every run passed on both hosts.

## Considered options
```

New:

```
- **DC-61 says what the method does, and PV-41 what ExPivot does with it.** Layer 2 checks that each
  request goes through the report's grid, under both Chromes. Layer 3 puts DOM focus in the records
  in the task that draws them, where a press there would put it, before the control's request can
  land. With either fix taken out, every run of those tests failed on the Server host; with both,
  every run passed on both hosts.

## Keys passed out as a dialog closes *(2026-10-13)*

*(Decided with the user: the keys typed as the keyboard goes back are held and handed on in order,
family-wide — ExPivot's Show Details dialog among the frames.)* On a circuit the dialog stood a round trip
and more after the Escape its grid raised `OnLeave` on, and the keyboard stayed in the dialog's grid
until it had gone: ↓ typed straight after the Escape moved in the closing grid and was lost, the report's
Focus staying where it was (observed 2026-10-13, the Server host at 80 ms, both Chromes).

- **`ExGrid.PassKeysOutAsync()`**, called from `OnLeave` before its first await, tells the grid the
  keyboard is leaving it for the Consumer's element around it. The keys the gate held behind that Escape,
  and those typed on the root until DOM focus leaves it, are passed to the root's parent, in order, as the
  keydowns they were, and the grid acts on none. A press on the grid takes the keyboard back into it.
- **While a Consumer listens to `OnLeave`, the keys after a plain Escape on the root wait for its answer**,
  as the keys after a declared key do, so none is the grid's before the Consumer has said where they go.
- It is not "the grid lets Escape bubble", rejected below: the Escape is the grid's as before, and only the
  keys after it go out, and only when the Consumer asks.
- **ExPivot's dialog closes the other way round.** However it closes, its frame holds the keys typed in it
  from then on, its grid passing its own out to it; a render takes the report's `inert` away while the
  dialog stands; the report's grid takes the keyboard back from the dialog with those keys
  (`ReturnKeyboardAsync(from, typedOnTheWay)`, ADR-0050 item 16's note of 2026-10-12); and then the dialog
  goes. Layer 3: Escape in the dialog's grid then ↓, and Escape on its Close then ↓, ↓, typed at once,
  under both Chromes, on both hosts.

## Considered options
```

### 20. docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md

Old:

```
- One menu or panel is open at a time in each ExPivot, and two ExPivots on one page share nothing
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).
```

New:

```
- One menu or panel is open at a time in each ExPivot, and two ExPivots on one page share nothing
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).

*(2026-10-13, decided with the user: the keys typed as the keyboard goes back are held and handed on in
order, family-wide — ExPivot's menus and panels among the frames.)* A menu or a panel closed from inside
it — Escape, a command, OK, Cancel — gives the keyboard back to its button a round trip later on a
circuit, and a key typed meanwhile was the closing menu's: Enter typed straight after Escape ran its first
command.

- **Closed from inside with DOM focus in it, it stands until the keyboard has left it** for its button,
  which the frame's `focusout` tells; its commands and OK do nothing meanwhile.
- **The keys typed in it meanwhile are the button's**, in order: Enter or Space presses it; Tab, which only
  the browser can act on, is dropped with every key after it (ADR-0010); anything else means nothing on a
  button.
- Closed with the keyboard outside it — its button pressed again, a press elsewhere — it goes at once.
- Nothing listens on `document`, nothing is measured, and no JavaScript is added: the frame's own Blazor
  `keydown`, `focusin` and `focusout` (ADR-0021).
- Layer 3: Escape in a field's menu, then Enter typed at once, runs no command and opens the menu again,
  under both Chromes, on both hosts.
```

### 21. docs/definition-of-done.md

Old:

```
| **SH-50** | MUST | Whenever the Selection changes, each formatting item shows the Focus cell's `CellFormatAt` (Bold pressed over a bold cell, `aria-pressed`); a press acts on the grid's Selection as it is now, as one undo step; while an edit is open every Toolbar Item, the Consumer's included, is unavailable; a pointer press leaves DOM focus on the Sheet, the toolbar is one Tab stop with ← and → among its items, and a command run from the keyboard gives the keyboard back to the Sheet. The same markup draws plain buttons under the built-in Chrome and MudBlazor's controls under `ExSheet.MudBlazor`, with the same behaviour; its dropdowns take Format Cells' frames; no script is added (ADR-0100, ADR-0071, ADR-0010, ADR-0021) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, with a clean console | as stated; a press straight after Shift+ArrowDown on the Server host formats the extended range |
```

New:

```
| **SH-50** | MUST | Whenever the Selection changes, each formatting item shows the Focus cell's `CellFormatAt` (Bold pressed over a bold cell, `aria-pressed`); a press acts on the grid's Selection as it is now, as one undo step; while an edit is open every Toolbar Item, the Consumer's included, is unavailable; a pointer press leaves DOM focus on the Sheet, the toolbar is one Tab stop under either Chrome (*2026-10-13, decided with the user*: no control inside it takes Tab) with ← and → among its items, and a command run from the keyboard gives the keyboard back to the Sheet. The same markup draws plain buttons under the built-in Chrome and MudBlazor's controls under `ExSheet.MudBlazor`, with the same behaviour; its dropdowns take Format Cells' frames; no script is added (ADR-0100, ADR-0071, ADR-0010, ADR-0021) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, with a clean console | as stated; a press straight after Shift+ArrowDown on the Server host formats the extended range |
```

### 22. docs/definition-of-done.md

Old:

```
| **SH-45** | MUST | Format Cells opens from Ctrl+1, from the Context Menu's "Format Cells…" and from `OpenFormatCellsAsync`; it offers Number, Alignment, Font, Border and Fill; Accounting and Fraction are shown disabled with the reason; a Custom code ExSheet does not read is refused by name; it opens on the Focus cell's Cell Format; OK sets only what the user touched as one undo step, and Cancel or Esc sets nothing; the tabs switch with the arrow keys; on closing the keyboard is the Sheet's again. Under the built-in Chrome it is a popover inside the Sheet's box (DC-60); under `ExSheet.MudBlazor` it is a `MudDialog` (ADR-0071, ADR-0010) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes | as stated |
```

New:

```
| **SH-45** | MUST | Format Cells opens from Ctrl+1, from the Context Menu's "Format Cells…" and from `OpenFormatCellsAsync`; it offers Number, Alignment, Font, Border and Fill; Accounting and Fraction are shown disabled with the reason; a Custom code ExSheet does not read is refused by name; it opens on the Focus cell's Cell Format; OK sets only what the user touched as one undo step, and Cancel or Esc sets nothing; the tabs switch with the arrow keys; on closing the keyboard is the Sheet's again, and the keys typed as it closes from Escape, OK or Cancel reach the Sheet in order, OK setting what was touched as it is pressed (*2026-10-13, decided with the user*). Under the built-in Chrome it is a popover inside the Sheet's box (DC-60); under `ExSheet.MudBlazor` it is a `MudDialog` (ADR-0071, ADR-0010) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, the keys after Escape and OK typed at once at an 80 ms round trip on the Server host | as stated |
```

### 23. docs/definition-of-done.md

Old:

```
| **PV-39** | MUST | Show Details' dialog closes on an Escape its grid has nothing left to dismiss. However the dialog closes — Escape, Close, the backdrop — the report's grid takes the keyboard back. A details tab that closes while selected hands the keyboard to the tab selected next: the report's grid, when that is the report's tab. Escape in a details tab's grid closes nothing (ADR-0070) | Layer 2; Layer 3 on `/pivot?details=dialog` and `/pivot` | as stated, under both Chromes |
```

New:

```
| **PV-39** | MUST | Show Details' dialog closes on an Escape its grid has nothing left to dismiss. However the dialog closes — Escape, Close, the backdrop — the report's grid takes the keyboard back, with the keys typed in the dialog as it closes, in order (*2026-10-13, decided with the user*); a field's menu or panel closed from inside gives the keys typed as it closes to its button. A details tab that closes while selected hands the keyboard to the tab selected next: the report's grid, when that is the report's tab. Escape in a details tab's grid closes nothing (ADR-0070) | Layer 2; Layer 3 on `/pivot?details=dialog` and `/pivot`, the keys after Escape typed at once at an 80 ms round trip on the Server host | as stated, under both Chromes |
```

### 24. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

Old:

```
                <b>Under <code>MudSheetChrome</code></b> (<code>ExSheet.MudBlazor</code>) it is a page-level, modal
                <code>MudDialog</code>, with MudBlazor's colour picker for More Colours. It needs a
                <code>&lt;MudDialogProvider /&gt;</code> in your layout, and is refused by name when it first opens
                without one.
```

New:

```
                <b>Under <code>MudSheetChrome</code></b> (<code>ExSheet.MudBlazor</code>) it is a page-level, modal
                <code>MudDialog</code>, with MudBlazor's colour picker for More Colours. It needs a
                <code>&lt;MudDialogProvider /&gt;</code> in your layout, and is refused by name when it first opens
                without one. The keys typed as it closes from Escape, OK or Cancel are the Sheet's, in order, over
                Blazor Server too: ↓ typed straight after Escape moves the Focus, and OK sets what was touched as it is
                pressed.
```

### 25. samples/ExGrid.Docs/Pages/Pivot/PivotValues.razor

Old:

```
            behind it, Excel's drill-down. They open in a tab at the report's foot, beside the <em>PivotTable</em> tab that leads back, or in a dialog with <code>DetailsView="PivotDetailsView.Dialog"</code>, which Escape closes, leaving the report the keyboard. The records are read
```

New:

```
            behind it, Excel's drill-down. They open in a tab at the report's foot, beside the <em>PivotTable</em> tab that leads back, or in a dialog with <code>DetailsView="PivotDetailsView.Dialog"</code>, which Escape closes, leaving the report the keyboard, with the keys typed as it closes: ↓ typed straight after Escape moves the report's Focus, over Blazor Server too. The records are read
```

### 26. samples/ExGrid.Docs/Pages/Pivot/PivotLayoutPage.razor

Old:

```
                Escape closes it, a panel's changes are discarded unless OK applied them, and the keyboard goes
                back to what opened it.
```

New:

```
                Escape closes it, a panel's changes are discarded unless OK applied them, and the keyboard goes
                back to what opened it, with the keys typed as it closes: Enter typed straight after Escape opens the
                menu again rather than running one of its commands, over Blazor Server too.
```
