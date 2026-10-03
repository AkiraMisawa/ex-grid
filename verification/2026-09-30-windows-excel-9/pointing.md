# Verification — 2026-09-30, Windows, Excel pointing into a Table and another workbook (ninth run, Part A)

**Scope: Part A of [`verify-on-windows-9.md`](../../docs/specs/exsheet/verify-on-windows-9.md)**, what
Excel writes and shows while a Formula is open in an edit and points into a Table or into another
workbook, and what its completion lists. These are the questions
[ADR-0058](../../docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md) leaves to this
run ([ticket 41](../../docs/specs/exsheet/issues/41-after-the-ninth-windows-run.md)). **Part B was not
run.** It waits for tickets 34–38.

**Verified commit: `6b02036f0241e23668341f9e166e811eef6ab8de`**, the tip of
`claude/exsheet-pointing-scope` when this run began. The results are committed on
`claude/exsheet-windows-verify-9`, branched from that commit. Part A asks Excel only, so nothing was
built. Nothing here changes an ADR, `CONTEXT.md` or the Definition of Done.

Files beside this one:

- `pointing.ps1`: the script. It runs one case or a list (`-Case 1,2`). `-Pass b` names a second
  pass's files apart (`9b-…`). `-Case tree` writes out Excel's UI Automation tree, with no key sent.
- `pointing.jsonl`: one line per case and pass. The environment is case `0`. Each line holds:
  - the geometry, read through COM before the edit;
  - in each state:
    - what UI Automation read: each window's Formula Bar, Name Box and status bar mode, the window in
      front and the focused element;
    - every other window of Excel's that was showing, with its UI Automation tree;
    - the pixels that changed on each named cell's edges.
- `shots/`: for each state:
  - `<case>-<state>.png`: both windows, or the one window, at 600 ms, with the account's initials
    blanked;
  - crops of each window's Formula Bar (`-book1-formula-bar`, `-book2-…`), Name Box (`-name-box`) and
    cells A1:E12 or so (`-cells`);
  - each popup window (`-popup-<n>`): a completion list, a ScreenTip, a value tip, a dialog.

## Environment

- The machine of the earlier runs: Windows 11 Pro (build 26200) with one display, 3840×2160 at 150%
  (144 dpi; the work area is 3840×2088).
  - Windows is in **light mode** (`AppsUseLightTheme` 1, `SystemUsesLightTheme` 1). High contrast is
    off.
  - The regional format is en-GB. This run did not change it.
- **Excel: Microsoft 365, Version 2609 (Build 20430.20092 Click-to-Run), Current Channel, 64-bit**
  (16.0.20430.20092), the eighth run's build.
  - **Office Theme: "Use system setting"** (File › Account, read through UI Automation). There is no
    `UI Theme` value in the registry.
  - Settings: Edit directly in cell on, Formula AutoComplete on, Function ScreenTips on, "Use table
    names in formulas" on (`GenerateTableRefs` 1), the standard font Aptos Narrow 11. The windows are
    at 100% zoom.
- **The keyboard.**
  - Each case's Excel started with **English (UK), 0x08090809**, already. So did the script's own
    thread. (The eighth run had left the machine at 0x04110411 with the IME off; the keyboard in use
    was found at English (UK) this time.)
  - The script posted `WM_INPUTLANGCHANGEREQUEST` for 0x08090809 to each case's Excel anyway. At the
    end it restored what it had found, 0x08090809. No IME belongs to that keyboard, so the Japanese
    IME was not in the path.
  - **The keys arrived as typed**: `'=SUM(Positions[PV])+XLOOKUP(1,A2:A4,B2:B4,,0,-1)`, typed into D10
    and entered, read back identical through COM.
- **Key and mouse input.**
  - Keys went through `SendInput` (a virtual-key and a scan code per key), as in the eighth run.
    Shift+↓ is Shift down, ↓, Shift up.
  - A click moves the pointer from where it is to the cell's middle in 15 steps of 15 ms, then presses
    for 100 ms (`SetCursorPos`, `mouse_event`).
  - A drag is `excel-driver.ps1`'s `Drag-From`: pressed on the first cell's middle, moved in 20 steps,
    released on the last cell's middle.
- **Screenshots: `PrintWindow` (PW_RENDERFULLCONTENT)**, each window of Excel's laid at its place.
  `CopyFromScreen` still returned empty pixels (alpha 0), as in the eighth run. A popup is a window of
  its own, so it was captured on its own and laid over. Office's shadow windows
  (`MSO_BORDEREFFECT_WINDOW_CLASS`) were left out.

## Method

1. **An Excel of the script's own for every case** (`New-Object`, its process checked to be new;
   never attached). Excel names its workbooks as a fresh Excel does, **Book1** and **Book2**.
2. **Set up through COM** only:
   - Book1 has one sheet, `Sheet1`. A1:B4 is the Table `Positions` (`Id`: R-1, R-2, R-3; `PV`: 10,
     20, 30).
   - Book2, where the case has it, holds the Table `Trades` in the same shape.
   - The two windows are arranged by `Application.Windows.Arrange(xlArrangeStyleVertical)`, View ›
     Arrange All › Vertical. Book1 lands on the left, at (−1,−1)–(1919,2089), and Book2 on the right,
     at (1919,−1)–(3841,2089). A single Book1 is maximised.
   - Every window is at 100% zoom with A1 in view, and Book1's D10 is selected.
   - The workbooks hold no Name, unlike the eighth run's hidden mark. Case `10x` adds one.
3. **Each state** is the case's keys and clicks, then:
   - at 600 ms a picture, and 300 ms later a second one;
   - then what UI Automation reads.

   After the last state, Escape until Excel answers COM again. D10 was empty afterwards in every case.
4. **What UI Automation could not read was read from the pictures:**
   - **The Name Box.** Its `Edit` answers empty through UI Automation's value and through
     `WM_GETTEXT`, even while it shows a name. In pass a, the search did not even find it.
     - Its crops for pass a were therefore cut afterwards from each state's picture, 4 px around the
       rectangle that UI Automation gave in the trial and in pass b: maximised (16,283)–(134,311);
       side by side, Book1 (26,284)–(144,312) and Book2 (1946,284)–(2064,312).
   - **Book1's Formula Bar while two windows are arranged.** Its element offers neither a value nor a
     TextPattern. Book2's was read through UI Automation, and the two pictures show the same text in
     every state.
5. **Pass b.** In pass a two things happened:
   - from case 9 on, UI Automation gave each list and the Paste Name dialog as two empty panes (their
     pictures show the items);
   - from case 7x on, each Excel did not quit within 10 s of `Quit()` and was ended by `Stop-Process`.
     None of them held a workbook that was not the script's.

   Cases 12, 13, 9, 11 and 10x were run again in a new PowerShell process (pass b, 13:08:37–13:09:24).
   Every Excel quit, and every list was read through UI Automation. **Pass b's texts are the items
   pass a's pictures show.** The cause was not found.
6. **Trials before the recorded run (12:53–13:01)** found the elements and fixed the script. They are
   not in `pointing.jsonl`; they are kept on the machine in
   `%LOCALAPPDATA%\exgrid-layer3\pointing-9\trial-1253-1301`.
   - **They found that one click on the other window points at nothing** (below), so the Book2 cases
     record the procedure's one click and, as additions, a second click and what follows.
   - Their readings match the recorded run wherever both ran, with one exception. In the 12:58 trial
     the pointer was set straight onto the cell, and after that first click Book2's ribbon stayed on
     Home. In the recorded run the pointer travelled there, and the ribbon showed Table Design.

## Results

The run: pass a at 13:02:58–13:07:27 (cases 0–13, 6x, 7x, 10x), pass b at 13:08:37–13:09:24.
**"first-click"** is the one click the procedure says; a state marked *(addition)* is not in the
procedure.

| # | Book2 | Keys and clicks | What is asked | Reading | Excel | Agrees |
|---|---|---|---|---|---|---|
| 1 | — | `=`, then click B3 | The text written | `=B3` | **`=B3`**. Mode Point, Name Box `B3`, B3 dashed, `B3` shown selected (UI Automation's selection `B3`). A value tip `20` shows above D10 | yes |
| 2 | open | `=`, then click B3 of Book2 | The text written | `=[Book2]Sheet1!$B$3` | **After the one click, nothing is written.** Both Formula Bars show `=`, both status bars show Enter, and nothing is dashed. D10 shows `=` without its cell border. The same after a further 1.5 s *(addition)*. **A second click** *(addition)* **writes `=[Book2]Sheet1!$B$3`** (both Formula Bars and D10), in mode Point, with Book2's B3 dashed | no after the one click; yes after a second |
| 3 | — | `=`, then drag B2 to B4 | The text written | `=Positions[PV]` | **`=Positions[PV]`**. Mode Point, Name Box `B2`, shown selected. Value tip `{10;20;30}` | yes |
| 4 | — | `=`, then drag A2 to B4 | The text written | `=Positions[[Id]:[PV]]` or `=Positions` | **`=Positions`**. Name Box `A2`, shown selected. Value tip `{"R-1",10;"R-2",20;"R-3",30}` | yes (the second) |
| 5 | — | `=`, then click B1 | The text written | `=Positions[[#Headers],[PV]]` | **`=Positions[[#Headers],[PV]]`**. Name Box `B1`, shown selected. Value tip `"PV"` | yes |
| 6 | open | As case 2, then `{DOWN}` | The text; where the outline is; which window is active | the outline moves to Book2's B4, and the text follows (`$B$4`) | **After the one click, ↓ writes `=[Book2]Sheet1!$A$2`**, and Book2's **A2** is dashed: Book2's own active cell, A1, moved down. **6x** *(addition, with the second click)*: ↓ writes **`=[Book2]Sheet1!$B$4`**, and Book2's B4 is dashed. Active window: see below | no after the one click; yes (6x) after a second |
| 7 | open | As case 2, then `+{DOWN}` | The same | the outline grows to B3:B4 in Book2 | **After the one click, Shift+↓ writes `=[Book2]Sheet1!$D$10:$D$11`**, and Book2's **D10:D11** is dashed: the edited cell's address, extended, in the other workbook. **7x** *(addition, with the second click)*: Shift+↓ writes **`=[Book2]Sheet1!$B$3:$B$4`**, and Book2's B3:B4 is dashed | no after the one click; yes (7x) after a second |
| 8 | open | As case 2 | The Name Box's text while Book2's cell is pointed at | open | **Book1's Name Box (the edited workbook's) shows the pointed cell, `B3`. Book2's Name Box is empty.** After the one click, before anything is pointed at, Book1's shows `A1` (and `SUM` 1.5 s later, in case 2). Case 2's run shows the same. | (open) |
| 9 | — | `=SUM(Positions[` | The list, if any | `Id`, `PV`, `#All`, `#Data`, `#Headers`, `#Totals`, `@` | **`@ - This Row`** (selected), `Id`, `PV`, `#All`, `#Data`, `#Headers`, `#Totals`. The tip beside it: "Only choose this row of the specified column". The ScreenTip reads `SUM(number1, [number2], ...)` | no: `@` comes first, and reads `@ - This Row` |
| 10 | — | `=`, then F3 | Is a dialog shown? Its title, and every name listed. Is `Positions` among them? | Paste Name; whether tables are listed is open | **No dialog, and nothing else.** The edit stays `=` in mode Enter. **10x** *(addition: a Name `Rate` defined in Book1)*: the **Paste Name** dialog lists **`Rate` only. `Positions` is not listed** | no: nothing is shown without a Name |
| 11 | — | `=Posit`, Escape, `{BS}` | Is the list shown again after the Backspace? Its items | yes, as for `=Posi` | After `=Posit`: the list shows **`Positions`** (selected). After Escape: no list, and the edit stays `=Posit` in mode Enter. **After the Backspace (`=Posi`), the list shows `Positions` again** (selected) | yes |
| 12 | — | `=XLOOKUP(1,A2:A4,B2:B4,,0,` | The list at `search_mode`, every item's text | `1 - Search first-to-last`, `-1 - Search last-to-first`, `2 - Binary search (sorted ascending)`, `-2 - Binary search (sorted descending)` | **`1 - Search first-to-last`** (selected), **`-1 - Search last-to-first`**, **`2 - Binary search (sorted ascending order)`**, **`-2 - Binary search (sorted descending order)`**. The tip: "Perform a search starting at the first item". The ScreenTip reads `XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode])` with `[search_mode]` in bold | no: the last two end in "order)" |
| 13 | — | `=XLOOKUP(1,A2:A4,B2:B4,,` | The list at `match_mode` | the five texts of 2026-09-27 item 14 | **`0 - Exact match`** (selected), **`-1 - Exact match or next smaller item`**, **`1 - Exact match or next larger item`**, **`2 - Wildcard character match`**, **`3 - Regex match`**: item 14's five, character for character. The tip: "Searches for an Exact match, if not found return #N/A". `[match_mode]` is in bold in the ScreenTip | yes |

### Pointing into Book2, in more detail (cases 2, 6, 6x, 7, 7x, 8)

- **Which window is active.** Throughout, the window in front (`GetForegroundWindow`) is **Book1's**,
  the focused element is Book1's cell pane (`EXCEL6`), and Book1's title bar is drawn active and
  Book2's inactive. That holds after the first click, after the second, and after ↓ or Shift+↓.
- **The Formula Bars.** The edit shows in both windows' Formula Bars at once, with the same text.
  After the first click, `=`; once Book2 is pointed at, the written Reference. It also shows in D10.
- **The Name Boxes.** Book1's shows the cell pointed at in Book2:
  - `B3` after the second click, `B4` after ↓ (6x) and `B3` after Shift+↓ (7x);
  - `A2` after the one click and ↓ (6), and `A1` after the one click and Shift+↓ (7).

  Book2's Name Box is empty in every state.
- **The mode.** Both status bars show Enter after the one click and Point once a cell of Book2 is
  pointed at.
- **Book2's ribbon** shows the Table Design tab from the first click on, in every state, with Table
  Name **`Positions`**, which is Book1's Table. Book2's own Table is `Trades`.
- **The dashes** are Excel's green, #217346, drawn as 2–11 runs along each edge. They are around
  exactly the cells the text names: Book2's B3, B4, A2, D10:D11 or B3:B4 (`outlines` in the JSON).
  Nothing is dashed in Book2 after the one click alone.
- UI Automation's selection in Book2's Formula Bar is empty in every one of these states (the grey
  selection of cases 1 and 3–5 is not reported there).

## Where Excel and the readings differ

Each is a difference from a reading of the procedure, or from a statement ADR-0058 makes about Excel.
Nothing here is decided.

1. **One click on the other workbook's window points at nothing** (cases 2, 6, 7, 8). The edit's
   `=` appears in Book2's Formula Bar too, and a second click writes the Reference. The text written after the second
   click is the reading's, `=[Book2]Sheet1!$B$3`. ADR-0058, "What Excel writes cannot be brought
   over": "`[Book2]Sheet1!$C$5` from another workbook. *(Recalled, not observed.)*"
2. **↓ after the one click points at Book2's A2** (`=[Book2]Sheet1!$A$2`, case 6), from Book2's own
   active cell. **Shift+↓ after the one click writes `=[Book2]Sheet1!$D$10:$D$11`** (case 7), the
   edited cell's address in the other workbook. **After a second click, the arrows move inside
   Book2**, as the readings say: ↓ to `$B$4` (6x), and Shift+↓ to `$B$3:$B$4` (7x). ADR-0058, "The
   keyboard" ("Moving inside the other grid instead is not yet known to be what Excel does") and "Not
   in the first version" ("Moving the pointed cell inside a registered grid with the arrow keys …
   waits for the ninth Windows run").
3. **The Name Box names the cell pointed at**, not the edited cell. While Book2's B3 is pointed at,
   the edited workbook's Name Box reads `B3` and Book2's is empty (case 8). The same holds inside one
   workbook: `B3`, `B2`, `A2` and `B1` in cases 1, 3, 4 and 5, while D10 is edited. The reading was
   open. ADR-0058, "What is written": "While Point writes from a registered grid, the Name Box names
   the edited cell."
4. **After `Table[`, `@` comes first and reads `@ - This Row`** (case 9), before `Id`, `PV`, `#All`,
   `#Data`, `#Headers`, `#Totals`. ADR-0058, "Completion, aligned with Excel", the table's `Table[` row:
   "the columns, and `#All`, `#Data`, `#Headers`, `#Totals`, `@` (recalled)".
5. **F3 shows nothing in a workbook without a Name** (case 10). With a Name, Paste Name lists the
   Name and not the Table (10x). ADR-0058, "Completion, aligned with Excel", the table's F3 row:
   "Paste Name; whether it lists tables is not known".
6. **`search_mode`'s texts end in "order)"**: `2 - Binary search (sorted ascending order)` and
   `-2 - Binary search (sorted descending order)` (case 12). ADR-0058, "Completion, aligned with Excel":
   "`search_mode`'s are read from Excel's documentation, and the ninth Windows run asks for them"
   (ticket 39; SH-36).

What agreed:

- the plain address inside one workbook (1);
- the structured references a drag and a header click write (3, 5; 4 wrote `=Positions`);
- the list again after Backspace (11);
- `match_mode`'s five texts (13).

Also seen, and asked by nothing:

- the value tip above the edited cell while a Reference is pointed at (1, 3, 4, 5);
- Book2's ribbon naming Book1's Table (above).

## The machine afterwards

- **No Excel process was left.** Every Excel this run started was its own (17 in pass a, 5 in pass b,
  and those of the trials). None of them was attached to, and no Excel of the user's was running.
- **The AutoRecover workbooks were not touched.** `Book1 (version 1).xlsb` and the unsaved workbook in
  `%APPDATA%\Microsoft\Excel` are byte for byte the backup taken before the first Excel started
  (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run9`). `Excel15.xlb`, Excel's own toolbar file
  beside them, was rewritten by an Excel quitting, at 13:04:35.
- **The keyboard** is English (UK), 0x08090809, as the run found it. The regional format is en-GB.
