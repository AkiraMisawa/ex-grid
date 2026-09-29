# Verification — 2026-09-29, Windows, Excel as the oracle, fifth run: the ordering operators

**Scope: Part A of [`verify-on-windows-5.md`](../../docs/specs/exsheet/verify-on-windows-5.md).**
ARITH-146..152 asked of a real Excel under en-US, through COM and with real keys, then `-Update`.
Parts B and C are in [`../2026-09-29-windows-5/results.md`](../2026-09-29-windows-5/results.md).

**Verified commit: `3fa16a90fb42f0c4c8223189d93649a9e8996e52`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began, for every part. The results are committed on
`claude/exsheet-windows-verify-5`, branched from that commit. Nothing here changes the engine, an
ADR, `CONTEXT.md` or the Definition of Done. The corpus changed only where `-Update` changed it:
`source` on three cases (below).

## Environment

- The machine of the earlier runs: Windows 11 Pro (build 26200), one display, 3840×2160 at 150%
  (144 dpi), display language en-GB, regional format en-GB, system locale ja-JP
- **Excel: Microsoft 365, 16.0.20326.20158, 64-bit**, the build of the earlier runs
- Windows PowerShell 5.1 drives Excel. Build and layers 1–2 ran in WSL2 through nix at the verified
  commit (18:12–18:13): `dotnet build ExGrid.slnx` gave **0 warnings, 0 errors**; `dotnet test
  ExGrid.slnx` was **green**: ExGrid.Tests 793, ExSheet.Engine.Tests 1771, ExGrid.MudBlazor.Tests 79,
  ExGrid.Components 868 (1 skipped), ExSheet.Components.Tests 246 passed, 0 failed
- `oracle.ps1` is the verified commit's, unchanged since before the fourth run (393eb61)
- **The regional format was changed under the user's advance authorisation** (`Set-Culture`), as
  the procedure's correction to the equality run says: **en-US at 18:13:50**, for the three passes,
  and **back to en-GB at 18:15:05**. `HKCU\Control Panel\International` was exported at 18:13:37,
  before the change ([`international-before.reg`](international-before.reg)), and at 18:15:05, after
  the return ([`international-after.reg`](international-after.reg)). **The two are identical, byte
  for byte** (SHA-256 `071c2b4716c2b1afdad10629c20cc64d99291bf7ec6466fcb1cc8b923456b1af`), and
  identical to the fourth run's two exports
- The AutoRecovered workbook in `%APPDATA%\Microsoft\Excel` was backed up first and is byte for
  byte unchanged; no Excel was running before or after

## Part A — the ordering operators

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1 -Id ARITH-146,…,ARITH-152`, under en-US:

| File | Entered through | Cases | agree | disagree | blocked | Time |
|---|---|---|---|---|---|---|
| `results-2026-09-29-run5.json` | COM (`Range.Formula2`) | 7 | **7** | 0 | 0 | 18:14:08–18:14:13 |
| `results-2026-09-29-run5-keys.json` | keys (typed, SendInput, then Enter) | 7 | **7** | 0 | 0 | 18:14:20–18:14:32 |
| `-Update` (`%LOCALAPPDATA%\exgrid-layer3\update-run5-en-US.json`, not committed) | COM | 7 | 7 | 0 | 0 | 18:14:38–18:14:41 |

(The committed files are in `tests/ExSheet.Engine.Tests/ExcelOracle/`.) The `-Keys` file's header
says `formulasEnteredThrough: typed (keyboard input), as every other cell of a case`: under en-US the
Formulas were typed, which the equality run under en-GB could not do.

**Excel's answer is the corpus's in all seven, through COM and typed alike:**

| Case | Formula | Corpus (engine) | **Excel** (COM and keys) | Excel's Formula read back |
|---|---|---|---|---|
| ARITH-146 | `=9+3E-14=9` | FALSE | **FALSE** | `=9+0.00000000000003=9` |
| ARITH-147 | `=1+4E-15=1+6E-15` | FALSE | **FALSE** | `=1+0.000000000000004=1+0.000000000000006` |
| ARITH-148 | `=1000+3.6E-12=1000` | TRUE | **TRUE** | `=1000+0.0000000000036=1000` |
| ARITH-149 | `=9+5E-15=9` | FALSE | **FALSE** | `=9+0.000000000000005=9` |
| **ARITH-150** | `=1+4.4E-15>1` | FALSE | **FALSE** | `=1+0.0000000000000044>1` |
| **ARITH-151** | `=9+3E-14>9` | TRUE | **TRUE** | `=9+0.00000000000003>9` |
| **ARITH-152** | `=1+4.8E-15<=1` | TRUE | **TRUE** | `=1+0.0000000000000048<=1` |

- **The three new cases, the ordering operators, agree** with the engine's reading (ADR-0047,
  "Settled by the equality run"): `>` counts `1+4.4E-15` not greater than 1, `>` counts `9+3E-14`
  greater than 9, and `<=` counts `1+4.8E-15` less than or equal to 1
- **ARITH-146..149 agree again**, as the equality run found them in Excel, now that the corpus
  expects Excel's answers (the start branch changed the engine and those cases since, 00ba84b)
- Excel read the constants back written out in decimal, through COM and typed alike, as in the
  equality run

### `-Update`

"7 agreeing cases now say source "observed"". Of those, ARITH-146..149 already said `observed` at
the verified commit, so **the corpus changed in three places: `source` on ARITH-150, 151 and 152,
from `uncertain` to `observed`** (`tests/ExSheet.Engine.Tests/ExcelCases/arithmetic.json`). The
corpus holds **1170 cases, 35 of them `uncertain`** after `-Update` (38 before).

**Recorded only:** `-Update` changes `source` and nothing else, so the descriptions of ARITH-150..152
still begin "Engine's answer, Excel to be asked (the next Windows run):" beside `source: "observed"`.
They were left as `-Update` left them.

## Disagreements

**None in Part A.** Every case agreed, through COM and typed.

## The machine afterwards (Part A)

Regional format en-GB, the export byte for byte the one taken first; no Excel running; no oracle
process left; the AutoRecovered workbook byte for byte unchanged.
