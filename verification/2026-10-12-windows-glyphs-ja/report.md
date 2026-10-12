# Windows, 2026-10-12: FN-12e's glyph widths under a Japanese display language

**Branch `claude/exsheet-windows-glyphs-ja-2026-10-12`, from `claude/run17gh-glyphs` at `f5b9f093`**, which carries
the fixed `tests/GlyphWidths/measure.mjs` (it runs on Windows and in Edge) and the English (United Kingdom) Windows
records of the seventeenth run. Asked by the requesting session, before `0.1.0-rc.1`, with the user's agreement. Run
by a Claude Code session in WSL2 on the Windows desktop of the earlier runs. **Nothing was decided.**

**Why.** The seventeenth run measured `system-ui` on Windows only under the English display language, where it is
Segoe UI. Under a Japanese display language, `system-ui` is expected to be Yu Gothic UI. This run measures that face
the same way, and the records' painters say which face painted each glyph.

*(Written in two parts: this part before the first sign-out, the rest after the measurements.)*

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
