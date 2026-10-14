# Decisions log (orchestrator), 2026-10-12 to 2026-10-14

The orchestrating session's own log, copied here at the handoff. Lines marked "(user)" are the
user's decisions; everything else is the orchestrator's record. A decision already written into an
ADR, `CONTEXT.md` or the Definition of Done is settled there; this log is not authoritative.

## User decisions asked by me (2026-10-13)
- PMT: restate the exception exactly ("1-11 ulp from Excel; can show in General's 15th digit"); start payment = exact payment rounded once; keep 2^6 cancellation refusal bound.
- Glossary: new term **Refused Formula**; public name Value.IsRefused (rename from IsRefusal).
- Refused anchor: spill refusals over the known shape; unknown shape -> Known limitation.
- Selection Summary over refused cells: show no numeric figures, say why.
## Orchestrator decisions (mine, applying existing rules)
- MOD: accept 53/64-bit judgment (user's B9/B10/B12 rule).
- Serial in (-1,0) for DATEDIF/YEARFRAC/HOUR/MINUTE/SECOND: refuse until asked.
- SORT fractional sort_index: refuse + uncertain case.
- #NAME? for an Excel function ExSheet lacks / refuses by decision (NPV, bare table name TABLE-024): a Refused Formula (uncatchable); typos stay #NAME?.
- Paste of a refused cell into another ExSheet: carry the refusal mark in ExSheet's HTML flavour.
- XLOOKUP/XMATCH mode outside list: label "by generalisation" (not refused).
- ValueChanges must report a change of refusal state (defect fix).
- Toolbar: keys typed while a list opens -> build the second seam member (same rule as close direction, user's ExGrid-wide hold).
- Toolbar: MudMenu extra Tab stops -> make them take no Tab (keep SH-50).
- f4: grey ground after '=' -> keep ADR-0057 until next run's question.
- pivot-general header precision etc.: user decided via win-run (below).
## User decisions relayed by Windows sessions (2026-10-13)
- pivot A1: copy header gets a raw form (ClipboardData + ADR-0005 change). A2: optional PivotOptions on SetValueField. A3: ja list word 個数.
- aria B1: Sheet row-number deferral NOT the user's (left open while building). B2: NVDA silence = user's decision (not a defect, record only). B3: Docs Narrator sentence credited to no one. B4: §21.11 row stays open; sign-off build re-checks; a defect found then may be fixed after the tag.
- UX-1: count inherited params; MudPivotButton Color/IconColor named exception (ADR-0201 EditorBrowsable(Never)).
- VZ-18: no Citrix naming; remote browser isolation service; Docs paragraph in Getting started.
- SRV-2: name ERR-10's pair as the fifth exception (checked first).
- Columns/HeaderGroups/Sorts changed in place: take it (element compare; state carried by column Name, AG Grid style; ADR decisions outrank).
- ExPivot OrderKeyPolicies change: ask the report again.
- Structured ref case: align with Excel, not 0.1.0 (ticket).
- CON-8: rule "the [ex-grid] failure paths in ex-grid.js". CP-14: two-part restatement (x:num typed full precision; shown text read as Excel pastes text).
- /density ResizeObserver loop at attach: fix first after budget reset. g3 then g2 resume after reset; SL-14/15 rewrite; two clauses; waitForTimeout static check.
- pivot drag race 1/120: unowned, fix from CI trace if recurs.
## Pending to ask
- Toolbar Alt keyup on root (ADR-0100 reversal) / family-wide hold scope / Shift+End in Overwrite / focus frame (#AFAFAF) / Headings mark.
## Branch tips reported 2026-10-13
- win-session: dod-gaps-grid-tests 92aa5651 (lists in place taken, state by Name; open gap: list mutated but not re-handed read by an internal render); dod-gaps-sheet-pivot-tests f88827f5 (OrderKeyPolicies ask again; ticket 180). It pushed c817a87b to main (reserve ExSheet tickets 180-199) unasked.
- win-session-4: dod-gaps-restatements 6d6e08e6 (17 edits); release-workflow-checks adf97c20.
- win-session-3: dod-gaps-browser-tests be205c3b (9 rows); g2/g3 paused until reset; /density RO loop first after reset.
- win-run: aria-rows 4ecbdbea, pivot-general ebf0e8d7, widening f5558209, readings 28e688ea.
- container: area-corrections 20578d06; f4 13988f1f; toolbar c313c369; focus a0822971; fill 890aa3b1; ed30 18571060 (pushed); glyphs 17526b04; mudblazor-rm local 91109761 (origin 7149a509); workflow-rm local 9d31fb97 (origin adf97c20).
## User decisions, batch 2 (2026-10-13)
- Toolbar Alt alone: root keyup holds Alt's release in order (shares ED-30's root keyup; reverses ADR-0100 "no listener added to the grid"; ADR-0021 entry 7 purpose widened).
- Hold scope: family-wide — ExSheet and ExPivot dialogs/menus/panels too (incl. MudBlazor Format Cells Escape race, ExPivot Show Details Escape+↓).
- Shift+End in Enter mode: leave it to the editor (as built, option c).
- Unhanded list mutated in place: the grid draws from the copy taken when the list was handed; a mutation shows when handed again.
## Pending to ask
- Focus among ranges on the Paper: R4's #AFAFAF frame around areas / white line; Headings mark.
## User decisions, batch 3 (2026-10-13)
- Focus among ranges, R4's #AFAFAF area frame + white line: do not draw; record in ADR-0008 as observed and not followed; next Windows run looks with gridlines off.
- Headings mark: ADD Excel's mark (2px #217346 at the active cell's Headings and a mark on every selected row/column Heading) — new decision: classes/tokens, products to name.
## Next steps (orchestrator)
1. Release mechanics PR: apply release-mechanics-apply.md on claude/release-workflow-checks (origin adf97c20), PR to main first. Then mudblazor-range-floor PR.
2. Small PRs: fixture-boot-stylesheets, ed30 (after Windows 60x), dod-gaps-sheet-pivot-tests, dod-gaps-restatements, dod-gaps-browser-tests (9 rows).
3. Follow-ups: win-run pivot A1-A3 + aria B1; win-session unhanded-list copy; container: area follow-ups, f4 verifier fixes, toolbar (Alt keyup + family hold + seam + Mud Tab + Format Cells Escape) after ed30 merges, focus Headings mark + verifier fixes, fill text, glyphs block 4.
## PRs opened (2026-10-13 ~10:50Z)
- #90 release-workflow-checks a3dae0cb (release mechanics, 32 edits applied). After merge: push 4 verified/* tags; tell win-run to tag 2026-10-12 dirs.
- #91 ed30-composition-race 0b4500e7 (keyup rule, 6 edits applied).
- #92 mudblazor-range-floor 04a697d4 (WR-10 floor [9.6.0,10.0.0), 8 edits).
- All three subscribed. send_later check-in trig_01CZ7KYzf5BYRNpokqNP7g8t at 11:42Z.
## Progress 2026-10-13 ~11:00-12:00Z
- PRs opened: #93 fixture-boot-stylesheets (3a37b5f7), #94 dod-gaps-sheet-pivot-tests (a55010ee, proposal applied: ADR-0152 note, Docs, LV-26), #95 dod-gaps-restatements (ee25e13e, 17 edits applied). All subscribed.
- Verifier fixes pushed: f4 ddec2e0d (Shift+End decided recorded; grey ground = orchestrator ruling (c)); focus b6a10189 (R4 frame: not drawn, recorded; Headings mark decided, build separately = task #32); fill 06d8e721 (Ctrl+R refused text, several-columns refusal, handleShows via paints; DC-12 21/21 both hosts x3); glyphs f99b3a19 (block 4 waits for verify-17 report merge; block 5 ADR-0202 after #95).
- Fill squash message must not credit the column-level format write to R5 (commit 6d0e28e9's subject).
## User decisions, batch 4 (2026-10-13 ~11:40Z)
- verified/* tags: this container's git proxy refuses tag pushes (403). User: win-run pushes them (4 existing + its 2026-10-12 dirs). Instructions sent to win-run.
- Window changed in place: keep as is + document (ADR-0141 cost rule): a Window's rows are read where painted; an in-place change shows when painted or when the Window is handed again; hand a new array to change rows.
## Merged
- #90 beb09362 (release mechanics), #93 46991a02 (fixture stylesheets).
- #91 MEM-4 fix 8559e1cb (keyup listener count) + win-session-3 installed Chrome/Edge results in PR body.
- #92 merged main 290398a1 (Release.Tests 252, Docs.Generator 424 pass).
- win-session: dod-gaps-grid-tests 6b336eac (copy on handing; proposal has ADR-0003 note, FN-22 row, GridData sentence).
## ~11:55Z
- Merged: #91 9e40d898 (ED-30), #92 7a60e7b1 (floor), #94 00d0bc91. Open: #95 fa2decb0, #96 749c8fb4 (grid-tests, Window decision written in ADR-0141), #97 fill 66db4b70.
- Integrated locally (commit, tests running): focus f871db8c, widening e798177a, readings 64e8a072, f4 153a28aa. Next-run questions moved to docs/specs/exsheet/run-18/<area>.md (not a ticket folder).
- Headings mark decision written into ADR-0008 (via focus) as "Decided, not yet built"; task #32.
- Toolbar round queued to win-session after ST-1 (4 user decisions). WR-10 floor run waits until Wrappers final (record invalidated by later src/*.MudBlazor changes).
- DISK: container filled up once (ENOSPC truncated ADR-0071 in widening; restored). Clean bin/obj after each test run.
- send_later check-in trig_01Hn36RVFx5NktfhKtBPNhNb at 12:42Z.
- Heading mark scope (user, ~12:00Z): every product's header — core draws it wherever a header or Row Headings stand (ExSheet Paper #217346; plain ExGrid header + Row Headings; ExPivot); colour = new Visual Token defaulting to the Focus outline's colour.
## ~12:05Z
- Merged #95 de0ed2bc (restatements), #96 f8808202 (grid-tests). Open: #97 fill, #98 records, #99 focus.
- Queued on Windows: win-session-4 -> Heading mark after area round; win-run -> by-hand.json index (claude/by-hand-index-2026-10-13) after A1-A3 + verifier; win-session -> toolbar round after ST-1.
- Paste of a refused cell (user ~12:15Z): CARRY the refusal into another Sheet — the target cell becomes a refused cell too (new kind of Entry holding a refusal + the ExGrid seam). Not as built by win-session-4 (which refused cross-Sheet pastes whole).
- Orchestrator rulings for area round: SummaryRefused Japanese = via Consumer CommandLabel (no new word set); MOD "B9/B10/B12 rule" uncited, judgment uncredited.
- #99 needs (--ex-dp, 1px) in StylesheetLengthTests' VisualTokenDefaults after merging main (win-session-4 found; fix fab7dbfe on heading-mark).
- CORRECTION (user, ~12:25Z, "sorry, as recommended"): paste of a refused cell = AS BUILT + new API (same-Sheet one-range copy pastes Formulas; any other marked paste refused whole by name; other programs get shown text; ExGrid seam ClipboardData.MarkAttribute / GridPasteIntent.MarkFor, ADR-0050 item 9, confirmed). Carry-refusal workflow stopped before any push; SD version 10 reservation released.
- Glyphs: main merged (4fa5eb35), L1+2 green on the merge (Tests 1924, Components 2116, Generator 424; Docs builds), pushed; PR #105 opened and subscribed.
- #102 widening merged 0a61c307. Glyphs: main merged again 5cb71511 (ADR-0202: both notes kept), pushed. Readings: main merged cddc7723 (format-cells.spec union, 15 tests), testing before push.
- Fill: 479b85d9 pushed after L1+2 green (Tests 1791, Components 2128) and selection-look 23/24 on both hosts (UX-19 forced-colors fails in container Chromium only, as known).
- #104 area merged 28d84c5d (no overlap with #102 beyond ADR-0202 and disjoint ExSheet.razor hunks; no IsRefusal use left).
- Readings: cddc7723 pushed (ExSheet.Components 807, Mud 68; format-cells h10+h7 x3 6/6 both hosts).
- #105 glyphs merged dd904b31.
- #97 fill merged 52f4abf2 (CI 30/30 incl. UX-19 under real Chrome/Edge).
- #103 readings merged df430d09.
- f4 (#101): fix agent pushed 905428b9 + main merge d30c7e64 (held chords replayed as the field would; Apple ⌘/Option per platform convention, agent's deviation from brief accepted as following the field). Orchestrator restated ADR-0010's replay paragraph with a dated note (uncredited) + run-18 q9, 13ffcbde pushed. PR body updated.
- MEM-1: agent fixed the test (5ca86a26; shared AngleSharp pool, static key table, one bUnit context; old 10^3/10^6 runs painted Placeholders only). Main merged 49762f9b, Components 2128 green, PR #106 opened.
  For the user (not decided): the 5% bound is loose for the new ~65 KB/step measure (lets ~3.3 KB/step TotalCount-growth through at 10^6) — tightening is a DoD change.
  Deferred races (timing at -maxThreads 32, fail on unchanged code too): AccessibilityTests.Focus_reaching_the_scroller_before_the_root_is_a_tab_stop_goes_to_it_once_it_is (2/12, 4/15); ScrollBurstTests.ASY3 (3/12); SelectionSummaryTests.ADR0130 (1/12); ConsistencyTests seed 424242 (1/15). MEM-7 shares the process key table (recorded, not gated).
  Product notes (unchanged): RootId string built per render; static RowIdentityKeys holds an entry per distinct row painted while alive.
- #106 MEM-1 merged 06dc01b9.
- #101 red: find.spec:154 (FD-1/ED-22) lost letters on Server Chrome. Reproduces on main df430d09 3/120 (f4 1/40): pre-existing race. Comment posted on #101 (rerun 403: no means). Fix agent dispatched on claude/fd1-find-letters-race; its fix will be merged into f4.
- MEM-1 bound: user chose 'Keep 5%' (2026-10-13). No DoD change before 0.1.0; revisit after the release.
- FD-1 fix: agent found cause = focus falls to body between the menu's removal and the find field's focus (not @key). Fix 3b419750 (ReclaimFocusAsync before CloseMenus in OpenFindAsync). PR #107. Consumer-popover gap (OpenConsumerPopover: Format Cells from Context Menu, figures menu, toolbar choices) needs user decision: (a) hold + Tab dropped (rewrite ADR-0039 2026-10-01 note, DC-60 test), (b) keep gap, (c) hold + core answers held Tab/Shift+Tab (new message, ADR note).
- #107 FD-1 merged 3e74eb06. f4: main merged fa9b1e76 (ExGrid.Components 2173, ExSheet.Components 836), pushed.
- #101 f4 merged 5a257fd4 (CI 30/30 on fa9b1e76 with #107).
- Consumer-popover gap: user chose 'Hold; Tab dropped' (2026-10-13). ADR-0039 note written on claude/consumer-popover-key-gap; implementation agent next.
- Consumer-popover gap implemented 76ee7928; ADR note corrected 32faf443 (case seen = Format Cells from Context Menu; figures menu and toolbar do not take the path). PR #108.
  Follow-ups: IME composition in a popover-opening gap can open an edit behind the popover (pre-existing for Ctrl+1/Alt+Down/Ctrl+F; existing rule "never the grid's" says it is a defect); toolbar More Number Formats one-message window (not reproduced).
- Excel automation: user chose "after 0.1.0" (2026-10-13); runner host "decide later".
- A11y verification: user chose "the same as MudBlazor (no screen reader)" (2026-10-13): no screen readers; judged by the accessibility tree and the APG pattern; DoD §21.11's by-hand screen-reader row goes. To write: ADR note + DoD.
- A11y no-SR decision written: ADR-0033 section + 3 pointers, ADR-0208 note, DoD §21.11 note + Step 4 row removed, by-hand.json row removed, RepositoryTests list. PR #109 (69845fcf). win-run was inactive when told; tell it (no §21.11 record; no NVDA/Narrator steps in aria-rows/pivot-general) when it is next active.
- #108 consumer-popover gap merged 112a0ccb (CI 30/30).
- Windows sessions (win-session, -3, -4, win-run) all ARCHIVED ~13:54Z (bridge disconnected). Pushed: toolbar round-4 proposal, aria-rows proposal, pivot-general proposal, dod-gaps-browser-tests. Heading mark NOT pushed (lost). by-hand index not started.
- User 2026-10-13: "do everything except what needs Windows". ExPivot opening key gap: user chose (b) hold and hand over (ADR-0061 note to write).
- Workflow wf_8ab8f33e-e12 (wlf8eaiqa): integrate toolbar (+ ExPivot hold + toolbar one-message window), pivot-general, dod-gaps-browser. Next workflow: aria-rows (after #109), heading mark, races L2/L3, IME popover gap.
- Workflow wf_e79919a3-648 (wk94illaw): heading-mark, races-l2, races-l3, ime-popover-gap — branches claude/<key>, orchestrator lists at scratchpad/<key>-orchestrator.md. aria-rows waits for #109.
- #109 merged 73d2b2bc. aria-rows brief saved (scratchpad/aria-rows-brief.md); launch after workflow 1.
- 2026-10-14 (user): win-session-1 (session_013zKuJyBb2iAZnsccyoek9J) created for Windows work. Sent: Excel readings 18a (area oracle 137 cases, run-18 UI questions, f4 q9 ED-2 on Windows Chrome/Edge) on claude/windows-readings-18a; by-hand rows wait for rc.1; no screen readers.
- 2026-10-14 (user): approved rewriting beta.1/beta.2 GitHub release bodies. Rendered here as release.yml does (REF=tag commit, ADR links at main 73d2b2bc); sha256 beta.1 affe8ea4…, beta.2 3f30f76a…; sent to win-session-1 to render identically and `gh release edit --notes-file` (no title/flag/asset change).
- beta.1/beta.2 GitHub release bodies rewritten by win-session-1 (hashes matched; title/flag/assets unchanged: beta.1 0 assets, beta.2 20 assets).
- Run 18a records merged as #110 (c476ddf8; [skip ci] head, local Engine 6036/Release 252/DocsGen 424). Differences to correct: oracle 66 + 4 #VALUE! refusals; F4 Q5 Ctrl+Down points; fill 4b/5; Format Cells 9/10 (+1-8 rule extension, case 2 thick+medium -> Thick); widening 5 = user decision.
- 2026-10-14 (user): widening 5 — the Sheet Toolbar's Font Colour widens as Excel's Home-tab button does (face and list); SetCellFormatAsync stays as COM's Font.Color (no widen). ADR-0071/0100 note to write; implement after toolbar merges.
- Planned run-18a corrections (after wf_8ab8f33e): oracle-dates, oracle-financial, oracle-arrays, oracle-misc; ui-f4 (Q5 Ctrl+Down points, Q6 grey ground -> ADR-0057), ui-fill (4b, 5), ui-format-cells (1-8 rule ext., 9, 10 ko-KR mmm), ui-widening (5 + compare); focus-among-ranges readings -> ADR-0008 notes.
- 2026-10-14 (user) on workflow-1 results: (1) ADR-0050 item 16 gate rule (keys a frame hands back follow a hand-off told meanwhile; ask back before telling the hand-off) ADOPTED; (2) ExPivot Filter… with no report grid: letters dropped + 0.1.0 Known limitation; (3) add a DoD PV row for ExPivot's opening hold; (4) ExPivot English: ALSO follow the UK edition (Max. of, Min. of, Max., Min., Summarise Values By) — new implementation on pivot-general.
- dod-gaps-browser: ADR-0013 correction applied (fb1fed36), main merged c6c174c7, PR #111.
- Workflow wf_26583f00-e56 (w3a369toe): finish toolbar (KeyTips failures, widening 5, decisions into list), pivot-general UK words, aria-rows integration. 18a corrections workflow after wf_e79919a3 finishes.
- #111 DoD-gaps browser tests merged 9a9638b4 (CI 30/30).
- 2026-10-14 00:57Z: every running agent failed: "You've hit your org's monthly spend limit" (workflows wf_26583f00 toolbar/aria-rows, wf_5efbe3bd 18a). Container restarted ~07:46Z. pivot-general had finished (cbde08a0). Resumed all as wf_f301ecf9-f69 (task wqp9qsejf), each continuing from its worktree.
- wf_e79919a3 results: heading-mark 69256236 (D1/D2 open), races-l2 44e6a9d5, races-l3 f91b5232 (More Colours product race open), ime-popover-gap e9b678ae (ED-30 DoD extension open).
- 2026-10-14 (user): heading mark D2 = the mark stands for the outline's side where a header/Row Headings cover it (2 DP, UX-18 as written; changes ADR-0008's 2026-10-01 rule). D1 = BUILD ALL THREE (shade incl. whole-axis #D3F0E0, letters #217346, corner triangle) — user chose against the recommendation; rows must still skip render.
- 2026-10-14 (user): More Colours late-FocusAsync race = brought under the family-wide popover key hold.
- 2026-10-14 (user): ExPivot English edition chosen by CurrentUICulture (en-GB -> UK words), as built; Label wins.
- 2026-10-14 (user): DoD additions: ED-30 gains the IME-composition-while-a-popover-opens case; PV gains the English editions clause (layer 2).
- 2026-10-14 (user): "make it possible to hand this over to Codex later" — keep a handoff a Codex agent can pick up: AGENTS.md on main + live state pushed to a branch.
- 2026-10-14 ~08:15Z (user): "you may stop at a good point; I will take it from here". Workflow wf_f301ecf9 stopped; every worktree's unfinished work committed as "WIP, unverified" and pushed; handoff written on claude/handoff.
