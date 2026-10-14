# Apply list: claude/run17cf-toolbar-mud (ADR, Definition of Done)

For the orchestrator. Blocks 1 to 19 are the proposal's ADR and Definition of Done edits (the proposal's
edits 1-11 and 16-23, in its order), re-based onto the branch after `origin/main` (112a0ccb) was merged
in: every Old text still occurs once there, and no block's meaning is changed by what main brought (main's
ADR-0010, ADR-0050, ADR-0071 and Definition of Done changes touch other passages). The proposal's Docs Site
edits (12-15, 24-26) are applied on the branch.

Block 20 is new: main's ADR-0039 note of 2026-10-13 (#108) says the Sheet Toolbar's choices do not take
that note's path; the branch now opens Format Cells from More Number Formats… in the list's place, which
does (the orchestrator's item B). It is marked "as built", not a decision of the user's.

Block 21 is new: ADR-0050 item 16 gains what the key gate now does with the keys a frame hands back while
a hand-off stands (needed by block 22, and it closes the gap the new layer-3 case found under
`ExSheet.MudBlazor`, block 20), built for block 22 and adopted with the user on 2026-10-14. It follows
block 5 in the same ADR.

Block 22 is the ADR-0061 text for the user's decision of 2026-10-13 on keys typed while one of ExPivot's
menus or panels opens (the proposal's stop 1, option (b)); it follows block 16 in the same ADR. Its
"decided with the user" covers option (b) itself; the mechanism is marked "as built".

Blocks 23 to 27 are the user's decision of 2026-10-14 on widening: the Sheet Toolbar's Font Colour widens
a column as Excel's Home tab Font Colour button does — by its face and from its list (the readings of
2026-10-14, run 18a, widening case 5) — while `SetCellFormatAsync` keeps widening as COM's `Font.Color`
does, which does not widen. Blocks 23 to 26 are ADR-0071's (notes on "What the seventeenth Windows run
settled", and a section of the date), block 27 ADR-0100's. The mechanism — the toolbar's items set the
colour through `SetCellFormatFromFocusAsync`, whose rule becomes Excel's Home tab buttons' — is marked
"as built".

Block 28 is ADR-0202's, for the user's decision of 2026-10-14 on block 22's limit: the letters typed while
a report filter's Filter… opens with no report grid shown are dropped, Escape excepted, and listed as a
Known limitation of `0.1.0` (its line is in `docs/agents/release-cut/0.1.0-known-limitations.md`). Block
22 says so too.

Block 29 is the Definition of Done row for ExPivot's opening hold (block 22), PV-50, added with the user on
2026-10-14.

Blocks 4 and 5 also say, as built on 2026-10-14, how three of their layer-3 cases were made to pass on the
Server host: an Alt typed while keys are held is judged where it lands in its turn (block 5), the band
answers the keys it held for itself so that one sending the keyboard on holds the rest (block 4), and the
built-in list answers Enter and Space at their keydown (block 4).

Check: `python3 apply_list.py <this file> --check --root <worktree>`.

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
  - *(As built, 2026-10-14.)* The keys the band holds for itself — typed in a list closing, before the band
    holds the keyboard — are answered in order once it does, and a key among them that sends the keyboard
    on, back to the Sheet or into a list, holds the rest from its first step, before it waits, as the band
    holds a key typed on it then. Answered one after another, the rest came after the grid had asked for
    the keys held, and were lost: three Escapes in a list a KeyTip opened, then Alt, H, H, left nothing on
    the Server host under `ExSheet.MudBlazor`. An Alt's release among them goes on with them.
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
    *(As built, 2026-10-14.)* The built-in list answers Enter and Space at their keydown, choosing where
    the keys have reached, as it answers the arrows, and not by the browser's activation of the button DOM
    focus is on: the grid hands a held key on as a key dispatched from script, which activates nothing, and
    on the Server host the Enter of Alt, H, H, ↓, →, Enter chose nothing. The button's own click after
    Enter or Space finds the choice made; a list makes one.
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
    *(As built, 2026-10-14.)* An Alt held is judged where it lands in its turn, not where it was typed: one
    typed in a popover closing among the held keys — three Escapes in a list a KeyTip opened, then Alt,
    H, H — reaches the Consumer alone when its turn hands it to the root, an edit or the frame the keys go
    to, and is no one's on a popover's control or in a text field, as an Alt typed there is, nor is its
    release held with it. Judged where it was typed, it was a chord's, and H, H opened an edit in the cell.
    A release handed to a frame among the keys going there is not followed by the question: the frame
    answers its keys in the order they reach it, and the keys after the release go on to it in the same
    task.
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

### 12. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

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

### 13. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

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

### 14. docs/adr/0021-javascript-is-allowlisted-not-minimised.md

Old:

```
| **The Keyboard Field's composition and focus, on the grid root** | Added 2026-10-02 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)). An IME starts only in an element that is editable, and no Blazor API gives the root, a `div`, an input context. So on a grid that edits a text field of the grid's own holds the keyboard. Its composition's end has to take its place among the held keys and presses, which exist only in the browser (the first entry's hold), so `compositionstart` and `compositionend` on the root are heard there, always on. The root's own `focus` is passed on to the field at once: Blazor's `@onfocus` and `FocusAsync` land a round trip later on a circuit, and a key typed in between was read as a descendant's (ADR-0033's scroller, 2026-09-26). The root's `focusout` empties the field as it is left, and ends the release of Tab when DOM focus leaves the grid (ADR-0012). No layout is read. *(`keyup` added 2026-10-12 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)), decided with the user: the root's `keyup` says that a key the IME took in the field has been told whole. The editor's request for the keyboard waits for it, so that it is granted in order with the IME's events, where the task after a composition's end missed a start the browser told a moment later. A key the IME took is told from its keydown, which the first entry already leaves to the IME, to the next keyup heard on the root, or until the keyboard leaves the root, which the root's `focusout` tells: Alt+Tab mid-composition sends its keyup to the other window. Nothing is measured, and no timer is set.)* |
```

New:

```
| **The Keyboard Field's composition and focus, on the grid root** | Added 2026-10-02 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)). An IME starts only in an element that is editable, and no Blazor API gives the root, a `div`, an input context. So on a grid that edits a text field of the grid's own holds the keyboard. Its composition's end has to take its place among the held keys and presses, which exist only in the browser (the first entry's hold), so `compositionstart` and `compositionend` on the root are heard there, always on. The root's own `focus` is passed on to the field at once: Blazor's `@onfocus` and `FocusAsync` land a round trip later on a circuit, and a key typed in between was read as a descendant's (ADR-0033's scroller, 2026-09-26). The root's `focusout` empties the field as it is left, and ends the release of Tab when DOM focus leaves the grid (ADR-0012). No layout is read. *(`keyup` added 2026-10-12 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)), decided with the user: the root's `keyup` says that a key the IME took in the field has been told whole. The editor's request for the keyboard waits for it, so that it is granted in order with the IME's events, where the task after a composition's end missed a start the browser told a moment later. A key the IME took is told from its keydown, which the first entry already leaves to the IME, to the next keyup heard on the root, or until the keyboard leaves the root, which the root's `focusout` tells: Alt+Tab mid-composition sends its keyup to the other window. Nothing is measured, and no timer is set.)* *(Widened 2026-10-13, decided with the user: the same `keyup` listener hears the Alt key's release where the Consumer declares the Alt key alone (ADR-0050 item 14, ADR-0100's KeyTips). While the first entry's hold holds keys, the release is kept from the Consumer and held in its place, and handed on, in its turn, as the keyup it was; a chord's release is kept from the Consumer. No listener is added, and the count MEM-4 reads is unchanged. Nothing is measured.)* |
```

### 15. docs/adr/0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md

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

### 16. docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md

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

### 17. docs/definition-of-done.md

Old:

```
| **SH-50** | MUST | Whenever the Selection changes, each formatting item shows the Focus cell's `CellFormatAt` (Bold pressed over a bold cell, `aria-pressed`); a press acts on the grid's Selection as it is now, as one undo step; while an edit is open every Toolbar Item, the Consumer's included, is unavailable; a pointer press leaves DOM focus on the Sheet, the toolbar is one Tab stop with ← and → among its items, and a command run from the keyboard gives the keyboard back to the Sheet. The same markup draws plain buttons under the built-in Chrome and MudBlazor's controls under `ExSheet.MudBlazor`, with the same behaviour; its dropdowns take Format Cells' frames; no script is added (ADR-0100, ADR-0071, ADR-0010, ADR-0021) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, with a clean console | as stated; a press straight after Shift+ArrowDown on the Server host formats the extended range |
```

New:

```
| **SH-50** | MUST | Whenever the Selection changes, each formatting item shows the Focus cell's `CellFormatAt` (Bold pressed over a bold cell, `aria-pressed`); a press acts on the grid's Selection as it is now, as one undo step; while an edit is open every Toolbar Item, the Consumer's included, is unavailable; a pointer press leaves DOM focus on the Sheet, the toolbar is one Tab stop under either Chrome (*2026-10-13, decided with the user*: no control inside it takes Tab) with ← and → among its items, and a command run from the keyboard gives the keyboard back to the Sheet. The same markup draws plain buttons under the built-in Chrome and MudBlazor's controls under `ExSheet.MudBlazor`, with the same behaviour; its dropdowns take Format Cells' frames; no script is added (ADR-0100, ADR-0071, ADR-0010, ADR-0021) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, with a clean console | as stated; a press straight after Shift+ArrowDown on the Server host formats the extended range |
```

### 18. docs/definition-of-done.md

Old:

```
| **SH-45** | MUST | Format Cells opens from Ctrl+1, from the Context Menu's "Format Cells…" and from `OpenFormatCellsAsync`; it offers Number, Alignment, Font, Border and Fill; Accounting and Fraction are shown disabled with the reason; a Custom code ExSheet does not read is refused by name; it opens on the Focus cell's Cell Format; OK sets only what the user touched as one undo step, and Cancel or Esc sets nothing; the tabs switch with the arrow keys; on closing the keyboard is the Sheet's again. Under the built-in Chrome it is a popover inside the Sheet's box (DC-60); under `ExSheet.MudBlazor` it is a `MudDialog` (ADR-0071, ADR-0010) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes | as stated |
```

New:

```
| **SH-45** | MUST | Format Cells opens from Ctrl+1, from the Context Menu's "Format Cells…" and from `OpenFormatCellsAsync`; it offers Number, Alignment, Font, Border and Fill; Accounting and Fraction are shown disabled with the reason; a Custom code ExSheet does not read is refused by name; it opens on the Focus cell's Cell Format; OK sets only what the user touched as one undo step, and Cancel or Esc sets nothing; the tabs switch with the arrow keys; on closing the keyboard is the Sheet's again, and the keys typed as it closes from Escape, OK or Cancel reach the Sheet in order, OK setting what was touched as it is pressed (*2026-10-13, decided with the user*). Under the built-in Chrome it is a popover inside the Sheet's box (DC-60); under `ExSheet.MudBlazor` it is a `MudDialog` (ADR-0071, ADR-0010) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, the keys after Escape and OK typed at once at an 80 ms round trip on the Server host | as stated |
```

### 19. docs/definition-of-done.md

Old:

```
| **PV-39** | MUST | Show Details' dialog closes on an Escape its grid has nothing left to dismiss. However the dialog closes — Escape, Close, the backdrop — the report's grid takes the keyboard back. A details tab that closes while selected hands the keyboard to the tab selected next: the report's grid, when that is the report's tab. Escape in a details tab's grid closes nothing (ADR-0070) | Layer 2; Layer 3 on `/pivot?details=dialog` and `/pivot` | as stated, under both Chromes |
```

New:

```
| **PV-39** | MUST | Show Details' dialog closes on an Escape its grid has nothing left to dismiss. However the dialog closes — Escape, Close, the backdrop — the report's grid takes the keyboard back, with the keys typed in the dialog as it closes, in order (*2026-10-13, decided with the user*); a field's menu or panel closed from inside gives the keys typed as it closes to its button. A details tab that closes while selected hands the keyboard to the tab selected next: the report's grid, when that is the report's tab. Escape in a details tab's grid closes nothing (ADR-0070) | Layer 2; Layer 3 on `/pivot?details=dialog` and `/pivot`, the keys after Escape typed at once at an 80 ms round trip on the Server host | as stated, under both Chromes |
```

### 20. docs/adr/0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md

Old:

```
  The status line's figures menu and the Sheet Toolbar's choices were named at first, but read from the
  code they do not take this path: the first opens from a right-click, whose press already puts the
  keyboard on the root, and the toolbar closes its choices before it opens another popover. Find had
  the same gap, closed by FD-1's fix (#107).
```

New:

```
  The status line's figures menu and the Sheet Toolbar's choices were named at first, but read from the
  code they do not take this path: the first opens from a right-click, whose press already puts the
  keyboard on the root, and the toolbar closes its choices before it opens another popover. Find had
  the same gap, closed by FD-1's fix (#107).
  - *(2026-10-13, as built, read off the toolbar's code after #108.)* One of the toolbar's choices opens
    a popover: More Number Formats…, which opens Format Cells. Under the built-in Chrome its list closed
    through the frame first, asking for the root, and Format Cells then opened over no popover, so the
    hand-off to it reached the browser a message after that request; on a circuit a key landing between
    the two was the grid's. Such a choice is now made before its list closes, so the core opens Format
    Cells in the list's place and takes this path: the hand-off first, then the root, while the list
    still holds DOM focus. The list is closed afterwards only where nothing took its place.
  - *(2026-10-13, as built.)* Under `ExSheet.MudBlazor` Format Cells is a frame of the Chrome's, and its
    hand-off was told while the toolbar's closing list still held the keyboard, outside the root, where
    the gate took it as reached at once; the keys the toolbar then handed back were the grid's: Escape
    typed straight after More Number Formats… was lost and Format Cells stayed open. The hand-off is now
    told once the toolbar has asked the grid for the keyboard back, and the keys handed back go on to
    Format Cells (ADR-0050 item 16's note of 2026-10-13).
```

### 21. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Old:

```
  - **A hand-off told while the hold drains keeps the hold until the keyboard arrives,** so the keys typed
    meanwhile go where the keyboard is going, and none is gated against the root.
```

New:

```
  - **A hand-off told while the hold drains keeps the hold until the keyboard arrives,** so the keys typed
    meanwhile go where the keyboard is going, and none is gated against the root.
    *(2026-10-13, built for ADR-0061's note of that day; adopted with the user on 2026-10-14.)* So do the
    keys a frame hands back with the keyboard (`ReturnKeyboardAsync(from, typedOnTheWay)`): told while
    they are asked for, the hand-off holds them until the keyboard arrives, and they go there in order.
    This carries keys typed on one frame of the Consumer's own through the root to another: ExPivot's
    report filter whose Filter… is opening, and `ExSheet.MudBlazor`'s toolbar list closing as Format Cells
    opens. The hand-off is told after the keyboard is asked back: told while a control outside the root
    still holds it, a hand-off to a frame is taken as reached there, as before.
```

### 22. docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md

Old:

```
- Layer 3: Escape in a field's menu, then Enter typed at once, runs no command and opens the menu again,
  under both Chromes, on both hosts.
```

New:

```
- Layer 3: Escape in a field's menu, then Enter typed at once, runs no command and opens the menu again,
  under both Chromes, on both hosts.

*(2026-10-13, decided with the user: keys typed on a Field List entry or a Pivot Toolbar button while
its menu or panel opens are held and handed to the menu or panel, in order, once it holds the keyboard,
as the Sheet Toolbar's band does.)* On a circuit a menu or panel opened from its button takes the
keyboard a round trip after the press, and a key typed on the button meanwhile was the button's: Enter
pressed it again and closed the menu. The keys held behind an Enter that the closing direction answers
as a press of the button, which opens the menu again, are now held for it in turn rather than dropped.

- *(As built.)* **From the press that opens it until DOM focus comes into its frame** (the frame's
  `focusin`), ExPivot holds the keys typed where its button stands — the Field List, or the Pivot
  Toolbar — heard by a Blazor `keydown` on an element of ExPivot's own around them, which renders
  nothing; around the Pivot Toolbar that element draws no box (`ex-pivot-toolbar-keys`,
  `display: contents`). Enter, and Space but for its repeats, also press the button in the browser, and each such
  press is no press: the key is the menu's.
- *(As built.)* **A menu answers them as typed on its first enabled command**, where it puts the
  keyboard: Enter or Space presses that command; Escape closes the menu, and the keys after it are typed
  as it closes (above); Tab, which only the browser can act on, is dropped with every key after it
  (ADR-0010); any other key means nothing on a command.
- *(As built.)* **A panel — the report filter band's Filter… — puts the keyboard in its search**, where
  what a key types only the browser can do. While the report is shown, ExPivot carries the keyboard
  through the report's grid: told the keyboard is going to a frame of ExPivot's
  (`HandKeyboardToFrameAsync`), the grid takes it from the button with the keys typed there
  (`ReturnKeyboardAsync(from, typedOnTheWay)`), holds those typed after, and types them into the search,
  in turn, as the search takes the keyboard from it (`PivotItemFilterContext.TakeKeyboard`, ExGrid's
  `HandKeyboardToAsync`; [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) item 16's notes of
  2026-10-12 and 2026-10-13: the hand-off is told after the keyboard is asked back, so it is not taken
  as reached on the button). With no report shown — only report filters placed, or a details tab over the report —
  there is no grid to carry them: Escape closes the panel, and any other key is dropped with every key
  after it (ADR-0010), rather than typed somewhere else. *(2026-10-14, decided with the user: dropped, and
  listed as a Known limitation of `0.1.0`, ADR-0202.)*
- *(As built.)* Closed before it holds the keyboard — its button pressed again, a press elsewhere — it
  goes at once, and the keys held for it go with it.
- No use of JavaScript is added ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)): Blazor
  `keydown` on ExPivot's own elements, and ExGrid's key hold, which carries the keys a frame hands back
  on to a hand-off told meanwhile (ADR-0050 item 16's note of 2026-10-13).
- Layer 3, the Server host at a 150 ms round trip, under both Chromes: Enter on a field's entry, then
  Enter, typed at once, presses Move Down; Enter on a report filter, then U and S, typed at once, fill
  its search.

### 23. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
  (`WidensAsAKeyDoes`). So do `SetCellFormatAsync` and `SetCellFormatFromFocusAsync`, the command the
  toolbar's toggles set through (ADR-0100).
```

New:

```
  (`WidensAsAKeyDoes`). So do `SetCellFormatAsync` and `SetCellFormatFromFocusAsync`, the command the
  toolbar's toggles set through (ADR-0100). *(2026-10-14: `SetCellFormatFromFocusAsync` widens for a Font
  colour too, and `SetCellFormatAsync` still does not: "What the readings of 2026-10-14 settled about
  widening" below.)*
```

### 24. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
- **The toolbar's Font Colour widens nothing**, its face or a colour from its list, since both set
  through `SetCellFormatAsync`. That Excel's Font Color button widens nothing is a reading.
```

New:

```
- **The toolbar's Font Colour widens nothing**, its face or a colour from its list, since both set
  through `SetCellFormatAsync`. That Excel's Font Color button widens nothing is a reading.
  *(Replaced 2026-10-14, decided with the user: the Sheet Toolbar's Font Colour widens a column as Excel's
  Home tab Font Colour button does — by its face and from its list — while `SetCellFormatAsync` keeps
  widening as COM's `Font.Color` does, which does not widen. The reading was wrong: the readings of
  2026-10-14 saw the button widen. See "What the readings of 2026-10-14 settled about widening".)*
```

### 25. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
- The Home tab's Font Color button widens nothing, as ExSheet's Font Colour does not.
- `Range.Font` through COM acts as the keys for italic, strikethrough and a colour too.
```

New:

```
- The Home tab's Font Color button widens nothing, as ExSheet's Font Colour does not. *(Read 2026-10-14,
  run 18a, widening case 5: it widens, by its face and from its list. See below.)*
- `Range.Font` through COM acts as the keys for italic, strikethrough and a colour too. *(Read 2026-10-14,
  run 18a, widening cases 2 and 6: it does — italic and a colour widen nothing, and strikethrough widens.)*
```

### 26. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
- This is a difference that follows this ADR, not a change. It is a known limitation of `0.1.0`
  (ADR-0202's note of this date).

## Consequences
```

New:

```
- This is a difference that follows this ADR, not a change. It is a known limitation of `0.1.0`
  (ADR-0202's note of this date).

## What the readings of 2026-10-14 settled about widening *(2026-10-14)*

The readings were made on Windows (`verification/2026-10-14-windows-readings-18a/report.md`, its
`widening.md` part; `records/widen.jsonl`). Each case was a new Excel, A1 holding 123456789.12 in `0.00`
at the standard width (8.09), showing `########`.

**What they observed.**
- **The Home tab's Font Colour widened column A to 11.73**, pressed on its face (red), and with Blue
  chosen from its list in a workbook of its own (case 5).
- **`Font.Color = 255` set through COM did not widen it** (cases 2 and 6). Excel's button and Excel's
  method part here, where for bold, italic and underline they agree (R6, cases (3) and (5); case 6).

**Decided with the user.**
- *(2026-10-14, decided with the user: the Sheet Toolbar's Font Colour widens a column as Excel's Home
  tab Font Colour button does — by its face and from its list — while `SetCellFormatAsync` keeps widening
  as COM's `Font.Color` does, which does not widen.)*

**What ExSheet builds** *(as built)*:
- **`SetCellFormatFromFocusAsync`, the command the Sheet Toolbar's toggles and colours set through,
  widens as Excel's Home tab buttons do** (`WidensAsAHomeTabButtonDoes`): as the formatting keys do, and
  for a Font colour too. ADR-0100 has an item act through public commands only, so the button's rule is a
  command's. The toggles widen as before, each as the key it names, as Excel's Bold, Italic and Underline
  buttons did (R6, case (3)).
- **The toolbar's Font Colour sets through it**, by its face and from its list, and widens a column the
  user has not sized, in the press's one undo step, recorded as a width an entry widened (SH-26). A column
  the user sized never widens.
- **The Fill Colour sets through it too, and widens nothing**, as Excel's Fill Color button did not (the
  fourteenth run's case 7).
- **`SetCellFormatAsync` is unchanged**: a Font colour set through it widens nothing, as `Font.Color` set
  through COM did not. One change, a Font colour, so widens through `SetCellFormatFromFocusAsync` and not
  through `SetCellFormatAsync`, as bold widens through Format Cells' OK and not through
  `SetCellFormatAsync`.

## Consequences
```

### 27. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Old:

```
    - The Font Colour, its face and its list, sets through `SetCellFormatAsync`, so it widens
      nothing. That Excel's Font Color button widens nothing is a reading.
```

New:

```
    - The Font Colour, its face and its list, sets through `SetCellFormatAsync`, so it widens
      nothing. That Excel's Font Color button widens nothing is a reading.
    - *(Replaced 2026-10-14, decided with the user: the Sheet Toolbar's Font Colour widens a column as
      Excel's Home tab Font Colour button does — by its face and from its list — while
      `SetCellFormatAsync` keeps widening as COM's `Font.Color` does, which does not widen.)* The
      readings of 2026-10-14 saw Excel's button widen (run 18a, widening case 5). *(As built.)* The Font
      Colour, and the Fill Colour beside it, set through `SetCellFormatFromFocusAsync`, whose rule is now
      Excel's Home tab buttons': the keys', and a Font colour too. The Fill widens nothing either way.
      ADR-0071's section of that date holds it.
```

### 28. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

Old:

```
  ADR-0071 judges italic by the regular widths. It is a difference that follows ADR-0071, not a
  change.)*
```

New:

```
  ADR-0071 judges italic by the regular widths. It is a difference that follows ADR-0071, not a
  change.)*
  *(2026-10-14, decided with the user: also ExPivot's report filter with no report grid shown — only
  report filters placed, or a details tab over the report. The letters typed while its Filter… opens are
  dropped, with every key after them, rather than typed into its search; Escape typed then still closes
  it. With the report shown, the report's grid carries them to the search (ADR-0061's note of
  2026-10-13).)*
```

### 29. docs/definition-of-done.md

Old:

```
| **PV-49** | MUST | While the report is a Stale Report (PV-37), its value cells carry the stale appearance beside the notice — `--ex-pivot-stale-value-color` on the report's `ex-pivot-report-stale`, static and readable at a contrast of at least 4.5:1 (UX-8) under the default and the MudBlazor wrapper, nothing animated (P8), no row rendered again for it — and lose it when the report recovers. Copy from a Stale Report is refused in ExPivot's words (`copy-stale-report`), saying the time of the version shown and to Retry first; the Selection Summary still answers (ADR-0067's section of 2026-10-09; decided with the user) | Layer 2: each cause of a Stale Report, then recovery; Copy and the Summary while stale, the refusal's Japanese and its replacement by id; Layer 3 on `/pivot-live`: a server cut off, its value cells' paint and contrast, built-in and MudBlazor | as stated |
```

New:

```
| **PV-49** | MUST | While the report is a Stale Report (PV-37), its value cells carry the stale appearance beside the notice — `--ex-pivot-stale-value-color` on the report's `ex-pivot-report-stale`, static and readable at a contrast of at least 4.5:1 (UX-8) under the default and the MudBlazor wrapper, nothing animated (P8), no row rendered again for it — and lose it when the report recovers. Copy from a Stale Report is refused in ExPivot's words (`copy-stale-report`), saying the time of the version shown and to Retry first; the Selection Summary still answers (ADR-0067's section of 2026-10-09; decided with the user) | Layer 2: each cause of a Stale Report, then recovery; Copy and the Summary while stale, the refusal's Japanese and its replacement by id; Layer 3 on `/pivot-live`: a server cut off, its value cells' paint and contrast, built-in and MudBlazor | as stated |
| **PV-50** | MUST | A menu or a panel opened from its button — a Field List entry's ▾, Layout ▾ on the Pivot Toolbar, a report filter's Filter… — takes the keys typed on that button from the press that opens it until it holds the keyboard, in order, under either Chrome (*2026-10-13, decided with the user*): a menu answers them as typed on its first enabled command — Enter presses it, Escape closes it and the keys after it are typed as it closes, Tab is dropped with every key after it — and the button's own press by such a key is none; a report filter's Filter… types them into its search, carried through the report's grid; with no report grid shown, Escape closes the panel and any other key is dropped with every key after it (*2026-10-14, decided with the user*: a Known limitation of `0.1.0`, ADR-0202); closed before it holds the keyboard, it goes at once with the keys held for it; no JavaScript is added (ADR-0061, ADR-0050, ADR-0021) | Layer 2 (`FieldListTests.Keys_typed_on_an_entry_while_its_menu_opens_are_the_menus`, `FieldListTests.Escape_and_enter_typed_while_a_menu_opens_close_it_and_open_it_again`, `FieldListTests.A_tab_typed_while_a_menu_opens_drops_the_keys_after_it`, `FieldListTests.Enter_typed_on_layout_while_its_menu_opens_is_the_menus`, `FieldListTests.Keys_typed_on_a_report_filter_while_filter_opens_go_through_the_grid_to_its_search`, `FieldListTests.With_no_report_a_report_filters_panel_answers_escape_and_drops_what_only_its_field_can_type`, `MudPivotKeyboardBackTests.Keys_typed_while_a_report_filters_panel_opens_reach_its_mud_search_through_the_grid`); Layer 3 on `/pivot`, the Server host at a 150 ms round trip with the keys typed at once, under both Chromes (`pivot.spec.mjs`: "with a 150 ms round trip, Enter on a field's entry and Enter typed at once press the menu's first command, as typed slowly", "with a 150 ms round trip, letters typed straight after Enter on a report filter are typed into its Filter…'s search, as typed slowly") | as stated: Enter, Enter typed at once on a field's entry presses Move Down, and Enter, U, S typed at once on a report filter leave `US` in its search, the keyboard in it |
```
