# The briefs written for run 18a's corrections (2026-10-14)

Each brief was given to one background agent, with the common rules below. The toolbar and aria-rows briefs are the ones the
handoff README summarises. Paths under `/tmp/claude-0/…` were the session's scratchpad: the apply lists are now under
`docs/agents/handoff/apply-lists/`, and `apply_list.py` under `docs/agents/handoff/tools/`.

## The rules every brief carried

```text
## Rules for this task (the repository's CLAUDE.md also applies; these are the parts you must not miss)
- Start: {{START}}
- Toolchain: dotnet only through nix, e.g. `nix develop "git+file://$PWD?shallow=1" -c dotnet build ExGrid.slnx -v q -nologo`; the browser shell is `nix develop "git+file://$PWD?shallow=1#browser" -c ...`. A new file must be `git add`-ed before the flake sees it.
- Disk (about 12 GB free) and CPU (4) are shared with other agents: after every build/test run delete bin/obj: `find . -type d \( -name bin -o -name obj \) -prune -not -path '*/node_modules/*' -exec rm -rf {} +`. Run test projects one at a time.
- A shipped script/stylesheet is edited in `src/<Package>/Assets/`, then `nix develop "git+file://$PWD?shallow=1#browser" -c bash -c 'cd tools/assets && npm ci && npm run minify'`, committing what it writes. Every public member of a shipped project needs an XML doc comment and an entry in its PublicAPI.Unshipped.txt (ADR-0201).
- Layer 3 in this container: copy `/tmp/claude-0/-home-user-ex-grid/c880a8c4-7126-5bf9-b6b5-cbf34734a8fd/scratchpad/playwright.container.local.mjs` into `tests/ExGrid.Browser/` (never commit it) and run every Playwright command under the shared lock `flock /tmp/claude-0/exgrid-layer3.lock`, headed under xvfb, with your own ports (given below), e.g.
  `nix develop "git+file://$PWD?shallow=1#browser" -c bash -c "cd tests/ExGrid.Browser && (test -d node_modules || npm ci --silent) && flock /tmp/claude-0/exgrid-layer3.lock bash -c 'EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:PORT xvfb-run -a -s \"-screen 0 2560x1600x24\" npx playwright test --config playwright.container.local.mjs --project=chrome --reporter=line SPEC'"`
  Afterwards delete the container config, `verification/<today>-linux/` and `tests/ExGrid.Browser/test-results`; never commit a `verification/*/console.json`. Only Chromium is available here (no Edge). A new or rewritten spec runs `--repeat-each=3` on each host it runs on before you push.
- Never change an ADR, `CONTEXT.md` or `docs/definition-of-done.md`. What they need goes into an apply list for the orchestrator at `/tmp/claude-0/-home-user-ex-grid/c880a8c4-7126-5bf9-b6b5-cbf34734a8fd/scratchpad/{{KEY}}-orchestrator.md`: sections `### N. <path>`, each with `Old:` and `New:` fenced blocks of WHOLE lines, each old text occurring exactly once when applied in order; prove it with `python3 /tmp/claude-0/-home-user-ex-grid/c880a8c4-7126-5bf9-b6b5-cbf34734a8fd/scratchpad/apply_list.py <list> --check --root <worktree>`. Other branches are writing ADR-0047/0050/0008 notes at the same time, so anchor each block on a line of the section it belongs to, not on the file's last line. If the work needs a decision no ADR has made, or would reverse a refusal or behaviour recorded as "decided with the user", stop at that point, keep the branch building, and report the decision with options and a recommendation.
- Excel's answers are in `verification/2026-10-14-windows-readings-18a/report.md` (and its records/shots). They are records; ADR-0208 says questions to Excel are asked when the feature is built, and the readings now say what ExSheet must do. Never credit to the user a choice that was not theirs: the user decided none of this run's corrections; an ADR note says "after run 18a", not "decided with the user".
- An outcome never depends on timing (spine rule 6): no waits, retries or longer timeouts as a fix, in the product or a test; never skip or quarantine a test; never loosen a comparison or relax a requirement.
- The Docs Site moves with a change a Consumer can see (CLAUDE.md "The Docs Site moves with the change"), with a Change Note under docs/releases/unreleased/; a ticket whose status moves keeps its `Roadmap:` line true (ADR-0191). Run the site once if a page changes (`nix develop "git+file://$PWD?shallow=1" -c dotnet run --project samples/ExGrid.Docs`) and say so.
- Commit messages in English, ending with exactly these two lines:
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_0128brJpkSJwPFbxWT1ESkDB
- Push your branch with `git push -u origin {{BRANCH}}`; do not open a PR. Remove nothing but your own temporary files; leave the worktree in place (bin/obj removed).
```

## toolbar (`claude/run17cf-toolbar-mud`)

The branch is integrated (last pushed 0749a2bd; the worktree's HEAD d8b7d6e8 merged origin/main at c476ddf8 since, unpushed). Its ADR/DoD/CONTEXT edits are in `/tmp/claude-0/-home-user-ex-grid/c880a8c4-7126-5bf9-b6b5-cbf34734a8fd/scratchpad/toolbar-orchestrator.md` (22 blocks). Finish it for its PR:
1. **Three KeyTips "typed at once" tests fail in this container, before and after the merge.** They are the branch's own tests, not on main:
   - sheet-toolbar.spec.mjs ~741, "Alt, Alt, H, 1 typed at once…": fails in both Chromes, on both hosts, 3/3. Even the slow half fails: after Alt, Alt, typing h then 1 opens no edit.
   - ~692, "Escape three times in a list a KeyTip opened, then Alt, H, H, all typed at once": fails on the Server host (':focus' not in the list).
   - ~772, "Alt, H, H, then ↓, →, Enter typed at once fills the cell": fails under the built-in Chrome on the Server host (B3 stays transparent).
   - Also seen: End and Enter typed right after Alt, H, N, before the list holds the keyboard, gave different results run to run (built-in Chrome, Server).

   CI runs layer 3 on Linux in headed Chrome and Edge, so these would fail there too. Find the root cause of each: the product's key handling, Linux Chromium's Alt behaviour, or the test. Fix it where it lives, deterministically; a product defect is fixed in the product. If a case cannot pass on Linux because of the platform, say exactly why, with evidence. Do not weaken the assertion.
2. **Decided with the user on 2026-10-14:** "the Sheet Toolbar's Font Colour widens a column as Excel's Home tab Font Colour button does — by its face and from its list (the readings of 2026-10-14, run 18a, widening case 5) — while SetCellFormatAsync keeps widening as COM's Font.Color does, which does not widen".
   - Implement it in the ExSheet toolbar. Read ADR-0071's widening section, ADR-0100's toolbar note, and verification/2026-10-14-windows-readings-18a/report.md's widening table on main.
   - Add layer-2 and layer-3 tests named ADR-0071/ADR-0100, the Docs Site's SheetFormatting widening paragraphs, and a Change Note.
   - Add apply-list blocks for ADR-0071 and ADR-0100, worded "(2026-10-14, decided with the user: …)".
3. **Decided with the user on 2026-10-14**, for the apply list:
   - **Block 21** (ADR-0050 item 16: the keys a frame hands back follow a hand-off told meanwhile; ask for the keyboard back before telling the hand-off): "adopted with the user on 2026-10-14". Reword its attribution so.
   - **Block 22's limit** (with no report grid shown, the letters typed while the report filter's Filter… opens are dropped, Escape excepted): "dropped, and listed as a Known limitation of 0.1.0". Add the line to docs/agents/release-cut/0.1.0-known-limitations.md, and an ADR-0202 block in the form of its other notes ("*(2026-10-14, decided with the user: also …)*").
   - **"Add a Definition of Done criterion row for ExPivot's opening hold"**: write the row (the next free PV id, MUST, its test names) as an apply-list block.
4. **Re-run the suites.**
   - Layer 2: the toolbar suites.
   - Layer 3: sheet-toolbar, format-cells-mud, format-cells, format-cells-keys, popovers and pivot, on both hosts. Every spec you change runs --repeat-each=3.
   - Keep `/tmp/claude-0/-home-user-ex-grid/c880a8c4-7126-5bf9-b6b5-cbf34734a8fd/scratchpad/toolbar-orchestrator.md` checking against the final tree.

Merge origin/main again first if it moved (merge commit, no rebase). Ports: server 5711, wasm 5712.

## aria-rows (`claude/run17cf-aria-rows`)

Integrate this proposal branch onto main for its PR. The proposal file is docs/agents/proposals/run17cf-aria-rows.md. It holds:
- what was built;
- the user's decision (A) of 2026-10-12: the header's rows are counted in ARIA's row indices;
- records of the Narrator and NVDA passes;
- numbered whole-line edits for the orchestrator.

The previous agent already merged origin/main (248139f1) and committed b8c0b04a ("ARIA's row indices on the Docs Site, and the reader's words far down left to users' reports, as #109 decided"). Neither is pushed.

**Main has changed the ground under it** (PR #109, decided with the user on 2026-10-13):
- accessibility is verified against Chromium's accessibility tree and the APG grid pattern, as MudBlazor does;
- no screen reader is run to verify a release;
- the Definition of Done's §21.11 check by hand was withdrawn;
- ADR-0033 has a section "Verified against the accessibility tree, not a screen reader" (read it on main).

Consequences for this branch:
- **Decision (A) and its tree-based tests stand** (aria-rows.spec.mjs over CDP). They are exactly the new method.
- **Superseded, the proposal's edit 10 note** (the sign-off's Windows run re-checking DOM focus under NVDA and Narrator, a defect fixable after the tag). Drop it, or keep it only as a record that the user decided it earlier on 2026-10-13 and that #109 superseded it the same day.
- **Not owed: "what a reader says far down is owed to the next Windows run".** Say what the grid writes, and that a reader's behaviour is learned from users' reports (ADR-0033's section).
- **"The Sheet's own row number (aria-rowindextext or rowheader cells) left open until the next Windows run reads support"** becomes left open, to be taken up on a user's report. This is a consequence of #109, not a new decision of the user's.
- **The Narrator and NVDA observations stay as records**, phrased as records: scan mode, and NVDA's first-pass silence "recorded only" (the user's decision of 2026-10-12).
- **The Docs Site's Narrator key sentence** (Insert+Space) may stay as observed guidance, claiming no tested support.
- **Update** the code comments, aria-rows.spec.mjs's header comment, and the Change Notes that say "owed to the next Windows run".

Steps:
1. Merge origin/main if it moved since 248139f1.
2. Apply the proposal's non-ADR/DoD/CONTEXT blocks (the Docs pages), adapted as above.
3. Re-base every ADR/DoD/CONTEXT block onto the merged tree, rewritten as above, into `/tmp/claude-0/-home-user-ex-grid/c880a8c4-7126-5bf9-b6b5-cbf34734a8fd/scratchpad/aria-rows-orchestrator.md`, marking each block you rewrote.
4. Move any remaining Windows-only question (none about a screen reader) to docs/specs/exsheet/run-18/aria-rows.md.
5. Delete the proposal file.
6. Run ExGrid.Components, ExSheet.Components, the ExPivot component suite and ExGrid.Tests.
7. Layer 3: aria-rows.spec.mjs and every spec the branch changed, on both hosts, with aria-rows.spec at --repeat-each=3.

Another branch, claude/races-l2, is changing AccessibilityTests.cs; keep your edits to that file minimal. Ports: server 5731, wasm 5732.

## run18a-oracle-dates (`claude/run18a-oracle-dates`)

Run 18a (`verification/2026-10-14-windows-readings-18a/report.md` section 1, and `verification/2026-10-14-windows-readings-18a/area/summary.md`, which lists each disagreement with Excel's double and text) asked Excel the corpus's uncertain cases through COM. Correct the engine for the cases listed below. The rule is ADR-0047's: each disagreement goes to Excel's answer, except where a refusal is named; a value refused "until Excel answers" or "until Excel is asked" is now answered. For each case:
1. Read the ADR-0047 notes (and ADR-0125's for arrays), the engine README's refusal list and the code that gives today's answer, and any decision "with the user" that bears on it. A refusal decided with the user is not reversed without a decision: report it instead.
2. Find the rule Excel's answers settle, together with the observed cases nearby in the function's corpus file. Implement it so that every observed case in that file still passes, and say what is by generalisation. Where the answers do not settle a rule the engine reproduces exactly, keep the refusal and record Excel's answer beside it, as the corpus does for a deliberate refusal (ADR-0047: "The corpus retains Excel's answer alongside each deliberate refusal").
3. Set each case's expected value and source as the corpus's conventions say. See how run 17's corrections (#83, 94ae975f) and the area round (#104, 28d84c5d) recorded theirs, and tests/ExSheet.Engine.Tests' own README.
Tests: `nix develop "git+file://$PWD?shallow=1" -c dotnet test tests/ExSheet.Engine.Tests`, and ExSheet.Components if a refusal mark or the engine's surface changes. The engine README's refusal list moves with the change. ADR-0047 gets a dated note ("2026-10-14, after run 18a") through the apply list. The Docs Site pages that list these functions' refused values move too (search samples/ExGrid.Docs for the function names). Add one Change Note (ExSheet, Changed: Formulas that were refused now answer as Excel does, naming the functions).

The cases:
- DATEDIF-048, EDATE-018, EDATE-019, EDATE-020, HOUR-005, MINUTE-003, SECOND-003, and every other date or time function's case in summary.md's "Every disagreement" (YEARFRAC-058 among them).
- The four agreements report.md section 1 put back to `uncertain`: EDATE-023, EOMONTH-018, NETWORKDAYS-010, WORKDAY-011. Excel answers `#VALUE!` there, an Error Value a Formula catches, where the engine refuses. Promoted while refused, they fail RefusalTests.ADR0047_every_refusal_in_the_corpus_is_the_formulas_and_every_excel_value_error_is_not. ADR-0047 refused these values until Excel was asked. Excel has now answered, so the engine gives that `#VALUE!` as an Error Value and the cases become observed. The exception is a refusal you find was decided with the user for its own sake: report it.
- MOD-033 to MOD-036: Excel answers where the engine refuses. Find why MOD refused them (the quotient-size or precision rule) and whether Excel's four doubles settle it.
- MOD-039: Excel's double is the expected double, 0 units in the last place. The oracle compared value2's text. Fix it where the comparison or the case's expected text lives, so that equal doubles agree, without loosening any other comparison.
- INDIRECT-075 and INDIRECT-076 (`R2C2:C3`, `R3:R2C2` in R1C1 style): Excel sums 1.333 and 46.333 where the engine gives `#REF!`. That is a range of a cell and a whole column or row. Find Excel's rule for such a range's extent from these two answers and the other observed R1C1 cases. Keep a refusal for the shapes the answers do not settle.

## run18a-oracle-financial (`claude/run18a-oracle-financial`)

Run 18a (`verification/2026-10-14-windows-readings-18a/report.md` section 1, and `verification/2026-10-14-windows-readings-18a/area/summary.md`, which lists each disagreement with Excel's double and text) asked Excel the corpus's uncertain cases through COM. Correct the engine for the cases listed below. The rule is ADR-0047's: each disagreement goes to Excel's answer, except where a refusal is named; a value refused "until Excel answers" or "until Excel is asked" is now answered. For each case:
1. Read the ADR-0047 notes (and ADR-0125's for arrays), the engine README's refusal list and the code that gives today's answer, and any decision "with the user" that bears on it. A refusal decided with the user is not reversed without a decision: report it instead.
2. Find the rule Excel's answers settle, together with the observed cases nearby in the function's corpus file. Implement it so that every observed case in that file still passes, and say what is by generalisation. Where the answers do not settle a rule the engine reproduces exactly, keep the refusal and record Excel's answer beside it, as the corpus does for a deliberate refusal (ADR-0047: "The corpus retains Excel's answer alongside each deliberate refusal").
3. Set each case's expected value and source as the corpus's conventions say. See how run 17's corrections (#83, 94ae975f) and the area round (#104, 28d84c5d) recorded theirs, and tests/ExSheet.Engine.Tests' own README.
Tests: `nix develop "git+file://$PWD?shallow=1" -c dotnet test tests/ExSheet.Engine.Tests`, and ExSheet.Components if a refusal mark or the engine's surface changes. The engine README's refusal list moves with the change. ADR-0047 gets a dated note ("2026-10-14, after run 18a") through the apply list. The Docs Site pages that list these functions' refused values move too (search samples/ExGrid.Docs for the function names). Add one Change Note (ExSheet, Changed: Formulas that were refused now answer as Excel does, naming the functions).

The cases: PMT-034 to PMT-053 (PMT-043 aside), PV-013, and XNPV-018, 019, 021 to 030 (summary.md has each formula, Excel's double and its text).
- First read ADR-0047's note "An exception for `PMT` alone" (2026-10-12, decided with the user) and the notes on PMT's and NPV's evaluation and small rates. Those decisions bind. A case Excel answers that the engine can give only by the evaluation those notes chose gives it. A case whose answer needs another evaluation is reported.
- PMT-043: Excel would not take `=PMT(1E-320,0.5,1000)` through COM (0x800A03EC). Leave it `uncertain`, and add it to `docs/specs/exsheet/run-18/area-oracle.md` as a case for the keys pass (`oracle.ps1 -Keys`), with the reason.

## run18a-oracle-arrays (`claude/run18a-oracle-arrays`)

Run 18a (`verification/2026-10-14-windows-readings-18a/report.md` section 1, and `verification/2026-10-14-windows-readings-18a/area/summary.md`, which lists each disagreement with Excel's double and text) asked Excel the corpus's uncertain cases through COM. Correct the engine for the cases listed below. The rule is ADR-0047's: each disagreement goes to Excel's answer, except where a refusal is named; a value refused "until Excel answers" or "until Excel is asked" is now answered. For each case:
1. Read the ADR-0047 notes (and ADR-0125's for arrays), the engine README's refusal list and the code that gives today's answer, and any decision "with the user" that bears on it. A refusal decided with the user is not reversed without a decision: report it instead.
2. Find the rule Excel's answers settle, together with the observed cases nearby in the function's corpus file. Implement it so that every observed case in that file still passes, and say what is by generalisation. Where the answers do not settle a rule the engine reproduces exactly, keep the refusal and record Excel's answer beside it, as the corpus does for a deliberate refusal (ADR-0047: "The corpus retains Excel's answer alongside each deliberate refusal").
3. Set each case's expected value and source as the corpus's conventions say. See how run 17's corrections (#83, 94ae975f) and the area round (#104, 28d84c5d) recorded theirs, and tests/ExSheet.Engine.Tests' own README.
Tests: `nix develop "git+file://$PWD?shallow=1" -c dotnet test tests/ExSheet.Engine.Tests`, and ExSheet.Components if a refusal mark or the engine's surface changes. The engine README's refusal list moves with the change. ADR-0047 gets a dated note ("2026-10-14, after run 18a") through the apply list. The Docs Site pages that list these functions' refused values move too (search samples/ExGrid.Docs for the function names). Add one Change Note (ExSheet, Changed: Formulas that were refused now answer as Excel does, naming the functions).

The cases: FILTER-014; SORT-026, 028 to 032, 034, 035, 037 to 039 and 042; SORTBY-010; TRANSPOSE-007; UNIQUE-021 to 023. summary.md has each formula and Excel's answer. The corpus file has each case's cells.
- First read ADR-0047's SORT/SORTBY/UNIQUE text (the note of 2026-10-12 after the two area passes, and the 2026-10-13 note on a fractional sort_index) and ADR-0125's notes. These refusals were made where the readings of how Excel orders a blank, an Error Value, text or a boolean among keys parted. Excel's answers now settle several of them, among them SORT-042's `sort_index` of 1.5.
- Implement each order the answers settle, and the blank-in-UNIQUE, blank-key and spill-into-aggregate cases (COUNT, COUNTA, MIN, AVERAGE, SUM over SORT/UNIQUE/TRANSPOSE/FILTER). Keep a refusal wherever an order is still not settled, by the rule the 2026-10-12 note states.

## run18a-f4 (`claude/run18a-f4`)

Run 18a asked Excel the eighteenth run's F4 and Enter-mode questions: `docs/specs/exsheet/run-18/f4-enter-mode.md`, answered in `verification/2026-10-14-windows-readings-18a/report.md` under "f4-enter-mode.md", with `records/f4.jsonl` and `shots/f4-*`. The report reads Excel's side only.

For each of answers 1 to 8:
- Find what ExSheet does today, and ExGrid's core where it owns the key: the code, its tests, and the ADRs that decided it. These are the F4, Point, Enter and Edit mode ADRs and their notes, #101's ADR-0010 note, and the readings of 2026-10-12 (R1) in verification/2026-10-12-windows-readings/.
- Where ExSheet differs, make it do what Excel does, with tests named for the ADR: layer 1 or 2 for the logic, and layer 3 for keys in a browser.

Check these first:
- **5.** `=` then Ctrl+↓ points, in Point mode, at the column's data edge: D1048576 on an empty column. Ctrl+Shift+↓ gives `=D10:D1048576`, anchored at the cell being edited.
- **6.** The pointed Reference stands on a grey ground in the cell, not in the Formula Bar. UI Automation gives the bar's selection as the Reference's text.
- **7.** From `abc` in Enter mode, each listed direction commits and moves or extends the Selection as it would with no edit open.
- **8.** `=` then Shift+Home gives `=A10:D10`, in Point mode.
- **1 to 4.** Where a caret selects the Reference after F4. A Sheet qualifier's selection starts at `$A$1`.

For the orchestrator: ADR notes recording each answer ("observed 2026-10-13, run 18a") and what was built. Ports: server 5781, wasm 5782.

## run18a-fill (`claude/run18a-fill`)

Run 18a answered `docs/specs/exsheet/run-18/fill-handle-axis.md` (`verification/2026-10-14-windows-readings-18a/report.md` under "fill-handle-axis.md", records/steps.jsonl, shots/fh-*).

**4. A lone number in a source of several cells that is not a whole axis.**
- C1:C2 dragged two columns right: C1 `1` and C2 `5` copy (`1 1`, `5 5`).
- C1 `1` with C2 empty gives a series (`2 3`), and row 2 stays empty.

**5. Whole columns B:C, with B1 `1` and C1 empty, filled right to E.** D1 is `2` and E1 is empty: the (number, blank) pattern is extended.

ADR-0050 governs: every rule ExSheet implements matches Excel's result, and a pattern it has not implemented is refused, not filled with copies.
- Today ExSheet copies the C2-empty case, giving `1 1` where Excel continues a series: a plausible, wrong answer.
- It refuses 5 ("numbers mixed with other cells").

What to do:
- Find the narrowest rule that reproduces every observed fill: these, R5's (verification/2026-10-12-windows-readings/), the seventeenth run's h3, and the earlier fill readings ADR-0050's notes cite.
- Implement it in ExSheet's fill (SheetEdit and its tests), and refuse every shape the readings do not settle rather than copy it. Say what is by generalisation.
- Layer 1 tests for the rule; layer 3 for a drag that shows it, if the existing fill-handle specs have the pattern.

Answers 1–3, 6 and 7 agree with ExGrid and ExSheet. Record them through the apply list in ADR-0050's "The handle on a whole axis": "whether it draws one at XFD1048576 was not read" and "Where Excel puts it when the Extent is the first column or row was not read" are now read, and say what they showed. Ports: server 5783, wasm 5784.

## run18a-format-cells (`claude/run18a-format-cells`)

Run 18a answered `docs/specs/exsheet/run-18/format-cells-readings.md` (`verification/2026-10-14-windows-readings-18a/report.md` under "format-cells-readings.md", records/fc.jsonl and t103.jsonl, shots/fc-* and t103-*).

**1 to 8: the Border tab's Style and Colour on opening.** Excel opens on the one line shown:
- whatever the number of edges (3);
- on left and right edges (1);
- for a range (4), and for several ranges (5);
- following the line's colour and style (6).

With a thick top and a medium right (2) it opens on Thick, the top's line. For ticket 102 (7), over C2 it opens on the drawn line (B2's thick red), not C2's own record.
- Find ExSheet's rule: the Format Cells ADR's notes, and the code that picks the Border tab's opening Style and Colour.
- Extend it to these cases, and pin each one in tests (layer 1 or 2; layer 3 where a spec already opens the tab).
- 8 is explained: COM's write replaced A1's bottom. Record it.

**9 (ticket 103).** Under en-US, Excel records a custom `d\-mmm\-yy` (numFmtId 164) as built-in 15 as soon as the file is opened. The Number tab then opens on Custom, Type `d-mmm-yy`, with the built-in entry selected and Delete greyed. Compare with what ticket 103 built, and correct ExSheet to it.

**10.**
- Month names: ko-KR's `mmm` shows `1` (no 월), and `mmmm` shows `1월`. zh-CN's `1月` and `一月` agree with ICU. ExSheet keeps ICU's names for zh-CN, and takes Windows' for ko-KR's abbreviated month, as it did for ja-JP.
- Also under ko-KR, a `-` typed into Custom became the date separator (stored `d/mmm/yy`). Decide by the existing ADRs whether ExSheet follows it. If no ADR covers it, report it as a decision.

Tickets 102 and 103 live under docs/specs/exsheet/: keep their status and Roadmap lines true.

Branch claude/run17cf-toolbar-mud (not merged) also touches the Sheet Toolbar and the Wrappers' Format Cells. Keep this branch's changes to the Border tab's opening, numFmt mapping and month names, so the two merge textually. Ports: server 5785, wasm 5786.

## run18a-focus (`claude/run18a-focus`)

Run 18a answered `docs/specs/exsheet/run-18/focus-among-ranges.md` 2–4; 1 was not asked (`verification/2026-10-14-windows-readings-18a/report.md` under "focus-among-ranges.md", records/focus.jsonl, shots/focus-*).

**2. D4 filled yellow.** The active cell's border is 1 device pixel of #217346. Between it and the area's outline:
- on the left and the top, a white device pixel: a line of its own, outside the cell;
- on the right and the bottom, the Fill.

The outline's own pixel is the yellow under the shade (#C7C700). The question says what decides: white means a line of its own, which the Paper then draws.
- Compare with what ExSheet's Paper (ExGrid's selection painting under ExSheet's tokens) paints today, and correct it to Excel's picture, on device pixels (ADR-0090).
- Add layer-3 paints tests (pixels.mjs: paints, onDeviceGrid, wholePixelCentres) named for ADR-0008.

**3.** While the active cell is edited, Excel draws no border on it; the #AFAFAF line around the area stays. ExGrid's Cell Editor covers the cell and the border inside it. Check in a browser that no border shows round the editor on a Sheet, and fix it if one does.

**4.** The #AFAFAF line is the gridline under the shade: it is gone with the gridlines off. The Paper does not draw it (decided with the user, 2026-10-13). Record this.

Through the apply list, ADR-0008's notes on the R4 readings get the answers.

Branch claude/heading-mark (not merged) also changes ADR-0008 and draws a line in the Headings. Do not touch the Headings' drawing, and anchor your ADR-0008 blocks in the R4 notes. Ports: server 5787, wasm 5788.

