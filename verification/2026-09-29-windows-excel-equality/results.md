# Verification — 2026-09-29, Windows, Excel as the oracle: where `=` stops counting two numbers equal

**Scope: [`verify-equality-on-windows.md`](../../docs/specs/exsheet/verify-equality-on-windows.md)**,
both parts: ARITH-136..149 asked of a real Excel, and one paste by hand.

**Verified commit: `c3029024da8c8ea58bfcd2c8129e359992bd3651`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began. The results are committed on
`claude/exsheet-windows-verify-equality`, branched from that commit. Nothing here changes the
engine, the corpus, an ADR, `CONTEXT.md` or the Definition of Done. Every disagreement is listed for
the user to decide.

## Environment

- The machine of the first four runs: Windows 11 Pro (build 26200), one display, 3840×2160 at 150%
  (144 dpi), **regional format en-GB**, the machine's own. Excel's decimal separator `.`, list
  separator `,`, system separators on. **`Set-Culture` was not used**
- **Excel: Microsoft 365, 16.0.20326.20158**, the build of the first four runs. Precision as
  displayed off, calculation automatic
- Windows PowerShell 5.1 drives Excel. Layers 1–2 were not run, as the procedure allows
- `oracle.ps1` is the verified commit's, unchanged since the fourth run (393eb61)
- The AutoRecovered workbook in `%APPDATA%\Microsoft\Excel` is byte for byte the fourth run's
  backup, before and after
- Times: the oracle's passes 14:06–14:08, the typed pass 14:08–14:09, the paste 14:09:52–14:10:01

## The run as the procedure writes it: every case blocked

`oracle.ps1 -Id ARITH-136,…,ARITH-149`, under en-GB, through COM, then with `-Keys`, then `-Update`:

| File | Entered through | Cases | agree | disagree | blocked |
|---|---|---|---|---|---|
| `results-2026-09-29-equality.json` | COM | 14 | 0 | 0 | **14** |
| `results-2026-09-29-equality-keys.json` | keys | 14 | 0 | 0 | **14** |
| `-Update` (`%LOCALAPPDATA%\exgrid-layer3\update-equality-en-GB.json`, not committed) | COM | 14 | 0 | 0 | **14** |

(The committed files are in `tests/ExSheet.Engine.Tests/ExcelOracle/`.)

**Every case was blocked with "needs en-US; this machine's regional format is en-GB"**, and
**`-Update` changed nothing**: "0 agreeing cases now say source "observed"". The corpus is as the
verified commit has it. The oracle at the verified commit does this by design, in two places:

- A case with no `culture` is an en-US case, and a case whose culture is not the machine's is
  blocked (`oracle.ps1`, lines 815 and 833; its help: "change the Windows regional format … and run
  again"). None of ARITH-136..149 names a culture
- With `-Keys`, a Formula is typed only when the machine's format is en-US (line 804,
  `$KeysForFormulas`); otherwise it goes in through `Range.Formula2`

The procedure's setup says en-GB "is enough, since no case names a format". That is not what the
oracle at the verified commit does with a case that names no culture.

## How Excel was asked all the same

The tool and the corpus were left unchanged, and the regional format too:

1. **The unchanged oracle over a copy of the corpus.** [`en-GB-copy.py`](en-GB-copy.py) writes a
   copy of `arithmetic.json` and `fixtures.json` to `%LOCALAPPDATA%\exgrid-layer3\equality-corpus-en-GB\`
   in which ARITH-136..149 carry `"culture": "en-GB"`; nothing else differs. `oracle.ps1 -Cases
   <that copy> -Id …` ran through COM and with `-Keys`. The `-Keys` file's header says
   `formulasEnteredThrough: Range.Formula2`, as line 804 says it would, so both files entered the
   Formulas through COM. `-Update` was not run on the copy, since it would rewrite only the copy
2. **Typed with real keys.** [`equality.ps1`](equality.ps1) `-Case typed`, in a visible, maximised
   Excel: each Formula typed into column B one Unicode character at a time through `SendInput` (as
   `oracle.ps1 -Keys` types) and committed with Enter; Excel's answer read through COM once Ready.
   Beside it, through COM only, the two sides of the `=` (C and D), read back with round-trip
   precision. Every row is in [`equality.jsonl`](equality.jsonl), and the sheet as it stood is
   [`shots/typed-ARITH-136-149.png`](shots/typed-ARITH-136-149.png)

| File | Entered through | Cases | agree | disagree | blocked |
|---|---|---|---|---|---|
| `results-2026-09-29-equality-en-GB-copy.json` | COM | 14 | 8 | 6 | 0 |
| `results-2026-09-29-equality-en-GB-copy-keys.json` | COM (`-Keys`, Formulas through Formula2 under en-GB) | 14 | 8 | 6 | 0 |
| `equality.jsonl` | **keys** | 14 | 8 | 6 | — |

**The three give the same answer in every case.**

## The answers

### The four cases that separate the two readings

The readings' columns are the procedure's table. The last column is each side's double as Excel
holds it (`Value2`, round trip), and each side rounded to 15 significant digits:

| Case | Formula | Corpus (engine) | Relative threshold | 15 significant digits | **Excel** | The two sides |
|---|---|---|---|---|---|---|
| **ARITH-146** | `=9+3E-14=9` | TRUE | TRUE | FALSE | **FALSE** | 9.00000000000003 → 9.00000000000003; 9 → 9.00000000000000 |
| **ARITH-147** | `=1+4E-15=1+6E-15` | TRUE | TRUE | FALSE | **FALSE** | 1.000000000000004 → 1.00000000000000; 1.000000000000006 → 1.00000000000001 |
| ARITH-148 | `=1000+3.6E-12=1000` | TRUE | TRUE | TRUE | **TRUE** (agrees) | 1000.0000000000036 → 1000.00000000000; 1000 → 1000.00000000000 |
| **ARITH-149** | `=9+5E-15=9` | TRUE | TRUE | FALSE | **FALSE** | 9.0000000000000053 → 9.00000000000001; 9 → 9.00000000000000 |

**Excel's answers are the "15 significant digits" column in all four. The relative threshold's
column matches only ARITH-148, the control, where the two readings agree.** Through COM and typed
alike.

### The fourth run's boundary cases, asked again

| Case | Formula | Corpus (engine) | **Excel** | The left side | …at 15 significant digits |
|---|---|---|---|---|---|
| ARITH-136 | `=1+4.2E-15=1` | TRUE | **TRUE** (agrees) | 1.0000000000000042 | 1.00000000000000 |
| **ARITH-137** | `=1+4.4E-15=1` | FALSE | **TRUE** | 1.0000000000000044 | 1.00000000000000 |
| **ARITH-138** | `=1+4.6E-15=1` | FALSE | **TRUE** | 1.0000000000000047 | 1.00000000000000 |
| **ARITH-139** | `=1+4.8E-15=1` | FALSE | **TRUE** | 1.0000000000000049 | 1.00000000000000 |

As in the fourth run. ARITH-107 (`=1+5E-15=1`, FALSE in Excel, observed in the third and fourth runs)
was not asked here; its left side is 1.000000000000005, which is 1.00000000000001 at 15 significant
digits. Rounding both sides to 15 significant digits (half up, on the double's exact decimal value)
gives Excel's answer for each of these nine cases (the arithmetic is Python's `decimal`, not Excel's).

### The rest: all agree, as in the fourth run

ARITH-140 and 141 are 0; ARITH-142 is 1.7763568394002505E-15; ARITH-143 is 1.7320508075688772;
ARITH-144 `="it's">"its"` is TRUE; ARITH-145 `="ß"<"ss"` is FALSE. Through COM and typed.

### Asked by hand: not asked

The procedure asks `=0.9+4E-16=0.9`, `=99+4E-13=99` and `=1+4.9E-15=1` only "if ARITH-146, 147 and
149 are split between the two readings, or fit neither". **All three fit one reading, 15 significant
digits, so the three were not asked.** `equality.ps1 -Case byhand` holds them and was not run.

### Seen on the way, recorded only

- **Excel keeps the constants written out in decimal.** Typed `=9+3E-14=9` reads back through
  `Formula2` as `=9+0.00000000000003=9`, and `=1+4.4E-15=1` as `=1+0.0000000000000044=1`; the
  oracle's COM files read the same Formulas back the same way
- **At a width of 26 characters, in General, Excel shows the sides as `1`, `9` and `1000`**: the
  Text of 1.0000000000000042, 9.00000000000003, 1.000000000000006 and 1000.0000000000036 is `1`,
  `9`, `1` and `1000` (the screenshot's columns C and D)
- **A comment in the engine pairs a number with the wrong Formula.** `src/ExSheet.Engine/Formulas/Arithmetic.cs`,
  line 19, at the verified commit: "not equal at 4.885E-15 (`=1+5E-15=1`)". The two sides of
  `=1+5E-15=1` are 5.107E-15 apart relative to each; 4.885E-15 is `=1+4.8E-15=1` (ARITH-139), which
  Excel counts equal (the fourth run and this one)

## The paste from C3 (ADR-0014's amendment of 2026-09-29)

[`equality.ps1`](equality.ps1) `-Case paste`, the fourth run's Part B item 3 with the range made from
C3. A file holding `=A1` (three characters, no line break) was opened in Notepad; Ctrl+A and Ctrl+C
there put it on the clipboard as **Text, UnicodeText** (and Windows' `EnterpriseDataProtectionId`),
nothing of Excel's. Then, in a fresh sheet, a click on C3, a Shift+click on B2, and Ctrl+V:

| | Selection | Active | B2 | C2 | B3 | C3 |
|---|---|---|---|---|---|---|
| before | B2:C3 | **C3** | | | | |
| **after Ctrl+V** | **B2** | **B2** | **`=A1`** (a Formula, shows 0) | empty | empty | empty |

**Excel put `=A1` into B2, the top-left, and not into C3, the active cell; the Selection became B2,
and B2 is active.** The paste options button showed beside B2
([`shots/paste-Ctrl-V-from-C3.png`](shots/paste-Ctrl-V-from-C3.png); before:
[`shots/paste-B2-C3-from-C3-selected.png`](shots/paste-B2-C3-from-C3-selected.png)). The amendment
took the top-left, and left this point "unobserved until a Windows run pastes with the active cell
away from the top-left".

**About Notepad.** Notepad was not running before this step. The tab this script opened was closed
with Ctrl+W, as the procedure says, and **one Notepad window was left open**; its title was not read
into the record. In the fourth run, Notepad restored the user's last session when it started.

## Disagreements — ADR-0047, ticket 04

Excel's answer beside the corpus's expectation, through COM and typed alike. **None was fixed here**,
in the engine or in the case; ARITH-137..139 and ARITH-146..149 stay `uncertain`.

| Case | Formula | Corpus (engine) | Excel |
|---|---|---|---|
| ARITH-137 | `=1+4.4E-15=1` | FALSE | **TRUE** (as in the fourth run) |
| ARITH-138 | `=1+4.6E-15=1` | FALSE | **TRUE** (as in the fourth run) |
| ARITH-139 | `=1+4.8E-15=1` | FALSE | **TRUE** (as in the fourth run) |
| ARITH-146 | `=9+3E-14=9` | TRUE | **FALSE** |
| ARITH-147 | `=1+4E-15=1+6E-15` | TRUE | **FALSE** |
| ARITH-149 | `=9+5E-15=9` | TRUE | **FALSE** |

## What went wrong on the way

- **The typed pass stopped once, at 14:08:45, on a fault in the script**: a local `$row` in
  `Ask-Typed` was PowerShell's same variable as its `[int]` parameter `$Row`. It had typed
  ARITH-136 into B2 and set its two sides; nothing was recorded but the failure. The variable was
  renamed and the whole pass run again at 14:08:58 in a fresh sheet. Both are in `equality.jsonl`
- The procedure's three oracle passes blocked every case (above)

## The machine afterwards

No Excel running (the scratch workbook closed unsaved, that Excel quit); regional format en-GB,
never changed; the AutoRecovered workbook byte for byte unchanged; one Notepad window left open
(above).
