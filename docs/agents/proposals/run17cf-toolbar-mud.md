# Run 17, Parts C to F: the toolbar under MudBlazor, and Format Cells' letters

Branch `claude/run17cf-toolbar-mud`, 2026-10-12. Findings D-mud-lists, D-mud-console and D-letter-O of the seventeenth
Windows run (`verification/2026-10-11-windows-17/report.md`, Part D; `part-d-summary.md`;
`records/part-d-extra-wasm-chrome.jsonl`; `records/part-d-sheet-server-{chrome,msedge}.jsonl`). This file holds the
text for files a background agent does not edit: ADR-0100 and ADR-0071 notes, an optional Definition of Done
wording, the Docs Site's two sentences, ExSheet's README sentence, and one stop that needs the user (a public member
on `ToolbarItemContext`, ADR-0201). Each edit is an exact old text and its new text, checked against `main` at
260d1531.

## The stop: closing a Chrome's own list when the keyboard goes back (needs a public member)

The triage asked, besides the two fixes made: "when the keyboard goes back to the Sheet while a list of the
toolbar's is open, that list closes. If closing a Chrome's own list needs a new member on the public
`ToolbarItemContext`, stop and say so (ADR-0201)." It does, and this part is not built.

- **Where it still happens.** With the fix, a list opened from the keys takes the keyboard into its choices, so
  Escape and every other key reach the list and the run's sequence (four lists at once) no longer arises. It
  remains in one window: on a circuit, a key typed between the KeyTip that opens a list and the list taking the
  keyboard (two round trips: the open request, then the popover provider's render) reaches the toolbar's band.
  Alt, H, F, C, Escape typed at machine speed on the Server host: the band's Escape gives the keyboard back to the
  Sheet, then the list arrives and takes it. A slower hand gets the list closed and the keyboard on the Sheet; a
  faster one gets the list open with the keyboard in it. The outcome depends on timing (the spine's sixth
  principle). This is read from the code, not observed: the layer-3 tests wait for the keyboard to be in the list
  before Escape, so they do not see it.
- **The built-in Chrome has the same window**: its list is the grid's popover, which takes the keyboard by its own
  focus request after the band has given it back. That half needs no public member: ExSheet owns that popover and
  can close it, or withdraw its focus request, when the toolbar hands the keyboard back.
- **What a Chrome is told today.** `ToolbarItemContext` has `OpenRequest` (open your list) and `ChoicesClosed`
  (your list closed). Nothing tells a Chrome to close its list, and the Chrome cannot see the hand-back: the
  band's focus leaving changes nothing in the item's context once the KeyTips are off.
- **Options.**
  1. **Recommended:** add `int CloseRequest` to `ToolbarItemContext`, beside `OpenRequest`: "Changes whenever the
     Sheet Toolbar takes the keyboard back while the item's choices may be open from the keyboard. A Chrome whose
     list is its own closes it when this changes, and does not call `ChoicesClosed` for that close." The toolbar
     bumps it on every item with an open request outstanding when it hands the keyboard back; `MudToolbarItem`
     closes its MudMenu (and drops a focus not yet landed); the built-in Chrome closes the grid's popover. It is a
     Chrome-seam member, so it is in the contract (ADR-0201, "The Chrome's seams"), and gets an `Added` Change Note.
  2. Hold the keys typed on the band between the KeyTip and the list's arrival, and hand them to the list once it
     holds the keyboard, as ticket 93 holds Format Cells' (ADR-0050 item 16, ADR-0039's notes of 2026-10-01). This
     also needs a member (the list must accept the held keys), and is more machinery for the same window.
  3. Leave it. The window is two round trips wide and needs a key typed inside it; the list then wins. Not
     recommended: it is the timing-dependent outcome the spine rules out.
- **Nothing is built for this until the user decides.** The record `ToolbarItemContext` and its equality are
  unchanged on this branch.

## Found while testing: a letter typed within a round trip of Alt is lost (not built; needs a decision)

- **What layer 3 showed.** On the Server host at an 80 ms round trip, `/sheet`, A4 pressed, then Alt, H, 1 typed at
  machine speed (Playwright, 12 to 17 ms apart): the end state is the rows' KeyTips (H, Y) standing, the toolbar
  holding the keyboard, a Cell Editor open over A4 behind them holding `h1`, and A4 not bold (a probe spec, not
  kept). The H and the 1 reached the Sheet before the KeyTips took the keyboard, and were typed into a new edit.
  On WebAssembly, and at a round trip of 0, the same keys set bold, and every existing KeyTips test types them
  so. A hand slower than one round trip gets bold; a faster one gets an edit under the KeyTips. The outcome
  depends on timing (the spine's sixth principle), and KeyTips standing over an open edit is a state SH-52 means
  never to arise ("while an edit is open they start nothing").
- **Why.** Alt's release is heard by a Blazor `keyup` on the Sheet's element (ADR-0100, "Found while implementing
  the keys"); the server then shows the KeyTips and moves DOM focus to the band with `FocusAsync`, a round trip
  later. Keys typed meanwhile go to the Keyboard Field, and the grid's key gate holds nothing for the toolbar,
  because it is told where the keyboard is going only for a popover or a Chrome's frame (ADR-0050 item 16,
  ADR-0039's notes of 2026-10-01).
- **What a fix needs.** The browser cannot know an Alt released alone starts the KeyTips until the server says so,
  unless the claim says it: the key gate would hold the keys after a declared Alt-alone release until the Sheet
  answers, then hand them to the band, or drop them with the keys behind them, as the hold does for a Tab. That is
  a change to ADR-0050 item 16's hold and to ADR-0100's KeyTips, and so a decision; this branch leaves it, and its
  layer-3 tests wait for the KeyTips to hold the keyboard before typing their letters.

## A question for the next Windows run

- **Escape in a palette that a KeyTip opened.** Under both Chromes ExSheet closes the list and gives the keyboard
  to the Sheet (one Escape). Excel may instead go back to the Home tab's KeyTips (Alt, H, FC, Escape showing the
  Home letters again, as "Escape goes back one level" reads for KeyTips). The seventeenth run read ExSheet's
  answer, not Excel's (d4 ran on ExSheet; d1 read the letters only). Ask Excel: Alt, H, FC, then Escape once —
  are the Home tab's KeyTips shown, or is the keyboard in the cell?

## ADR-0100

### 1. A section for what the seventeenth run read (after "Found while implementing the keys")

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

- **Format Cells' KeyTip is O, E.** On Excel's Home tab, O opens the Cells group's Format menu, and that
  menu's E is Format Cells… (d1). ExSheet's item answered O alone, so Alt, H, O opened Format Cells at
  once, and the E that Excel's hands type next reached the dialog.
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
  - A list closes through an empty element of the item's own, as the toolbar's band gives the keyboard
    back (above). MudBlazor's provider removes the list in a render of its own, and a hand-back could
    otherwise reach the browser before that removal on a circuit and be declined.
```

### 2. The hand-off element is not aria-hidden (under "The keyboard goes back from the toolbar through an element of its own")

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
  run: "Read on Windows" below.)*
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

### `samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor`, the KeyTips item (about line 118)

Old:

```
                <b>KeyTips.</b> Release Alt on its own, or press F10, and each row shows its letter: ExSheet's row is H,
                as Excel's Home tab is, and its items carry Excel's letters, so Alt, H, 1 sets bold. Your rows and
                buttons have a KeyTip where you set <code>KeyTip</code>; two letters that collide at one level are
                refused when the toolbar renders.
```

New:

```
                <b>KeyTips.</b> Release Alt on its own, or press F10, and each row shows its letter: ExSheet's row is H,
                as Excel's Home tab is, and its items carry Excel's letters, so Alt, H, 1 sets bold and Alt, H, O, E
                opens Format Cells, as Excel's Format menu reaches it. A list a KeyTip opens takes the keyboard under
                either Chrome: the arrows move among its choices, Enter makes one, and Escape closes it and gives the
                keyboard back to the Sheet. Your rows and buttons have a KeyTip where you set <code>KeyTip</code>; two
                letters that collide at one level are refused when the toolbar renders.
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

Nothing else on the site changes: the console fix is invisible on the page, and the Examples' toolbars
(`Examples/Sheet/Toolbar.razor`, `ToolbarMud.razor`) declare no KeyTip of their own that meets Format Cells'.

## ExSheet's README (`src/ExSheet/README.md`, about line 387)

Old:

```
Home tab is. The row's letter then shows the items' letters, which are Excel's, so Alt, H, 1 sets
bold. A row or a `ToolbarButton` of yours has a KeyTip only where you set its `KeyTip`. Two letters
```

New:

```
Home tab is. The row's letter then shows the items' letters, which are Excel's, so Alt, H, 1 sets
bold and Alt, H, O, E opens Format Cells. A row or a `ToolbarButton` of yours has a KeyTip only where
you set its `KeyTip`. Two letters
```
