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
| `part_f.py`, `loopback.ps1`, `part_f_heard.py` | Part F: the probe; under Narrator, the playback device's recording and its transcription |

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
- **Narrator** (Part F): asked, the user chose to leave it to the end of the run; at the end, the user asked the session
  to start it, then said they were away from the desk, and chose to run it when they are back. Back on 2026-10-12, the
  user asked whether Narrator could be picked up with a microphone, and the passes of record were recorded from the
  playback device instead (Part F).

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

**Narrator 10.0.26100.8972**, run on 2026-10-12 from 20:21 to 20:53 BST, when the user was back at the desk.

- **The first try** (12:30): the user said they were not at the Windows desk at the first step. Narrator was started,
  its "Narrator updates" window closed with Escape, and its home window minimised; the home window had kept the browser
  from the front. The steps stopped there, and Narrator was stopped (Insert+Escape). The user chose to run it when back.
  Records: `records/part-f-first-try/part-f-narrator-chrome-user-away*` and `-home-window-in-front*`.
- **Two tries with the user answering each step, as the procedure says, did not hold** (`records/part-f-first-try/`):
  - `-repeat-asked`: at the first step the user asked to hear it again, and the pass was started over.
  - `-front-refused` and `-alt-tab`: the user answered in the session's terminal on the same desktop, which took the
    foreground, so the next key would have reached the terminal. While Narrator ran, Windows refused to bring the
    browser back by script: `SetForegroundWindow` after an Alt tap (`front`), and the same after Playwright's
    `bringToFront` (`raise`, added to `pg-server.mjs` for this). `part_f.py` then waited for the user to bring it back
    with Alt+Tab, and pressed the key 3 s later. A window switch so came before every key; before the second Tab, DOM
    focus had moved from the Sheet Toolbar to its item 2, and the user heard "Italic". The try was stopped after ↓ (DOM
    focus on `body`, no keydown), when the user asked what the test was for.
- **The passes of record were heard from the sound Narrator made**, not by ear. The user asked whether Narrator could be
  picked up with a microphone. It was recorded from the playback device itself instead, which takes no sound from the
  room:
  - `loopback.ps1` records what the default playback device plays (WASAPI loopback; Speakers (Realtek USB2.0 Audio),
    48 kHz, 32-bit float, stereo). A loopback stream delivers nothing while nothing plays, so each packet is placed at its
    own time and the gaps are written as silence.
  - `part_f.py <browser> narrator paced` runs the same steps with no one touching the desktop. It waits 6 s after each
    step and records the step's time (`at`). The browser was in front at every step (`front` in each record).
  - `part_f_heard.py` cuts the recording at the steps' times, each piece 6.2 s, and transcribes each with whisper.cpp
    1.9.2 (`ggml-small.en`), on this machine. Before the passes, WSL's and Windows' clocks differed by about +0.06 s
    (a file's write time on NTFS against WSL's clock).
  - The transcripts are `records/part-f-narrator-<browser>[-scan-off]-heard.json`. The recordings stay on Windows
    (`%LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\audio\`), because they also hold Narrator reading the session's
    terminal title. A test recording showed that title being read about every 2 s while the terminal was in front.
  - **Whisper's mishearings**, left as they are in the transcripts and corrected in the tables below: "16,300 and" and
    "16,380" for 16,384; "copy", "kapi" and "capi" for "cap E"; "J" for column 7, which is G. "O" and "T" after → are
    short sounds.
- **Two passes in each browser:**
  - **As Narrator starts.** Narrator is in scan mode, and says "Scan" as the page loads.
  - **Scan mode off** (`paced scanoff`). After each fresh page, scan mode was turned off with Narrator's own key,
    Insert+Space, before the steps, and Narrator said "Scan off". As the second page loaded, Narrator said "Scan" again,
    and it was turned off again.

**The DOM as Narrator starts, in scan mode** (`records/part-f-narrator-<browser>.jsonl`; "keydown" is whether the page
saw the step's key):

| Start | Step | Chrome | keydown | Edge | keydown |
|---|---|---|---|---|---|
| without a Focus | the script's focus | `button#sheet-revalue` | – | `button#sheet-revalue` | – |
| | Tab | `button#sheet-undo` | no | `div.ex-sheet-toolbar` | Tab |
| | Tab | `button#sheet-redo` | Tab | `input.ex-key-field` | Tab |
| | ↓ | `button#sheet-format-cells` | no | `body` | no |
| | → | `button#sheet-format-cells` | no | `body` | no |
| | ↓ | `button#sheet-insert-row` | no | `body` | no |
| | ↓ | `button#sheet-revalue` | no | `body` | no |
| with a Focus | press D10 | `input.ex-key-field`, Focus D10 | – | the same | – |
| | ↓ | `body`, Focus D10 | no | the same | no |
| | → | `body`, Focus D10 | no | the same | no |
| | ↓ | `body`, Focus D10 | no | the same | no |
| | ↓ | `body`, no Focus cell named | no | the same | no |

- **In scan mode no arrow reached the page, in either browser, with or without a Focus.** DOM focus left the Keyboard
  Field for `body` at the first ↓. In Chrome without a Focus, the grid was never reached: DOM focus moved from button
  to button. The first Tab there was not seen by the page either.
- The Focus cell did not move. After the last ↓, neither the Keyboard Field nor the root named a Focus cell.

**What Narrator said, in scan mode:**

| Start | Step | Chrome | Edge |
|---|---|---|---|
| without a Focus | the page loads | `Scan, loading.` … the window's title … | the window's title, `… Microsoft Edge window …` |
| | the script's focus | `… has finished loading, link, all link, pages, sheet, heading level 1, sheet, ExSheet drawn by …` | `Scan, revalue the positions by +1%, button.` |
| | Tab | `Undo button.` | `Home group, bold, toggle button, off, bold, control and B.` |
| | Tab | `Redo button` | `Enter table, 1,048,576 by 16,384` |
| | ↓ | `Format cells, button` | `Scan off. Item. Scan.` |
| | → | a short sound | a short sound |
| | ↓ | `Insert a row above row 2, button.` | `Quantity, column header cap B, column 2 of 16384.` |
| | ↓ | `Revalue the positions by +1%, button.` | `Price, column header cap C, column 3 of 16384.` |
| with a Focus | the page loads | `Loading. ExGrid demo host has finished loading. Loading. Unavailable.` | `Exit table, ExGrid demo host, loading complete, …, unavailable` |
| | press D10 | `Enter table 1,048,576 by 16,384` | the same |
| | ↓ | `column header cap E, column 5 of 16384.` | the same |
| | → | `New line.` | the same |
| | ↓ | `column header cap F, column 6 of 16384` | the same |
| | ↓ | `column header cap G, column 7 of 16384` | the same |

- In Chrome, Narrator was still reading the page from the top when the script gave the Revalue button focus.
- After the press on D10, ↓ moved Narrator's own reading cursor along the column headers (E, F, G), not the grid's
  Focus. This is what the sixteenth run heard: "Scan", with DOM focus on `body` (ADR-0080).

**With scan mode off** (`records/part-f-narrator-<browser>-scan-off.jsonl`), **the DOM is the table of the passes
without a reader and under NVDA, in both browsers**:
- every arrow's keydown reached the Keyboard Field, which kept DOM focus;
- the Focus moved A1, B1, B2, B3 after the two Tabs, and D10, D11, E11, E12, E13 from the press.

What Narrator said, the same in Chrome and in Edge:

| Start | Step | Narrator said |
|---|---|---|
| without a Focus | the script's focus | `Revalue the positions by +1%, button.` |
| | Tab | `Home group, bold, toggle button, off, bold, control and B.` |
| | Tab | `Enter table, 1,048,576 by 16,384` |
| | ↓ | `Item, column header cap A, row 2 of 1,048,576, column 1` |
| | → | `Quantity, column header cap B, column 2 of 16384.` |
| | ↓ | `12, row 3 of 1,048,576.` |
| | ↓ | `7, row 4 of 1,048,576` |
| with a Focus | press D10 | `Enter table 1,048,576 by 16,384` |
| | ↓ | `Row 12 of 1,048,576.` |
| | → | `column header cap E, column 5 of 16384.` |
| | ↓ | `Row 13 of 1,048,576.` |
| | ↓ | `Row 14 of 1,048,576` |

- **Narrator names the row as the sheet's row plus one, as NVDA does**: D11 is `Row 12`, A1 is `row 2`.
- It reads what changed: the row after ↓, the column and its header after →. A cell's value comes first. B1 holds `Qty`,
  and the transcript has `Quantity`.
- With scan mode off, the first Tab landed on the Sheet Toolbar in both browsers, as without a reader.

## Part G — ExGrid's by-hand rows, the record (ADR-0208)

This Part is the record of these rows at the verified commit **`8436e45e`**. Each verdict is read against the row as
the Definition of Done words it at that commit. The display stayed at Windows' **150%** throughout (3840 × 2160, 144
DPI). The display scale was not changed: that needs a sign-out, which ends WSL and this session. The display
language is **English (United Kingdom)**, and the regional format en-GB.

### VZ-14 — the Focus inside the readable area at a fractional display scale

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| VZ-14 | `8436e45e` | Chrome 154.0.8037.98 | WebAssembly | 150% (DPR 1.5) | **pass** |
| VZ-14 | `8436e45e` | Edge 155.0.4283.45 | WebAssembly | 150% (DPR 1.5) | **pass** |
| VZ-14 | `8436e45e` | Chrome 154.0.8037.98 | Server | 150% (DPR 1.5) | **pass** |
| VZ-14 | `8436e45e` | Edge 155.0.4283.45 | Server | 150% (DPR 1.5) | **pass** |

- **What ran.** `scrollbar.spec.mjs`'s block "on a Windows desktop at fractional display scaling (VZ-14)", headed, from
  a copy of `tests/ExGrid.Browser` on Windows (`part-g/vz14.cmd`; `part-g/vz14/`), 11:03 local time.
- **The row's pass column**, "passes with the OS doing the scaling": the test's own guards held.
  - The browser reported `Windows`.
  - The bars took layout: the gutter was 12 × 12 CSS px, the grid's own bar.
  - `devicePixelRatio` was 1.5, not a whole number.
  - The Focus lay inside the client box at End and at Home.
- **Against the fifteenth run** (2026-10-01, 150%, Chrome and Edge, WebAssembly): the same. This run adds the Server
  host.

### VZ-16 — the Device Pixel told as the zoom changes, and the lines at a real 2.25

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| VZ-16, a real browser zoom told as it changes | `8436e45e` | Chrome 154 | WebAssembly | display 150%, zoom 90% to 200% | **pass** |
| VZ-16, a real browser zoom told as it changes | `8436e45e` | Edge 155 | WebAssembly | display 150%, zoom 90% to 200% | **pass** |
| VZ-16, a thin line and a gridline at a real 225% | `8436e45e` | Chrome 154 | WebAssembly | display 150%, zoom 150% (DPR 2.25) | **pass** |
| VZ-16, a thin line and a gridline at a real 225% | `8436e45e` | Edge 155 | WebAssembly | display 150%, zoom 150% (DPR 2.25) | **pass** |

**The zoom.** `/wide`, a display-only page, never `/sheet`. The browser's zoom was changed by real keys outside any
Sheet: Ctrl+= five times, Ctrl+0, Ctrl+- four times, Ctrl+0 (`part_g_vz16.py`; `records/part-g-vz16-<browser>.jsonl`).
After each change the grid's root carried `--ex-dp` equal to 1 / `devicePixelRatio`. Chrome and Edge read the same:

| Zoom | `devicePixelRatio` | `--ex-dp` |
|---|---|---|
| 100% | 1.5 | 0.6666666666666666 |
| 110% | 1.6500000953674316 | 0.6060605710312484 |
| 125% | 1.875 | 0.5333333333333333 |
| 150% | 2.25 | 0.4444444444444444 |
| 175% | 2.625 | 0.38095238095238093 |
| 200% | 3 | 0.3333333333333333 |
| 90% | 1.3499999046325684 | 0.7407407930685533 |
| 80% | 1.2000000476837158 | 0.8333333002196431 |
| 75% | 1.125 | 0.8888888888888888 |
| 67% | 1 | 1 |

**The real 2.25.** The display's 150% and the browser's 150%, reached by Ctrl+= three times on `/wide`. Then
`/sheet?case=11` on the same origin, which keeps the zoom: `devicePixelRatio` 2.25, `--ex-dp` 0.4444444444444444.

- **How it was read.** The page was scrolled down 400 CSS px, a multiple of 4 CSS px (9 Device Pixels), so that E8 lay
  inside the viewport. The compositor's picture (CDP `Page.captureScreenshot`, `fromSurface`) was taken of a 16 × 4 CSS px
  clip across each edge, whose origin lay on a multiple of 4 CSS px. That is how `device-pixel-225.spec.mjs` reads it.
- **The clip's units.** The clip is given in the document's units before the browser's zoom: a CSS px is 1.5 of them
  here, and the page's scroll is part of the clip. Each picture was 36 × 9 Device Pixels.
- **Three probe errors first.** The first passes gave the clip in CSS px, then read E8 below the fold, then left the
  scroll out. Each read the wrong place, and is kept in `records/part-g-first-try/`. The figures below are the fourth
  pass's, the same in Chrome and Edge:

| Line | Device pixels across it, not the white Paper | Reading |
|---|---|---|
| B2's thin Border, right edge (B2 \| C2) | `#E1E1E1`, `#000000` | the Border **one Device Pixel**, black, unblended. Beside it a pixel of the gridline's colour |
| B2's thin Border, bottom edge (B2 / B3) | `#E0E0E0`, `#000000` | the same |
| E8's gridline, right edge | `#E0E0E0`, `#E0E0E0` | the gridline **two Device Pixels**, both its colour, no third blended pixel |
| E8's gridline, bottom edge | `#E0E0E0`, `#E0E0E0` | the same |

- The console held `info` messages only.
- **Against the eleventh run's Part C** (2026-10-02), which first read 2.25 by the browser's zoom at this display: this
  run reads the row's own figures, one Device Pixel and two.
- **Not read here**, from the row's pass column: "nothing per render reaches JavaScript". It is P1–P9's to run.

### FN-12e — each class's widest glyph on Windows, and the two paintings

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| FN-12e, the measurements on Windows | `8436e45e` | Chrome 154 and Edge 155 | (the measuring page) | 150% | **not met as written**: seven glyphs measure wider than they are charged (below) |
| FN-12e, the bold amount and `####` (the pass column) | `8436e45e` | Chrome 154 | WebAssembly and Server | 150% | **pass** |
| FN-12e, the bold amount and `####` (the pass column) | `8436e45e` | Edge 155 | WebAssembly and Server | 150% | **pass** |

**The measurements.** `tests/GlyphWidths/measure.mjs` ran in Chrome and Edge on this desktop, over `system-ui` (Segoe
UI under this English display language) and the grid's face, Roboto. The records are
`part-g/glyphs/<face>.windows.<browser>.json`.

- **`measure.mjs` fails on Windows as written**: `new URL(…).pathname` gives `/C:/…`, which Node reads as `C:\C:\…`.
  The Windows copy was patched to use `fileURLToPath`, and the repository was left unchanged.
- **Per class** (`part-g/glyph-classes.txt`, `glyph-classes.py`): at 14 px and 12 px, regular and bold, each class's
  widest glyph is within its `CellTextMetrics` default in both faces and both browsers.
  - Segoe UI at 14 px bold, as an example: wide 12.141 (`%`) against 14.36, digit 9.078 (`₫`) against 9.75, narrow
    6.219 (`/`) against 6.41.
  - Segoe UI painted 483 of the corpus's glyphs, Tahoma 28, and Microsoft YaHei (Chrome) or Noto Sans JP (Edge) 11.
- **Per glyph, as layer 1 reads it.** `GlyphWidthCorpusTests` ran in a scratch copy with the Windows records added
  (`part-g/glyph-corpus-tests.txt`): 1313 passed and 32 failed.
  - Every failure is in `Every_glyph_is_charged_at_least_its_width`, under `system-ui` (Segoe UI and Segoe UI Semibold),
    in Chrome and Edge alike. Seven glyphs are charged under their width:
    - `ƒ` (U+0192) at every density and weight: 7.547 against 6.1 at 14 px, weight 400;
    - `ť` (U+0165) at weight 700;
    - the Hebrew `ג` `ט` `י` `ס` `פ` at some weights, by 0.03 to 0.76 px. `ס` at 12 px bold is 8.641 against 8.64.
  - `No_text_a_built_in_format_paints_is_wider_than_its_estimate` passes for every Windows record: no string the
    built-in formats paint is wider than its estimate.
  - Layer 1 reads `system-ui` and DejaVu Sans. The Windows Roboto records are not among its faces, and were compared by
    class only.
  - The row's first clause, "each `CellTextMetrics` default is at least the widest glyph measured for its class on
    Windows", is therefore met by class and not met by these seven glyphs.
- **ADR-0016's table** is not changed by this run. The Windows measurements are recorded here, beside it.

**The two paintings** (the row's pass column). Run on Windows, headed (`part-g/fn12e.cmd`; `part-g/fn12e/`):

- `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)", on
  `/sizing`;
- `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and no
  more fit (ADR-0016)", on `/sheet?case=3c`.

Both passed on Chrome and Edge, on WebAssembly and on Server: 8 of 8. A bold amount estimated to fit painted without an
ellipsis, and `####` filled its cell with no room for one more `#`.

- **Against the earlier runs**: ADR-0016 records macOS and Linux; Windows was missing (`tests/GlyphWidths/README.md`).
  This is Windows' first record.

### ED-30 — a real IME on a selected cell

The sixteenth run's Part A, cases k1 to k16 with its additions, on `8436e45e`, with the same keys and readings, and
one case added for DC-47 (`part-g/ed30/`).

- **The probe** is the sixteenth run's `ime-probe.mjs` with its own window title, the case `dc47` added, and two
  readings added to the coloured layer: its `data-ex-colours` and the CSS highlights over its text. This build paints
  the Reference colours with highlights, not spans, and the sixteenth run's span reading reads none.
- **The IME** is the Microsoft IME (10.0.26100.9278), Japanese, typed by real keys through `SendInput`, one virtual key
  and scan code per key. The window's keyboard was set to Japanese for each run and put back to English (UK) after it.
- **The configurations**: `/sheet` and `/sheet?chrome=mud`, each on WebAssembly, on Server, and on Server behind 150 ms,
  in Chrome 154 and in Edge 155: 12 records per case. k7 ran behind 150 ms only (4 records), k11 on `/features` and
  `/features?chrome=mud`, and k14 on `/sheets` and `/sheets?chrome=mud` (12 each).
- **The run** was 36 records, 11:15 to 11:58, with no error. Every console message was `info`.
- **The readings**:
  - `part-g/ed30/summary.txt` groups each state across the configurations: 159 of 168 states read the same in every
    configuration. The other nine are dc47's, which differ only in the highlight's generated name.
  - `part-g/ed30/against-16.txt` compares every state with the sixteenth run's record of the same case, state and
    configuration: 136 of 1884 states read differently, all of them in k6, k6x, k6y, k16 and dc47 (below).
  - Pictures of the WebAssembly and Chrome records are in `part-g/ed30/shots/`.

**The row** (the Definition of Done's pass column):

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| ED-30, "D10 shows the composition while no edit is open" | `8436e45e` | Chrome 154, Edge 155 | WebAssembly, Server, Server behind 150 ms | 150% | **pass** (k2, every configuration) |
| ED-30, "after its end the Cell Editor holds the text and has DOM focus" | `8436e45e` | as above | as above | 150% | **pass** (k2) |
| ED-30, "Enter commits it and moves to D11" | `8436e45e` | as above | as above | 150% | **pass** (k2) |
| ED-30, "after a press on D12 mid-composition, D10 holds the text and D12 is selected" | `8436e45e` | as above | as above | 150% | **not run as written**: the press on D12 lands on the IME's own prediction list, which stands under the composition, and never reaches the page (k8, as in the sixteenth run and in Excel). The same press on D8, clear of that list (k8x): D10 `かな`, the Focus D8, no edit open, in every configuration |

**Every case** (each the same in all its configurations unless the row says otherwise):

| # | Read at `8436e45e` | Expected of the build (the sixteenth run's column) | Verdict | Beside the sixteenth run |
|---|---|---|---|---|
| k1 | The IME's open status on after VK_IME_ON, Hiragana; the keyboard in the Keyboard Field | On | **pass** | the same |
| k2 | `ｋ`, `か`, `かｎ`, `かな` composing in the field over D10, no edit open, the Focus D10. The first Enter: the Cell Editor open holding `かな`, with the keyboard. The second: D10 `かな`, the Focus D11 | as described | **pass** | the same |
| k3 | The first Escape: the Cell Editor open and empty, with the keyboard. The second: D10 empty | as described | **pass** | the same |
| k4 | ↓, ↓ while composing: nothing moves, nothing written. Then D10 `かな`, D11 | as described | **pass** | the same |
| k5 | After F2, the IME on, Escape, and a press on D10: `kana` composes from the first key. D10 `かな` | D10 `かな`, not `k穴` | **pass** | the same |
| k6 | `kana`, Space, then `k` ends the first composition: the Cell Editor opens holding `かな`, and **the keyboard stays in the Keyboard Field**, where the second composition goes on (`かなｋ` … `かなかんじ`, Space `かな感じ`). The first Enter types it into the edit: `かな感じ`. The second: D10 `かな感じ`, D11. **Every configuration, WebAssembly included** | One composition of two clauses, converted together; D10 holds both | **not met as written**: two compositions, not one. D10 holds both, `かな感じ`, as Excel's does | **differs**: on WebAssembly the sixteenth run moved the keyboard to the Cell Editor 22 to 45 ms after the first composition ended. A key of the second was lost there (D10 `かなあんじ` → `かな暗示`). Now every configuration reads as the Server host did |
| k6x | `kanji` at 150 ms between keys: D10 `かな感じ` in every configuration | — (an addition) | the same as k6 | **differs**: WebAssembly gave `かな暗示` |
| k6y | `kanji` at 30 ms: D10 `かな感じ` in every configuration | — (an addition) | the same as k6 | **differs**: WebAssembly gave `かな暗示` (`/sheet`) and `かなん時` (`/sheet?chrome=mud`) |
| k7 | Behind 150 ms, typed at 150 ms with no pause: D10 `かなです`, D11 | D10 `かなです`, D11; nothing lost, doubled or reordered | **pass** | the same |
| k8 | The press on D12 never reached the page: the composition became `悲しんで` and ended; the Cell Editor open holding `悲しんで`, the Focus D10 | D10 `かな`, the Focus D12, no edit open | **not met as written**: the IME took the press, as Excel's did | the same |
| k8x | The press on D8: D10 `かな`, the Focus D8, no edit open, the keyboard in the field | as k8's | **pass** | the same |
| k9 | The press on the Formula Bar's text: the edit open holding `かな`, the keyboard in the bar, the Focus D10 | record | recorded: as Excel | the same |
| k10 | The press on column D's Heading: column D selected, the Focus D1; D10 `かな`; nothing sorted, no edit | `かな` goes to D10 | **pass** | the same |
| k11 | `/features`: the Book cell's field read-only, the IME left off, `kana` typed nothing and opened nothing. The Trader cell: composed as k2; B1 `かな`, the Focus B2 | Book: off, nothing typed or opened. Trader: as k2 | **pass** | the same |
| k12 | Space with the IME on and nothing composed: keydown `Process`; the Cell Editor open holding the IME's full-width space `　` | record | recorded: as Excel | the same |
| k13 | With the IME on and nothing composed, each key the grid's: ↓ → Tab Enter Delete, Ctrl+C, a press on D12, Ctrl+V; nothing typed into the field | each the grid's | **pass** | the same |
| k13x | With something to copy: D12 `150` after Ctrl+V; Delete cleared it | — (an addition) | as expected | the same |
| k14 | `/sheets`: the left edit stands holding `かな` while the right Sheet's field has the keyboard, and nothing reaches the left | as described | **pass** | the same |
| k15 | Alt+Tab away: the composition ended there; the Cell Editor open holding `かな`. Back: the same. Enter: D10 `かな`, D11; Enter: D12 | record whether it survives | recorded: it does not survive, and its text stays in the open edit, as in Excel | the same |
| k16 | The press on the composition's own text never reached the page; the field still `かな`, composing; no edit | record | recorded: the press did not end it, as in Excel | **differs** in one reading: the root carries its focus ring mark (`data-ex-focus-visible`) in every state of k16, where the sixteenth run read it absent. The case runs after k15's Alt+Tab |
| dc47 | In D10, `=A1+` with the IME off: the Cell Editor's text transparent over the layer `=A1+`, `A1` under the highlight `ex…-reference-1`, A1 outlined. With the IME on, `kana`: **the layer hidden and the field's own uncoloured text showing** (`=A1+かな`). Space: the same. **Enter, and no further key: the composition ends, the field's text transparent again, and the layer shown as `=A1+かな`, `A1` under its highlight again**, read again 1.5 s later. The Formula Bar's text stays plain. A1's outline stays throughout | DC-47: the coloured text only while it is the field's text; whenever the field's own text is transparent, the layer's text equals the field's value; while composing, the field's own text shows | **pass** | new (the fifteenth run's i5 ended its composition with Escape) |

**Against the sixteenth run, in short**: k6's lost key on WebAssembly is gone (k6, k6x, k6y). The keyboard now stays in
the Keyboard Field through a second composition on every host, and D10 holds `かな感じ`, as Excel's does. The
composition is still two, not one. k16's root now carries its focus ring mark. Nothing else reads differently.

### The scale

The display scale stayed at **150%** throughout, as the user had it, and was never changed (`HKCU\Control Panel\Desktop`
`LogPixels` 144). Every browser zoom changed here was put back to 100% with Ctrl+0 before its browser closed.

## Part H — what else waits on Excel or this desktop

Run 2026-10-12, 12:02 to 12:24. The tools:

- **Excel**: an Excel of the Excel driver's own (`excel/xl-server.ps1`), each case in a new workbook at 100% zoom, keys and
  the mouse by `SendInput` (`excel/part_h_excel.py`; records `records/part-h-excel.jsonl`).
- **ExSheet**: `/sheet` and `/sheet?case=fills`, WebAssembly, Chrome 154, real presses and drags (`part_h_sheet.py`;
  `records/part-h-sheet-chrome*.jsonl`).
- **The pictures** in `part-h/shots/` are cut to the cells, never Excel's title bar.
- **The regional format** is en-GB, but for h9's ja-JP half.

**Three probe errors were met and are kept in `records/part-h-first-try/`.** A second Excel driver, left from an earlier
step, answered the same commands at the start: it was ended, together with the two blank Excels the drivers had
started. The row Headings carry no `rowheader` role, and were found by class. And at the browser's 150%, h11's first
pictures had the Sheet below the fold, then were resampled by Playwright; the record is the compositor's picture in
device pixels.

| # | Excel | ExSheet (where it was put beside) | Settles |
|---|---|---|---|
| h1 | A1:C5 dragged: the range's outline, dark green, around A1:C5. A1 untinted, with **no outline of its own** (`h-h1-1-a1-zoom.png`). The fill handle at C5's bottom right. The Quick Analysis button below C5 | The same: the range's outline, A1 untinted with no outline of its own (`h-h1-sheet-a1-zoom.png`), the fill handle at C5's bottom right. No Quick Analysis button | UX-19, ticket 24 question 1 |
| h2 | A1:B2, then Ctrl+drag D4:E6: **neither range carries the outline**. The active cell D4 untinted, with a thin **black** border. **No fill handle**. The selected rows' and columns' Headings shaded | Neither range carries the outline, and there is no fill handle, as in Excel. **The active cell D4's border is green**, not black | ticket 24 question 2 |
| h3 | Column C from its Heading: the outline round the column, C1 untinted, **the fill handle at the column's top right corner**, beside C's Heading (`h-h3-1-corner-zoom.png`). Row 3 from its Heading: the outline round the row, A3 untinted, **the fill handle at the row's bottom left corner** (`h-h3-2-corner-zoom.png`) | Column C: the outline, C1 untinted, **no fill handle drawn** in view. Row 3: the outline, A3 untinted, the fill handle drawn at the row's **far right end**, at x = 1622128 CSS px, off screen | ticket 24 question 3 |
| h4 | A1:B2, Ctrl+click F6: Selection `A1:B2,F6`, the active cell F6. Ctrl+click F6 again: **Selection `A1:B2`, the active cell A1**. Enter: A2, then B1, then B2, the Selection kept | — | SL-5 (the reading, A1, observed) |
| h5 | The lists, verbatim, in order (each entry led by Excel's `(…)` icon): `=VLOOKUP(1,A1:B2,2,` and `=HLOOKUP(1,A1:B2,2,`: `TRUE - Approximate match`, `FALSE - Exact match`. `=MATCH(1,A1:A2,`: `1 - Less than`, `0 - Exact match`, `-1 - Greater than`. `=YEARFRAC(A1,A2,`: `0 - US (NASD) 30/360`, `1 - Actual/actual`, `2 - Actual/360`, `3 - Actual/365`, `4 - European 30/360`. `=XMATCH(1,A1:A2,`: `0 - Exact match`, `-1 - Exact match or next smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`, `3 - Regex match`. `=XMATCH(1,A1:A2,0,`: `1 - Search first-to-last`, `-1 - Search last-to-first`, `2 - Binary search (sorted ascending order)`, `-2 - Binary search (sorted descending order)`. The Boolean argument's list is VLOOKUP's. UI Automation does not see these lists; they were read from the pictures (`h-h5-*-list-cells.png`) | `src/ExSheet.Engine/Formulas/Functions.cs` at `8436e45e`: VLOOKUP's, HLOOKUP's, MATCH's and YEARFRAC's lists are the same text in the same order. **XMATCH has no list**: its entry carries no `Values`. The two lists Excel offers for XMATCH stand in the catalogue word for word, as XLOOKUP's arguments 5 and 6 | SH-36 (tickets 04, 09, 11) |
| h6 | The third run's clipboard probe (`part-h/clipboard-probe.mjs`), on Chrome and Edge, WebAssembly and Server (`part-h/h6/`). The one change: E4's `#,##0.00` is now set by Ctrl+!, since the page no longer has the probe's button. **Each of the four records reads as the third run's**: Excel → ExSheet wide pasted `6`, `26/09/2026`, `1,234.50`; narrow refused by name ("Nothing was pasted: the field for F8 is '######' …"). ExSheet → Excel by Ctrl+C and by the Context Menu: the clipboard `24`, `9/26/2026`, `1,234.50` with `data-ex-grid="invariant"`; Excel wrote 24, 46291 (`m/d/yyyy`, showing `########` at the standard width) and 1234.5 (`#,##0.00`) | **One reading differs from the third run**: straight after the refused narrow paste, the live region was empty, where the third run read the earlier Selection's words still there ("3 rows by 1 columns selected, F 2 to F 4"). The Name Box stayed `F7` in both | DC-33, ticket 14 |
| h7 | Over a number showing `####` (123456789.12 in `0.00`). **At the standard width**, a width nobody set: Ctrl+I does **not** widen (8.09, now `#######`); Ctrl+U **widens** to 11.73 (`123456789.12`); Format Cells' Font tab, Bold, OK **widens** to 11.73. **At a width set through COM** (5): none of the three widens | — | SH-45 |
| h8 | Ctrl+5 (strikethrough) alone over `####`: **at the standard width it widens** column A to 11.73; at a set width it does not | — | ticket 100 (the fourteenth run's case 7 observed again) |
| h9 | Format Cells, Custom, the code typed, on A1 (12 Oct 2026 13:30, serial 46307.5625) and A2 (13:30 alone, 0.5625). Under en-GB: `d-mmm-yy` → `12-Oct-26` and `0-Jan-00`; `m/d/yyyy` → `10/12/2026` and `1/0/1900`; `h:mm` → `13:30` and `13:30`. Under ja-JP (Set-Culture, Excel started again; the regional format put back to en-GB after, the International key the same byte for byte): `mmmmm` → **`1` and `1`**, the first character of `10月` and of `1月` | — | ADR-0071, ticket 103 |
| h10 | **A shown top taken away**: A1's bottom thick, A2 recording nothing. Format Cells over A2 opens its Border tab with **a thick top, its button pressed**. The Top button (Alt+T) takes it away, and after OK **A1's bottom is gone too**: neither cell keeps a line there. **Both cells recording an outer edge** could not be made here: set through COM, A2's top thin after A1's bottom thick reads back as A1's bottom thin as well (one line on the edge), and Format Cells over A2 and over A1 shows that thin line | — | ticket 102: the taking-away observed; "which one the dialog shows where both record one" not asked, since Excel would not record two |
| h11 | The DemoHost's case `fills`, cell for cell, at Excel's 100% and 150% zoom (`h-h11-zoom-*-cells.png`): no gridline between two filled cells side by side (B2 \| C2, B5 \| C5). A double line is black, a pixel of the fill's colour, and black, **one device pixel each**, at both zooms (`h-h11-excel*-e2-double-zoom.png`). The case holds double lines and no dashes | `/sheet?case=fills` at the browser's 100% and 150% (DPR 1.5 and 2.25; the zoom set on `/wide`): the same, no gridline between filled cells, and the double line black, the fill's colour and black, one Device Pixel each, at both (`h-h11-sheet*-e2-double-zoom.png`). A case opens without its Sheet Toolbar, as the page says | SH-46, ticket 99 |
| h12 | `@;0` and `0;@;0`, on 12 and on `abc`: **Excel refuses both**. Through COM, "Unable to set the NumberFormat property of the Range class". Typed in Format Cells' Custom box, OK leaves the dialog open with the code selected, and **says nothing**: no message box. Both cells stay General, `12` and `abc` | — | ticket 89 |

## Every difference, and what it belongs to

Each difference this run found from a reading, from Excel, or from an earlier run, with the ticket, ADR or Definition of
Done row it belongs to. Nothing is proposed: each is recorded for a human to decide.

**Part A, the corpus against Excel (ADR-0202, SH-7, ADR-0047).** 79 cases disagree. Each is on its own line, with its
Formula and Excel's answer, in Part A's "Every disagreement":

- **ticket 16 and the revisions, nested SUBTOTAL and AGGREGATE**: SUBTOTAL-020, 021, 035 to 038; AGGREGATE-025, 026,
  042 to 054. Excel counts or skips the nested Formula's cell, where the corpus refuses.
- **ticket 17, ERROR.TYPE of the newer Error Values**: ERRORTYPE-013 to 019 and 020. Excel answers 9, 14, 13, 11, 10,
  12 and 0.
- **ticket 18, INDIRECT's R1C1**: INDIRECT-023 (45).
- **AGGREGATE-038 to 040; LARGE-007; SUBTOTAL-027, 029, 030; RANKEQ-005; SORT-011; UNIQUE-009; XMATCH-008; AND-011;
  INDEX-012; SUBSTITUTE-005; EDATE-008; EOMONTH-008; NETWORKDAYS-005; ISNUMBER-008**: Excel answers, refuses, or
  answers otherwise, as listed there.
- **TEXTFN-009** (de-DE), **TEXTFN-017 to 020**.
- **The last digits** (ADR-0047's digits): COS-004, SIN-004, TAN-004, FV-001, FV-003, NPV-001, NPV-002, PMT-001 to
  003, PV-001, PV-003. **PMT-008**: Excel answers −147.95, where the corpus gives `#VALUE!`.
- **2^27**: COTH-002, CSCH-002, SECH-003, MOD-008.
- **LET typed by keys** (ADR-0181): LET-101, 103, 106, 107, 111 and 112 are entered, where the corpus refuses them.
- **Carried since the first run**: TABLE-011, TABLE-013, COPY-025.

**Part B, by hand in Excel.**

- b4, ticket 16: SUBTOTAL's list in `Functions.cs` names `STDEV`, `STDEVP`, `VAR`, `VARP` (7, 8, 10, 11, 107, 108,
  110, 111), where Excel shows `STDEV.S`, `STDEV.P`, `VAR.S`, `VAR.P`.
- b4: AGGREGATE's `function_num` list leaves out 13 and 16 to 19, which Excel lists. This is ExSheet's own decision,
  by its comment.

**Part C, F4 (SH-28, ADR-0051) and the Heading (ADR-0052).** Part C's "The differences", items 1 to 11:

- typed in Enter mode, ← and Home commit, in both Excel and ExSheet;
- case 7: Shift+Home in Enter mode commits in Excel, and selects text in ExSheet;
- after F4, Excel selects the rewritten Reference, and ExSheet leaves a caret;
- Excel cycles only the end at the caret (cases 5, 6, 22), and ExSheet both ends;
- A:A's third press: Excel cycles two forms, and ExSheet's third press stays as the second;
- case 21: Excel upper-cases the Reference, and ExSheet keeps its case;
- ExSheet's Formula Bar does not point after `=` and ↓ (cases 15, 15b, 16);
- the Heading reading: the view moves sideways in both Excel and ExSheet, where the seventh run read no move.

**Part D, KeyTips (SH-51, SH-52, ticket 160).**

- ExSheet's Home KeyTips: `4` (free in Excel's Home tab) is Strikethrough, and `O` opens Format Cells, where Excel's `O`
  is the Format menu.
- **Under the MudBlazor Chrome, the lists FC, H, B and N stay open after Escape and pile up**, in all four
  configurations (`ExSheet.MudBlazor`, ticket 160).
- On the Server host under the MudBlazor Chrome, the console warns "Blocked aria-hidden on an element because its
  descendant retained focus…" (CON-1 to 4).

**Part E, ExPivot (ticket 07, ADR-0060, ADR-0061, ADR-0064).**

- item 4: Count Numbers over a column read as Text: Excel 1, 2, 3; ExPivot 0.
- item 5 (a): over no numbers, Average, StdDev and Var are an empty cell in both, not `#DIV/0!` as the reading says.
- item 16: Excel's General shows eleven characters; ExPivot's shows fifteen digits. Excel truncates a CSV's numbers to
  fifteen digits on opening it.
- items 12 and 13: Excel's `Column Labels` corner row and its `Total Sum of …` grand total columns; ExPivot draws no
  corner row, and a `Grand Total` band.
- item 27: Excel paints `列ラベル`, and ExPivot has no corner row for it. The Field List's two instructions end in `:` in
  ExPivot; UI Automation reads Excel's without one, which no picture settled.

**Part F, the screen readers (§21.11).**

- Without a Focus, the first Tab from Revalue lands on the Sheet Toolbar, and a second Tab reaches the grid (ADR-0100).
  The first ↓ then makes A1 the Focus.
- Under NVDA, nothing was spoken through the first steps after the browser started, when they began with the script's
  focus and Tab. A pass that began with a press spoke throughout.
- NVDA names the row as the sheet's row plus one: `row 11` for D10.
- **Narrator, as it starts (scan mode)**: no arrow reached the grid, in Chrome or Edge, with or without a Focus. DOM
  focus left the Keyboard Field for `body`; in Chrome without a Focus, it moved among the page's buttons. Narrator's
  own cursor read the column headers instead. This is the sixteenth run's "Scan" with `body` (ADR-0080). §21.11 says a
  keyboard defect here is fixed before the tag; whether this is one is not decided here.
- **Narrator with scan mode off** (Insert+Space): the grid behaved as it did without a reader and under NVDA. Narrator
  names the row as the sheet's row plus one, as NVDA does.

**Part G, ExGrid's by-hand rows (ADR-0208).**

- FN-12e: seven Segoe UI glyphs measure above their charge on Windows: ƒ at every weight; ť and the Hebrew `ג` `ט` `י`
  `ס` `פ` at some weights. The row's first clause is not met as written.
- ED-30: k6 is still two compositions where the reading expects one; D10 holds `かな感じ`, as Excel's does. k8's press on
  D12 lands on the IME's own list, as in the sixteenth run and in Excel, so the row's last clause is not run as written.
- ED-30, beside the sixteenth run: k6, k6x and k6y no longer lose a key on WebAssembly. k16's root now carries its focus
  ring mark.
- VZ-16: the probe's first passes misread the compositor's clip at the browser's zoom. That was a probe error, not a
  finding; the passes are kept in `records/part-g-first-try/`.

**Part H, beside Excel.**

- h2, ticket 24 question 2: ExSheet's active cell border in a multi-range selection is green, where Excel's is black.
- h3, ticket 24 question 3: Excel puts a whole column's fill handle at its top right, and a whole row's at its bottom
  left. ExSheet draws none in view for the column, and the row's at its far right end, off screen.
- h5, SH-36: XMATCH's two lists are Excel's and are not ExSheet's; XMATCH carries no list in `Functions.cs`.
- h6, DC-33 and ticket 14: after a refused paste, the live region is empty, where the third run read the earlier
  Selection's words.
- h7, SH-45: at the standard width, Ctrl+U and Format Cells' Font tab widen a column over `####`, and Ctrl+I does not.
  At a set width none widens.
- h8, ticket 100: Ctrl+5 widens at the standard width, as the fourteenth run read.
- h10, ticket 102: two cells each recording their own line on a shared edge could not be made in Excel.
- h12, ticket 89: Excel refuses `@;0` and `0;@;0`, through COM and, without a message, in Format Cells.

