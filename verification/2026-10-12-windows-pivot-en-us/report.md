# Windows, 2026-10-12: ExPivot's captions in Excel's English (US) edition

**Branch `claude/exsheet-windows-pivot-en-us-2026-10-12`, from `claude/exsheet-windows-readings-2026-10-12` at
`89e0b5ad`**, whose R3 this follows. It is the requesting session's item 5, run at the user's ask to carry on with
whatever was still possible, 22:06 to 22:24 BST. Run by a Claude Code session in WSL2 on the Windows desktop of the
earlier runs. **Nothing was decided.**

**What was asked.** R3 read Excel in its English (United Kingdom) edition: `Max. of Note`, `Min. of Note`, and `Max.`,
`Min.` in the Summarise list. This reads the same things in the English (United States) edition:
- R3 (a)'s three headers;
- (d)'s Custom Name after Count Numbers is chosen, and the cell's header after OK;
- the Summarize Values By list's names;
- the Field List's two instruction lines.

## The first look (22:06 to 22:10): English (US) needs a download

`excel/office-ui-lang.ps1` (R3's, with modes added) started an Excel of its own, opened File > Options > Language
through its KeyTips, read the page through UI Automation, and cancelled every dialog it opened
(`logs/lang-dump-before.log`, `logs/lang-adddump.log`).

- **`LanguageID(msoLanguageIDUI)` was 2057**, English (United Kingdom).
- **The display language list**, as R3 left it: `1. Match Microsoft Windows [English] <preferred>`, `2. English`,
  `3. Japanese [日本語]`.
  - `LanguageResources` read `UILanguageTag` en-GB and `UISnapshotLanguages` en-gb;ja-jp.
  - English (US) was installed only as an editing language, with proofing.
- **The display group's Add a Language…** opens **Install a display language**. English (United Kingdom) and Japanese
  are greyed there. **English (United States) is offered with the dialog's Install button**, a download.
- As the request then said, the readings stopped there, and the requester was told.
- **The user then decided to install it and read**, confirmed in this session's terminal, and approved the UAC
  prompts.

## The install (22:11 to 22:15)

`logs/office-state-before-install.log`: Office 16.0.20430.20146 (`O365HomePremRetail`, x64, Current Channel),
`ClientCulture` en-gb, the culture keys en-gb, ja-jp and x-none. There was no `root\Office16\1033`.

- **The route was run 17's for Japanese** (`verification/2026-10-11-windows-17/expivot.md`, item 27). The dialog's
  Install button was not pressed. What it does was not tried: it may hand the download to the default browser, which
  is the user's own Chrome, and that was not to be touched.
  - `scripts/lap-en-us.ps1` downloaded Microsoft's English (United States) language accessory pack from
    `c2rsetup.officeapps.live.com`.
  - Its signature was **Valid, CN=Microsoft Corporation**, and the file "Microsoft 365 and Office" 16.0.20430.20146
    (`logs/lap-download.log`).
  - It ran elevated, the user approving UAC, from 22:13:25 to 22:14:53 (`logs/lap-run.log`, `logs/lap-wait.log`).
- **Installed** (`logs/office-state-after-install.log`): `root\Office16\1033\XLINTL32.DLL` 16.0.20430.20032, and the
  culture key en-us.16. The en-GB and Japanese resources were left as they were.
- **What the install changed besides**:
  - `HKLM\…\ClickToRun\Configuration\ClientCulture` went from en-gb to **en-us**, as the Japanese install had turned it
    to ja-jp. It was put back at the end.
  - `LanguageResources`: `UIFallbackLanguages` and `HelpFallbackLanguages` gained en-us.
  - `UISnapshotLanguages` was emptied by the install, and refilled as `en-gb;en-us;ja-jp` at Excel's next start.
- The display list then read: `1. Match Microsoft Windows [English (United Kingdom)] <preferred>`,
  `2. English (United Kingdom)`, `3. Japanese [日本語]`, `4. English (United States)`. With two Englishes,
  "English" is now named "English (United Kingdom)".

## English (US) as Office's display language (22:15)

- Through Options: English (United States) selected, Set as Preferred, OK, the restart notice answered
  (`logs/lang-set-en-us.log`). `UILanguageTag` became en-us.
- **A new Excel started with `LanguageID(msoLanguageIDUI)` = 1033** (`logs/languageid-en-us.log`).

## The readings in English (US)

### (a) R3 (a)'s headers over `e05c.csv`, through COM

`part-e/part-e-excel.ps1` (run 17's, unchanged) with `part-e/items-r3.json` (R3's): the record is
`part-e/excel-r3-en-us.json`.

| Value Field | English (UK), R3 | **English (US)** | ExPivot (R3) |
|---|---|---|---|
| Max | `Max. of Note` | **`Max of Note`** | `Max of Note` |
| Min | `Min. of Note` | **`Min of Note`** | `Min of Note` |
| the second Count-like (Count Numbers, function 11) | `Count of Note2` | **`Count of Note2`** | `Count Numbers of Note` |

- **Every other header and every cell is the same as R3's English (UK) record**, for e05c (5 rows, 12 columns) and for
  e16b (`1.23456789`, `1.23457E+11`). Compared cell by cell, the only differences are the two dots.

### (d) Value Field Settings with the real pointer

The same small PivotTable as R3 (rows Group, Sum of Amount, over A1:B4), with the same steps and the same screen
places. The dialog stood at the same place, 153,713 to 688,1226 (`records/xs.jsonl`; pictures in `shots/`).

- **The right press menu** on a value cell reads **`Summarize Values By`**, with a z (R3's English (UK) read
  `Summarise Values By`), `Show Values As` and `Value Field Settings...`.
- **Its submenu** (`shots/us-d-summarize-menu-c.png`): `Sum`, `Count`, `Average`, **`Max`**, **`Min`**, `Product`,
  `Distinct Count` (greyed), `More Options...`. UI Automation reads `Max` and `Min` without a dot too.
- **Value Field Settings' list** (`us-d-1-dialog.png`, `us-d-5-dialog.png`): `Sum`, `Count`, `Average`, **`Max`**,
  **`Min`**, `Product`, `Count Numbers`, `StdDev`, `StdDevp`, `Var`, `Varp`. The tab reads `Summarize Values By` and
  the heading `Summarize value field by`.
- **Count Numbers chosen** (the list's first entry pressed, then ↓ six times): the Custom Name becomes
  **`Count of Amount`** (`us-d-2-dialog.png`), as in English (UK).
- **After OK**: the cell's header is **`Count of Amount`** (`us-d-3-cells.png`), and COM reads the function back as
  **11**, as in English (UK).
- Opened again, the dialog still has Count Numbers selected (`us-d-4-dialog.png`). Pressing End there showed the list's
  end (`us-d-5-dialog.png`); the dialog was cancelled, and the field stayed Count Numbers (function 11).

### (b) The Field List's two instruction lines

The same pane, at the same places as R3's English (UK) pictures (`shots/us-b-pane.png`; each line enlarged 4 times by
nearest neighbour, `us-b-line1-x4.png`, `us-b-line2-x4.png`):

| Line | English (US) | UI Automation's name |
|---|---|---|
| 1 | `Choose fields to add to report:` | `Choose fields to add to report` |
| 2 | `Drag fields between areas below:` | `Drag fields between areas below` |

- **Each line ends with a colon**, as in English (UK). UI Automation's names leave it out, as there.

## Put back (22:20 to 22:24)

- **The display language**, through Options:
  - "Match Microsoft Windows" set as preferred (`logs/lang-set-windows.log`). The list then read Match, English (US),
    English (UK), Japanese.
  - English (United Kingdom) moved up, then Japanese moved up (`logs/lang-set-en-gb-up.log`,
    `logs/lang-set-ja-up.log`).
  - **The list reads `1. Match Microsoft Windows [English (United Kingdom)] <preferred>`, `2. English (United Kingdom)`,
    `3. Japanese [日本語]`, `4. English (United States)`** (`logs/lang-dump-after.log`). That is R3's order, with the
    installed English (US) last, as the install had placed it.
- **A new Excel starts with `LanguageID(msoLanguageIDUI)` = 2057** (`logs/languageid-final.log`).
- **`PreviousUILanguage`**: Excel had rewritten it from `en-gb` to `en-GB`, as it did in R3. It was set back with
  `reg add`. A later Excel start left it `en-gb`.
- **`ClientCulture`** was set back to en-gb, elevated, the user approving UAC (`scripts/client-culture-restore.ps1`,
  `logs/client-culture-restore.log`, 22:21:43).
- **`HKCU\Software\Microsoft\Office\16.0\Common\LanguageResources` against before** (`logs/languageresources.txt`):

  | Value | R3's end state | Now | Why |
  |---|---|---|---|
  | `RegionalAndLanguageSettingsTimestamp` (two places) | 2026-10-11 20:47:54 UTC | 2026-10-12 21:07:53 UTC | Office's stamp of reading Windows' language settings, written when the first Excel of the first look started. Those settings had been switched to Japanese and back that evening (`verification/2026-10-12-windows-glyphs-ja/`). Left as Office wrote it |
  | `UIFallbackLanguages` | `x-none;en-gb;ja-jp` | `x-none;en-gb;ja-jp;en-us` | the installed English (US) pack, in the list's order |
  | `HelpFallbackLanguages` | `x-none;en-gb;ja-jp` | `x-none;en-gb;ja-jp;en-us` | the same |
  | `UISnapshotLanguages` | `en-gb;ja-jp` | `en-gb;en-us;ja-jp` | the installed UI languages, as Office lists them |

  - Every other value is the same, `UILanguageTag` en-GB and `PreviousUILanguage` en-gb among them.
  - The three values that name en-us follow from the pack being installed, and stay with it.
- **The English (United States) pack stays installed** (`root\Office16\1033`, the culture key en-us.16), as the
  requester said.
- No Excel is left running. Whole-window pictures stay on the machine (`%LOCALAPPDATA%\exgrid-layer3\
  pivot-en-us-2026-10-12\xs\shots`), because they show the title bar's account corner. `shots/` holds crops only.

## The tools

- `excel/xl-server.ps1` and `excel/xs.py` are the readings' copies; `xs.py` points at this record's folder.
- `excel/xs-start.sh`, `excel/x.sh` and `excel/xq.py` start it and send it one command from the shell. Every exchange
  is in `records/xs.jsonl`.
- `excel/office-ui-lang.ps1` is R3's, with the modes `adddump` (read Install a display language, then cancel) and the
  targets `en-us`, `en-gb-up` and `ja-up` added.
- `scripts/`:
  - `lap-en-us.ps1` and `c2r-wait-en-us.ps1` are run 17's `lap.ps1` and `c2r-wait.ps1` for English (US);
  - `office-state.ps1` reads Click-to-Run's configuration, the UI resource folders and `LanguageResources`, and exports
    the key;
  - `client-culture-restore.ps1` sets `ClientCulture` back.

## Nothing for the Docs Site

This branch adds a verification record only.
