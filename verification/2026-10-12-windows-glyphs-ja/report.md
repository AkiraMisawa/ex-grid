# Windows, 2026-10-12: FN-12e's glyph widths under a Japanese display language

**Verified commit:** `f5b9f093dd22b0977f13f0886d971aef2a855e13` — tagged `verified/2026-10-12-windows-glyphs-ja` (the orchestrator's reading of 2026-10-13: the measure.mjs and corpus.json it ran are f5b9f093's)

**Branch `claude/exsheet-windows-glyphs-ja-2026-10-12`, from `claude/run17gh-glyphs` at `f5b9f093`**, which carries
the fixed `tests/GlyphWidths/measure.mjs` (it runs on Windows and in Edge) and the English (United Kingdom) Windows
records of the seventeenth run. Asked by the requesting session, before `0.1.0-rc.1`, with the user's agreement. Run
by a Claude Code session in WSL2 on the Windows desktop of the earlier runs. **Nothing was decided.**

**Why.** The seventeenth run measured `system-ui` on Windows only under the English display language, where it is
Segoe UI. Under a Japanese display language, `system-ui` is expected to be Yu Gothic UI. This run measures that face
the same way, and the records' painters say which face painted each glyph.

*(Written in two parts: the plan and the switch before the first sign-out, the measurements after it.)*

## Environment

- Windows 11 25H2 (build 26200.9457), 3840 × 2160 device px at **150%** (`LogPixels` 144).
- **Chrome 154.0.8037.98** (`C:\Program Files (x86)\Google\Chrome\Application\chrome.exe`, given to `measure.mjs` with
  `--chrome`, because it is not where `measure.mjs` looks) and **Edge 155.0.4283.45**.
- Node v24.14.1 on Windows (the portable copy at `%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64`).
- `measure.mjs` runs each browser headless, with a profile of its own and `--force-device-scale-factor=1`. It ran
  from a copy of the files it reads (`tests/GlyphWidths/` and `samples/ExGrid.DemoPages/wwwroot/fonts/roboto/`) at
  `%LOCALAPPDATA%\exgrid-layer3\glyphs-ja-2026-10-12\repo`, so the repository's English records are not overwritten.
- **`tests/GlyphWidths/corpus.json`**: SHA-256 `6e57bbb7dadc6aea4798c0dc4f67bafd9228eb55f3ffa378ca2f84daa2e615c2`,
  the branch's. The records hold only while it is unchanged.

## The display language before the switch

`scripts/lang-state.ps1` wrote `logs/lang-before.json` at 21:11 BST:

- no UI language override; the user's language list en-GB, en-US, ja;
- `PreferredUILanguages` en-GB, no `PreferredUILanguagesPending`;
- the regional format en-GB; the system locale (for programs that are not Unicode) ja-JP;
- the message font Segoe UI.

The Japanese display language pack was already installed (`ja-JP`: `LpCab`, `LXP`), so nothing was downloaded.

## The switch (21:14 BST)

- `Set-WinUILanguageOverride -Language ja-JP`: the override became `ja`, `PreferredUILanguagesPending` ja-JP, and
  `User Profile\WindowsOverride` `ja` (`logs/lang-switched.json`).
- **It also changed the regional format to ja-JP at once** (`LocaleName`, `sShortDate` `yyyy/MM/dd`, `sCurrency` `¥` and
  the rest). So that the display language is the only thing changed, the format was put back with `Set-Culture en-GB`
  (`scripts/culture-en-gb.ps1`, `logs/culture-before-sign-out.log`). Against the export of 2026-10-12 16:13
  (`international-before.reg`, SHA-256 `071C2B47…456B1AF`), the only difference left is `User Profile\WindowsOverride`,
  the override itself.

## How it runs

1. Before the first sign-out: this record so far is committed and pushed. The display language is set to Japanese for
   the next sign-in with `Set-WinUILanguageOverride -Language ja-JP`.
2. The user signs out and signs in. Windows' display language is then Japanese. WSL ends with the sign-out; the
   session is resumed after the sign-in.
3. `scripts/measure-ja.cmd` runs `measure.mjs` for `system-ui` and `roboto`, in Chrome and in Edge, on Windows' Node.
   The four records are copied here as `glyphs/<face>.windows-ja.<browser>.json`. They are not put in
   `tests/GlyphWidths/`, which layer 1 reads; where they belong is the requester's call.
4. The records' painters are read: which face painted `system-ui`.
5. The override is cleared again (`Set-WinUILanguageOverride` with no language), and `lang-state.ps1` is compared
   with `logs/lang-before.json`. The user signs out and signs in again, back in English (United Kingdom).

## After the sign-in (21:33 BST)

`logs/lang-signed-in-ja.json`:
- the UI culture **ja-JP**, `PreferredUILanguages` ja-JP, the override `ja`;
- the regional format **en-GB**, as it was put back; the scale **150%** (`LogPixels` 144);
- the message font **Yu Gothic UI**.

## The measurements (21:33 to 21:34 BST)

`scripts/measure-ja.cmd`, `logs/measure.log`. Every run exited 0. Each record holds 523 glyphs and 3091 strings, and
its corpus hash (`0508ec5d…`) is the English records'. The records are `glyphs/<face>.windows-ja.<browser>.json`:

| Record | Browser |
|---|---|
| `system-ui.windows-ja.chrome.json` | Chrome 154.0.8037.98, headless |
| `system-ui.windows-ja.msedge.json` | Edge 155.0.4283.45, headless |
| `roboto.windows-ja.chrome.json` | Chrome 154.0.8037.98, headless |
| `roboto.windows-ja.msedge.json` | Edge 155.0.4283.45, headless |

## Which faces painted (`painters.md`, from `painters.py`)

**`system-ui` painted Yu Gothic UI, in Chrome and in Edge.** The counts, per weight, are the same in both browsers:

| Weight | English (UK) display, the seventeenth run | Japanese display |
|---|---|---|
| 400 | Segoe UI 483; Tahoma 28; Microsoft YaHei (Chrome) or Noto Sans JP (Edge) 11; Malgun Gothic 1 | **Yu Gothic UI 467**; Tahoma 37; Segoe UI 18; Malgun Gothic 1 |
| 500 | Segoe UI Semibold 483; Tahoma 28; the same 11; Malgun Gothic 1 | **Yu Gothic UI Semibold 467**; Tahoma 37; Segoe UI Semibold 18; Malgun Gothic 1 |
| 600 | Segoe UI Semibold 483; Tahoma 28; the same 11; Malgun Gothic 1 | **Yu Gothic UI Semibold 467**; Tahoma 37; Segoe UI Semibold 18; Malgun Gothic 1 |
| 700 | Segoe UI 483; Tahoma 28; the same 11; Malgun Gothic 1 | **Yu Gothic UI 467**; Tahoma 37; Segoe UI 18; Malgun Gothic 1 |

- At 400, 476 of the 523 glyphs changed face. Segoe UI → Yu Gothic UI: 456, the Latin letters, the digits and the
  signs. Microsoft YaHei or Noto Sans JP → Yu Gothic UI: 11, the kanji numerals and 月. Segoe UI → Tahoma: 9, the
  currency signs `؋₭₲₴₵₼₽₾₿`, which Yu Gothic UI lacks.
- 18 glyphs stayed in Segoe UI: glyphs Yu Gothic UI lacks that Segoe UI has.
- **`roboto` is unchanged but for the 11 CJK glyphs in Chrome**, which moved from Microsoft YaHei to Noto Sans JP, the
  face Edge used under both displays.

## Each class's widest glyph (`glyph-classes.txt`)

Run 17's `part-g/glyph-classes.py`: each `CellTextMetrics` class's widest glyph, at 14 px and 12 px, regular (the
widest of 400, 500 and 600) and bold (700), against the core's defaults. The defaults in `GridMetrics.cs` are the same
at `f5b9f093` as at `8436e45e`. The class members are the script's, as run 17 used them.

| `system-ui` under the Japanese display, Chrome and Edge alike | widest | default |
|---|---|---|
| wide, 14 px regular / bold | 14.000 (`−`) | 14.04 / 14.36 |
| digit, 14 px regular / bold | **9.703** (`₺`) | 9.75 |
| narrow, 14 px | 5.797 (`/`) | 6.41 |
| wide, 12 px regular / bold | 12.000 (`−`) | 12.04 / 12.44 |
| digit, 12 px | **8.328** (`₺`) | 8.36 |
| narrow, 12 px | 4.969 (`/`) | 5.49 |
| other, 14 px (full-width aside) | **15.438** (`ѝ`, 700) | 15.46 |
| other, 12 px (full-width aside) | 13.234 (`ѝ`, 700) | 13.26 |

- **By class, every widest glyph is within its default**, in both faces and both browsers. Three are within 0.05 px:
  the digit `₺` at both sizes, and the other class's `ѝ` at 14 px bold.
  *(Corrected 2026-10-12, when the records were folded on `claude/run17gh-glyphs`: `ѝ` is a letter the core's table
  holds, so it is charged its entry, not the other class, and was charged 9.82 px at 14 px, under what Yu Gothic UI
  paints; the table now charges it 15.45. The widest glyph no table holds is `؋` in Tahoma, 14.328 px against 15.46
  (12.281 against 13.26 at 12 px). Within 0.05 px are the digit `₺` at both sizes, by 0.047 and 0.032 px, and the
  wide class's `−` at both sizes, regular, by 0.04 px.)*
- `roboto` under the Japanese display is within too, as under the English one (wide 12.453, digit 9.344, narrow 4.672
  at 14 px).
  *(Corrected 2026-10-12: those are compared with the core's defaults. Against the widths `MudExGridPresentation`
  cascades for Roboto, `%` and `E` are past the wide and digit classes (10.4 and 8.3). And they are not Roboto's:
  every glyph the records name Roboto as the painter of reads Arial's advance, 446 of 451 exactly, so the widths
  were read before Roboto applied. Layer 1 does not read these two records; `tests/GlyphWidths/README.md` says why.)*
- **Per glyph** (layer 1's `Every_glyph_is_charged_at_least_its_width`, and `tables.mjs`'s tables) is not run here. The
  requester folds these records into `tests/GlyphWidths/` and runs `tables.mjs` there.

## The switch back (21:37 BST)

- `Set-WinUILanguageOverride` with no language cleared the override, and left the regional format en-GB.
  `PreferredUILanguagesPending` is en-GB, for the next sign-in (`logs/lang-switched-back.json`).
- **`HKCU\Control Panel\International` is byte for byte the export of 16:13** (SHA-256 `071C2B47…456B1AF`,
  `logs/international-switched-back.log`). `User Profile\WindowsOverride` is gone.
- Left until the next sign-in: `PreferredUILanguages` ja-JP, which the pending en-GB replaces, and the UI culture and
  the message font, which follow it.

## After the second sign-in (21:40 BST)

`logs/lang-signed-in-again.json` **matches `logs/lang-before.json` in every value**: no override, `PreferredUILanguages`
en-GB, no `PreferredUILanguagesPending`, the UI culture and the regional format en-GB, the message font Segoe UI, 150%.
`HKCU\Control Panel\International` is still byte for byte the export of 16:13
(`logs/international-signed-in-again.log`). Windows is back in English (United Kingdom).

## Nothing for the Docs Site

This branch adds a verification record only.

