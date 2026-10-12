# Run 17, Parts C to F: the toolbar under MudBlazor, and Format Cells' letters

Branch `claude/run17cf-toolbar-mud`, 2026-10-12, two rounds. Findings D-mud-lists, D-mud-console and D-letter-O
of the seventeenth Windows run (`verification/2026-10-11-windows-17/report.md`, Part D), and the user's decisions
(B) and (C) of 2026-10-12. This file holds the text for files a background agent does not edit: notes for ADR-0100,
ADR-0050, ADR-0021, ADR-0044 and ADR-0071, an optional Definition of Done wording, and the Docs Site's sentences; and what
the second round stopped on. Each edit is an exact old text and its new text, checked against `main` at 260d1531.
ExSheet's README is edited on the branch itself (it is not one of the files reserved to the orchestrator), so the
first round's README edit is no longer here.

## What the second round built

- **(C), the Alt-alone hold.** The core's key gate knows the release of a declared Alt that went down alone by the
  key after it (one without Alt, or Alt pressed again; no keyup listener). That key and every key after it are
  held until an empty question to the core, which travels behind the release the Sheet heard, is answered. ExSheet
  tells the gate, as it starts the KeyTips, that the keyboard is going to a frame of its own
  (`HandKeyboardToFrameAsync`), and gives the band the keyboard through the core (`HandKeyboardToAsync`), which,
  while it holds keys for the band, gives it in turn with them, so the held letters go to the band in order and none
  typed straight on it overtakes them; told nothing, they are the grid's. A Tab among them is dropped with the keys behind it, as the hold already does. F10 takes the same way. Layer 3:
  A4 pressed, Alt, H, 1 at machine speed on the Server host at 80 ms: A4 bold, no edit, KeyTips gone, under both
  Chromes. The existing KeyTips specs type without waiting. Alt pressed alone again while the keys wait is
  forwarded ahead of them (the ADR-0050 note below), and on WebAssembly, where the core takes the keyboard back
  inside the handler of the key that gave it back, the band keeps that key's default from reaching the Sheet
  until the key has been dispatched.
- **(B), `CloseRequest`.** Built as decided, under both Chromes; one case goes beyond the decision's words (the
  first stop below). Layer 3: Alt, H, F, C, Escape at machine speed at 80 ms: the list is closed and the keyboard
  on the Sheet, under both Chromes, three times on each host.
- **(1), the keys typed on the way back.** The core gains
  `ExGrid.ReturnKeyboardAsync(ElementReference from, Func<IReadOnlyList<KeyboardEventArgs>> typedOnTheWay)`: it takes
  the keyboard back while DOM focus is still on the Consumer's frame (the core's hand-back, its condition
  widened: ADR-0021), and asks for the keys typed there on the way
  once it has it (a second `[JSInvokable]`, `KeysTypedOnTheWay`). The toolbar now gives the keyboard back from its
  band directly, with no element of its own; it holds the keys typed on the band, or on an element inside it while
  the band itself does not hold the keyboard (MudBlazor's hand-off element), and hands them over. Under the
  built-in Chrome a list is the grid's popover: Enter on a `menuitemradio` there now holds the keys behind it until
  the popover is gone, as Enter on a `menuitem` did (ADR-0044). A Consumer's popover closed before its contents
  took the keyboard now withdraws the gate's wait for them (`handOff('none')`), which otherwise held the keys typed
  meanwhile two seconds and dropped them.
  Format Cells' item now gives the keyboard back after its press from the keys, as every other item does (the
  hand-back from a frame leaves a keyboard already inside the grid where it is), so the keys typed on the band after
  E reach Format Cells. The built-in list keeps the place its keys set when a focus it asked for lands late.
- **(2), D-letter-O.** The comment on `FormatCellsItem.DefaultKeyTip` and the note below now say the E is Excel's
  documented Alt, H, O, E, still to be confirmed by the next Windows run, not that d1 read it.

## Stops and deviations, for the user

### 1. `CloseRequest` when the list already holds the keyboard

The decision: "a Chrome whose list is its own closes it when it changes and does not call `ChoicesClosed` for that
close; `MudToolbarItem` closes its MudMenu and drops a focus not yet landed."

- **The case.** A MudBlazor list asks for the keyboard (`FocusAsync`) once the popover provider has drawn it. If
  that request was sent before the server handled the Escape typed on the band, it reaches the browser before the
  toolbar's hand-back, the list holds the keyboard when the hand-back lands, and the hand-back, granted only from
  the grid, nothing or the band, is declined. Closed silently, the list would leave the keyboard on nothing.
- **What is built.** `MudToolbarItem` drops a focus it has not yet sent, and never sends one after a close request
  it has not answered (the machine-speed case: the list never takes the keyboard). A list whose focus was already
  sent closes as Escape in it does, through its own element, and calls `ChoicesClosed`. The `CloseRequest` XML doc
  says so. The window is a round trip wide and needs Escape typed on the band after the list was drawn and before
  it took the keyboard; layer 3 does not reach it, and layer 2 pins it (`A_close_request_closes_a_list_that_holds_the_keyboard`).
- **Options.** (a) Keep this, and let ADR-0100's note say it (the note below does). (b) Grant the toolbar's
  hand-back also from a list the toolbar's items own, which needs the core to know the list (a member, or an ARIA
  relation such as `aria-controls` read by the gate).

### 2. Keys typed in a MudBlazor list while it closes

- **The case.** After a choice or Escape in a MudBlazor list, the keyboard is put on the item's element inside the
  band a round trip later on a circuit. Keys typed on that element reach the band, which holds them for the Sheet
  (built). Keys typed in the list itself before that are the list's: the list is in MudBlazor's popover provider,
  outside the band, and nothing in `ToolbarItemContext` can carry them to the toolbar, so they are dropped while it
  closes, as before this round. Enter straight after the choosing Enter is lost under MudBlazor; under the built-in
  Chrome it reaches the Sheet.
- **What it needs.** A Chrome-seam member (ADR-0201): for example `ChoicesClosed` taking the keys typed in the list
  since the choice (`Func<IReadOnlyList<KeyboardEventArgs>, Task>`), which the toolbar would put ahead of the keys
  its band holds. Not built. The layer-3 case under MudBlazor types its second Enter once the keyboard has left the
  list, and says why.

### 3. A public member and a JS-invokable added to ExGrid

`ReturnKeyboardAsync(ElementReference, Func<IReadOnlyList<KeyboardEventArgs>>)` is in the Consumer's contract
(ADR-0201, item 1: ExSheet uses it as a Consumer), with an `Added` Change Note. `KeysTypedOnTheWay` makes the
interop methods ADR-0201 counts 27, not 26; the branch that writes the baseline lists it. `Ping` is now also asked
asynchronously, as the Alt hold's question; its XML doc says so. This branch is cut from 260d1531, before main's
public-API baseline (#89): at integration, `ExGrid.ReturnKeyboardAsync`, `ExGrid.KeysTypedOnTheWay` and
`ToolbarItemContext.CloseRequest` (its property, and the record's constructor with it) go into the packages'
`PublicAPI.Unshipped.txt`.

### 4. Keys typed while a toolbar list closes, an Alt among them

- **The case.** Escape in a toolbar list, then Alt, H, H typed at once on a circuit (layer 3, Server host, 80 ms,
  three runs under each Chrome): nothing opens, every time. On WebAssembly the Fill's list opens, every time.
  - Under the built-in Chrome the list is the grid's popover. Escape there is the core's to answer, and the gate
    holds nothing behind it (ADR-0044's hold is behind Enter, Space, a letter that runs a command and, since this
    round, Enter on a choice; not behind Escape). The keys after it reach the popover still standing, which does
    nothing with them. The Alt goes down in the popover, not on the root, so the gate does not take it for a
    declared Alt alone; the Sheet hears its release without its press, and the KeyTips do not start. The trace
    shows the list closed, no KeyTips, no edit, and the keyboard on the Sheet.
  - Under MudBlazor's Chrome the keys land in the list itself, and are dropped (stop 2). Typed a little later, on
    the element the list leaves through, the band holds them for the Sheet; but the Alt among them is a modifier
    the gate's replay skips, and its release reached the Sheet before, so H, H would open an edit in the Focus
    cell holding `hh`, as A4, Alt, H, 1 did before (C) (read from the code, not observed).
  - The first half is not the toolbar's: Escape, then ↓ typed within a round trip in a column's popover moves in
    the closing menu and is lost (read from the code, not observed).
- **What is built.** Nothing beyond (B) and (C). The layer-3 case types the Alt once the keyboard is back on the
  Sheet, the toolbar's hand-back possibly still standing, and the letters at once; its comment names this stop.
- **Options.**
  - (a) Escape that closes a popover holds the keys behind it until the popover is gone, as Enter on a command
    does (ADR-0010's rule; a change to the gate for every popover, verified across every popover spec), and a
    declared Alt held behind a close counts as the root's for (C). This settles the built-in Chrome.
  - (b) The toolbar's hand-back carries Alt's release among the keys typed on the way (the band hears it), the
    gate's replay treats an Alt alone followed by a key as the release (C) waits behind, and the Sheet starts the
    KeyTips there. With stop 2's seam member, this settles MudBlazor's.
  - (c) Keep it, and let ADR-0100's note say that the keys typed while a list closes are not the KeyTips'.

## Questions for the next Windows run

- **Escape in a palette that a KeyTip opened.** Under both Chromes ExSheet closes the list and gives the keyboard
  to the Sheet (one Escape). Excel may instead go back to the Home tab's KeyTips (Alt, H, FC, Escape showing the
  Home letters again, as "Escape goes back one level" reads for KeyTips). The seventeenth run read ExSheet's
  answer, not Excel's (d4 ran on ExSheet; d1 read the letters only). Ask Excel: Alt, H, FC, then Escape once —
  are the Home tab's KeyTips shown, or is the keyboard in the cell?
- **Format Cells' E.** ExSheet's Format Cells KeyTip is O, E because Microsoft documents Alt, H, O, E (the Home tab's
  O opens the Cells group's Format menu, whose E is Format Cells…). The seventeenth run read the Home tab's letters
  (d1), not the Format menu's. Ask Excel: Alt, H, O — which letter does Format Cells… carry in the menu that opens?

## ADR-0100

### 1. Two sections after "Found while implementing the keys"

Old (the last bullet of "### Found while implementing the keys", followed by the next heading):

```
- **A KeyTip on a split control opens its list,** as Excel's Alt, H, H opens the Fill's palette. A
  toggle's KeyTip sets it and gives the keyboard back.

## Considered options
```

New:

```
- **A KeyTip on a split control opens its list,** as Excel's Alt, H, H opens the Fill's palette. A
  toggle's KeyTip sets it and gives the keyboard back.

### Read on Windows *(the seventeenth run, 2026-10-12)*

- **Format Cells' KeyTip is O, E.** On Excel's Home tab, O opens the Cells group's Format menu (d1), and
  that menu's E is Format Cells…, as Microsoft documents Alt, H, O, E. The E is the documentation's, not
  a reading: the next Windows run confirms it. ExSheet's item answered O alone, so Alt, H, O opened
  Format Cells at once, and the E that Excel's hands type next reached the dialog.
  - The item's letters are now both, `OE`. O narrows the KeyTips to it, as A narrows them to AL, AC and
    AR, and E runs it. Excel's hands carry over unchanged.
  - ExSheet draws no Format menu: nothing else on it (row height, column width, …) is a Toolbar Item. If
    one becomes an item, O becomes a menu as Excel's is, with Format Cells its E.
  - A Consumer's O beside `FormatCellsItem` collides with `OE` and is refused by name, as it was with O.
- **The other letters agree with Excel's** (d1): 1, 2, 3, FC, H, B, AL, AC, AR, N, P and K. 4 is free on
  Excel's Home tab, as Strikethrough's own letter needs it to be; the free letters are G, Q, R, Z, 4, 7
  and 8, and Y where Acrobat's add-in is not installed.
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
    toolbar takes the keyboard back (below). MudBlazor's provider removes the list in a render of its own,
    outside the band.

### Decided with the user *(2026-10-12)*

The seventeenth run's Part D, and layer 3 at an 80 ms round trip, found three places where what a key did on
the toolbar depended on when it was typed (the spine's sixth principle). Each is now held in order.

- **Letters typed straight after Alt are the KeyTips'.** On a circuit the KeyTips take the keyboard a round
  trip after Alt's release, and A4 pressed, Alt, H, 1 typed at machine speed opened an edit in A4 holding
  `h1` under the KeyTips.
  - The grid's key gate holds the keys typed after the release of a declared Alt that went down alone,
    until the Sheet has answered the release (ADR-0050 item 16's note of 2026-10-12). The Sheet, starting
    the KeyTips, tells the gate the keyboard is going to a frame of its own, the toolbar, and gives the band
    the keyboard through the core, which gives it in turn, with the held keys, in order: none typed on the
    band can overtake them. A Tab among them is dropped with the keys behind it, as the hold does for a
    Tab anywhere. F10 takes the same way.
  - So the KeyTips can be typed at once, at any speed, on either host.
- **Escape on the toolbar before a list a KeyTip opened has the keyboard closes that list**
  (`ToolbarItemContext.CloseRequest`). Alt, H, F, C, Escape typed at once on a circuit gave the keyboard
  back to the Sheet, and then the list arrived and took it.
  - Whenever the toolbar takes the keyboard back while an item's list may be open from the keyboard, the
    item's `CloseRequest` changes, to the value its `OpenRequest` had. A Chrome whose list is its own closes
    it, drops a focus into it not yet given, and does not call `ChoicesClosed` for that close: the toolbar
    has taken the keyboard back itself. An opening asked for before the close, and not yet answered, is
    not answered.
  - `ExSheet.MudBlazor` closes its MudMenu. The built-in Chrome closes the grid's popover, if it still shows
    that list, and the core stops holding keys for contents that will not take the keyboard.
  - *(As built.)* A MudBlazor list whose request for the keyboard had already been sent when the close was
    asked for holds the keyboard by the time the toolbar's hand-back lands, which is then declined. That
    list closes as Escape in it does, and calls `ChoicesClosed`.
- **A key typed while the keyboard goes back to the Sheet is the Sheet's.** The element the toolbar gave the
  keyboard back through held it about a round trip and a half on a circuit, and Enter typed then pressed the
  last item again.
  - The core now takes the keyboard back from the band itself (ADR-0050 item 16's note of 2026-10-12). From
    the moment the toolbar asks, it holds the keys typed on the band, and on an element inside it while the
    band itself does not hold the keyboard — `ExSheet.MudBlazor`'s, on the way out of its list — and the
    core hands them to the grid, in order, ahead of any typed after.
  - Under the built-in Chrome a list is the grid's popover: Enter on a choice holds the keys behind it until
    the popover has gone (ADR-0044's note of 2026-10-12).
  - Format Cells' item now gives the keyboard back after its press from the keys, as every item does. It
    declined that, because the hand-back would have taken the keyboard from the built-in popover. The core's
    hand-back from a frame now leaves a keyboard already inside the grid where it is, and the grid holds the
    keys typed meanwhile for Format Cells, those typed on the band among them: Escape straight after Alt, H,
    O, E closes it.
  - Under `ExSheet.MudBlazor`, a key typed in the list itself in the round trip after a choice or Escape is
    still the list's, and is dropped: nothing in the Chrome's seam carries it to the toolbar.

## Considered options
```

### 2. The hand-off element (under "The keyboard goes back from the toolbar through an element of its own")

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
  - *(Replaced the same day, with the user's decisions below.)* The element held the keyboard about a
    round trip and a half on a circuit, and a key typed then reached the toolbar: Enter pressed the last
    item again. The core now takes the keyboard back from the band itself (ADR-0050 item 16's note of
    2026-10-12), and the toolbar has no element of its own for it. `ExSheet.MudBlazor`'s list still leaves
    through one, inside the band; Format Cells' dialog still leaves through its own.
```

### 3. Optional: the reading it settles ("Readings until a Windows run")

Old:

```
- **Excel's Home tab letters.** Microsoft's page confirms H for Fill, B for Borders and AC for
  Center. The rest, and the free letters, are read off Excel by a Windows run.
```

New:

```
- **Excel's Home tab letters.** Microsoft's page confirms H for Fill, B for Borders and AC for
  Center. The rest, and the free letters, are read off Excel by a Windows run. *(Read by the seventeenth
  run: "Read on Windows" below. Format Cells' E, in the Format menu, is still the documentation's.)*
```

## ADR-0050, item 16

Old (the last bullet of item 16, before the next section):

```
  - A dynamic call was chosen over a fixed flag on the command, because the same command opens the
    core's popover under one Chrome and a frame of its own under another.

## A Formula Bar text that is not the cell's own *(2026-10-03, with ADR-0125)*
```

New:

```
  - A dynamic call was chosen over a fixed flag on the command, because the same command opens the
    core's popover under one Chrome and a frame of its own under another.
- *(2026-10-12, decided with the user; ExSheet's Sheet Toolbar, ADR-0100.)* **The hold covers the
  keyboard's way to and from a frame of the Consumer's own, and the release of the Alt key.**
  - **After the release of a declared Alt that went down alone, the keys wait for the Consumer.** The
    release is the Consumer's to hear (ExSheet's `keyup`, ADR-0100); the gate hears no keyup (ADR-0021)
    and knows the release by the key after it: one that does not carry Alt, or Alt pressed again. That
    key and every key after it are held until an empty question to the core, which travels behind the
    release, is answered: after whatever the Consumer told the gate on hearing it. Handed off
    (`HandKeyboardToFrameAsync`), the keys go to the frame, or are dropped with every key behind one it
    cannot take, a Tab; told nothing, they are the grid's. A chord (a key carrying Alt while it is down),
    a press on the grid and DOM focus leaving it end the wait before it starts.
  - **Alt going down alone again while keys wait goes first.** Behind nothing but such a release and the
    keys a frame heard on the way back, a declared Alt pressed alone is forwarded at once, and leaves a
    mark among the held keys that a frame's hand-off waits behind. Held, it would reach the Consumer after
    its own release, which the Consumer hears straight from the browser, and that release would start
    nothing: Alt pressed within a round trip of the toolbar giving the keyboard back. It changes nothing
    the core holds, so no key's outcome moves.
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
  - **A control handed the keyboard while it is on its way to a frame takes it in turn.**
    `HandKeyboardToAsync` called while a frame hand-off is pending and the gate holds keys gives the
    control the keyboard when the hold reaches it, and hands it the held keys in the same task: a key
    typed straight on the control can no longer overtake one still held (Alt, H, O typed at once, the O
    on the band before the held H). ExSheet's KeyTips give the band the keyboard this way.
```

## ADR-0021 (the hand-back's condition, widened; no entry is added)

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
`document.activeElement` and no layout; script still moves DOM focus in seven places. And the sixth
decision, the keyboard handed on to a control of the Consumer's, is made in turn by the key hold while
the keyboard is on its way to that control's frame, with the keys held for it.)*
```

## ADR-0044 (the hold behind a key that closes the popover)

Old:

```
  second review, decided with the user.)* A key that closes the popover — a command run, Enter
  in the filter's field — holds the keys behind it too, until the popover is gone
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). E held on a checkbox outside
```

New:

```
  second review, decided with the user.)* A key that closes the popover — a command run, Enter
  in the filter's field, *(2026-10-12)* Enter on a `menuitemradio`, which ARIA's menu pattern closes
  the menu on (ExSheet's toolbar lists in a Consumer's popover; a second Enter typed before the
  popover went chose again) — holds the keys behind it too, until the popover is gone
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). E held on a checkbox outside
```

## ADR-0071 (optional; its contract does not change)

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

## Definition of Done (optional wording; the criterion does not change)

SH-52 already holds ExSheet to "Excel's Home tab letters where Excel has one", and O, E is Excel's way to Format
Cells. If the orchestrator wants the case named, in SH-52's requirement:

Old: `Excel's Home tab letters where Excel has one and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses)`

New: `Excel's Home tab letters where Excel has one, Format Cells' through Excel's Format menu (O, E), and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses)`

## The Docs Site

### `samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor`, the KeyTips item and the Chrome item (about line 118)

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
                opens Format Cells, as Excel's Format menu reaches it. Type them as fast as you like: the letters typed
                before the KeyTips show wait for them, and a key typed as the keyboard goes back to the Sheet is the
                Sheet's, over Blazor Server too. A list a KeyTip opens takes the keyboard under either Chrome: the
                arrows move among its choices, Enter makes one, and Escape closes it and gives the keyboard back to the
                Sheet. Your rows and buttons have a KeyTip where you set <code>KeyTip</code>; two letters that collide
                at one level are refused when the toolbar renders.
            </li>
            <li>
                <b>The look is the Chrome's.</b> The built-in Chrome draws plain buttons with inline-SVG pictures;
                <code>MudSheetChrome</code> draws MudBlazor's controls. A Chrome of your own draws each item from its
                <code>ToolbarItemContext</code>: a list of its own opens when <code>OpenRequest</code> changes, and
                closes when <code>CloseRequest</code> does, without calling <code>ChoicesClosed</code> — the toolbar has
                taken the keyboard back before the list took it.
            </li>
```

### `samples/ExGrid.Docs/Pages/KeyboardByHand.cs`, the KeyTips row (about line 116)

Old:

```
            "Show the KeyTips: each row's letter, then each item's, so that [Alt], [H], [1] sets bold.",
```

New:

```
            "Show the KeyTips: each row's letter, then each item's, so that [Alt], [H], [1] sets bold and [Alt], [H], [O], [E] opens Format Cells.",
```

### ExGrid's new member

No page of the site describes a frame of the Consumer's own (`HandKeyboardToFrameAsync` and `ReturnKeyboardAsync`
appear only in the generated API reference), so `ReturnKeyboardAsync(ElementReference, Func<…>)` has no prose to
join. If the orchestrator wants one, in `samples/ExGrid.Docs/Pages/Grid/GridAppearance.razor`, after the paragraph
that begins "<strong>A Chrome of your own</strong>", a new paragraph:

```
        <p>
            <strong>A frame of your own</strong> outside the grid — a dialog, a toolbar — takes the keyboard
            through <code>HandKeyboardToFrameAsync</code> and gives it back through <code>ReturnKeyboardAsync</code>.
            Over Blazor Server each lands a round trip late, and the grid holds the keys typed meanwhile: on the
            way out it hands them to your frame, and on the way back, given the frame's element and the keys it
            heard, it takes them as its own, in order. ExSheet's Sheet Toolbar is built this way.
        </p>
```

The Sheet Toolbar's Example on exsheet/formatting runs it; no Example of its own is proposed.

Nothing else on the site changes: the Examples' toolbars (`Examples/Sheet/Toolbar.razor`, `ToolbarMud.razor`)
declare no KeyTip of their own that meets Format Cells', and draw no Chrome of their own.
