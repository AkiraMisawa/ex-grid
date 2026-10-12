# Windows, 2026-10-12: ExPivot's captions in Excel's English (US) edition — not run

**Branch `claude/exsheet-windows-pivot-en-us-2026-10-12`, from `claude/exsheet-windows-readings-2026-10-12` at
`89e0b5ad`**, whose R3 this follows. Asked by the requesting session as its item 5, and run at the user's ask to carry
on with whatever was still possible, 22:06 to 22:10 BST. Run by a Claude Code session in WSL2 on the Windows desktop of
the earlier runs. **Nothing was decided, and Office's display language was not changed.**

**What was asked.** R3 read Excel in its English (United Kingdom) edition: `Max. of Note`, `Min. of Note`, and `Max.`,
`Min.` in the Summarize list. The request:
1. read `Application.LanguageSettings.LanguageID(msoLanguageIDUI)` as it is now;
2. if English (US) can be set as Office's display language through File > Options > Language without admin rights or
   a download, set it and read R3 (a)'s three headers, (d)'s Custom Name after Count Numbers is chosen, and the
   Summarize list's names from pictures;
3. otherwise, say so and stop.

## What was read

`excel/office-ui-lang.ps1` (R3's, with a mode added, `adddump`) started an Excel of its own and opened File >
Options > Language through its KeyTips. It read the page through UI Automation, and cancelled every dialog it opened.

- **`LanguageID(msoLanguageIDUI)` is 2057**, English (United Kingdom) (`logs/lang-dump-before.log`).
- **Office display language**, as R3 left it:
  - `1. Match Microsoft Windows [English] <preferred>`, `2. English`, `3. Japanese [日本語]`;
  - `LanguageResources` reads `UILanguageTag` en-GB and `UISnapshotLanguages` en-gb;ja-jp.
  - **English (US) is not in the list.**
- **The display group's Add a Language…** opens **Install a display language**
  (`logs/lang-adddump.log`), "Select the language you want to install":
  - English (United Kingdom) and Japanese are greyed (installed);
  - **English (United States) is offered, with the dialog's Install button** and a "Set as Office display language"
    check box.
  - So English (US) is a display language to be installed, a download. Japanese, for run 17, was installed the same way
    and asked for the user's approval of each UAC prompt.
- English (United States) is installed as an **editing** language with proofing, which does not change the display
  language.

**English (US) cannot be set without a download, so the readings were not made, as the request says.** Install was
not pressed. Both dialogs were cancelled, and Excel quit.

## What changed

`logs/languageresources.txt`: `HKCU\Software\Microsoft\Office\16.0\Common\LanguageResources` exported before Excel
started (SHA-256 `828124b6…`, R3's end state) and after it quit.
- **One value differs**, `RegionalAndLanguageSettingsTimestamp`, in its two places: from 2026-10-11 20:47:54 UTC to
  2026-10-12 21:07:53 UTC, the moment this Excel started.
- It is Office's own time stamp of when it last read Windows' regional and language settings. Those had been switched to
  Japanese and back that evening (`verification/2026-10-12-windows-glyphs-ja/`).
- It was left as Office wrote it. The display language list and every other value are as before.

## Nothing for the Docs Site

This branch adds a verification record only.
