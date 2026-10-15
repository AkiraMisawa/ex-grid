# The Sheet Toolbar ships as an opt-in part of ExSheet

*(Decided with the user on 2026-10-02, grilling ticket 54. This replaces ADR-0071's "A toolbar is
a sample, not a product"; that section now points here and keeps its first reason.)*

ADR-0071 kept the formatting toolbar off the product: "Shipping one would promise its look and the
order of its buttons." Both promises can be avoided without leaving every Consumer to rebuild the same
buttons:

- **The look is the Chrome's.** It is not ExSheet's to promise (principle 4).
- **The order is the Consumer's.** The Consumer declares the order, and ExSheet's default order is
  only an example.

**What ExSheet promises is which commands exist and what each one means.**

## The decision

- **A Sheet Toolbar is part of `ExSheet`, shown only when asked for.** `ShowToolbar` defaults to
  `false`.
  - A Consumer who upgrades gets no new band, and loses no rows to one.
  - `ShowToolbar` shows or hides every Toolbar Row together. This holds for the default content too.
- **It belongs to one Sheet, and acts on that Sheet's Selection.** Several Sheets on a page have one
  toolbar each.
  - One toolbar for several Sheets was considered and rejected. Once a button is pressed, DOM focus
    is no longer on any Sheet. The toolbar would have to remember which Sheet it means. A Sheet
    scrolled out of view could then be formatted by a press made while looking at another one,
    which is quietly wrong (principle 1).
- **It stands above the Formula Bar.** This is Excel's order: ribbon, formula bar, grid.
  - It is inside the Sheet's box. It is outside ExGrid's instance root, so the capture-phase key
    listener (ADR-0010, ADR-0018) never hears a key pressed on a Toolbar Item.
  - The Sheet's `Height` is its outer height, the Sheet Toolbar included, as it includes the Formula
    Bar.
- **The toolbar's height is resolved in C#** (ADR-0028).
  - Every Toolbar Row of a Sheet is the same height, set from the Density as the Formula Bar's is.
  - The toolbar's height is the number of rows times that height. A Toolbar Row's content is fitted
    to it and clipped, never allowed to grow it.
  - No stylesheet value sets a row's height.
- **What it holds is declared as child components**, in `ToolbarContent`:

  ```razor
  <ExSheet ShowToolbar="true">
      <ToolbarContent>
          <ToolbarRow> <BoldItem /> <ItalicItem /> … </ToolbarRow>
          <ToolbarRow> <ToolbarButton OnClick="Approve">Approve</ToolbarButton> </ToolbarRow>
      </ToolbarContent>
  </ExSheet>
  ```

  - The markup's order is the toolbar's order.
  - Without `ToolbarRow`, the content is one row. Without `ToolbarContent`, the default row is
    shown.
  - A Consumer's own action is a Toolbar Item like ExSheet's, in a row of its own or among the
    formatting items.
  - A new command is a new item component. No existing item and no order changes for it.
  - A list of item ids was considered and rejected. It cannot carry an argument (which Number
    Format) or a Consumer's own action without a second mechanism.
  - An end user rearranging the toolbar is not offered. A Consumer who wants it holds the
    arrangement as View State and renders its items from it with `@foreach`.
- **A Toolbar Item declares what it means; the Chrome draws it** (ADR-0010).
  - The same `<BoldItem />` is a plain toggle button under the built-in Chrome, and a
    `MudToggleIconButton` under `ExSheet.MudBlazor`'s. Swapping the Chrome changes no markup and
    no behaviour.
  - The built-in Chrome's icons are inline SVG, coloured by `currentColor` and Visual Tokens. They
    need no font and no script.
  - Tooltips give the item's name and its key ("Bold (Ctrl+B)") through `SheetWords`.
  - A dropdown (Font colour, Fill, Borders, Number Format) takes the frame Format Cells takes
    (ADR-0071): a popover inside the Sheet's box under the built-in Chrome (ADR-0040), and
    `MudMenu` / `MudPopover` under MudBlazor's.
- **Items are built on public commands only:** `SetCellFormatAsync`, `CellFormatAt`,
  `OpenFormatCellsAsync`, and the notifications of the Selection and of an edit.
  - An item reads the Sheet through what the toolbar cascades. Nothing internal is cascaded.
    *(2026-10-12, ADR-0201: the toolbar's rows also cascade `ToolbarOrder`, the scope that keeps
    items in markup order, and it is internal. It is the rows' bookkeeping, not the Sheet's: an item
    still reads the Sheet only through public commands.)*
  - The compiler cannot hold this inside one assembly, so a test holds it: no item type references
    an internal member.
- **An item's state follows the Focus cell.** Whenever the Selection changes, each item reads the
  Focus cell's `CellFormatAt`. Bold is shown pressed (`aria-pressed`) when that cell is bold, as the
  toggle key follows the Focus cell (ADR-0071).
- **While an edit is open, every Toolbar Item is unavailable**, the Consumer's included.
  - The commands are refused then (ADR-0048).
  - A Consumer's action would read a document that does not yet hold the typed text.
- **A press keeps the keyboard where it was.**
  - A pointer press does not move DOM focus off the Sheet (`@onmousedown:preventDefault`).
  - The toolbar is one Tab stop, with ← and → among its items (ARIA's toolbar pattern).
  - After a command run from the keyboard, the Sheet takes the keyboard back through the core's
    focus function.
  - A Consumer's item can decline this, for an action that opens something of its own.
- **The default row** is:

  ```
  Bold Italic Underline Strikethrough │ Font colour▾ Fill▾ │ Borders▾ │ Left Center Right │
  Number Format▾ Percent Comma │ Format Cells…
  ```

  - Number Format▾ lists Excel's categories in Excel's order. Accounting and Fraction are disabled
    with the reason, as in Format Cells.
  - Increase and decrease decimal are left out. ExSheet has no command that rewrites a code's
    decimals. They are an item to add when it has one.
- **An end user can hide and show the toolbar with Ctrl+F1**, Excel's key for the ribbon, but only
  where the Consumer binds `ShowToolbarChanged`.
  - The Sheet raises the change and holds nothing. Whether the toolbar is shown is View State, held
    by the Consumer (principle 3).
  - Unbound, the key is not claimed (ADR-0050, item 14). A Consumer who never wanted a toolbar does
    not get one from a key.
  - Excel collapses the ribbon to its tab names. A Sheet Toolbar has no tabs, so it hides entirely.

## KeyTips

Excel shows a letter over each command when Alt is released, and the letters run commands: Alt, H, 1
is bold.

- **They start when Alt (Option on macOS) is pressed and released alone, or with F10.** This holds
  only while the Sheet holds the keyboard, the Sheet Toolbar is shown, and no edit is open. Otherwise
  both keys stay the browser's.
  - Alt held with another key is a chord, not a KeyTip. Alt+↓ (ADR-0044) and Alt+Enter keep their
    meaning.
  - Option is read by `code`, since Option+letter types a character on macOS.
- **No script is added** (ADR-0021).
  - The existing capture-phase `keydown` claims Alt alone and F10, so the browser does not act on
    them. On Windows, Chrome and Edge focus their menu button when Alt is released.
  - The release is heard by a Blazor `keyup` handler on the root. It reaches the root from the
    Keyboard Field (ADR-0080).
- **A Toolbar Row is a tab of Excel's ribbon.**
  - Alt shows one letter per row. The formatting row's letter is H, Excel's Home.
  - The row's letter then shows the items' letters, which are Excel's Home tab letters: 1 bold, 2
    italic, 3 underline, H fill, B borders, AL / AC / AR alignment, and so on.
  - A row is never skipped, even when it is the only one, so Excel's hands carry over unchanged.
  - Alt followed straight by a digit was rejected. In Excel that is the Quick Access Toolbar.
- **Where Excel has no KeyTip, ExSheet gives one of its own.** Strikethrough is the case today; Excel
  has only Ctrl+5.
  - An own letter is never one Excel's Home tab uses. If Excel later takes it, Excel's meaning wins
    and the own letter moves.
  - Strikethrough's is 4, a reading until a Windows run reads every Home tab letter.
- **A Consumer's row and item carry the letters the Consumer declares**, and none otherwise.
  - Letters are never assigned automatically. An automatic letter changes when the order changes,
    and a remembered sequence would then run another command.
  - Two letters that collide at one level are refused when rendered, naming both. This includes a
    letter of ExSheet's own.
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

## Readings until a Windows run

- **Excel's Home tab letters.** Microsoft's page confirms H for Fill, B for Borders and AC for
  Center. The rest, and the free letters, are read off Excel by a Windows run. *(Read: the seventeenth
  run read the Home tab's letters (d1), and the readings of 2026-10-12 read the Format menu's, R2 (a):
  "Read on Windows" below.)*
- **The browser keys.**
  - Does `preventDefault` on Alt's `keydown` stop Chrome's and Edge's menu on its release? This
    rests on a third-party report and on Chromium's source, which only reaches the menu when the
    page left the key unhandled.
  - Do Ctrl+F1 and F10 reach the page?

  If Alt's release cannot be kept from the browser, KeyTips start from F10 alone. The Alt way is
  then recorded as not taken, with the run's evidence.

## Found while implementing *(ticket 54, 2026-10-02)*

None of these changes the decision. Each records how a part of it is held, or a detail it left open.

- **A toggle needs a public command that reads the Focus cell when it runs.** That command is
  `SetCellFormatFromFocusAsync(Func<CellFormat, CellFormatChange>)`.
  - It reads the Focus cell's Cell Format from the grid's Selection as it is at that moment
    (ticket 56), and sets the change it is handed.
  - The Focus format the toolbar last heard is not enough: on a circuit it is a round trip old.
  - It is a new name, not an overload of `SetCellFormatAsync`. An overload made a Consumer's
    `SetCellFormatAsync(null)` ambiguous.
  - *(2026-10-12, decided with the user: each Sheet Toolbar toggle widens as the key it names.)*
    *(2026-10-12, decided with the user: `SetCellFormatAsync` widens as the formatting keys do, not
    as Format Cells' OK does.)* ADR-0071, "What the seventeenth Windows run settled", holds both.
    - An item acts through public commands only, so the toggles' rule is this command's. It widens
      a column over `####` as the formatting keys do: Underline and Strikethrough widen, and Bold and
      Italic do not.
    - The Font Colour, its face and its list, sets through `SetCellFormatAsync`, so it widens
      nothing. That Excel's Font Color button widens nothing is a reading.
    - *(Replaced 2026-10-14, decided with the user: the Sheet Toolbar's Font Colour widens a column as
      Excel's Home tab Font Colour button does — by its face and from its list — while
      `SetCellFormatAsync` keeps widening as COM's `Font.Color` does, which does not widen.)* The
      readings of 2026-10-14 saw Excel's button widen (run 18a, widening case 5). *(As built.)* The Font
      Colour, and the Fill Colour beside it, set through `SetCellFormatFromFocusAsync`, whose rule is now
      Excel's Home tab buttons': the keys', and a Font colour too. The Fill widens nothing either way.
      ADR-0071's section of that date holds it.
- **Comma Style is in the default row, disabled, with the reason.** Excel's Comma Style sets the
  built-in Comma style, whose code pads with `*` as Accounting does, and ExSheet does not read
  that code. Showing it disabled is what Format Cells does for Accounting. Setting another code
  under Excel's name would be quietly wrong.
- **The Number Format's list is Format Cells' categories, less two.**
  - Special is not on Excel's ribbon list, so it is left out.
  - Custom becomes "More Number Formats…", which opens Format Cells, as Excel's does.
  - Each available category sets what Format Cells' Number tab sets when that category is chosen
    with its defaults.
- **The keyboard goes back from the toolbar through an element of its own.**
  - The core's focus function is granted only while DOM focus is in the grid or on nothing
    (ADR-0021's note of 2026-09-30), and the toolbar is neither.
  - So the toolbar focuses an empty element of its own, removes it, and asks the core once the
    browser has applied that render.
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
- **The toolbar's keys follow the markup's order, which Blazor does not report.**
  - Blazor initialises a component nested one level deeper after its parent's later siblings. The
    default row's items would come after a Consumer row placed beside it.
  - So each container cascades a scope (`ToolbarOrder`), and each row and item takes the next
    place in the nearest scope.
  - A component of the Consumer's own that holds items stands inside a `ToolbarRow`.
- **DOM focus on the toolbar counts as the Sheet holding the keyboard** for a Pointing Scope
  (ADR-0058). The toolbar is inside the element whose focus events the Sheet reads. While an edit
  is open every item is disabled, so the toolbar is reached only by Tab, and the Point it keeps is
  the edit's own.

### Found while implementing the keys *(ticket 160, 2026-10-02)*

- **The Alt key alone needed one change to the core.** The core refused to declare a modifier by
  itself. Its own keydown now canonicalises to `Alt+Alt`, the one modifier that can be declared
  (ADR-0050 item 14's note of 2026-10-02). F10 and Ctrl+F1 were declarable already.
- **Each key is declared only while the toolbar can use it.**
  - The Alt key alone and F10 are declared while the toolbar is shown. With an edit open they are
    still claimed, and start nothing.
  - Ctrl+F1 is declared while `ShowToolbarChanged` is bound.
  - Otherwise each stays the browser's.
  - *(Corrected the same day.)* The first build also withdrew Alt and F10 while an edit was open,
    as "Otherwise both keys stay the browser's" reads above. That meant re-rendering the Sheet at
    every edit, which it never otherwise does. On a circuit the re-render, and the claims it
    re-told the grid, landed among the edit's own keys and presses, and lost what was typed
    (layer 3, the Server host: SRV-5 and two others).
  - So with an edit open the browser does not get Alt or F10 while the toolbar is shown. Its menu
    does not open from the Sheet then. This replaces the KeyTips paragraph's "otherwise both keys
    stay the browser's" for the open edit.
- **The KeyTips take the keyboard to the toolbar, as Excel's take it to the ribbon.** The letters
  typed next reach the toolbar, never the cell, so no edit opens.
  - From the KeyTips' start until DOM focus leaves the toolbar, every key's default is
    prevented.
  - It has to last past their end. The key that ends them gives the keyboard back while it is
    still being dispatched. On WebAssembly that comes before the key's default runs, and the
    letter was typed into the Sheet (layer 3).
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
- **The toolbar greys out after the grid has answered what opened the edit, not inside that
  answer.** *(Found on the Server host, SRV-5.)*
  - The edit's opening and ending are raised from inside the grid's answer to a key or a press.
  - Drawn there, every item's change of state landed, on a circuit, among the keys and presses
    the grid holds behind that answer, and a value typed into the next cell clicked was lost.
  - So the toolbar is told after the answer, by order rather than by a delay. A press on an item
    in between is refused by the command and said in the notice, as every press during an edit is.
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

- **A DemoHost sample only** (ADR-0071 as first written). Rejected. Its reasons are answered above.
  Every Consumer rebuilding the same buttons would rebuild them slightly differently.
- **A separate package, such as `ExSheet.Toolbar`.** Rejected. ADR-0019 separates packages by their
  dependencies, and a row of buttons adds none.
- **A separate component outside the Sheet, `<SheetToolbar Sheet="…" />`.** Rejected.
  - It can stand anywhere on the page. But the Consumer would have to account for its height, and
    hiding it would be the Consumer's own `@if`.
  - One toolbar per Sheet and a toggle are what a part of the Sheet gives directly.
- **Excel for the web's Alt+Windows key.** Rejected. The Windows key is often the operating system's,
  and it means nothing on Linux or macOS. Excel for the web takes it only because its browser holds
  Alt.

## Consequences

- ADR-0071's "A toolbar is a sample, not a product" is replaced. Ticket 54 builds the shipped toolbar
  and uses it on `/sheet` and its MudBlazor twin.
- `CONTEXT.md` gains Sheet Toolbar, Toolbar Row, Toolbar Item and KeyTip. The Definition of Done gains SH-48
  to SH-52.
- KeyTips and Ctrl+F1 are ticket 160. Their readings join the next Windows run.
