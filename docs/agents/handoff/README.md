# Handoff: the work in flight towards 0.1.0, as of 2026-10-14 08:30 UTC

Written by the orchestrating Claude Code session when the user took the work over, so that another agent
(Codex, or a new session) can pick it up. This file lives on the branch `claude/handoff` only, and is not
merged. **It describes state, not decisions.** The ADRs, `CONTEXT.md` and `docs/definition-of-done.md`
are authoritative. Where this file disagrees with them, they win.

`main` is at `9a9638b4` (#111). No pull request is open.

## Start here

1. Read `AGENTS.md` at the repository's root (`CLAUDE.md` is a symlink to it). It is the guide for
   every agent here, not only Claude, and every rule in it applies to you. Its Claude-specific words
   map as follows:
   - **"Background agents", "workflows" and "the orchestrator".** Work serially unless your tool runs
     tasks in parallel. Either way you are the orchestrator, and only the orchestrator edits an ADR,
     `CONTEXT.md` or `docs/definition-of-done.md`.
   - **`.claude/skills/*`** are plain Markdown procedures (`implement`, `tdd`, `code-review`, …). Read
     the one a task names and follow it.
   - **`.claude/worktrees/`** is just where checkouts went. Any separate checkout per branch will do,
     with its own layer-3 ports.
   - **Commit trailers.** The `Co-Authored-By`/`Claude-Session` lines in the history are Claude's.
     Use your own tool's attribution, or none.
2. Read this file to the end, then `decisions-log.md` beside it for the history.
3. Take the queue below in order, one branch at a time, unless the user says otherwise.

## The goal and the user's standing rules

The goal is the family's first stable version, `0.1.0`, released under the Definition of Done (§22, "The
final verification checklist") and ADR-0200 to ADR-0209. The last prerelease is `0.1.0-beta.2`. The
user also set these rules, which are not all in `AGENTS.md`:

- **No tag and no release without the user's explicit approval.** That includes `v0.1.0-rc.1`.
- **Push to `main` only to reserve numbers** (`docs/agents/numbering.md`, with `[skip ci]`). Everything
  else goes through a branch and a pull request, merged when the PR is done.
  - Merges are squash merges titled `<PR title> (#N)`.
  - CI runs only while a PR merges cleanly with `main`. A PR with no check runs at all usually means a
    conflict.
  - A head commit whose message carries `[skip ci]` gets no CI.
- **Never relax a requirement** to fit the code; never skip or quarantine a test; an outcome never
  depends on timing (spine rule 6). A wait, retry or longer timeout is never the fix.
- **"Decided with the user" is written only for the user's own decisions.** Every such decision in
  flight is listed below. Never credit the user with a choice that was not theirs. Wording that only
  follows an existing ADR rule says "after run 18a" or names the rule, not the user.
- **Decisions are the user's; implementation is anyone's.** When a branch needs a decision no ADR made,
  stop, put the options and a recommendation to the user, and write the answer into the ADR.
- **Windows work is done on the user's Windows desktop:** Excel, by-hand rows, `verified/*` tags.
  - The cloud container's git proxy refused tag pushes (403), so the user decided that `verified/*`
    tags are pushed from Windows.
  - The CDN deploy-purge measurement is the user's to start (`docs.yml` dispatched twice).
- **Widening a GitHub token's scope** is the user's own action.
- **Economise:** the user asked for budget-conscious work: one verifier per branch, no re-verify loops.

## Branches in flight

Each one is pushed. "WIP, unverified" commits were made at the handoff from work an agent left
uncommitted. Nothing in such a commit was verified after its last change.

The apply lists (`apply-lists/<key>-orchestrator.md`) hold each branch's edits to the ADRs, `CONTEXT.md`
and the DoD as whole-line Old/New blocks, for the orchestrator to apply. A background agent never edits
those files itself. To check and apply:

```sh
python3 docs/agents/handoff/tools/apply_list.py <list> --check --root <checkout>
python3 docs/agents/handoff/tools/apply_list.py <list> --root <checkout>
```

At the handoff every list below checks against its branch and against `main`.

### Ready for a PR

| Branch | Head | What it does | Before the PR |
|---|---|---|---|
| `claude/races-l2` | `44e6a9d5` | Layer-2 tests read once the renderer's dispatcher is idle, never through bUnit's WaitForAssertion or a timeout (EditingNotification, Accessibility scroller, ScrollBurst ASY3, SelectionSummary ADR-0130, ST-2). Tests only. | Merge `main`. The seed-424242 ConsistencyTests failure (1 of 15, once) was not reproduced; say so in the PR. Its report also proposed an optional `AGENTS.md` line (see "Found, not changed"). |
| `claude/races-l3` | `f91b5232` | pivot.spec counts each report removal before the next drop; the layer-3 README says to keep a desktop's pointer off a headed browser (a stray pointer move ends a press in Chromium). Tests only. | Merge `main`. The product half of the format-cells-mud race (More Colours) was decided by the user; it is a separate change (queue item 6). |
| `claude/ime-popover-gap` | `e9b678ae` | An IME composition finished while a popover opens goes to the popover, never to an edit behind it (ADR-0039, ADR-0010, ADR-0080). Change Note written. | Apply `apply-lists/ime-popover-gap-orchestrator.md`: an ADR-0080 note, plus ED-30's DoD extension, which **the user approved on 2026-10-14**. |
| `claude/run17cf-pivot-general` | `cbde08a0` | ExPivot's English follows Excel's English (UK) edition under an `en-GB` UI culture (`Max. of`, `Min. of`, `Max.`, `Min.`, `Summarise value field by`), the US edition otherwise; the Consumer's `Label` wins. Docs page and Example, Change Note. | Apply `apply-lists/pivot-general-orchestrator.md` (12 edits). **The user confirmed on 2026-10-14** that the UI culture picks the edition, as built. Also add the DoD clause **the user approved on 2026-10-14** (to PV-33 or PV-11, layer 2): "under an en-GB UI culture the English (UK) edition's words, the English (US) edition's otherwise; `Label` wins". No block exists for it yet. |

### Needs more work

#### `claude/heading-mark` (`69256236`): Excel's Heading Mark

**Built:** a 2-Device-Pixel line on the edge of each selected column's and row's Heading, drawn as an
overlay so no row learns the Selection (ADR-0008, ADR-0029). It uses a new token, `--ex-heading-mark`,
which falls back to `--ex-focus-outline`; on a Sheet it is `#217346`. Three Change Notes.

**The user decided on 2026-10-14:**

- **D2:** where a header or the Row Headings cover a Focus's or single range's outline side, that side
  is not drawn, and the mark stands for it (2 DP, as Excel). This changes ADR-0008's 2026-10-01 bullet.
  UX-18's three tests (selection-look.spec.mjs:110, 131, 151) then pass as written; today they fail.
- **D1: build all three of Excel's Heading looks**, against the recommendation (which was the shade only):
  - the shade, `#E1E1E1` over `#F0F0F0`, or `#D3F0E0` when the whole column or row is selected;
  - the letters, `#217346` instead of `#444444`;
  - the corner triangle in `#217346` when every cell is selected.

  The constraint stands: rows must still skip their render (ADR-0003, ADR-0008). The Row Headings'
  letters live inside rows. An overlay over the painted Row Headings (bounded by the Window, not the
  data) is the likely way; write the choice into ADR-0008. R4/R5 of
  `verification/2026-10-12-windows-readings/` are the readings.

**Then:**

- Rewrite apply-list edit 1 for D1/D2 (it says they are "raised with the user").
- Keep UX-21 as the criterion id.
- UX-19 (selection-look.spec.mjs:412, forced colors) failed in the container even before forced colors
  are turned on; it looks like the container's Chromium. Check it in CI.

#### `claude/run17cf-toolbar-mud` (`a6fca866`, 33 ahead of main): the Sheet Toolbar and Wrapper round

**What the WIP commit holds, untested as a whole:**

- **Three KeyTips "typed at once" fixes:**
  - a test reading the wrong locator;
  - in `ex-grid.js`, a held Alt judged where it lands, and a release handed to a frame going on with
    the keys after it;
  - in `SheetToolbar.razor`, a key that sends the keyboard on holds the keys behind it;
  - in `ToolbarChoicesPanel`, Enter and Space answered at keydown, with one choice only.
- **The Font Colour widening the user decided on 2026-10-14:** the toolbar's Font Colour widens as
  Excel's Home-tab button does, by its face and from its list; `SetCellFormatAsync` stays as COM's
  `Font.Color`, which does not widen. See `SheetWidening.WidensAsAHomeTabButtonDoes`.
- **Apply-list blocks 21 to 29:**
  - block 21: ADR-0050 item 16's gate rule, adopted by the user on 2026-10-14;
  - block 22 and block 28: the report filter's letters dropped while Filter… opens with no report
    grid, a 0.1.0 Known limitation, decided by the user on 2026-10-14, with an ADR-0202 note;
  - block 29: DoD row PV-50 for ExPivot's opening hold, added at the user's request on 2026-10-14.

  All of these attributions are the user's own and correct, whatever an agent's report doubted.

**Remaining:**

1. **A lost Ctrl+Z:** sheet-toolbar.spec.mjs:227, built-in Chrome, Server host, 1 run in 3. After Blue
   is chosen from the Font Colour list, Ctrl+Z is lost or refused. This is a race to fix in the
   product; look first at the new keydown latch in `ToolbarChoicesPanel` and at the keyboard's return
   after a pointer choice.
2. Merge `main`. `git merge-tree` showed conflicts only in generated files (`ex-grid.min.js`, its map,
   `ShippedAssets.g.cs`); re-minify after the merge.
3. Layer 3 on both hosts: sheet-toolbar, format-cells-mud, format-cells, format-cells-keys, popovers and
   pivot, with changed specs at `--repeat-each=3`. The WebAssembly half was cut off.
4. Run the Docs Site once.
5. Use the draft commit message in `tools/toolbar-draft-commit-msg.txt` as the PR's text.

#### `claude/run17cf-aria-rows` (`650980e4`): ARIA row indices count the header's rows

**Done:** decision (A), the user's, 2026-10-12. Merged with main at `248139f1`. `b8c0b04a` adapted it to
#109: no screen reader verifies a release; a reader's behaviour far down is left to users' reports.

**Remaining:** finish the integration as in the brief kept in `decisions-log.md` (2026-10-14 lines) and
`apply-lists/aria-rows-orchestrator.md` (11 edits; check which blocks still say "owed to the next Windows
run"). Then run aria-rows.spec.mjs on both hosts with `--repeat-each=3`.

- The WIP commit only removes four lines in `tests/ExGrid.Browser/fixtures.mjs`, left by the agent; it
  is probably debugging. Revert it unless it is needed.
- Delete `docs/agents/proposals/run17cf-aria-rows.md` once its edits are applied.

#### `claude/run18a-oracle-dates` (`85323a00`) and `claude/run18a-oracle-financial` (`99bab3d7`): run 18a's oracle corrections, part-way

These are the corrections from run 18a's oracle pass; the brief and the rule are in queue item 5.

- **dates:** DATEDIF, EDATE, EOMONTH, HOUR/MINUTE/SECOND, YEARFRAC, MOD and INDIRECT's R1C1 mixed ranges.
  The WIP touches the engine, the corpus, the tests, `oracle.ps1` and two spikes' readings.
- **financial:** PMT, PV and XNPV.

Neither has an apply list or a Change Note yet. Neither was run against the full Engine suite after its
last change.

## Decisions taken with the user and not yet written into an ADR or the DoD

Each goes in as "*(2026-10-14, decided with the user: …)*" in the place named:

| Decision | Where it is written | State |
|---|---|---|
| Toolbar Font Colour widens as Excel's button; `SetCellFormatAsync` does not (run 18a widening case 5) | ADR-0071, ADR-0100 | toolbar list blocks 23–27 |
| ADR-0050 item 16's gate rule (keys a frame hands back follow a hand-off told meanwhile; ask back before telling the hand-off) adopted | ADR-0050 | toolbar list block 21 |
| ExPivot Filter… with no report grid: letters dropped, a 0.1.0 Known limitation | ADR-0202, `docs/agents/release-cut/0.1.0-known-limitations.md` | toolbar WIP + blocks 22, 28 |
| A DoD row for ExPivot's opening hold | DoD (PV-50) | toolbar list block 29 |
| ExPivot's English also follows the UK edition, chosen by the UI culture; `Label` wins | ADR-0060 | pivot-general list block 5 |
| DoD: the English editions clause | DoD PV-33 or PV-11 | **not drafted** |
| DoD: ED-30 covers an IME composition while a popover opens | DoD ED-30 | ime-popover-gap list item 2 |
| Heading Mark D2: the mark stands for a covered outline side | ADR-0008 | **not drafted** (heading-mark list edit 1 to rewrite) |
| Heading Mark D1: build the shade, the green letters and the corner triangle too | ADR-0008 (and ADR-0029's tokens) | **not drafted** |
| More Colours' late `FocusAsync` (MudBlazor) is brought under the family-wide hold of keys typed while a popover opens | ADR-0039 (the hold's note) | **not drafted**, not built |
| Excel automation (a self-hosted Windows runner for `oracle.ps1`) starts after 0.1.0; its host is decided then | its own ADR, when the work starts | plan only |

## The queue to 0.1.0, in order

1. **PRs for the ready branches:** races-l2, races-l3, ime-popover-gap, pivot-general. Apply each list on
   its branch, commit, open the PR, and merge when CI is green.
2. **The toolbar branch:** finish as above. Then the WR-10 floor run (DoD §22 Step 4, the MudBlazor
   floor at `[9.6.0,10.0.0)`): run it only once `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are
   final, because any later change there invalidates its record.
3. **aria-rows**, then **heading-mark** with D1/D2.
4. **More Colours** under the popover hold (ExSheet.MudBlazor Format Cells, Server host). races-l3's
   report has the analysis: MudBlazor's picker calls `FocusAsync` on its own read-only field a round
   trip after its popover shows.
5. **Run 18a's corrections** (`verification/2026-10-14-windows-readings-18a/report.md`; Excel's answers
   are records, and ADR-0208 says the questions are asked when the feature is built). The full briefs
   used are in `tools/run18a-briefs.md`. In short:
   - **The oracle, 66 disagreements plus four `#VALUE!` cases** (`area/summary.md`). ADR-0047's rule
     applies: a value refused "until Excel answers" takes Excel's answer where the answers settle a rule
     the engine reproduces exactly; otherwise it stays refused, with Excel's answer recorded beside it.
     A refusal decided with the user (PMT's exception, NPV/PMT at small rates) is not reversed without
     the user.
     - dates/misc (WIP): DATEDIF, EDATE, EOMONTH, HOUR/MINUTE/SECOND, YEARFRAC, MOD, INDIRECT.
     - financial (WIP): PMT, PV, XNPV. PMT-043 goes back to `run-18/area-oracle.md` for the keys pass.
     - arrays (not started): FILTER, SORT, SORTBY, TRANSPOSE, UNIQUE.
     - The four `#VALUE!` cases (EDATE-023, EOMONTH-018, NETWORKDAYS-010, WORKDAY-011): Excel's
       `#VALUE!` is a catchable Error Value where the engine refuses. ADR-0047 refused them until Excel
       was asked, so the engine should now answer it.
   - **F4 and Enter mode, readings 1 to 8** (not started). Compare each with ExSheet. Q5: `=` then
     Ctrl+↓ points at the column's data edge. Q6: the grey ground under the pointed Reference in the
     cell. Q7: every direction commits. Q8: `=` then Shift+Home.
   - **The fill handle** (not started), under ADR-0050's rule that a pattern not implemented is refused,
     never copied:
     - 4: C1 `1` with C2 empty, dragged right, is a series in Excel; ExSheet copies it, which is wrong.
     - 5: whole columns B:C with B1 `1` and C1 empty gives D1 `2` and E1 empty.
     - Record 1–3, 6 and 7 in ADR-0050.
   - **Format Cells** (not started):
     - 1–8: extend the Border tab's opening rule to one line shown on any edge, in ranges and in
       several ranges; a thick top with a medium right opens on Thick.
     - 9: under en-US a custom `d-mmm-yy` is built-in 15 on opening.
     - 10: ko-KR's `mmm` is `1` (Windows' name, as ja-JP took). ko-KR's typed `-` stored as `/` is a
       decision if no ADR covers it.
   - **Focus among ranges** (not started):
     - 2: with a Fill, a white device pixel on the left and top between the border and the outline,
       which the Paper should draw.
     - 3: no border while the cell is edited.
     - 4: the `#AFAFAF` line is the gridline; record it.
6. **The run-18 spec**, `docs/specs/exsheet/verify-on-windows-18.md`, from `docs/specs/exsheet/run-18/*.md`.
   - It includes ED-30's k8 with the Microsoft IME's Predictive input off.
   - The by-hand rows (VZ-14, VZ-16, FN-12e and glyphs, ED-30's real IME, SH-42, SH-51, SH-52, KB-3) are
     re-recorded on Windows at rc.1, and `verification/by-hand.json` indexed then.
7. **The release:**
   - Keep `docs/agents/release-cut/0.1.0-known-limitations.md` current.
   - Cut `release/0.1`.
   - Ask the user before tagging `v0.1.0-rc.1`; `release.yml` refuses an rc while a question to Excel is
     open or a by-hand row does not hold (ADR-0208).
   - Run 18 on Windows at the rc, and the macOS run on the user's Mac. Then sign-off (ADR-0205) and
     `v0.1.0`.
8. **After 0.1.0:** Excel automation (task above), and ticket 180 (structured reference case).

## Found, not changed (worth a ticket or a line in a PR)

- races-l2 found the same bUnit-wait flaw, untouched:
  - ExPivot.Components: 25 tests (LiveData, Asking, ReportReadAhead, DetailsAndVersion, StaleReport,
    ReportShrink);
  - `MudFormatCellsTests.Opening_again_replaces_the_dialog`;
  - `MudPivotLiveItemsTests.Filter_keeps…`.

  Its helpers (`GridTestContext.ReadWhenIdleAsync` and friends, `HeldJSAnswer`) can be shared. It also
  proposed an `AGENTS.md` line for the Tests section (the user's to accept): "A layer-2 test reads what an
  answer it gave led to once the renderer's dispatcher is idle, never through WaitForAssertion or a
  WaitAsync timeout, and holds a browser's answer with HeldJSAnswer, not bUnit's handler."
- heading-mark found:
  - a probable latent offset of pinned Header Groups when Row Headings, Pinned Columns and pinned
    groups are combined;
  - a `page.waitForTimeout(200)` in selection-look.spec.mjs, which ADR-0056 forbids;
  - that the layer-3 README has no entry for selection-look.spec.mjs.
- ime-popover-gap: the core opens an edit for typed text while a popover stands, after the hold's
  two-second fallback with a popover that never took focus. A guard for text alone would be a decision.

## Working in this repository

`AGENTS.md` covers the toolchain. These notes add what the container taught:

- **dotnet is under nix:**
  - `nix develop -c dotnet build ExGrid.slnx`, and `nix develop -c dotnet test <project>`, one test
    project at a time on a small machine.
  - In a shallow clone, use `nix develop "git+file://$PWD?shallow=1" -c …`.
  - A new file must be `git add`-ed before the flake sees it.
- **Layer 3:**
  - It needs Chrome and Edge, or a local config that points the `chrome` project at a Chromium. See
    `tools/playwright.container.local.mjs.example`; copy it next to `playwright.config.mjs` and never
    commit it.
  - Run one layer-3 run at a time. Give each checkout its own ports (`EXGRID_BASE_URL=http://localhost:5398`
    for the Server host, `…:5399` for WebAssembly), because a running host is reused.
  - Never commit `verification/*-linux/` or a `console.json`.
- **Shipped scripts and stylesheets** are edited in `src/<Package>/Assets/`, then
  `nix develop .#browser -c bash -c 'cd tools/assets && npm ci && npm run minify'`.
- **Every public member** is declared in its `PublicAPI.Unshipped.txt` (ADR-0201).
- **Disk:** delete `bin/` and `obj/` after runs if disk is short.
- **A pull request's body** follows the repository's habit: "What changes", "Verified", "Docs Site",
  "Release notes". A Consumer-visible change carries a Change Note under `docs/releases/unreleased/` and
  its Docs Site page.

## Files beside this one

| | |
|---|---|
| `decisions-log.md` | the orchestrator's log, 2026-10-12 to 2026-10-14: every decision, merge and stop, the user's marked |
| `apply-lists/*.md` | each in-flight branch's ADR/DoD/CONTEXT edits |
| `tools/apply_list.py` | checks and applies an apply list |
| `tools/setcase.py` | edits one corpus case in place, keeping the file's layout (used by the oracle work) |
| `tools/playwright.container.local.mjs.example` | layer 3 with a bundled Chromium in place of Chrome |
| `tools/toolbar-draft-commit-msg.txt` | the toolbar branch's draft message |
| `tools/run18a-briefs.md` | the briefs written for run 18a's corrections |
