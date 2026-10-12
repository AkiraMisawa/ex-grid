# Windows, seventeenth run: the Excel half of the sign-off, before the release candidate (ADR-0202)

**Verified commit: `8436e45e`** (`claude/windows-run-17`), on branch `claude/exsheet-windows-verify-17` from it. The
procedure is `docs/specs/exsheet/verify-on-windows-17.md`. Run on 2026-10-11 from 21:40 local time (BST) by a Claude
Code session in WSL2 on the Windows desktop of the earlier runs, handed the run by the user's other session (ex-grid-78).
**Nothing was decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed, and no code or test: the
corpus changed only where `-Update` changed it.

**Two merges, at the requester's ask, with no product code in either.** While the run was under way the requester asked
for two commits of `claude/windows-run-17` to be merged into this branch:

- `e746b090` (merged as `c9d6c840`): ADR-0208 and its notes, the procedure's Part G wording (the record of the by-hand
  rows, not a dry run), ticket 16, and `aggregate.json` (AGGREGATE-025 reshaped, AGGREGATE-042 to 051 added);
- `ca486e45` (merged as `bf91585c`): the procedure revised after a completeness check (Part A's `-Update` in each pass's
  own call, more cases, b3 to b10, Part C's Heading reading, Part D under the MudBlazor Chrome and SH-42, Part E's CSV
  files, Part G without a change of display scale, Part H), `aggregate.json`, `subtotal.json` and `indirect.json`, and
  one line of `oracle.ps1`, which learnt the action name `cellFormat`.

`8436e45e..ca486e45` touches no file under `src/` or `samples/` (checked with `git diff --name-only`), so `8436e45e`
stays the verified commit of every Part that runs the build. The DemoHosts were built at `8436e45e` and not rebuilt.

## Files

| | |
|---|---|
| `report.md` | this report |
| `part-a/` | Part A: the scripts that ran each pass on Windows (`culture-pass.ps1`, `keys-pass.ps1`, `update-pass.ps1`, `com-ids.ps1`, `let-copy-keys.ps1`, `restore-culture.ps1`), the logs and id lists of every pass (`logs/`), `summarise.py` and its output `summary.md`, and the regional format's exports before and after (`international-*.reg`) |
| `corpus-merge.py` | Part A: the run's promotions carried onto the corpus the merges brought in |
| `../../tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-11-run17*.json` | Part A: every pass's results, as the oracle wrote them |
| `excel/xl-server.ps1`, `excel/xs.py` | Parts B, C, D and H in Excel: an Excel of the script's own, driven with real keys and the real mouse |
| `pg-server.mjs`, `pg.py`, `input-server.ps1` | the browser half: a headed Windows Chrome or Edge, real keys and presses through the sixteenth run's `input-server.ps1` (more named keys added) |

## Environment

- **Windows 11 Pro 25H2**, build 26200.9457 (the registry's `ProductName` still reads "Windows 10 Pro"). One display,
  3840 × 2160 device px at **150%** (DPI 144). Display language **en-GB**.
- **Regional format en-GB** at the start and at the end (below). The language list: en-GB (keyboard English (UK)),
  en-US, and ja with the Microsoft IME (`imjptip.dll` 10.0.26100.9278). The keyboard of the user's terminal in front
  was the Japanese one (0x04110411) when the run started.
- **Excel**: Microsoft 365, version 16.0.20430.20146, x64, **Current Channel** (`CDNBaseUrl` 492350f6-…), English
  (UK) display language, as the earlier runs.
- **Chrome 154.0.8037.98** and **Edge 155.0.4283.45** (the installed Windows builds).
- **NVDA 2026.2** (`C:\Program Files\NVDA`), installed by the user for this run when asked; **Narrator**
  10.0.26100.8972.
- **How Windows was reached from WSL.** This run drove Windows from WSL2 (Ubuntu 24.04), as the earlier runs did:
  - Excel through Windows PowerShell 5.1 (`powershell.exe` by interop): `oracle.ps1` and the run's own `xl-server.ps1`,
    each starting an Excel of its own with `New-Object -ComObject Excel.Application`;
  - the browsers are Windows' own Chrome and Edge, launched by Playwright 1.62.1 under Windows' portable Node v24.14.1
    (`%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64`, run by interop), from a copy of `tests/ExGrid.Browser` at the
    verified commit (`npm ci` on Windows). No Linux browser was used;
  - the DemoHosts ran in WSL (.NET SDK via nix, built at `8436e45e`): WebAssembly on `localhost:5299`, the Server host on
    `localhost:6298`, the demo API on 8299 and 8298 (20,000 trades), reached by Windows' browsers through WSL's
    localhost forwarding. Behind 150 ms, `latency-proxy.mjs` ran on Windows' Node (5298 → 6298, control on 7298).
- **Excel's AutoRecover folder** (`%APPDATA%\Microsoft\Excel`) was copied before the first Excel started
  (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run17`). No Excel was running when the run began.

## Authorisation

- The request that handed the run over relayed the user's advance authorisation: real keys and mouse to Excel and the
  browsers, the IME switched on and off by its keys, and NVDA started and stopped. **The user confirmed it in this
  session's terminal** before the first input.
- **The regional format.** The user was asked before the first change (to en-US) and agreed. Asked again before de-DE,
  the user answered "変更していいよ。いちいちきかなくていい" (go ahead; no need to ask each time), and agreed to ja-JP. The
  later changes of Part A were made under that answer, and each is recorded below.
- NVDA was not installed; asked whether to install it, the user installed it (2026.2).
- **Office's Japanese display language** (Part E, item 27): asked, the user agreed to add it, approved each UAC prompt,
  and asked for Excel's display language to be changed without them ("僕の介入なしでできるようにして"). It was changed
  through Excel's own Options by the session and put back.
- **Narrator** (Part F): asked, the user chose to leave it to the end of the run.

## Part A — the case corpus, put to Excel (ADR-0202, SH-7)

### How it ran

- **The oracle and the corpus ran from a copy on Windows** (`%LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\oracle`),
  identical by SHA-256 to the worktree's at `8436e45e` when copied. Each pass's results file and, at the end, the corpus
  as `-Update` left it were copied back into the worktree.
- **The passes**, in order (the times are when each finished; every file is in `ExcelOracle/`):
  1. en-US, COM, the whole corpus (21:47:53 to 21:53:04);
  2. en-US, keys, the 89 cases the COM pass blocked with "ask by keys";
  3. de-DE, COM then keys, its 20 cases; ja-JP, COM then keys, its 6;
  4. `-Update` through COM under ja-JP, de-DE, then en-US over the whole corpus, then `-Keys -Update` over the 89;
  5. en-GB, COM over its 1031 cases (22:05 to 22:17), keys over its 15 "ask by keys" cases;
  6. after the first merge: AGGREGATE-025 and 042 to 051, COM, with and without `-Update`, under en-US;
  7. an addition (below): the 15 en-GB "ask by keys" cases typed under en-US from a copy;
  8. `-Update` under en-GB over the 1031, and `-Keys -Update` over the 15;
  9. after the second merge, under en-US: `-Update` through COM over the revision's new and reshaped cases
     (AGGREGATE-025, 042 to 054, SUBTOTAL-035 to 038, INDIRECT-023) and LVL-027 to 029 (the `cellFormat` action);
     `-Keys -Update` over ERRORTYPE-013 and 016 to 019; and `-Keys -Update` alone for each of the five cases that raised
     a dialog in the keys pass (LET-102, LET-104, LET-105, TYPED-019, TYPED-020), whose texts the full pass could not
     read within 2 s;
  10. `-Keys -Update` under de-DE and under ja-JP.
- **`-Update` and the revision's rule.** Every `-Update` was its own call, which reran the pass and promoted only the
  cases that agreed in that call (the revised step 5). The en-US COM pass was rerun once with `-Update` (pass 4); each
  culture's COM and keys passes, and the en-US keys pass, were rerun with it.
- **The regional format**, each change made with `Set-Culture` and checked from a new PowerShell: en-US at 21:47:44;
  de-DE about 21:56:30; ja-JP about 21:57:05; de-DE about 21:57:42; en-US 21:57:56; en-GB 22:05:32; en-US about
  22:20:30; en-GB 22:21:41; en-US 22:46:48; de-DE 22:47:56; ja-JP 22:48:15; **en-GB 22:48:32**, where the machine was
  left. `HKCU\Control Panel\International` was exported at 21:47:44, before the first change, and at 22:48:33, after the
  last: **the two are identical byte for byte** (SHA-256 `071c2b47…456b1af`, the same as the fourth and fifth runs').
- **The corpus after the run.** 678 cases changed `source` to `observed`, and nothing else changed (the corpus's diff is
  678 `source` lines). The promotions made in the copy before the merges were carried onto the merged files by
  `corpus-merge.py`: a case reshaped by a revision (AGGREGATE-025, INDIRECT-023) kept the revision's `source` and was
  asked again.
- **What the keys pass under en-GB does not type.** `oracle.ps1 -Keys` types Formulas only under en-US
  (`KeysForFormulas`); under another regional format it types the constants and puts Formulas in through `Formula2`
  (each results file's `formulasEnteredThrough`). The 15 en-GB "ask by keys" cases are all Formulas (LET-023 and
  LET-KEYS-001 to 014), so their en-GB keys pass is a COM answer: LET-023 and LET-KEYS-004, which expect Excel to refuse
  the typed `=LET(A1,2,A1)`, read "Excel entered it" there. **As an addition**, the 15 were typed under en-US from a
  scratch copy of the corpus whose 15 cases name en-US (`%LOCALAPPDATA%\…\let-en-US-copy`, not committed;
  `results-2026-10-11-run17-keys-en-GB-cases-typed-under-en-US.json`): **all 15 agree**, LET-023 and LET-KEYS-004
  refused with "There's a problem with this formula", LET-KEYS-007 with "You can't define the same name twice in a LET
  function", LET-KEYS-011 with "You've entered too few arguments for this function", LET-KEYS-012 with "You can't include
  a parameter to a LET function after defining its calculation". That copy's run is not an `-Update`: the cases are
  already `observed`.
- **The dialogs.** Rerun alone, the five read: LET-102 (`=LET(TRUE,2,1)`), LET-104 (`=LET(R2,2,R2+1)`) and LET-105
  (`=LET(C3,2,C3+1)`): "There's a problem with this formula" — all three agree (refused); TYPED-019 (`+A1`) and TYPED-020
  (`-A1`) typed into A1: "There are one or more circular references where a formula refers to its own cell…" — both agree.
- Layers 1 and 2 were not run (the procedure: they need not be).

### What was read

The tables `part-a/summary.md` holds, from `part-a/summarise.py`. The case of record for an id is the last pass that asked
it in the way the procedure names for it: a pass that typed its Formulas for an "ask by keys" case (or one the
procedure adds to the keys pass), the pass under its own culture through COM otherwise.

#### Counts (step 1, and after)

| Corpus | Cases | observed | documented | uncertain | By culture |
|---|---|---|---|---|---|
| `8436e45e`, before anything ran | 2934 | 2139 | 450 | 345 | none (en-US) 1862, en-GB 1046, de-DE 20, ja-JP 6 |
| `ca486e45`, after the two merges | 2951 | 2139 | 450 | 362 | none (en-US) 1879, en-GB 1046, de-DE 20, ja-JP 6 |
| **after this run** | **2951** | **2817** | **50** | **84** | as `ca486e45` |

The per-file counts are in `part-a/summary.md`.

#### Still `uncertain` or `documented`, and why (134)

- **They disagree (79)**: every case of the table below but TABLE-011 and TABLE-013 and COPY-025, which are `observed`.
- **"recorded" (35)**: each differs from Excel by decision (`engineDiffersByDecision`) and has no `excelExpect`, so the
  oracle records Excel's answer and compares nothing: AGGREGATE-027 to 031, ERR-078 to 081, 083, 084, 093,
  ERRORTYPE-021, FILL-045, 052 to 054, INDIRECT-024 to 026, NAME-009, and the rest listed in `part-a/summary.md`.
- **A Linked Table still waiting for its data (6)**: TABLE-005 to 010. Excel has no counterpart.
- **Excel's undo does not reach COM (6)**: CW-013, CW-014, LVL-023, LVL-024, LVL-030 (Part B's b9), NAME-036.
- **A width widened by entry (5)**: CW-021, 022, 023, 025, 026 (Part B's b10).
- **A qualifier naming no sheet (3)**: NAME-006, 007, 008. Excel reads it as an external workbook and may open a file
  dialog; asked by hand in the earlier runs, not by this procedure.

#### Every disagreement

Excel's answer beside the corpus's `expect` (or its `excelExpect`). "refused" is COM's refusal to enter the Formula
(`0x800A03EC`). Through COM under en-US unless the last column says otherwise.

| Case | File | Formula or input | Excel | Corpus | Pass |
|---|---|---|---|---|---|
| AGGREGATE-025 | `aggregate.json` | D1 1, D2 2, D3 `=SUBTOTAL(9,D1:D2)`; `=AGGREGATE(9,4,D1:D3)` | **6** | `#VALUE!` (reshaped: no `excelExpect`) | |
| AGGREGATE-026 | `aggregate.json` | D3 `=1+SUBTOTAL(9,D1:D2)`; `=AGGREGATE(9,0,D1:D3)` | **3** | `#VALUE!` | |
| AGGREGATE-038 | `aggregate.json` | `=AGGREGATE(14,6,A1:A11,1.5)` | 81 | `#VALUE!` | |
| AGGREGATE-039 | `aggregate.json` | `=AGGREGATE(14,6,5,1)` | 5 | `#VALUE!` | |
| AGGREGATE-040 | `aggregate.json` | `=AGGREGATE(14,6,A1:A11,1,2)` | refused | `#VALUE!` | |
| AGGREGATE-042 | `aggregate.json` | D3 `=SUBTOTAL(9,D1:D2)`; `=AGGREGATE(9,5,D1:D3)` | **6** | `#VALUE!` | |
| AGGREGATE-043 | `aggregate.json` | as 042 with option 6 | **6** | `#VALUE!` | |
| AGGREGATE-044 | `aggregate.json` | as 042 with option 7 | **6** | `#VALUE!` | |
| AGGREGATE-045 | `aggregate.json` | D3 `=AGGREGATE(9,0,D1:D2)`; `=AGGREGATE(9,4,D1:D3)` | **6** | `#VALUE!` | |
| AGGREGATE-046 | `aggregate.json` | as 045 with option 5 | **6** | `#VALUE!` | |
| AGGREGATE-047 | `aggregate.json` | as 045 with option 6 | **6** | `#VALUE!` | |
| AGGREGATE-048 | `aggregate.json` | as 045 with option 7 | **6** | `#VALUE!` | |
| AGGREGATE-049 | `aggregate.json` | D3 `=SUBTOTAL(9,D1:D2)`; `=AGGREGATE(14,4,D1:D3,1)` | **3** | `#VALUE!` | |
| AGGREGATE-050 | `aggregate.json` | as 049 with option 6 | **3** | `#VALUE!` | |
| AGGREGATE-051 | `aggregate.json` | D3 `=AGGREGATE(9,0,D1:D2)`; `=AGGREGATE(14,4,D1:D3,1)` | **3** | `#VALUE!` | |
| AGGREGATE-052 | `aggregate.json` | A3 `=(SUBTOTAL(9,A1:A2))`; `=AGGREGATE(9,6,A1:A3)` | **6** | `#VALUE!` | |
| AGGREGATE-053 | `aggregate.json` | A3 `=IF(TRUE,SUBTOTAL(9,A1:A2))`; `=AGGREGATE(9,6,A1:A3)` | **6** | `#VALUE!` | |
| AGGREGATE-054 | `aggregate.json` | A3 `=AGGREGATE(9,0,A1:A2)+0`; `=AGGREGATE(9,6,A1:A3)` | **6** | `#VALUE!` | |
| AND-011 | `and.json` | `=AND("abc",TRUE)` | TRUE | `#VALUE!` | |
| COPY-025 | `copy.json` | (actions: a copy under de-DE) | text `=SUM(1.5,2)` | 3.5 | COM and keys, de-DE (`observed`, carried since the first run) |
| COS-004 | `cos.json` | `=COS(PI()/2)` | 6.1257422745431E-17 | 6.123233995736766E-17 | |
| COTH-002 | `coth.json` | `=COTH(2^27)` | 1 | `#NUM!` | |
| CSCH-002 | `csch.json` | `=CSCH(2^27)` | 0 | `#NUM!` | |
| EDATE-008 | `edate.json` | `=EDATE(DATE(1900,3,15),-1)` | 46 | `#VALUE!` | |
| EOMONTH-008 | `eomonth.json` | `=EOMONTH(31,1)` | 59 | `#VALUE!` | |
| ERRORTYPE-013 | `error-type.json` | A1 typed `#SPILL!`; `=ERROR.TYPE(A1)` | **9** (COM: `#N/A`) | `#VALUE!` | keys, en-US |
| ERRORTYPE-014 | `error-type.json` | A1 `=SEQUENCE(2)`, A2 `x`; `=ERROR.TYPE(A1)` | **9** | `#VALUE!` | |
| ERRORTYPE-015 | `error-type.json` | `=ERROR.TYPE(FILTER(E1:E2,E1:E2>100))` | **14** | `#VALUE!` | |
| ERRORTYPE-016 | `error-type.json` | A1 typed `#FIELD!` | **13** (COM: `#N/A`) | `#VALUE!` | keys, en-US |
| ERRORTYPE-017 | `error-type.json` | A1 typed `#BLOCKED!` | **11** (COM: `#N/A`) | `#VALUE!` | keys, en-US |
| ERRORTYPE-018 | `error-type.json` | A1 typed `#CONNECT!` | **10** (COM: `#N/A`) | `#VALUE!` | keys, en-US |
| ERRORTYPE-019 | `error-type.json` | A1 typed `#UNKNOWN!` | **12** (COM: `#N/A`) | `#VALUE!` | keys, en-US |
| ERRORTYPE-020 | `error-type.json` | A1 `=B1`, B1 `=A1`; `=ERROR.TYPE(A1)` | 0 | `#N/A` | |
| FV-001 | `fv.json` | `=FV(0.06/12,10,-200,-500,1)` | 2581.4033740601185 | 2581.4033740601362 (12 digits) | |
| FV-003 | `fv.json` | `=FV(0.11/12,35,-2000,,1)` | 82846.24637190055 | 82846.24637190059 (12 digits) | |
| INDEX-012 | `index.json` | `=INDEX(G1:H5,2)` | `#REF!` | `Banana` | |
| INDIRECT-023 | `indirect.json` | `=INDIRECT("R3C2",FALSE)` over A2:B5 | **45** | `#VALUE!` (reshaped: no `excelExpect`) | |
| ISNUMBER-008 | `isnumber.json` | A1 `=B1`, B1 `=A1`; `=ISNUMBER(A1)` | 0 | TRUE | |
| LARGE-007 | `large.json` | `=LARGE(A1:B5,1.5)` | 6 | `#VALUE!` | |
| LET-101 | `let.json` | typed `=LET(SUM,2,SUM+1)` | **entered: 3** | refused | keys, en-US |
| LET-103 | `let.json` | typed `=LET(R1C1,2,R1C1+1)` | **entered: 3** | refused | keys, en-US |
| LET-106 | `let.json` | typed `=LET(RC,2,RC+1)` | **entered: 3** | refused | keys, en-US |
| LET-107 | `let.json` | typed `=LET(x,,x)` | **entered: 0** | refused | keys, en-US |
| LET-111 | `let.json` | typed `=LET(IRR,1,IRR)` | **entered: 1** | refused | keys, en-US |
| LET-112 | `let.json` | typed `=LET(N,1,N)` | **entered: 1** | refused | keys, en-US |
| MOD-008 | `mod.json` | `=MOD(2^27,1)` | 0 | `#NUM!` | |
| NETWORKDAYS-005 | `networkdays.json` | `=NETWORKDAYS(30,100)` | 51 | `#VALUE!` | |
| NPV-001 | `npv.json` | `=NPV(10%,-10000,3000,4200,6800)` | 1188.4434123352207 | 1188.4434123352216 (12 digits) | |
| NPV-002 | `npv.json` | `=NPV(A1,A3:A7)+A2` | 1922.0615549323702 | 1922.061554932363 (12 digits) | |
| PMT-001 | `pmt.json` | `=PMT(8%/12,10,10000)` | -1037.0320893591522 | -1037.0320893591636 (12 digits) | |
| PMT-002 | `pmt.json` | `=PMT(8%/12,10,10000,0,1)` | -1030.1643271779658 | -1030.1643271779772 (12 digits) | |
| PMT-003 | `pmt.json` | `=PMT(6%/12,18*12,0,50000)` | -129.08116086799092 | -129.0811608679954 (12 digits) | |
| PMT-008 | `pmt.json` | `=PMT(0.1,10,1000,0,2)` | -147.95035898410148 | `#VALUE!` | |
| PV-001 | `pv.json` | `=PV(0.08/12,12*20,500,,0)` | -59777.14585118782 | -59777.14585118777 (12 digits) | |
| PV-003 | `pv.json` | `=PV(0.05,10,-100,1000,1)` | 196.8689140236461 | 196.86891402364662 (12 digits) | |
| RANKEQ-005 | `rank-eq.json` | `=RANK.EQ(1,1)` | refused | `#VALUE!` | |
| SECH-003 | `sech.json` | `=SECH(2^27)` | 0 | `#NUM!` | |
| SIN-004 | `sin.json` | `=SIN(PI())` | 1.22514845490862E-16 | 1.2246467991473532E-16 | |
| SORT-011 | `sort.json` | `=SORT(Q1:Q2)` | 7 | `#VALUE!` | |
| SUBSTITUTE-005 | `substitute.json` | `=SUBSTITUTE("aaaa","aa","b",2)` | `aba` | `aab` | |
| SUBTOTAL-020 | `subtotal.json` | A6 `=SUBTOTAL(9,A2:A5)*1`; `=SUBTOTAL(9,A2:A6)` | **303** | `#VALUE!` | |
| SUBTOTAL-021 | `subtotal.json` | A6 `=AGGREGATE(9,0,A2:A5)`; `=SUBTOTAL(9,A2:A6)` | **303** | `#VALUE!` | |
| SUBTOTAL-027 | `subtotal.json` | `=SUBTOTAL(9.5,A2:A5)` | 303 | `#VALUE!` | |
| SUBTOTAL-029 | `subtotal.json` | `=SUBTOTAL(9,SEQUENCE(3))` | refused | `#VALUE!` | |
| SUBTOTAL-030 | `subtotal.json` | `=SUBTOTAL(9,5)` | refused | `#VALUE!` | |
| SUBTOTAL-035 | `subtotal.json` | A3 `=(SUBTOTAL(9,A1:A2))`; `=SUBTOTAL(9,A1:A3)` | **3** | `#VALUE!` | |
| SUBTOTAL-036 | `subtotal.json` | A3 `=IF(TRUE,SUBTOTAL(9,A1:A2))`; `=SUBTOTAL(9,A1:A3)` | **3** | `#VALUE!` | |
| SUBTOTAL-037 | `subtotal.json` | A3 `=AGGREGATE(9,0,A1:A2)+0`; `=SUBTOTAL(9,A1:A3)` | **3** | `#VALUE!` | |
| SUBTOTAL-038 | `subtotal.json` | A1 `=SEQUENCE(2,1,SUBTOTAL(9,C1))`; `=SUBTOTAL(9,A2)` | **0** | `#VALUE!` | |
| TABLE-011 | `linked-tables.json` | `=SUM(Positions[Delta])` | refused | `#REF!` (`observed`, carried since the first run) | |
| TABLE-013 | `linked-tables.json` | `=Positions[PV]` | refused | `#NAME?` (`observed`, carried since the first run) | |
| TAN-004 | `tan.json` | `=TAN(PI()/2)` | 16324552277619072 | 16331239353195370 | |
| TEXTFN-009 | `text.json` | `=TEXT(1234.5,"#,##0.00")` | `1234,5000` | `1.234,50` | COM and keys, de-DE |
| TEXTFN-017 | `text.json` | `=TEXT(1/3,"General")` | `0.333333333` | `#VALUE!` | |
| TEXTFN-018 | `text.json` | `=TEXT(5,"")` | `` (empty text) | `#VALUE!` | |
| TEXTFN-019 | `text.json` | `=TEXT(4.34,"# ?/?")` | `4 1/3` | `#VALUE!` | |
| TEXTFN-020 | `text.json` | `=TEXT(5,"[<=9]0;0.0")` | `5` | `#VALUE!` | |
| UNIQUE-009 | `unique.json` | `=UNIQUE(Q1:Q2)` | 7 | `#VALUE!` | |
| XMATCH-008 | `xmatch.json` | `=XMATCH("Banana",G1:G5,0,2)` | 2 | `#VALUE!` | |

**The cases the tickets name.**

- **SUBTOTAL-020, SUBTOTAL-021, AGGREGATE-025, AGGREGATE-026** (ticket 16): Excel totals the nested SUBTOTAL or
  AGGREGATE Formula's cell, or answers as if none were there, where the corpus refuses with `#VALUE!` (above).
  **AGGREGATE-025 keeps Microsoft's 6 in Excel**: with its old shape (`excelExpect` 6) it agreed in the first COM pass,
  and in its reshaped form it disagrees with the same 6.
- **AGGREGATE-042 to 054 and SUBTOTAL-035 to 038** (the revisions): every one disagrees, each with the number in the
  table: options 4 to 7 count the nested SUBTOTAL or AGGREGATE (6, or 3 for LARGE), and SUBTOTAL over a range holding
  one counts it (3), or reads the spilled cell as 0 (SUBTOTAL-038).
- **ERRORTYPE-011 to 019** (ticket 17): ERRORTYPE-011 and 012 agree (`#N/A`). 013 to 019 disagree: Excel's
  `ERROR.TYPE` gives `#SPILL!` 9, `#CALC!` 14, `#FIELD!` 13, `#BLOCKED!` 11, `#CONNECT!` 10 and `#UNKNOWN!` 12, where
  the corpus answers `#VALUE!`. ERRORTYPE-013 and 016 to 019 typed with real keys (the revision's step 3); through COM
  each reads `#N/A`, which is not Excel's real answer (the procedure says why).
- **INDIRECT-023 to 029** (ticket 18): INDIRECT-023 (reshaped) disagrees with 45; 024 to 026 are recorded (45, 250.5,
  6); 027 to 029 agree (0, 62, 7).
- **TABLE-011, TABLE-013, COPY-025**, carried since the first run, disagree as before.

## Part B — what the corpus cannot ask, by hand in Excel

**How it ran.** `excel/xl-server.ps1`, fed one command at a time by `excel/part_b.py` (and `excel/xs.py`): for each case
an Excel of the script's own (`New-Object`; each one's process id was checked to be new), a new workbook of one sheet,
maximised at 100%, its window on the English (UK) keyboard. Every key went through SendInput as a virtual-key and a scan
code; every press with the real pointer; a Value was read through COM only after a step, with Excel Ready. Each step is a
line of `records/part-b.jsonl` (every command and answer in `records/part-b-xs.jsonl`). Pictures are PrintWindow's;
the committed ones are crops (`shots/part-b/`), cut clear of the title bar, which shows the account. 23:15 to 23:28.
Each Excel was ended by `Stop-Process` (its own process) when it had not quit within 10 s of `Quit`.

| # | What was done | What was read | Asked by |
|---|---|---|---|
| b1 | A1 `=RAND()`, Enter; ↑ →; B1 `1`, Enter; Ctrl+Z; Ctrl+Y; then, as an addition, Ctrl+Z and Ctrl+Y again | A1: **0.728584010700264** after its entry; **0.035222293413679884** after B1; after Ctrl+Z **0.63343631701647329**: neither the draw before B1 nor the one after it, **a new draw**. After Ctrl+Y **0.94031907607955856**, again new. The second Ctrl+Z 0.96869660901116994 and Ctrl+Y 0.39002255258321294: new each time. B1 empty after each Ctrl+Z, 1 after each Ctrl+Y | ADR-0180, "Undo draws again" |
| b2 | A1 `=RAND()`, Enter; a double click on the tab `Sheet1` (it went into its name's edit, the name selected); `Data`, Enter; Ctrl+Z | A1 0.21946455506413098 after its entry; **after the rename 0.50683781294117847: drawn again**. **Excel offered the undo** (the Undo button enabled after the rename); **Ctrl+Z put the name back to `Sheet1`**, and **A1 drew again: 0.3434572829235909** | ADR-0180's first note of 2026-10-11 |
| b3 | `b3.csv` written to disk (`Key,Val` / `a,1` / `b,2` / `c,3`, CRLF). A new workbook: A1 `=RAND()`, Enter. Data › From Text/CSV (KeyTips Alt, A, F, T), the file's path typed into the dialog's File name and Enter; the preview (1252: Western European (Windows), Comma, Based on first 200 rows; Key/Val a 1, b 2, c 3), **Load**: a table on a new sheet `b3`, placed left of Sheet1 and shown. Back on Sheet1 (Ctrl+PgDn): Refresh All (Ctrl+Alt+F5) twice with the file unchanged; then **the file rewritten outside Excel** (`c,3` → `c,30`), Refresh All; Refresh All once more | A1: 0.66587690107406272 after its entry; 0.39872817435436025 after the Load; 0.41572896601076748 back on Sheet1 (two Refresh Alls had run while the sheet `b3` was shown, below). **Refresh All with the file unchanged: 0.21801555519652582, then 0.082116763526578929: each refresh drew again.** **After the file changed: 0.81199667908518791**, and the table's `c` row read 30: drawn again. Refresh All once more, unchanged since: 0.23499631821526956, drawn again | ADR-0180's second note of 2026-10-11 |
| b4 | `=SUBTOTAL(` typed in A1, the list read, ↓ to its end, Escape twice; the same for `=AGGREGATE(` and `=AGGREGATE(1,` | `=SUBTOTAL(` (`function_num`): **1 - AVERAGE, 2 - COUNT, 3 - COUNTA, 4 - MAX, 5 - MIN, 6 - PRODUCT, 7 - STDEV.S, 8 - STDEV.P, 9 - SUM, 10 - VAR.S, 11 - VAR.P, 101 - AVERAGE, 102 - COUNT, 103 - COUNTA, 104 - MAX, 105 - MIN, 106 - PRODUCT, 107 - STDEV.S, 108 - STDEV.P, 109 - SUM, 110 - VAR.S, 111 - VAR.P** (22). `=AGGREGATE(` (`function_num`, the tip showing both forms, `array, [k]` and `ref1, ...`): **1 - AVERAGE, 2 - COUNT, 3 - COUNTA, 4 - MAX, 5 - MIN, 6 - PRODUCT, 7 - STDEV.S, 8 - STDEV.P, 9 - SUM, 10 - VAR.S, 11 - VAR.P, 12 - MEDIAN, 13 - MODE.SNGL, 14 - LARGE, 15 - SMALL, 16 - PERCENTILE.INC, 17 - QUARTILE.INC, 18 - PERCENTILE.EXC, 19 - QUARTILE.EXC** (19). `=AGGREGATE(1,` (`options`): **0 - Ignore nested SUBTOTAL and AGGREGATE functions, 1 - Ignore hidden rows, nested SUBTOTAL and AGGREGATE functions, 2 - Ignore error values, nested SUBTOTAL and AGGREGATE functions, 3 - Ignore hidden rows, error values, nested SUBTOTAL and AGGREGATE functions, 4 - Ignore nothing, 5 - Ignore hidden rows, 6 - Ignore error values, 7 - Ignore hidden rows and error values** (8). Beside `src/ExSheet.Engine/Formulas/Functions.cs` at the verified commit: below | ticket 16 |
| b5 | A right click on the status bar: the menu **Customise Status Bar**, Average, Count and Sum ticked, Numerical Count, Minimum and Maximum not; those three ticked by a click each, Escape. A1 `1`, A2 `2`, A3 `=1/0`, A4 `x`, A5 blank (typed); a press on A1, Shift+press on A5 | **`Count: 4    Numerical Count: 2`**, and nothing else: no Average, Min., Max. or Sum | SM-7 (ADR-0130) |
| b6 | A3 cleared (Delete); A1:A5 selected again | **`Average: 1.5    Count: 3    Numerical Count: 2    Min.: 1    Max.: 2    Sum: 3`** | SM-7's control |
| b7 | A1:A5 cleared; A1 `1`, A2 `TRUE`, A3 `2026-10-11` (read as the date 46306, shown `11/10/2026`), A4 `x`; A1:A4; A1:A2; A3 | A1:A4: **`Average: 23153.5    Count: 4    Numerical Count: 2    Min.: 1    Max.: 46306    Sum: 46307`**: the Boolean counted but not numerically, the date counted as a number, the figures in General. A1:A2: **`Average: 1    Count: 2    Numerical Count: 1    Min.: 1    Max.: 1    Sum: 1`**. A3 alone: **no figure** | ADR-0130's table |
| b8 | A1:A5 cleared; the formats set through COM on the empty cells (setup): A1 `#,##0.00`, A2 `0%`; A1 `1000`, A2 `2000` typed. **A2 became 20, shown `2000%`**: Excel's automatic percent entry on a cell formatted `0%`. A press on A1, Shift+press on A2; then a press on A2, Shift+press on A1 | From A1 (the active cell A1): **`Average: 510.00    Count: 2    Numerical Count: 2    Min.: 20.00    Max.: 1,000.00    Sum: 1,020.00`**. From A2 (active A2): **`Average: 51000%    Count: 2    Numerical Count: 2    Min.: 2000%    Max.: 100000%    Sum: 102000%`**. Every figure takes the active cell's format | SM-9 (ADR-0130) |
| b9 | `by-hand.ps1`'s gestures (`verification/2026-09-27-windows-excel-2/`), sent through SendInput instead of its SendKeys (which reached Excel garbled in the eighth run), each in a new Excel: the ribbon by its KeyTips (Alt, H, O, W for Column Width; Alt, H, D, C and Alt, H, D, R for Delete Sheet Columns and Rows; Alt, H, N then `Number` for the number-format box), Ctrl+Shift+5 for `0%`, a press on a heading for a whole column or row, Ctrl+press for a second one; then Ctrl+Z | **CW-013**: B 20, Ctrl+Z: **B's standard width again** (`UseStandardWidth` True, 8.09) — `expect` `width: null`: **agrees**. **CW-014**: B 20, column B deleted (B then 8.09), Ctrl+Z: **B 20** — `expect` 20: **agrees**. **LVL-023**: B2 `0.5` at `0%`, column B set to Number (B2 `0.50`, B5 `0.00`), Ctrl+Z: **B2 `50%` `0%`**, B5 General — **agrees**. **LVL-024**: row 2 at `0%` (B2 `50%`, E2 `0%`), row 2 deleted, Ctrl+Z: **B2 `50%` `0%`**, E2 `0%` — **agrees**. **LVL-030**: B2 `0%`; column B and row 2 selected (`B:B,2:2`) and set to Number at once; Ctrl+Z: **B2 `50%` `0%`**, B5 and E2 General — **agrees** | the corpus (ADR-0208) |
| b10 | In a new Excel each: B1 (C1 for CW-025) typed `12345678901`, which widens a standard column; then the case's own gesture; the column's width read through COM and, from a copy saved to the temporary folder and deleted at once, its `<col>` in the sheet's XML | **CW-021**: the entry widened B to **11.18** (the file: `width="11.81640625" bestFit="1" customWidth="1"`). **CW-022**: then Column Width 11: **11**, `customWidth="1"`, `bestFit` gone. **CW-023**: then a column inserted at C (Alt, H, I, C): **the new C 11.18**, `customWidth="1"` without `bestFit`; B unchanged. **CW-025**: C widened to 11.18 (`bestFit`, `customWidth`), column B deleted: **B 11.18**, `bestFit="1" customWidth="1"`. **CW-026**: B widened, Column Width 20 (`customWidth`), Ctrl+Z: **11.18 again, `bestFit="1" customWidth="1"`**. The corpus's `expect` is `width 11, custom true` in each (the engine's width for its own entry): Excel's width is the one its entry gives, and the custom flag is set in every case | as b9 |

**Notes.**

- b3's first attempt to go back to Sheet1 was Ctrl+PgUp, which did nothing: the loaded sheet `b3` stands left of Sheet1.
  Two Refresh Alls ran while `b3` was shown, and Sheet1's A1 was not read after them (records' steps 6, 7 and 7b); the
  readings above are those taken on Sheet1 after Ctrl+PgDn.
- b3's file was rewritten from WSL, not edited in Notepad: Notepad on this machine restores the user's own unsaved tabs
  when it starts. The change reached Excel from outside the workbook, which is what the step asks.
- b8: the procedure's "A2 `2000` formatted `0%`" typed into a cell already formatted `0%` becomes 20 under Excel's
  automatic percent entry. The rule asked about (the format of the figures) does not depend on it.
- b4 beside `Functions.cs` (ticket 16): **SUBTOTAL's list differs** in eight entries: ExSheet's `7 - STDEV`, `8 - STDEVP`,
  `10 - VAR`, `11 - VARP`, `107 - STDEV`, `108 - STDEVP`, `110 - VAR`, `111 - VARP` where Excel shows `7 - STDEV.S`,
  `8 - STDEV.P`, `10 - VAR.S`, `11 - VAR.P`, `107 - STDEV.S`, `108 - STDEV.P`, `110 - VAR.S`, `111 - VAR.P`; the other
  fourteen match. **AGGREGATE's `options` match**, all eight, word for word. **AGGREGATE's `function_num`**: each of
  ExSheet's fourteen entries matches Excel's text; Excel also lists 13 - MODE.SNGL and 16 to 19, which ExSheet leaves
  out by its comment's decision (the engine answers `#VALUE!` for them).
- **The status bar was put back.** The three figures were unticked again in the same menu (`shots/part-b/b5-9-menu.png`).
  Ticking them had created `HKCU\Software\Microsoft\Office\16.0\Excel\StatusBar` (absent before, by the export taken
  before b5), and unticking left it holding `NumberCount`, `MinValue` and `MaxValue` at 0, Excel's defaults. **The key
  was deleted**, so the export after b10 holds no `StatusBar` key, as before. Its other differences from the export
  before b5 are Excel's own session counters (`ExcelPreviousSessionId`, the AutoRecover sentinels, an add-in's load
  times), which change with every Excel started. `Excel15.xlb` is unchanged since 22:47:48 (the last oracle pass).

## Part C — F4's edge cases (SH-28, ADR-0051), and the Heading reading (ADR-0052)

### How it ran

- **Excel** (`excel/part_c.py` over `excel/xl-server.ps1`), 23:30 to 23:37 on 2026-10-11, under **en-US**, as the
  seventh run's Part A writes ("a blank sheet, en-US"): `Set-Culture en-US` at 23:30:20 and en-GB again at 23:37:29,
  under the user's answer of Part A; the International export after it is identical to the one before the run. One
  Excel of the script's own, a new workbook. Before each case the sheet was cleared through COM (setup) and the
  target cell selected (D10; D1 for case 11 with the Table `Positions` (A1:B3, `Id`, `PV`) made through COM; A1 for
  15 and 16; a `Sheet2` added for case 10). Then the case's keys, every F4 a real key press (SendInput).
- **How Excel's text was read before the commit**, with Excel's own clipboard, after a picture (the Formula Bar and the
  status bar's mode cropped, `shots/part-c-excel/`): F2 when the edit was in Enter or Point mode (never after the case
  pressed F2 itself), so that the next keys move the caret; Ctrl+C (what is selected); Shift+Home and Ctrl+C (the text
  from the start to the caret, or to the selection's anchor); End, Shift+Home and Ctrl+C (the whole text); Escape. When
  Excel answered COM after the case's keys, no edit was open: **the keys had committed the entry**, and the cells were
  read instead.
- **Each case ran as written, typed into the cell, and, where its keys move the caret, also with F2 pressed after the
  text (an addition).** Typed into a cell, the edit is in Enter mode, where ← or Home commits the entry and moves the
  selection; the seventh run's caret readings are about Edit mode, which F2 gives. Excel's Formula Bar was not asked
  separately.
- **ExSheet** (`part_c_sheet.py` over `pg-server.mjs`), on `/sheet` under Chrome and Edge, on WebAssembly and on the
  Server host (proxy at 0 ms), each case typed into D10 and again into the Formula Bar (a press 8 CSS px inside its
  right end, then the same keys), and, in the cell, with F2 too where Excel had it; the text and the selection read
  from the DOM (`records/part-c-sheet-<host>-<browser>.jsonl`, pictures of the Sheet for WebAssembly and Chrome in
  `shots/part-c-sheet/`). ExSheet's cases all ran in D10 (`/sheet` keeps D10 empty), so cases 15 and 16 point at D11
  and D12 where Excel's pointed at A2 and A3. Behind 150 ms, on the Server host: case 1 as a burst (`=B2` and four F4
  in one SendInput call), twice, and F4 with no edit open. Chrome WebAssembly, Chrome Server and Edge Server ran
  2026-10-11 23:44 to 00:01; Edge WebAssembly 2026-10-12 09:06 (its first try, 00:02 to 00:07, was interrupted at case
  20 and is kept as `records/part-c-sheet-wasm-msedge-interrupted.jsonl`).

**Two things the setup itself did, found while reading the records:**

- **F4 with no edit open repeats Excel's last action, and that includes the setup's COM `Cells.Clear()`.** Checked on
  its own (`repeat-check` in `records/part-c-excel.jsonl`): D10 typed `abc`, D10 selected, F4: D10 empty. So in cases 7
  and 19 typed as written, where the Enter-mode keys committed the entry and left a selection over D10, the F4 that
  followed cleared it (`shots/part-c-excel/c7-typed-f4x1-cells.png`, `c19-…`). Case 17 was therefore asked again by
  keys alone in a fresh Excel (below).
- Typed as written, case 6 committed `=$A1:B2`, which spilled into D10:E11 (zeros on Excel's blank sheet; `/sheet`'s
  A1:B2 in ExSheet).

### The 22 cases

`[n]` a caret after n characters; `[a-b]` a selection from a to b. "committed": no edit open after the keys. ExSheet's
four configurations (Chrome and Edge, WebAssembly and Server) read the same in every row.

| Case | F4 | Reading | Excel, typed | Excel, F2 first | ExSheet, cell typed | ExSheet, cell F2 first | ExSheet, Formula Bar | Configurations agreeing |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | `=$B$2` | `=$B$2 [5]` | `—` | `=$B$2 [5]` | `—` | `=$B$2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 1 | 2 | `=B$2` | `=B$2 [4]` | `—` | `=B$2 [4]` | `—` | `=B$2 [4]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 1 | 3 | `=$B2` | `=$B2 [4]` | `—` | `=$B2 [4]` | `—` | `=$B2 [4]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 1 | 4 | `=B2` | `=B2 [3]` | `—` | `=B2 [3]` | `—` | `=B2 [3]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 2 | 1 | `=$A$1+B2` | `committed` | `=$A$1+B2 [1-5]` | `committed` | `=$A$1+B2 [5]` | `=$A$1+B2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 3 | 1 | `=$A$1+B2` | `committed` | `=$A$1+B2 [1-5]` | `committed` | `=$A$1+B2 [5]` | `=$A$1+B2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 4 | 1 | `=$A$1+B2` | `committed` | `=$A$1+B2 [1-5]` | `committed` | `=$A$1+B2 [5]` | `=$A$1+B2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 1 | `=SUM($A$1:$B$2)` | `committed` | `=SUM(A1:$B$2) [8-12]` | `committed` | `=SUM($A$1:$B$2) [14]` | `=SUM($A$1:$B$2) [14]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 2 | `=SUM(A$1:B$2)` | `committed` | `=SUM(A1:B$2) [8-11]` | `committed` | `=SUM(A$1:B$2) [12]` | `=SUM(A$1:B$2) [12]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 3 | `=SUM($A1:$B2)` | `committed` | `=SUM(A1:$B2) [8-11]` | `committed` | `=SUM($A1:$B2) [12]` | `=SUM($A1:$B2) [12]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 4 | `=SUM(A1:B2)` | `committed` | `=SUM(A1:B2) [8-10]` | `committed` | `=SUM(A1:B2) [11]` | `=SUM(A1:B2) [11]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 6 | 1 | `=A1:B2` | `committed` | `=$A1:$B$2 [5-9]` | `committed` | `=A1:B2 [6]` | `=A1:B2 [6]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 7 | 1 | `=$A$1+$B$2 (the span selected)` | `committed` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 8 | 1 | `=SUM($A:$A)` | `committed` | `=SUM($A:$A) [5-10]` | `committed` | `=SUM($A:$A) [10]` | `=SUM($A:$A) [10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 8 | 2 | `=SUM(A:A)` | `committed` | `=SUM(A:A) [5-8]` | `committed` | `=SUM(A:A) [9]` | `=SUM(A:A) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 8 | 3 | `=SUM($A:$A)` | `committed` | `=SUM($A:$A) [5-10]` | `committed` | `=SUM(A:A) [9]` | `=SUM(A:A) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 9 | 1 | `=SUM($1:$1)` | `committed` | `=SUM($1:$1) [5-10]` | `committed` | `=SUM($1:$1) [10]` | `=SUM($1:$1) [10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 9 | 2 | `=SUM(1:1)` | `committed` | `=SUM(1:1) [5-8]` | `committed` | `=SUM(1:1) [9]` | `=SUM(1:1) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 9 | 3 | `=SUM($1:$1)` | `committed` | `=SUM($1:$1) [5-10]` | `committed` | `=SUM(1:1) [9]` | `=SUM(1:1) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 10 | 1 | `=Sheet2!$A$1` | `=Sheet2!$A$1 [12]` | `—` | `=Sheet2!$A$1 [12]` | `—` | `=Sheet2!$A$1 [12]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 11 | 1 | `unchanged` | `committed` | `=SUM(Positions[PV]) [18]` | `committed` | `=SUM(Positions[PV]) [18]` | `=SUM(Positions[PV]) [18]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 12 | 1 | `unchanged` | `committed` | `=SUM(A1) [2]` | `committed` | `=SUM(A1) [2]` | `=SUM(A1) [2]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 13 | 1 | `unchanged` | `=1+2 [4]` | `—` | `=1+2 [4]` | `—` | `=1+2 [4]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 14 | 1 | `unchanged` | `B2 [2]` | `—` | `B2 [2]` | `—` | `B2 [2]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 15 | 1 | `=$A$2` | `=$A$2 [1-5]` | `—` | `=$D$11 [6]` | `—` | `= [1]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 15b | 1 | `=A3 (or =$A$3: record which)` | `=A3 [1-3]` | `—` | `=D12 [4]` | `—` | `= [1]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 16 | 1 | `=$A$2+, pointing ends` | `=$A$2+ [6]` | `—` | `=$D$11+ [7]` | `—` | `=+ [2]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 18 | 1 | `=$A$1+$B$1` | `committed` | `=$A$1+$B$1 [1-10]` | `committed` | `=$A$1+$B$1 [1-10]` | `=$A$1+$B$1 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 19 | 1 | `=$A$1+$B$2` | `committed` | `=$A$1+$B$2 [1-10]` | `committed` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 20 | 1 | `=$A$1+$B$2` | `committed` | `=$A$1+$B$2 [1-10]` | `committed` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 21 | 1 | `=$b$2` | `=$B$2 [5]` | `—` | `=$b$2 [5]` | `—` | `=$b$2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 22 | 1 | `=$B$2:$A$1` | `=B2:$A$1 [8]` | `—` | `=$B$2:$A$1 [10]` | `—` | `=$B$2:$A$1 [10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |


**F4 with no edit open (case 17).** In a fresh Excel, by keys alone (D10 reached with the arrows, `=B2` typed, Enter,
↑, F4): **nothing changed**, no edit opened. (With the setup's COM write as the last action, F4 repeated it: see above.)
In ExSheet, in every configuration (D10 typed `=B2`, Enter, ↑, F4): **nothing changed**, no edit opened; the page saw
one F4 keydown, not default-prevented.

**Behind 150 ms (Server host, Chrome and Edge, twice each):** `=B2` and four F4 in one SendInput call (16 key events in
8 to 14 ms): the Cell Editor read **`=B2`** at 0.3 s, 1 s and 3 s, which is the fourth form of the cycle, as case 1's
fourth press reads unhurried. Nothing was lost, doubled or reordered, as far as the end state shows.

### The Heading reading (the seventh run's Part C, ADR-0052)

| | Excel (empty sheet, en-US) | ExSheet, `/sheet`, all four configurations |
|---|---|---|
| A click on Column Heading C | `C:C`, the active cell C1 | the Name Box `C1`, column C selected |
| Then Ctrl+Shift+→ | **`C:XFD`**, the active cell C1; **the view moved sideways** to the right end (XDR to XEF in view), not down | **C:XFD** shaded, the active cell C1 (Name Box `C1`, the bar `Price`); **the view moved sideways** to the end (XEZ to XFD in view beside the pinned A), not down |

`/sheet` is not an empty Sheet (row 1: `Item`, `Qty`, `Price`, `Amount`). Excel and ExSheet agree; the seventh run's
reading, "it moves on neither axis", is not what either does.

### The differences (ADR-0051, "F4 cycles the Reference at the caret"; ADR-0052 for the Heading)

1. **Typed into a cell, ← and Home commit the entry before F4, in Excel and in ExSheet alike** (cases 2 to 6, 8, 9, 11,
   12, 18 to 20 as written). The seventh run's readings for those cases hold only in Edit mode (F2) or the Formula Bar.
2. **Case 7 as written:** Shift+Home and Shift+→ in Enter mode: **Excel committed** (the selection then B10:D10);
   **ExSheet selected the text in the editor** (`A1+B2`, 1 to 10) and F4 rewrote it to `=$A$1+$B$2`, the span selected.
3. **After F4 in Edit mode, Excel selects the rewritten Reference** (cases 2 to 4: `$A$1` selected, 1 to 5; case 5:
   `$B$2`; cases 8 and 9: the whole column or row Reference); **ExSheet leaves a caret at its end**, as the seventh run
   read. Typed in Enter mode (case 1, 10, 13, 21, 22), Excel left a caret at the end too.
4. **Case 5, a range at the caret:** **Excel cycles only the end at the caret**: `=SUM(A1:$B$2)`, `=SUM(A1:B$2)`,
   `=SUM(A1:$B2)`, `=SUM(A1:B2)`. **ExSheet cycles both ends**, as the seventh run read: `=SUM($A$1:$B$2)` …
5. **Case 6:** **Excel `=$A1:$B$2`** (the end at the caret, `B2`, given the next form; `$A1` left); **ExSheet `=A1:B2`**,
   as read.
6. **Case 22:** **Excel `=B2:$A$1`** (only `A1`, at the caret); **ExSheet `=$B$2:$A$1`**, as read.
7. **Cases 8 and 9, the third press:** **Excel `=SUM($A:$A)` and `=SUM($1:$1)`** (a cycle of two, as read); **ExSheet
   `=SUM(A:A)` and `=SUM(1:1)`**: its third press leaves the text as the second did.
8. **Case 21:** **Excel `=$B$2`**, upper case; **ExSheet `=$b$2`**, the case kept, as read.
9. **Case 15b:** after F4 then ↓ while pointing, **Excel `=A3`** (relative again), the reading's first option; ExSheet the
   same (`=D12`).
10. **In ExSheet's Formula Bar, `=` then ↓ does not point** (cases 15, 15b, 16): the bar reads `=`, and `=+` after case
    16. Excel's Formula Bar was not asked.
11. **The Heading reading:** both move the view sideways (above), where the seventh run read no movement.

## Part D — KeyTips, Ctrl+F1 and F10 (SH-51, SH-52, ticket 160), and SH-42

### Excel (d1, d2)

`excel/xl-server.ps1`, a new Excel of its own (16.0.20430.20146, English (UK) UI), 2026-10-12 09:14; the KeyTips read
from Excel's pictures (each KeyTip is a window of its own, `MSO_KEYTIP_WINDOW_CLASS`, drawn by PrintWindow;
`shots/part-d/d1-*.png`).

**d1, Alt then H: every Home tab KeyTip, and what it stands on.**

| Group | Letters |
|---|---|
| Clipboard | **V** Paste ▾, **X** Cut, **C** Copy ▾, **FP** Format Painter, **FO** the Clipboard pane (the group's launcher) |
| Font | **FF** Font, **FS** Font Size, **FG** Increase Font Size, **FK** Decrease Font Size, **1** Bold, **2** Italic, **3** Underline ▾, **B** Borders ▾, **H** Fill Colour ▾, **FC** Font Colour ▾, **FN** the Font tab of Format Cells (launcher) |
| Alignment | **AT** Top Align, **AM** Middle Align, **AB** Bottom Align, **FQ** Orientation ▾, **W** Wrap Text, **AL** Align Left, **AC** Centre, **AR** Align Right, **5** Decrease Indent, **6** Increase Indent, **M** Merge & Centre ▾, **FA** the Alignment tab (launcher) |
| Number | **N** Number Format ▾, **AN** Accounting Number Format ▾, **P** Percent Style, **K** Comma Style, **0** Increase Decimal, **9** Decrease Decimal, **FM** the Number tab (launcher) |
| Styles | **L** Conditional Formatting ▾, **T** Format as Table ▾, **J** Cell Styles ▾ |
| Cells | **I** Insert ▾, **D** Delete ▾, **O** Format ▾ (a menu: row height, column width, …, Format Cells…) |
| Editing | **U** AutoSum ▾, **FI** Fill ▾, **E** Clear ▾, **S** Sort & Filter ▾, **FD** Find & Select ▾ |
| Add-ins | **AS** Add-ins; **Y** Create a PDF (Adobe Acrobat's add-in, installed on this machine) |

**The letters the Home tab leaves free** (neither a KeyTip nor the first letter of one): **G, Q, R, Z, 4, 7, 8**; and
**Y** where the Acrobat add-in is not installed. **Strikethrough has no Home tab letter.** Alt alone showed the tabs'
KeyTips: F File, H Home, N Insert, JI Draw, P Page Layout, M Formulas, A Data, R Review, W View, U2 Automate, Y1 Help,
Y2 Acrobat, and the Quick Access Toolbar's 1 to 4 and B. Escape twice: Ready, A1.

**d2, Ctrl+F1 twice:** the first **collapsed the ribbon to its tabs**; the second **brought it back**
(`shots/part-d/d2-*.png`).

### ExSheet (d3 to d6)

`part_d_sheet.py` over `pg-server.mjs` (real keys and presses), on `/sheet` (d3 to d6) and `/sheet?chrome=mud` (d3, d4,
d6), under Chrome and Edge, on WebAssembly and on the Server host (0 ms), 2026-10-12 09:17 to 09:32. A first try of
Chrome WebAssembly at 09:14 ran d4 on an empty D10, where a number format shows nothing; it is kept in
`records/part-d-first-try/`, and every configuration then ran with D10 holding `1234.5` for d4. After each step the
DOM was read (the KeyTips, where DOM focus is, whether the document had focus, the edit, D10's look), with the window
in front and every visible top-level window of the browser's process: **no window of the browser's own (a menu) appeared
at any step**, and the browser's window stayed in front. (The screen's pictures, CopyFromScreen, came out blank: the
display was off. The page's own pictures were taken for the additions below.) The table, from `part-d-summary.md`:
**every configuration read the same** in every step, but for one console warning (below).

| # | `/sheet` (and `/sheet?chrome=mud`, where run) | Reading |
|---|---|---|
| d3 | The press on D10: the keyboard in the Keyboard Field. **Alt pressed and released alone: the Sheet's KeyTips, `H` (Home) and `Y` (This page)**, the keyboard on the toolbar; the document kept focus; **the browser's menu did not open** and neither its menu bar nor its address bar took the keyboard. Escape: the KeyTips gone, the keyboard back in the field. **F10: the same KeyTips**, the same keyboard. Escape: back | — |
| d4 | Alt, H: **`1` Bold, `2` Italic, `3` Underline, `4` Strikethrough, `FC` Font Colour, `H` Fill Colour, `B` Borders, `AL` Align Left, `AC` Centre, `AR` Align Right, `N` Number Format, `P` Percent Style, `K` Comma Style, `O` Format Cells**. On D10 (`1234.5`), one at a time with Escape between: 1 bold, 2 italic, 3 underline, 4 a line through, each pressed on the toolbar; FC, H, B and N opened their lists (the colours, the borders, the Number Formats), the keyboard in the list; AL left, AC centre (AR: D10 was right-aligned already, the button pressed); **P `123450%`**; **K changed nothing** (ExSheet does not read Excel's Comma Style, which pads with `*`, and says so: ADR-0100, `SheetWords.CommaStyleUnread`); O opened Format Cells. Each format was undone with Ctrl+Z before the next | ExSheet uses Excel's Home tab letter where Excel has one (ADR-0100): **the same** for 1, 2, 3, FC, H, B, AL, AC, AR, N, P, K. **4**: Excel's Home tab has no Strikethrough letter, and 4 is free there. **O**: Excel's O opens the Format menu of the Cells group, ExSheet's opens Format Cells |
| d5 | Ctrl+F1: **the toolbar hidden**, the keyboard in the field; Ctrl+F1 again: **shown**. The browser did not take the key | — |
| d6 | F2 on D10: the Cell Editor open. **Alt: nothing started** (no KeyTips), the keyboard in the editor; **F10: nothing**; Escape: back in the field | SH-52: nothing starts |

**The MudBlazor Chrome** read as the built-in one in d3, d4 and d6, but for this (**an addition**, `part_d_extra.py`,
Chrome WebAssembly, `records/part-d-extra-wasm-chrome.jsonl`, `shots/part-d/extra-mud-*.png`): **the lists FC, H, B and N
open stay open after Escape, once or twice.** The keyboard goes back to the Keyboard Field, but each list stays on screen
(`.mud-popover-open`, laid out and visible in the page's own picture), and the next letter's list opens beside it: after
FC, H, B and N, all four stood over the Sheet. Under the built-in Chrome the same lists closed with Escape.

**The console:** on the Server host, under the MudBlazor Chrome (Chrome and Edge), one warning: `Blocked aria-hidden on an
element because its descendant retained focus. The focus must not be hidden from assistive technology users. Avoid using
aria-hidden on a focused element or its ancestor…` (Chromium's own). The built-in Chrome, and WebAssembly under either,
logged info messages only.

**An addition: the toolbar's Comma Style pressed with the real pointer** on D10 (`1234.5`): D10 unchanged, as K left it.

### SH-42: Ctrl+1 to Ctrl+5 on the build

`sh42-probe.mjs`, the eleventh run's `keys-probe.mjs` with `/sheet` in `keys.html`'s place (the eleventh run's
`browser-input.ps1`, SendInput), in a profile of its own with seven tabs (`/sheet` third), under Chrome and Edge, on
WebAssembly and the Server host, 2026-10-12 09:35. Before each key, `/sheet` the selected tab, D10 the Focus and the
keyboard in the Keyboard Field; after it, the tab strip read through UI Automation (`records/sh42/`).

| Key | What the page received | The Sheet | The browser |
|---|---|---|---|
| Ctrl+1 | `key=1`, on the Keyboard Field | **Format Cells opened** | no tab switched |
| Ctrl+2 | `key=2` | **D10 bold** | no tab switched |
| Ctrl+3 | `key=3` | **D10 italic** | no tab switched |
| Ctrl+4 | `key=4` | **D10 underlined** | no tab switched |
| Ctrl+5 | `key=5` | **D10 struck through** | no tab switched |

The same in all four configurations, and each format undone with Ctrl+Z, the dialog closed with Escape. (The probe's
record lists "tab opened: Number; …; Fill" after Ctrl+1: UI Automation's TabItem search reached Format Cells' own tabs
inside the page. The browser's selected tab stayed `sheet-verify-17` throughout.)

## Part E — ExPivot's readings beside Excel (ticket 07)

The record is **`expivot.md`**, one row per item; the cells side by side in `part-e/expivot-cells.md`. In short:

- **The twelve readings that gate `0.1.0`**: **observed** for items 2, 3, 5 (b), 6 (a), 6 (b), 7, 8, 15 and 17: the same
  cells in Excel and in ExPivot. **Contradicted**: item 4's Count Numbers (Excel 1, 2, 3; ExPivot 0 over a column it read
  as Text), item 5 (a) (Excel and ExPivot both an empty cell, not `#DIV/0!`), item 16 (Excel's General is eleven
  characters, ExPivot's fifteen digits; Excel truncates a CSV's numbers to fifteen digits on opening it).
- **Observed besides**: items 1 (for text), 9, 12 (with Excel's `Column Labels` corner row, which ExPivot does not draw)
  and 14. **Item 13** differs in wording: Excel's `Total Sum of Amount` grand total columns.
- **Item 27**, Excel's Japanese edition: **observed**. Excel was put in Japanese through its own Options, after the
  Japanese language accessory pack was installed with the user's approval, then put back. Every word of the reading
  that ExPivot paints is Excel's. ExPivot does not paint `列ラベル`, having no corner row.
- **Not run in this pass**: items 10, 11, 18 to 26 and 28 (layout and wording only; Excel's Field List gestures need its
  pane driven by hand).

## Part F — where the keyboard is after ↓ (the Definition of Done's §21.11)

`/sheet` on the WebAssembly host, in Chrome 154 and then Edge 155, headed on Windows (`part_f.py`). Every key is a real key
through `SendInput`, and the press on D10 is a real click. After each step the probe read `document.activeElement`, its
`aria-activedescendant`, the Focus cell, and whether the document had focus. Under NVDA it also read what the Speech
Viewer gained, from its own edit control (`WM_GETTEXT`, no key sent). Records: `records/part-f-<reader>-<browser>*.jsonl`.

- **Without a Focus.** A fresh page. Focus was given to the Revalue button by script, then Tab was pressed.
  - **The first Tab lands on the Sheet Toolbar** (`div.ex-sheet-toolbar`, `aria-activedescendant` its first item). It is
    the tab stop between the Revalue button and the grid (ADR-0100). A second Tab reaches the grid.
  - The probe's first pass pressed Tab once, so ↓ → ↓ ↓ went to the toolbar: ↓ stayed on item 1, → went to item 2. That
    pass is kept in `records/part-f-first-try/`. The pass of record presses Tab twice.
- **With a Focus.** A fresh page, a press on D10.
- **The steps.** In each state ↓, →, ↓, ↓. The console held two `info` messages and nothing else, in every pass.

**The DOM, the same without a reader and under NVDA, in Chrome and in Edge:**

| Start | Step | `activeElement` | `aria-activedescendant` | Focus |
|---|---|---|---|---|
| without a Focus | the script's focus | `button#sheet-revalue` | – | – |
| | Tab | `div.ex-sheet-toolbar` | the toolbar's item 1 | – |
| | Tab | `input.ex-key-field` | – | – |
| | ↓ | `input.ex-key-field` | A1 | A1 |
| | → | `input.ex-key-field` | B1 | B1 |
| | ↓ | `input.ex-key-field` | B2 | B2 |
| | ↓ | `input.ex-key-field` | B3 | B3 |
| with a Focus | press D10 | `input.ex-key-field` | D10 | D10 |
| | ↓ | `input.ex-key-field` | D11 | D11 |
| | → | `input.ex-key-field` | E11 | E11 |
| | ↓ | `input.ex-key-field` | E12 | E12 |
| | ↓ | `input.ex-key-field` | E13 | E13 |

- **DOM focus stays on the Keyboard Field at every step, with and without a Focus, under both readers' runs. It never
  goes to `body`.** After each arrow, the Focus moves and `aria-activedescendant` names its cell, which exists.
- Reaching the grid by Tab gives the Keyboard Field focus with no Focus cell. The first ↓ then makes **A1** the Focus. It
  does not move a Focus, since there was none.

**NVDA 2026.2** (OneCore voice, English (UK)). Started with its Speech Viewer shown at start-up: `showSpeechViewerAtStartup`
was added to `%APPDATA%\nvda\nvda.ini` for the run, and the file was put back byte for byte afterwards (SHA-256
`71a66181…`). NVDA was stopped with `nvda -q`. What it said, step by step:

| Start | Step | NVDA said (Speech Viewer) |
|---|---|---|
| with a Focus | press D10 | `table` / `row 11  D  column 4` |
| | ↓ | `row 12  D  column 4` |
| | → | `row 12  E  column 5` |
| | ↓ | `row 13  E  column 5` |
| | ↓ | `row 14  E  column 5` |
| without a Focus | the script's focus | `Revalue the positions by +1%  button` |
| | Tab | `Formatting  tool bar` / `Home  grouping` / `Bold  toggle button  not pressed  Bold (Ctrl+B)` |
| | Tab | `table` / `edit  blank` |
| | ↓ | `Item  row 2  A  column 1` |
| | → | `Qty  row 2  B  column 2` |
| | ↓ | `12  row 3  B  column 2` |
| | ↓ | `7  row 4  B  column 2` |

- The same words in Chrome and in Edge.
- **The row NVDA names is the sheet's row plus one**: D10 is `row 11`, A1 is `row 2`. The column is the letter and its
  index. A cell with a value is read value first (`Item`, `Qty`, `12`, `7`); D10 to E13 are empty and are read without one.
- **The first pass said nothing at all through its without-a-Focus steps**, in both browsers
  (`records/part-f-nvda-<browser>.jsonl`).
  - In that pass, those steps came first after the browser started. NVDA had read the page as it loaded
    (`ExGrid demo host`, `Loading…`; in Edge also `Loading page`, `Loading complete`), and then nothing from the script's
    focus to the last ↓. The DOM moved as in the table above. The with-a-Focus steps that followed were read as in the
    table.
  - **A second pass in the other order** (`records/part-f-nvda-<browser>-reversed.jsonl`): the press on D10 came first,
    and NVDA spoke from that step (`ExGrid demo host` / `Loading…` / `table` / `row 11 …`). The without-a-Focus steps
    that followed were read as in the table.
  - Which is the cause was not asked further: the order, or a press as the first thing after the browser starts. The
    Speech Viewer's whole text of both passes is in `records/part-f-nvda-*speech-viewer.txt`, with the terminal's window
    title left out.

**Narrator: not yet run.** Asked first, as the procedure says, the user chose to leave it to the end of the run.

## Parts G and H

*(Below, as each Part is finished.)*
