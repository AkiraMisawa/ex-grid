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

## Parts B to H

*(Below, as each Part is finished.)*
