# Run 17, Parts C to F: the toolbar under MudBlazor, and Format Cells' letters

Branch `claude/run17cf-toolbar-mud`, 2026-10-12, three rounds. Findings D-mud-lists, D-mud-console and D-letter-O
of the seventeenth Windows run (`verification/2026-10-11-windows-17/report.md`, Part D); readings R2 (a) and (b) of
the Windows readings of 2026-10-12 (`verification/2026-10-12-windows-readings/report.md` on
`claude/exsheet-windows-readings-2026-10-12`); the user's decisions of 2026-10-12 listed below; and the verifier's
six findings on round 2. This file holds the text for the files a background agent does not edit — ADR-0100,
ADR-0050, ADR-0044, ADR-0021, ADR-0071, the Definition of Done and the Docs Site — as numbered edits of whole lines
under "Edits", each old text occurring once at the branch tip and the list applying in order; and what the third
round stopped on.

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

The user also said that round 2's stop 1 stands. How it reads now is my reading of decision (B), below.

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
the close came from Escape typed on the band (R2 (b)). The `CloseRequest` XML doc says so. Layer 2 pins it
(`A_close_request_closes_a_list_that_holds_the_keyboard_back_to_the_keytips`); layer 3 does not reach the window,
which is a round trip wide.

## Stops, for the user

### 1. The Alt-alone hold cannot give one outcome at every speed without a decision (finding 2)

**The case.** The gate hears no `keyup` (ADR-0021), and the release of the Alt key is the Consumer's to hear: ADR-0100
chose the Sheet's own Blazor `keyup` for it "rather than adding a listener to the grid", and ADR-0050 item 14's note
says "No listener is added to the grid". The gate knows the release of a declared Alt that went down alone by the
key after it, and holds that key until the Sheet has answered the release; the Sheet decides on the release from the
press it heard through the declared key. Whenever an Alt goes down alone while the gate holds keys, its press and
its release reach the Sheet in an order that depends on the round trip:

- Held in its turn, its press reaches the Sheet after its own release, which the Sheet then heard as no Alt's:
  Enter (an edit's commit), then Alt typed at once, and the KeyTips never show.
- Forwarded at once (round 2's `altGoesFirst`), its press overtakes the keys held before it: Alt, Alt, H, 1 typed
  slowly turns the KeyTips off and types "h1"; typed at once the second Alt restarts them and sets bold. Forwarded
  ahead of keys a frame heard on the way back, whose contents are not yet known, it overtakes them too.
- Replayed to the band, as any key, it would be skipped as a modifier, and its release would already have reached
  the Sheet.
- Typed on the toolbar's band while it gives the keyboard back, or in a MudBlazor list as it closes, its press is
  held there with the keys, and its release reaches the Sheet, or nothing at all from MudBlazor's popover, which is
  outside the Sheet's element.

Evidence: the trace of these orders is in the code (`afterAltAlone`, `altGoesFirst`, the drain's modifier skip,
`ExSheet.OnKeyUpAsync`, `SheetToolbar.OnKeyAsync`); the first two are the verifier's observations on round 2. No
layer-3 case pins them: a case would pin a defect. Pairing the Sheet's `keyup` with the gate's presses by counting
them needs a count both sides keep, which a key pressed or released outside the Sheet's element breaks for every key
after it.

**Options.** Each but (c) makes every order give the slow outcome; each but (c) changes a recorded decision or adds
an ask of the core.

- (a) **The gate hears Alt's `keyup` on the root, in the capture phase, and holds it with the keys** while it holds
  any, replaying it in turn as it replays a key: the Sheet's handler hears the release where it was typed, on the
  root or on the band, and every order gives the slow outcome. The inference by the next key, `altGoesFirst` and the
  empty question go. The band and a MudBlazor list hold the `keyup`s typed on them in flight with their keys (Blazor
  handlers, no script). It is a listener ADR-0021's first entry does not name, and it reverses ADR-0100's and
  ADR-0050 item 14's "no listener is added to the grid": a widening of the first entry, or a new one. A capture-phase
  `keyup` listener on the root is already proposed on `claude/ed30-composition-race` (23c6e9de, marked there as
  pending a decision), for the Keyboard Field's IME under ADR-0021's seventh entry; (a) would widen what that
  listener is for, rather than add a second one.
- (d) **The core hears the release through a Blazor `keyup` on its own root** (no script; ADR-0021 unchanged): the
  gate tells the core of a held Alt as it holds it, the core pairs the release it hears after that with it, and
  raises the press in its turn with whether it was already released (a new member of `GridDeclaredKeyPress`). It
  still reverses ADR-0050 item 14's "No listener is added to the grid", and the band and a MudBlazor list still hold
  the `keyup`s typed on them in flight, as in (a).
- (h) **The release stays the Consumer's, and the core raises a held Alt twice**: its press at once, marked as ahead of
  held keys, so that the release the Consumer hears comes after it, and its turn once the keys before it have been
  answered, or word that it went to a frame or a popover, or was dropped; the Consumer acts on the release only at
  the turn. No listener, and no recorded decision reversed; new members of `GridDeclaredKeyPress` and a ticket per
  held Alt, which the toolbar's band also issues for an Alt typed on it while it gives the keyboard back. The
  heaviest, and an Alt typed in a MudBlazor list as it closes is still not heard released (its popover is outside
  the Sheet's element) unless the list passes its `keyup`s on too.
- (b) **The core raises the Alt key alone in turn and the Consumer answers the press, not the release**: the KeyTips
  start on Alt's keydown, a chord's key after it ends them. ADR-0100's "pressed and released alone" changes, and so
  does what a Consumer hears for `Alt+Alt`.
- (c) **Keep the Alt-alone hold as built**, and let ADR-0100 and the Change Notes say that an Alt typed while keys are
  held can have an outcome that depends on the round trip: a known limitation against the spine's sixth principle.

My recommendation, not a decision: (a), the one queue every key and its release go through, and the smallest change.

Until this is decided the branch keeps round 2's code for Alt; the texts below claim no more than its tests ran (one
Alt, or F10, then letters, typed at once).

### 2. Keys typed on the band while a list opens

Not covered by the decisions. Under either Chrome a list a KeyTip or the band's ↓ opened takes the keyboard a round
trip later on a circuit, and a key typed on the band meanwhile is the band's: ↓ moves nothing in the list, and Enter
presses the band's item. Escape is handled (it goes back a level, R2 (b)). Holding the others for the list needs the
toolbar to hand keys to a Chrome's list — a second seam member — and the built-in popover's hold to take keys from
the band. Options: (a) build both; (b) leave it, and let ADR-0100 say so.

### 3. MudMenu's own buttons are tab stops under `MudSheetChrome`

ADR-0100 and SH-50 hold the toolbar to one Tab stop. Under MudBlazor's Chrome Tab also reaches Number Format's button
and the three split controls' arrows, which MudMenu draws (its activator wrapper carries `tabindex="0"` even with
`ActivatorContent`). The trap is gone, and a key typed there is the button's. Options: (a) leave the extra stops and
let ADR-0100 say so; (b) draw the menus' lists in a MudPopover of the Chrome's own, with buttons of its own that take
no Tab (a rewrite of `MudToolbarItem`'s lists, re-run on both hosts).

### 4. Popovers and dialogs outside the grid's root

The decision of 2026-10-12 names "any popover the grid or a Chrome shows … filter panels, Format Cells' and other
dialogs' lists … under the built-in Chrome and under MudBlazor". Two kinds stand outside the grid's root, where its
key gate does not reach, and were not built:

- ExPivot's own frames (ADR-0061): the Field List's menus and panels (an item filter among them) and the Pivot
  Toolbar's, `PivotPopupFrame` and `PivotDialogFrame`, under both of ExPivot's Chromes. Escape or a choice closes
  one in a render, and the button that opened it takes the keyboard a round trip later (`PivotFocusButton`).
- `ExSheet.MudBlazor`'s Format Cells, a MudDialog (ADR-0071), closed by Escape, OK or Cancel; under the built-in
  Chrome Format Cells is the grid's popover and holds.

Read off the code, not run: on a circuit a key typed while one closes reaches the closing frame's control, or
nothing once the frame is gone and before the button has the keyboard. Holding it needs its default prevented the
moment it is typed — Tab, an arrow on a radio, Space on a checkbox act at once — and a Blazor handler's
`preventDefault` is the render's, a round trip late; ADR-0021 keeps script out of these frames. Options: (a) leave
them, and let ADR-0044's note say the hold covers the grid's popovers and the Sheet Toolbar's lists, with these a
known limitation; (b) each frame holds the keys it hears once its close has begun and hands them on where the
keyboard lands, with the defaults of keys typed before that render reaches the browser still acting in the closing
frame (narrower, not deterministic); (c) open them in the grid's popover frame (`OpenPopoverAsync`), where the gate
holds: ExPivot's frames stand outside any grid by ADR-0061, and MudBlazor's dialog is ADR-0071's choice; (d) a
listener of the grid's script on these frames, a new ADR-0021 entry.

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
- ExPivot's frames and MudBlazor's Format Cells dialog left to stop 4 rather than built.
- The F10 and the ↓ cases, rather than narrowing the texts that name them.
- Stop 1's options (d) and (h), and the recommendation of (a).

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
  - *(As built, 2026-10-12.)* MudMenu draws its own button for Number Format and for each split control's
    arrow, and Tab reaches them: under MudBlazor's Chrome the toolbar is not one Tab stop. A key typed on
    one is the button's, and the toolbar holds none of it.

### The toolbar's keys, held in order *(2026-10-12)*

The seventeenth run's Part D, layer 3 at an 80 ms round trip, and the verifier's two rounds found places
where what a key did on the toolbar depended on when it was typed (the spine's sixth principle).

- *(2026-10-12, decided with the user, decision (C): the letters typed after the release of the Alt key
  alone wait for the KeyTips.)* On a circuit the KeyTips take the keyboard a round trip after Alt's
  release, and A4 pressed, Alt, H, 1 typed at machine speed opened an edit in A4 holding `h1`.
  - The grid's key gate holds the keys typed after the release of a declared Alt that went down alone,
    until the Sheet has answered the release (ADR-0050 item 16's note of 2026-10-12). The Sheet, starting
    the KeyTips, tells the gate the keyboard is going to a frame of its own, the toolbar, before it first
    waits, and gives the band the keyboard through the core, which gives it in turn, with the held keys,
    in order. A Tab among them is dropped with the keys behind it. F10 takes the same way.
  - *(As built.)* One Alt, or F10, then letters, typed at once: the same outcome as typed slowly, on
    either host. An Alt pressed alone again while keys are held is not: its press and its release can
    reach the Sheet in either order. Open.
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
    for the Consumer.** The release is the Consumer's to hear (ExSheet's `keyup`, ADR-0100); the gate hears
    no keyup (ADR-0021) and knows the release by the key after it: one that does not carry Alt, or Alt
    pressed again. That key and every key after it are held until an empty question to the core (`Ping`),
    which travels behind the release, is answered. The gate waits for nothing else, so the Consumer's
    handler tells it before its first `await`: `HandKeyboardToFrameAsync`, then `HandKeyboardToAsync` for
    the frame's control. Handed off, the keys go to the frame, or are dropped with every key behind one it
    cannot take, a Tab; told nothing, they are the grid's. A chord (a key carrying Alt while it is down), a
    press on the grid and DOM focus leaving it end the wait before it starts.
  - *(As built; open, ADR-0100's note of 2026-10-12.)* An Alt pressed alone again while keys are held is
    forwarded at once when nothing but such releases and the keys a frame heard on the way back stand
    before it, and leaves a mark a frame's hand-off waits behind; otherwise it is held as a modifier. Its
    press and its release can then reach the Consumer in either order, and the outcome can depend on the
    round trip.
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
  filter panel's OK was lost on the closing button. E held on a checkbox outside
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
```

### 10. docs/definition-of-done.md

Old:

```
| **SH-52** | MUST | While the Sheet holds the keyboard, its Sheet Toolbar is shown and no edit is open, Alt (Option on macOS) released alone, or F10, shows a KeyTip per Toolbar Row (the formatting row's is H); a row's letter shows its items' letters, Excel's Home tab letters where Excel has one and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses); a letter runs its item and leaves the keyboard on the Sheet; Escape goes back one level and ends from the top; an unmatched key ends it doing nothing; Alt with another key stays a chord (Alt+↓, Alt+Enter); a Consumer's letters are only those declared, and two colliding at one level are refused at render naming both; while an edit is open they start nothing, and with the toolbar hidden Alt and F10 stay the browser's; on Windows the browser's menu does not open from a Sheet whose toolbar is shown (ADR-0100, ADR-0021) | Layer 2; Layer 3; beside Excel and by hand on Windows for the browser's keys (a Windows run) | as Excel, where Excel has the letter |
```

New:

```
| **SH-52** | MUST | While the Sheet holds the keyboard, its Sheet Toolbar is shown and no edit is open, Alt (Option on macOS) released alone, or F10, shows a KeyTip per Toolbar Row (the formatting row's is H); a row's letter shows its items' letters, Excel's Home tab letters where Excel has one, Format Cells' through Excel's Format menu (O, E), and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses); a letter runs its item and leaves the keyboard on the Sheet; Escape goes back one level and ends from the top — from a list a KeyTip opened to its row's KeyTips, from letters that narrow a level to that level's (*2026-10-12, the readings' R2 (a) and (b), observed*); an unmatched key ends it doing nothing; Alt with another key stays a chord (Alt+↓, Alt+Enter); a Consumer's letters are only those declared, and two colliding at one level are refused at render naming both; while an edit is open they start nothing, and with the toolbar hidden Alt and F10 stay the browser's; on Windows the browser's menu does not open from a Sheet whose toolbar is shown (ADR-0100, ADR-0021) | Layer 2; Layer 3; beside Excel and by hand on Windows for the browser's keys (a Windows run) | as Excel, where Excel has the letter |
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
                opens Format Cells, as Excel's Format menu reaches it (read on Windows). The letters can be typed
                straight after Alt, over Blazor Server too: those typed before the KeyTips show wait for them. A list a
                KeyTip opens takes the keyboard under either Chrome: the arrows move among its choices and Enter makes
                one. Escape goes back one level, as Excel's did when read on Windows: from a list to its row's letters,
                then to the rows', then to the Sheet. A letter, an arrow, Enter or Escape typed while a list closes, or
                while the keyboard goes back to the Sheet, goes where the keyboard lands. Your rows and buttons have a
                KeyTip where you set <code>KeyTip</code>; two letters that collide at one level are refused when the
                toolbar renders.
            </li>
            <li>
                <b>The look is the Chrome's.</b> The built-in Chrome draws plain buttons with inline-SVG pictures;
                <code>MudSheetChrome</code> draws MudBlazor's controls. A Chrome of your own draws each item from its
                <code>ToolbarItemContext</code>: a list of its own opens when <code>OpenRequest</code> changes and closes
                when <code>CloseRequest</code> does; as it begins to close from a key or a choice it calls
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
            hands them on, in order, to where the keyboard lands. ExSheet's Sheet Toolbar is built this way.
        </p>
```
