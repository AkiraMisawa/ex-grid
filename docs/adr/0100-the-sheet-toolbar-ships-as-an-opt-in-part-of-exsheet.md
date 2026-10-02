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

## Readings until a Windows run

- **Excel's Home tab letters.** Microsoft's page confirms H for Fill, B for Borders and AC for
  Center. The rest, and the free letters, are read off Excel by a Windows run.
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
- **Each key is declared only while it means something.**
  - The Alt key alone and F10 are declared while the toolbar is shown and no edit is open.
  - Ctrl+F1 is declared while `ShowToolbarChanged` is bound.
  - Otherwise each stays the browser's.
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
- **A KeyTip on a split control opens its list,** as Excel's Alt, H, H opens the Fill's palette. A
  toggle's KeyTip sets it and gives the keyboard back.

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
