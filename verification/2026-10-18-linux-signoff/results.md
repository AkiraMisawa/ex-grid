# The Definition of Done signed off for 0.1.0

**X = `cff4a0dd94d0bbbb0fd171e5a276dabebbdb1c07`**, the commit `v0.1.0-rc.3` is tagged on, at the head of
`release/0.1`. Signed off on 2026-10-18 (ADR-0205, the Definition of Done's §22 Step 7).

X is `28ab5e1bf8c1e4c71dbcb19e9bd430cbd0612f48` (0.1.0-rc.3's code, the sign-off's fixes of #168, merged
into `release/0.1` as #169) plus the records of #171 and #173, which add only `verification/`
(`git diff --stat 28ab5e1b cff4a0dd -- . ':!verification'` is empty). The local steps below ran at
`28ab5e1b`, so at X's code; CI's ran at X itself. The tag `v0.1.0` is X plus this directory and
`docs/releases/`.

## Verdict

- **Every MUST passes**: 645 criteria, of which 592 pass, 37 pass with a clause no test asserts that was
  accepted as it stands (below), and 16 OBSERVATIONAL numbers are recorded. None fails, none is
  `blocked`, none is `n-a`.
- **Every SHOULD passes**: FN-20, UX-10 and DIR-4. None is accepted failing.
- **Every OBSERVATIONAL number is recorded with its comparison**: [`measure/README.md`](measure/README.md)
  for the browser and live measurements, [`metrics.json`](metrics.json) against 0.1.0-rc.2's rehearsal for CI's
  layer-3 numbers, and `measure-coreclr.log` for the CoreCLR ones.
- **§21 lists no open question, and no criterion reads `BLOCKED`** (§21: "No open question remains").
- **Every reservation of §21.11 still has its trigger unfired** (below).
- **The rows checked by hand hold on their records** (below), and VZ-18's is a Known limitation of 0.1.0.
- **ADR-0042's prerequisites hold**: the public surface is ADR-0201's and its baselines (every
  `src/*/PublicAPI.Unshipped.txt` holds only `#nullable enable` at X), and the release workflow's
  check of a stable tag (ADR-0205) is on `release/0.1` from its cut.

## The user's decision of 2026-10-18

While the evidence for each row was read, 37 criteria turned out to pass with one clause of their
Pass that no test in any layer asserts: the test named for the row checks the rest, and the clause is
held by the code as it reads, not by a test. Asked whether to sign off with those clauses as they
stand or to check each first (a scratch test, then a new release candidate if one failed), the user
chose **"Sign off as is"** on 2026-10-18 (19:37 UTC): the clauses are listed here, each with what is
not asserted, and each becomes a ticket after 0.1.0. They read `pass, accepted as is` in the table.

FN-1, VZ-2, SL-1, SL-2, KB-5, KB-9, KB-42, CTX-3, PST-4, ERR-1, DC-40, DA-18, A11Y-6, A11Y-9, A11Y-10,
ED-13, ED-19, SL-11, KB-3 (its layer-3 half; its macOS row passes by hand), KB-10, KB-11, KB-28, KB-40,
CP-7, ERR-7, ASY-7, WR-1, DC-39, DC-62, DC-63, SH-60, PV-3, PV-7, PV-13, PV-18, LV-27.

## Toolchain, OS and browsers

- **Local steps** (0 to 3, 5 and 6): a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB;
  Ubuntu 24.04.5 LTS, Linux 6.18; .NET SDK 10.0.203 through nix, runtime 10.0.7; node 24.14.1;
  Playwright 1.62.1; Chromium 141.0.7390.37 (Playwright's build) as `chrome`, headed under xvfb.
- **CI** (Step 4, the soak, ST-1 at 10⁶ on its long run): release run
  [39158128144](https://github.com/AkiraMisawa/ex-grid/actions/runs/39158128144) on the tag `v0.1.0-rc.3`,
  GitHub's `ubuntu-24.04` runner image 20261012.334.1 (Ubuntu 24.04.5 LTS), .NET SDK 10.0.402 (runtime 10.0.13), Node 22.23.3, Playwright 1.62.1, headed under xvfb; Google Chrome 155.0.8059.39 and Microsoft Edge 155.0.4283.45.
- **By hand**: the records the rows name (below), each with its own toolchain.

## Step 0 — Preconditions

| Row | Result | Record |
|---|---|---|
| PRE-1 | pass: the solution builds, 0 warnings, 0 errors | [`pre-1.log`](pre-1.log) |
| PRE-2 | pass: every `src/` project `net10.0`, no `LangVersion` | [`step0-static.log`](step0-static.log) |
| PRE-3 | pass: the package check, `0.0.0-smoke.1792328489 passed` | [`pre-3.log`](pre-3.log) |
| PRE-4 | pass: no core project references MudBlazor or Fluxor | [`pre-4.log`](pre-4.log), [`step3-static.log`](step3-static.log) |
| PRE-5 | pass: Chrome and Edge projects | [`step0-static.log`](step0-static.log) |
| PRE-6 | pass: `git status --porcelain -- src/` empty | [`step0-static.log`](step0-static.log) |

## Steps 1 and 2 — Layers 1 and 2

One run of `ExGrid.slnx` with `--timeout 5m` per project: [`layers-1-2.log`](layers-1-2.log), and one
`.trx` per project beside it. **14,030 tests in 14 projects, 0 failed.** The skips are exactly those
Step 2 allows, read by name from the `.trx` files by `ExGrid.Release skips` ([`skips.log`](skips.log)):
ST-1's 10⁶-row case, run in Step 5, and the ten explicit measurements, run in Step 6. No project was
stopped by its timeout (ASY-6), and CON-7's `UnobservedTaskWatch`, which fails a suite on any
unobserved exception, failed none.

| Project | Passed | Skipped | Total |
|---|---|---|---|
| ExGrid.Tests | 1,944 | 0 | 1,944 |
| ExGrid.Components | 2,376 | 1 | 2,377 |
| ExGrid.MudBlazor.Tests | 316 | 0 | 316 |
| ExGrid.Data.Tests | 366 | 2 | 368 |
| ExGrid.Data.Arrow.Tests | 182 | 1 | 183 |
| ExSheet.Engine.Tests | 6,095 | 3 | 6,098 |
| ExSheet.Components | 886 | 0 | 886 |
| ExSheet.MudBlazor.Tests | 98 | 0 | 98 |
| ExPivot.Engine.Tests | 505 | 4 | 509 |
| ExPivot.Components | 348 | 0 | 348 |
| ExPivot.MudBlazor.Tests | 84 | 0 | 84 |
| ExGrid.DemoApi.Tests | 143 | 0 | 143 |
| ExGrid.Docs.Generator.Tests | 424 | 0 | 424 |
| ExGrid.Release.Tests | 252 | 0 | 252 |
| **All** | **14,019** | **11** | **14,030** |

## Step 3 — Static inspection

The greps: [`step3-static.log`](step3-static.log). PF-1, PRE-4 and KB-16 print nothing; FN-15's matches
are read in [`inspect-rows.md`](inspect-rows.md) and none orders or filters the Consumer's rows. The
rows judged by reading the code are in [`inspect-rows.md`](inspect-rows.md), read at X: all pass. FN-18,
KB-2 and RR-9, which failed the sign-off of 0.1.0-rc.2, pass at X.

## Step 4 — Layer 3

CI ran the whole of layer 3 at X in the release run of `v0.1.0-rc.3`, CI's long run: the `chrome`,
`msedge` and `chrome-150` projects, on the WebAssembly host and on the Server host, in 20 shards. Every
job and its Playwright summary: [`layer3/jobs.md`](layer3/jobs.md), each job's log beside it.

- **5,246 passed, 186 skipped, 0 failed, 0 flaky, 0 did not run**; no retry (`retries: 0`) and no
  `Test timeout of … exceeded` in any log (ASY-6). The same totals as 0.1.0-rc.2's rehearsal.
- **The skips.** On both hosts: the measurements Step 6 runs by name (`measure*.spec.mjs`), VZ-14's
  Windows block, run by hand, and two `test.fixme` of `sheet-vs-excel.spec.mjs`, items 18 and 24.
  On WebAssembly only: the tests that need a circuit. On the Server host only: ERR-10's two stamp tests,
  which SRV-2 names as an exception (the host pins the script by integrity, and the integrity-pinned
  test runs instead). SRV-2 holds.
- **The soak** (MEM-5, `EXGRID_SOAK=1`): ten minutes on Chrome and on Edge, on each host, all four passed
  (shard 2 of 4 of each). MEM-6 is read at the end of each, in `metrics.json`.
- **`console.json`**, the console and page errors of every test, merged from the twenty jobs' records:
  no page error. The errors are the harness self-test's own (`a test whose console reports an error
  hands no page on`, CON-8's check that such a test fails), and the one warning from ExGrid's code is the
  one VZ-12b asks for, once per instance, naming the parent. No third-party warning (CON-3).
- **`metrics.json`**, the OBSERVATIONAL numbers per host and browser, merged from the jobs' records with
  no two jobs disagreeing on a value.
- **Browsers**: Google Chrome 155.0.8059.39 and Microsoft Edge 155.0.4283.45, installed on the runner
  (`chrome-150` is Chrome at a 150% device scale).

### The rows checked by hand

Each holds on its record: the record is a pass, it names its commit, its tag is `verified/` and its
directory's name, and none of the paths it covers changed between its commit and X. The release
workflow checked the same of `v0.1.0-rc.3` and admitted it.

| Row | Where | Record | Commit | Verdict |
|---|---|---|---|---|
| VZ-14 | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| VZ-16 | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| FN-12e | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| FN-12e | macOS | [`2026-10-18-macos-18b`](../2026-10-18-macos-18b/report.md) | `28ab5e1b` | pass |
| ED-30 | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| SH-42 | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| SH-51 | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| SH-52 | Windows | [`2026-10-18-windows-18b`](../2026-10-18-windows-18b/report.md) | `28ab5e1b` | pass |
| KB-3 | macOS | [`2026-10-18-macos-18b`](../2026-10-18-macos-18b/report.md) | `28ab5e1b` | pass |
| WR-10 | Linux | [`2026-10-18-linux-mudblazor-floor`](../2026-10-18-linux-mudblazor-floor/report.md) | `f6b5e8fe` | pass, MudBlazor 9.6.0 to 9.11.0 |
| VZ-18 | the remote-isolation PC | none | | Known limitation of 0.1.0 (decided 2026-10-12) |

## Step 5 — The randomised consistency run

ST-1 at 10⁶ rows, `EXGRID_ST1_MILLION=1`: [`st1.log`](st1.log), 21 of 21 passed. The seeds are
**20260901** (200, 1,000 and 1,000,000 rows) and **424242** (200 and 1,000 rows), read from
`ExGrid.Components.trx` and `st1.log`. Zero violations. CI's long run ran the same case at X.

## Step 6 — Observational numbers

- **CoreCLR**: [`measure-coreclr.log`](measure-coreclr.log), the ten explicit measurements (PV-21's four,
  DA-17's two, ADR-0065's one, SH-19's three), exit 0. MEM-7: [`mem-7.log`](mem-7.log), 66,149 bytes a
  one-row scroll frame.
- **Browser and live** (PV-21, DA-17, PV-48, LV-15, LV-23, SRV-6, SH-19 on a circuit, PF-8):
  [`measure/README.md`](measure/README.md), each compared on one container against the code of the last
  record, since containers of one kind read 10–60% apart.
- **CI's layer-3 numbers** (BIG-6, BIG-7, PST-6, PF-6, PF-7, MEM-2, MEM-6, DOM-5): `metrics.json`, compared
  below.

CI's layer-3 numbers at X against 0.1.0-rc.2's rehearsal (release run 39116731609, the same runners and browsers' major versions), rc.2 → X:

| Number | WebAssembly, Chrome | WebAssembly, Edge | Server, Chrome | Server, Edge |
|---|---|---|---|---|
| BIG-6, settle repaint, median ms | 15.12 → 16.12 | 18.09 → 26.26 | 6.38 → 6.25 | 8.38 → 5.02 |
| BIG-6, sustained scroll frame, median / max ms | 16.7 / 16.8 → 16.7 / 16.8 | 16.7 / 16.8 → 16.7 / 33.3 | 16.7 / 16.8 → 16.7 / 16.8 | 16.7 / 16.8 → 16.7 / 16.8 |
| BIG-7, mount to first painted row, ms | 972 → 977 | 964 → 1267 | 357 → 390 | 371 → 368 |
| DOM-5, elements with the band on / off | 250 / 2230 → 250 / 2230 | 250 / 2230 → 250 / 2230 | 250 / 2230 → 250 / 2230 | 250 / 2230 → 250 / 2230 |
| PF-6, settle repaint on / off, median ms | 15.12 / 45.57 → 16.12 / 44.29 | 18.09 / 53.93 → 26.26 / 75 | 6.38 / 40.46 → 6.25 / 41.69 | 8.38 / 57.51 → 5.02 / 28.86 |
| PF-7, a step's cost, median / p95 ms | 4.27 / 10.96 → 4.23 / 10.01 | 4.59 / 11.04 → 5.83 / 16.54 | 0.8 / 1.05 → 0.86 / 1.09 | 1.06 / 1.38 → 0.99 / 1.22 |
| PST-6, parse and Intent / next key, ms | 3125 / 88 → 3207 / 87 | 3365 / 106 → 4726 / 117 | 2830 / 24 → 2914 / 57 | 3091 / 25 → 1840 / 100 |
| MEM-2, drift in nodes / listeners over 50 cycles | 0 / 0 → 0 / 0 | 0 / 0 → 0 / 0 | 0 / 0 → 0 / 0 | 0 / 0 → 0 / 0 |
| MEM-5, JS heap, first sample / median, MB | 4.8 / 6.5 → 4.8 / 6.5 | 5.0 / 6.6 → 4.8 / 6.6 | 3.2 / 3.3 → 3.2 / 3.3 | 3.3 / 3.3 → 3.3 / 3.3 |
| MEM-6, managed heap before / after, MB | 9.7 / 9.8 → 9.7 / 9.8 | 9.7 / 9.8 → 9.7 / 9.8 | 43.8 / 25.0 → 41.4 / 25.0 | 40.5 / 26.2 → 43.4 / 24.9 |

The DOM and memory figures are the same at both commits: the DOM does not grow with the row count
(DOM-5, BIG-6), no node or listener drifts (MEM-2), and the heap levels off over ten minutes (MEM-5).
The times move both ways between two runs on shared runners, Edge on WebAssembly slower this run and
Edge on the Server host faster; one run a side does not tell a change from the runner, and between
rc.2 and X the code changed only in the three fixes of #168. None is gated.

## §21 and §21.11

§21 states that no open question remains, and no row reads `BLOCKED`. The reservations of §21.11 still
standing, and their triggers at X:

| Reserved | Trigger | At X |
|---|---|---|
| The Overlay application and the bundled undo stack | the first scenario-backed Consumer | unfired: no such page or Consumer is in the repository |
| The Cell Editor's own ARIA semantics, a richer announced contract | a real screen reader | out of 0.1.0's scope, accepted in writing (Release Notes, Out of scope) |
| The year ▸ month ▸ day tree over a date column's value list | a Consumer declaring a value list on a date column | unfired: no page in `samples/` declares one |
| A range of columns through a Pointing Scope | the engine gaining column ranges | unfired: SH-37 pins today's refusal and passes |
| ExGrid reading a Snapshot | a screen showing one live feed in ExGrid and ExPivot, or a measurement | unfired, as confirmed with the user on 2026-10-11; no such screen has been added since |
| Which layer VZ-18's repaint answers | — | VZ-18's check is a Known limitation of 0.1.0; its layer-3 half passes |
| Find All and Replace | none named | out of 0.1.0's scope, accepted in writing on 2026-10-11 |
| Dragging a Reference Outline | none named | out of 0.1.0's scope, accepted in writing on 2026-10-11 |
| A column band | none named | out of 0.1.0's scope, accepted in writing on 2026-10-11 |

## Found during the sign-off, for after 0.1.0

None of these fails a criterion as written. Each is to become a ticket after 0.1.0.

- **The 37 clauses no test asserts**, listed above and in the table: a test for each.
- **PV-50 appears twice** in the Definition of Done, at two adjacent lines with the same text.
- **A command the core marked disabled is not refused by the core**: `RunCommandAsync`
  (`ExGrid.razor:5564-5576`) runs `command.Invoke()` without reading `Enabled`, so a substituted Chrome
  that ignores `Enabled` could ask for what the core disabled; the same shape as FN-18's gap
  ([`inspect-rows.md`](inspect-rows.md), FN-18).
- **ADR-0021 is behind ADR-0204**: it still says `scrollIntoView()` "is refused" and carries no dated note
  for the page-follow use ADR-0204 allows; and `ex-grid.js:1` still says "The six permitted uses", where
  there are ten (PF-2).
- **The popover hold in `ex-grid.js` mirrors `MenuKeys.ResolveInColumnMenu`** under a "must move
  together" comment: the one key resolution the script still carries twice (KB-2's note).
- **Two layer-3 gaps found on the Mac**: the Sheet-against-Excel clipboard tests press Control+C and
  Control+V, which fail on macOS, and no layer-3 test presses Command+Enter in the Sheet's editor
  (`verification/2026-10-18-macos-18/`).

## Every criterion

`L1/2` names the layer-1 or layer-2 tests that hold the row, passed in Step 1 (their `.trx` files
here); `L3` the layer-3 tests, passed in Step 4 on both hosts in Chrome and Edge. A count such as
`(+3)` is further tests of the same row. Rows read from the code are in [`inspect-rows.md`](inspect-rows.md).

| ID | Level | Result | Evidence |
|---|---|---|---|
| PRE-1 | MUST | pass | `pre-1.log`: 0 errors |
| PRE-2 | MUST | pass | `step0-static.log`: every `src/` project `net10.0`, no `LangVersion` |
| PRE-3 | MUST | pass | `pre-3.log`: the package check, exit 0 |
| PRE-4 | MUST | pass | `pre-4.log` and `step3-static.log`: no core project references MudBlazor or Fluxor |
| PRE-5 | MUST | pass | `step0-static.log`: the projects line of `playwright.config.mjs` |
| PRE-6 | MUST | pass | `step0-static.log`: `git status --porcelain -- src/` empty |
| FN-1 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/GridRenderingTests.cs:22 The_window_rows_render_one_ex_row_each_in_order`, `tests/ExGrid.Components/GridSourceBindingTests.cs:27 A_source_alone_paints_its_window` (+1). Not asserted at layers 1–2: Rows painted match the pushed Window, WindowStart/TotalCount placeholders are checked; no test asserts that no component field retains a Window after the parameter changes. |
| FN-2 | MUST | pass | L1/2: `tests/ExGrid.Components/GridSourceBindingTests.cs:81 A_source_passed_with_a_window_is_refused_by_name`, `tests/ExGrid.Components/GridSourceBindingTests.cs:93 Neither_a_window_nor_a_source_is_refused` |
| FN-3 | MUST | pass | L1/2: `ADR0183_At_a_margin_of_0_the_rows_on_screen_are_asked_for_once_they_leave_the_Window` |
| FN-3a | MUST | pass | L1/2: `ADR0183_A_Source_is_asked_before_the_Window_runs_out_as_the_push_form_is`, `ADR0183_A_margin_asks_for_the_rows_on_screen_before_the_Window_runs_out_at_its_end`, `ADR0183_A_margin_asks_for_the_rows_on_screen_before_the_Window_runs_out_at_its_start` (+10); L3: `pivot-db.spec.mjs:130`, `pivot-db.spec.mjs:193` |
| FN-4 | MUST | pass | L1/2: `ADR0183_A_Source_is_asked_before_the_Window_runs_out_as_the_push_form_is`, `FN4_from_and_fetch_paint_the_same_and_their_rows_render_the_same`, `FN4_the_grid_renders_more_under_fetch_by_exactly_the_states_fetch_pushes` |
| FN-5 | MUST | pass | L1/2: `tests/ExGrid.Tests/GridSourceFetchTests.cs:53 The_first_page_is_fetched_at_bind_time`, `tests/ExGrid.Tests/GridSourceFetchTests.cs:121 The_same_range_in_flight_is_coalesced` (+2) |
| FN-6 | MUST | pass | L3: `virtualisation.spec.mjs:680`; L1/2: `tests/ExGrid.Components/PinnedColumnTests.cs:55 The_pinned_columns_stay_painted_at_the_far_right`, `tests/ExGrid.Components/PinnedColumnTests.cs:147 The_pinned_columns_are_painted_through_a_fling` (+1) |
| FN-6a | MUST | pass | L1/2: `A_band_of_exactly_min_width_keeps_pinning`, `A_box_narrowed_under_the_pinned_block_suspends_pinning_until_it_widens`, `A_focus_moved_right_while_suspended_is_revealed` (+2); L3: `sizing.spec.mjs:308` |
| FN-7 | MUST | pass | L1/2: `tests/ExGrid.Components/CellStateTests.cs:18 An_answered_state_paints_on_that_cell_only`, `tests/ExGrid.Tests/CellStateTests.cs:36 Every_state_paints_a_distinct_class` (+2) |
| FN-7a | MUST | pass | L3: `mud.spec.mjs:203`; L1/2: `tests/ExGrid.Components/CellToneTests.cs:39 The_rule_paints_one_class_per_answer`, `tests/ExGrid.Components/CellToneTests.cs:53 Without_a_rule_a_negative_is_an_ordinary_cell` (+2); L3: `tests/ExGrid.Browser/mud.spec.mjs:203 'a loss is painted in the palette'` |
| FN-8 | MUST | pass | L1/2: `tests/ExGrid.Components/RowKindTests.cs:16 A_declared_role_paints_on_that_row_only`, `tests/ExGrid.Components/RowKindTests.cs:30 Without_a_delegate_every_row_is_a_detail_row` (+1) |
| FN-9 | MUST | pass | L1/2: `FN9_component_instances_grow_with_painted_rows_and_never_with_cells` |
| FN-10 | MUST | pass | L1/2: `tests/ExGrid.Tests/OverflowRuleTests.cs:21 An_overflowing_number_is_hashed`, `tests/ExGrid.Tests/OverflowRuleTests.cs:60 An_overflowing_date_is_hashed` (+4) |
| FN-11 | MUST | pass | L1/2: `tests/ExGrid.Tests/AutoWidthTests.cs:34 Shorter_observations_never_narrow_the_width`, `tests/ExGrid.Tests/AutoWidthTests.cs:42 The_same_observations_settle_identically_in_any_order` (+3) |
| FN-12 | MUST | pass | L1/2: `A_fixed_width_above_max_width_is_the_users_and_is_kept`, `A_fixed_width_below_min_width_is_refused_naming_the_column`, `A_spec_leaves_the_min_width_refusal_to_its_column` (+2); L3: `gestures.spec.mjs:129` |
| FN-12a | MUST | pass | L1/2: `A_double_click_fit_is_bounded_by_max_width`, `A_double_click_fits_over_the_whole_window_not_the_painted_rows`; L3: `sizing.spec.mjs:77` |
| FN-12b | MUST | pass | L1/2: `A_click_on_the_grip_reports_no_width`, `A_double_click_reports_only_the_fit`; L3: `sizing.spec.mjs:93` |
| FN-12c | MUST | pass | L1/2: `A_double_click_on_a_whole_column_selection_fits_each_column`, `A_drag_on_a_whole_column_selection_resizes_every_whole_column`, `A_grip_outside_the_whole_columns_resizes_only_itself` (+5); L3: `sizing.spec.mjs:273` |
| FN-12d | MUST | pass | L1/2: `A_double_click_fits_a_header_with_its_menu_band_sort_room_and_slack`, `An_auto_columns_header_counts_its_menu_band_sort_room_and_slack`; L3: `sizing.spec.mjs:47`, `sizing.spec.mjs:53` |
| FN-12e | MUST | pass | L1/2: `FN_12e_is_two_rows_and_WR_10_names_the_wrappers_and_the_demo_pages`, `Roboto_is_held_to_macOS_and_Windows_in_both_browsers`, `The_core_is_held_to_macOS_Windows_under_English_and_Japanese_in_both_browsers_and_DejaVu_Sans` (+1); L3: `part-c.spec.mjs:481`, `sizing.spec.mjs:62`; by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass; by hand, macOS: `verification/2026-10-18-macos-18b/report.md` at `28ab5e1bf8c1`, pass |
| FN-13 | MUST | pass | L1/2: `ADR0207_a_query_of_another_version_is_refused`, `FN13_a_decimal_operand_is_its_exact_digits_in_a_string`, `FN13_a_query_carries_its_version` (+9); inspection: pass (`inspect-rows.md`) |
| FN-14 | MUST | pass | L1/2: `tests/ExGrid.Tests/SortSemanticsTests.cs:22 Blanks_sort_last_ascending`, `tests/ExGrid.Tests/SortSemanticsTests.cs:12 Text_sorts_case_insensitively` (+2) |
| FN-15 | MUST | pass | inspection: pass (`inspect-rows.md`); `step3-static.log` and `inspect-rows.md` |
| FN-16 | MUST | pass | L1/2: `A_page_click_raises_a_range_request`, `The_pager_exists_only_under_a_page_size` |
| FN-17 | MUST | pass | L1/2: `A_stub_chrome_renders_but_the_core_still_decides`, `E_goes_to_the_operator_while_it_takes_no_value`, `FN17_swapping_chrome_changes_rendering_and_nothing_about_behaviour` (+3); L3: `mud-app.spec.mjs:450` |
| FN-18 | MUST | pass | L1/2: `The_core_refuses_an_operator_it_did_not_offer_from_any_chrome`, `The_menu_commands_are_the_cores`; inspection: pass (with an observation) (`inspect-rows.md`) |
| FN-19 | MUST | pass | L1/2: `FN19_a_selection_on_a_pinned_column_stays_inside_the_visible_range`, `FN19_a_selection_scrolled_out_sideways_is_reported_outside_the_visible_range` |
| FN-20 | SHOULD | pass | L1/2: `tests/ExGrid.Components/AccessibilityTests.cs:376 The_focused_value_is_the_raw_value_never_the_hashes` |
| FN-21 | MUST | pass | L3: `popovers.spec.mjs:874`, `popovers.spec.mjs:900`, `popovers.spec.mjs:928` (+2); L1/2: `tests/ExGrid.Components/PopoverKeyboardTests.cs:798 A_reported_inner_popup_reaches_the_key_gate_and_goes_with_the_popover`, `tests/ExGrid.Components/PopoverKeyboardTests.cs:902 A_popup_report_does_not_outlive_its_popover` (+2) |
| FN-22 | MUST | pass | L1/2: `FN22_a_change_in_place_waits_for_the_list_to_be_handed_again`, `FN22_a_column_added_in_place_repaints`, `FN22_a_column_moved_in_place_keeps_its_measured_auto_width` (+11) |
| UX-1 | MUST | pass | L1/2: `A_place_outside_the_palette_is_refused`, `The_grip_and_guide_widths_are_emitted_from_the_metrics`, `The_resize_guide_stands_on_a_device_pixel` (+4) |
| UX-2 | MUST | pass | L1/2: `The_geometry_tokens_are_inline_on_the_root`; L3: `presentation.spec.mjs:20` |
| UX-3 | MUST | pass | L3: `mud.spec.mjs:24`, `presentation.spec.mjs:48`, `presentation.spec.mjs:105` |
| UX-4 | MUST | pass | L3: `presentation.spec.mjs:714` |
| UX-5 | MUST | pass | L1/2: `ADR0160_LV22_the_grid_holds_no_display_row_of_an_earlier_window`, `FN22_lists_handed_again_unchanged_render_nothing`, `UX5_a_row_replaced_in_the_window_in_place_still_repaints` (+1); L3: `presentation.spec.mjs:185` |
| UX-6 | MUST | pass | L1/2: `Nothing_in_the_core_stylesheet_transitions_or_animates`, `The_wrapper_transitions_and_animates_nothing`; L3: `copied-range.spec.mjs:42`, `mud.spec.mjs:45`, `presentation.spec.mjs:201` |
| UX-7 | MUST | pass | L1/2: `The_forced_colors_block_restates_the_change_highlight`, `Under_forced_colours_no_tint_is_painted`; L3: `presentation.spec.mjs:217` |
| UX-8 | MUST | pass | L1/2: `The_stale_value_colour_keeps_body_texts_contrast_and_stays_muted`; L3: `pivot-live.spec.mjs:196`, `presentation.spec.mjs:394` |
| UX-9 | MUST | pass | L3: `gestures.spec.mjs:200`, `mud.spec.mjs:79`, `mud.spec.mjs:160` |
| UX-10 | SHOULD | pass | L3: `gestures.spec.mjs:225`, `stripes.spec.mjs:275` |
| UX-11 | MUST | pass | L1/2: `A_column_popover_is_bounded_by_the_grids_box`; L3: `mud-app.spec.mjs:450`, `popovers.spec.mjs:213`, `popovers.spec.mjs:235` |
| UX-11a | MUST | pass | L1/2: `A_column_menu_with_a_row_of_room_stays`, `A_column_menu_with_less_than_a_row_of_room_closes`, `A_context_menu_with_no_room_closes_and_the_root_takes_the_keyboard` (+1); L3: `stretch.spec.mjs:116`; L3: `tests/ExGrid.Browser/stretch.spec.mjs:116 'a column menu with less than a row of room closes'` |
| UX-11b | MUST | pass | L3: `sizing.spec.mjs:288` |
| UX-12 | MUST | pass | every row of §4.1 (A11Y-*) below |
| UX-13 | MUST | pass | L3: `mud.spec.mjs:243` |
| UX-14 | MUST | pass | L3: `features.spec.mjs:1324` |
| UX-15 | MUST | pass | L1/2: `Placeholders_of_a_fling_are_striped_by_their_position`, `Placeholders_outside_the_window_are_striped_by_their_position`, `Stripes_are_off_by_default` (+2); L3: `appearance.spec.mjs:380`, `stripes.spec.mjs:87` |
| UX-16 | MUST | pass | L1/2: `A_lined_missing_cell_keeps_its_states_tint_and_its_lines`, `A_row_kind_counts_in_the_parity`, `The_change_highlight_is_a_tint_ordered_against_the_other_grounds`; L3: `stripes.spec.mjs:111`, `stripes.spec.mjs:226`, `stripes.spec.mjs:258` |
| UX-17 | MUST | pass | L3: `selection-look.spec.mjs:76` |
| UX-18 | MUST | pass | L1/2: `Beside_the_pinned_block_the_left_stays_inside`, `The_focus_outlines_width_and_inward_offset_are_written_once`; L3: `selection-look.spec.mjs:110`, `selection-look.spec.mjs:131`, `selection-look.spec.mjs:151` |
| UX-19 | MUST | pass | L1/2: `A_one_cell_selection_is_the_focus_outline_alone`, `A_single_range_is_one_outlined_rectangle_with_a_hole_at_the_focus`, `Among_several_ranges_the_focus_border_is_one_device_pixel_in_its_own_token` (+8); L3: `mud.spec.mjs:160`, `selection-look.spec.mjs:181`, `selection-look.spec.mjs:194` (+9) |
| UX-20 | MUST | pass | L3: `presentation.spec.mjs:345` |
| UX-21 | MUST | pass | L3: `heading-mark.spec.mjs:75`, `heading-mark.spec.mjs:122`, `heading-mark.spec.mjs:164` (+8); L1/2: `tests/ExGrid.Components/HeadingMarkTests.cs:293 ADR0008_a_range_is_one_box_per_axis`, `tests/ExGrid.Components/HeadingMarkTests.cs:305 ADR0008_several_ranges_mark_each_ranges_headings_and_neighbours_join` (+6) |
| A11Y-1 | MUST | pass | L1/2: `The_roles_are_present` |
| A11Y-2 | MUST | pass | L1/2: `The_row_count_is_the_total_with_the_header_row`; L3: `aria-rows.spec.mjs:139`, `aria-rows.spec.mjs:161` |
| A11Y-3 | MUST | pass | L1/2: `At_the_top_the_header_row_is_one_and_the_data_rows_follow_it`, `Far_down_a_rows_index_is_its_position_after_the_header_row`; L3: `aria-rows.spec.mjs:100`, `aria-rows.spec.mjs:139`, `aria-rows.spec.mjs:161` |
| A11Y-4 | MUST | pass | L1/2: `ADR0080_a_display_only_grid_has_no_field_and_its_root_is_the_tab_stop`, `ADR0080_a_grid_that_edits_has_one_keyboard_field_and_it_is_the_tab_stop`, `ADR0080_the_headers_menu_buttons_are_out_of_the_tab_sequence` (+3); L3: `features.spec.mjs:1075`, `key-field.spec.mjs:517`, `key-field.spec.mjs:723` (+2) |
| A11Y-5 | MUST | pass | L1/2: `ADR0080_a_display_only_grid_keeps_aria_activedescendant_on_its_root`, `ADR0080_the_field_carries_aria_activedescendant_and_the_root_none`, `ADR0080_the_sheets_keyboard_field_is_the_tab_stop_and_names_the_focus_cell` (+3); L3: `key-field.spec.mjs:789` |
| A11Y-6 | MUST | pass, accepted as is | L1/2: `ADR0080_the_fields_aria_activedescendant_is_cleared_while_the_focus_is_not_painted`, `Activedescendant_clears_when_the_focus_leaves_the_paint`; L3: no test. Not asserted at layer 3: No Playwright test scrolls by wheel until the Focus row leaves and asserts aria-activedescendant is cleared and restored; only the Layer 2 half (AccessibilityTests.Activedescendant_clears_when_the_focus_leaves_the_paint) exists. |
| A11Y-7 | MUST | pass | L1/2: `A_hashed_cells_accessible_name_is_the_value`, `The_focused_value_is_the_raw_value_never_the_hashes` |
| A11Y-8 | MUST | pass | L1/2: `Nothing_carries_aria_selected` |
| A11Y-9 | MUST | pass, accepted as is | L1/2: `The_live_region_announces_once_per_settled_selection`; L3: `tests/ExGrid.Browser/features.spec.mjs:370 '3 rows by 1 columns selected'`, `tests/ExGrid.Browser/page-follow.spec.mjs:136 'rows by 1 columns selected'`. Not asserted at layer 3: Specs read the announced text after a selection settles, but none drags across 200 rows and counts live-region writes (only the bUnit AccessibilityTests.The_live_region_announces_once_per_settled_selection). |
| A11Y-10 | MUST | pass, accepted as is | L3: `tests/ExGrid.Browser/features.spec.mjs:383 'toHaveText('')'`. Not asserted at layer 3: Only a collapse to one cell is shown to leave the region empty (features.spec); no test presses plain arrows and asserts the live region unchanged. |
| A11Y-11 | MUST | pass | L1/2: `Placeholder_rows_carry_aria_busy` |
| A11Y-12 | MUST | pass | L1/2: `The_selection_overlay_is_aria_hidden`, `The_shade_layer_is_hidden_from_assistive_technology` |
| A11Y-13 | MUST | pass | L1/2: `A_group_cell_carries_its_colspan` |
| A11Y-14 | MUST | pass | L1/2: `Announcing_costs_no_row_render` |
| A11Y-15 | MUST | pass | L1/2: `A11Y15_an_accept_or_a_flag_writes_nothing_into_the_live_region`, `A11Y15_each_reject_is_written_once_into_the_live_region`, `A_reject_is_announced_with_the_consumers_own_sentence` |
| A11Y-16 | MUST | pass | L3: `features.spec.mjs:440`; L3: `tests/ExGrid.Browser/features.spec.mjs:440 'a refusal is announced in the root'` |
| A11Y-17 | MUST | pass | L1/2: `Action_buttons_are_out_of_the_tab_sequence`; L3: `features.spec.mjs:1302` |
| A11Y-18 | MUST | pass | L1/2: `The_chosen_button_is_named_only_while_it_is_painted` |
| A11Y-19 | MUST | pass | L1/2: `Each_popover_is_named`, `Every_select_names_the_element_that_takes_its_keyboard`, `In_the_grid_a_command_run_from_the_mud_menu_closes_it`; L3: `popovers.spec.mjs:825` |
| A11Y-20 | MUST | pass | L1/2: `ADR0080_a_prerendered_grid_that_edits_has_no_field_and_no_tab_stop`, `ADR0080_the_field_waits_for_the_key_listener`, `A_prerendered_grid_is_busy_and_out_of_the_tab_sequence` (+3); L3: `circuit.spec.mjs:375` |
| A11Y-21 | MUST | pass | L3: `key-field.spec.mjs:618`, `key-field.spec.mjs:853` |
| HG-1 | MUST | pass | L1/2: `HG1_members_declared_scrambled_paint_in_the_columns_order`, `Members_resolve_in_the_flat_order_not_the_declarations` |
| HG-2 | MUST | pass | L1/2: `The_rectangle_spans_its_members_and_tiers` |
| HG-3 | MUST | pass | L1/2: `An_uncovered_column_stretches_the_full_band`, `An_uncovered_leaf_is_band_tall` |
| HG-4 | MUST | pass | L1/2: `An_unknown_member_is_refused_by_name` |
| HG-5 | MUST | pass | L1/2: `HG5_a_reorder_that_separates_the_members_is_refused`, `Non_adjacent_members_are_refused` |
| HG-6 | MUST | pass | L1/2: `Overlapping_rectangles_are_refused` |
| HG-7 | MUST | pass | L1/2: `A_pinned_straddle_is_refused` |
| HG-8 | MUST | pass | L1/2: `The_band_leaves_the_row_arithmetic_alone` |
| HG-9 | MUST | pass | L1/2: `A_viewport_under_the_band_is_refused_naming_the_band` |
| HG-10 | MUST | pass | L1/2: `A_rectangle_stands_when_its_leaves_are_virtualised_away`, `HG10_a_rectangle_stands_when_its_first_members_are_virtualised_away` |
| HG-11 | MUST | pass | L1/2: `A_pushed_declaration_is_resolved_again`, `Membership_resolves_once_per_push_and_never_while_scrolling` |
| HG-12 | MUST | pass | L1/2: `A_long_label_gets_the_text_treatment_and_grows_nothing`, `HG12_a_long_group_label_feeds_no_auto_width_and_gets_the_ellipsis` |
| HG-13 | MUST | pass | L1/2: `Nothing_public_offers_a_vertical_alignment`, `The_shipped_stylesheet_aligns_nothing_vertically`; L3: `presentation.spec.mjs:510` |
| HG-14 | MUST | pass | L1/2: `A_leaf_inside_a_group_clamps_at_its_edges`; L3: `gestures.spec.mjs:22`, `gestures.spec.mjs:44` |
| HG-15 | MUST | pass | L1/2: `Reorder_moves_the_unit_whole`, `The_declaration_is_not_rewritten_by_resolution`; L3: `gestures.spec.mjs:75` |
| HG-16 | MUST | pass | L1/2: `A_group_click_does_not_sort_the_leaf_under_it`, `HG16_nothing_below_the_band_knows_tiers_exist` |
| HG-17 | MUST | pass | L1/2: `Cost_is_per_rectangle_never_per_column`, `HG17_three_groups_over_forty_columns_cost_per_rectangle_and_no_row_renders` |
| DIR-1 | MUST | pass | L1/2: `tests/ExGrid.Components/GridRenderingTests.cs:75 (ADR-0031 test asserting dir="ltr" on .ex-grid)` |
| DIR-2 | MUST | pass | L3: `presentation.spec.mjs:417` |
| DIR-3 | MUST | pass | L3: `presentation.spec.mjs:473` |
| DIR-4 | SHOULD | pass | L1/2: `DIR4_the_shipped_stylesheet_uses_no_logical_property`; inspection: pass (`inspect-rows.md`) |
| VZ-1 | MUST | pass | L3: `virtualisation.spec.mjs:209` |
| VZ-2 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/VirtualisationTests.cs:31 Only_the_viewport_slice_renders_rows_however_large_the_total`. Not asserted at layers 1–2: FindComponents<ExGridRow<T>>().Count equals RowsPerViewport at one large total (100,000); not swept over several TotalCounts. |
| VZ-3 | MUST | pass | L1/2: `tests/ExGrid.Tests/ViewportGeometryTests.cs:22 Rows_per_viewport_covers_the_partly_visible_row`, `tests/ExGrid.Tests/ViewportGeometryTests.cs:36 A_fractional_row_height_divides_exactly` (+1) |
| VZ-4 | MUST | pass | L1/2: `tests/ExGrid.Components/HorizontalVirtualisationTests.cs:86 Only_the_columns_the_viewport_reaches_are_painted`, `tests/ExGrid.Components/HorizontalVirtualisationTests.cs:106 The_header_and_the_rows_keep_the_same_column_set_while_panning` (+3) |
| VZ-5 | MUST | pass | L1/2: `tests/ExGrid.Components/FlingTests.cs:31 A_fling_paints_placeholders_until_scrolling_settles`, `tests/ExGrid.Components/FlingTests.cs:54 Scrolling_again_before_the_delay_keeps_the_placeholders` (+1) |
| VZ-6 | MUST | pass | L1/2: `tests/ExGrid.Components/HorizontalVirtualisationTests.cs:135 Virtualisation_off_paints_every_column_and_no_spacer`, `tests/ExGrid.Components/HorizontalVirtualisationTests.cs:54 A_sideways_fling_paints_no_placeholders_when_the_columns_are_not_virtualised` (+1) |
| VZ-7 | MUST | pass | L1/2: `tests/ExGrid.Components/VirtualisationTests.cs:40 The_spacer_spans_the_whole_result_and_the_viewport_is_offset_by_the_first_row` |
| VZ-8 | MUST | pass | L1/2: `A_million_rows_fit_at_28px_and_are_refused_by_name_at_40px`, `The_refusal_is_at_the_scale_one_layout_ceiling`, `The_scale_one_ceiling_compresses_nothing_that_is_accepted` |
| VZ-9 | MUST | pass | L1/2: `tests/ExGrid.Tests/ViewportBoxTests.cs:14 A_gutter_of_zero_leaves_the_declared_size_untouched`, `tests/ExGrid.Tests/ViewportBoxTests.cs:35 A_classic_scrollbar_comes_off_the_declared_size` (+3); L3: `tests/ExGrid.Browser/scrollbar.spec.mjs:240 'with scrollbars that occupy layout'`, `tests/ExGrid.Browser/scrollbar.spec.mjs:230 'with the platform'` |
| VZ-10 | MUST | pass | L3: `tests/ExGrid.Browser/scrollbar.spec.mjs:266 'at every zoom level, after moving again'` |
| VZ-11 | MUST | pass | L1/2: `A_density_change_re_anchors_on_the_first_visible_row` |
| VZ-12 | MUST | pass | L1/2: `A_declared_viewport_shorter_than_the_header_is_refused`, `A_release_built_without_the_definition_of_done_is_an_error`, `A_release_candidate_s_blocks_are_checked_as_a_release_s_are` (+2) |
| VZ-12a | MUST | pass | L1/2: `A_stretch_height_takes_the_parents_height`; L3: `stretch.spec.mjs:35`, `stretch.spec.mjs:47`, `stretch.spec.mjs:90` |
| VZ-12b | MUST | pass | L1/2: `A_parent_with_no_height_is_named_once_until_a_real_height_arrives`; L3: `stretch.spec.mjs:74` |
| VZ-13 | MUST | pass | L1/2: `tests/ExGrid.Components/VirtualisationTests.cs:235 A_paged_result_of_any_size_is_not_refused`, `tests/ExGrid.Components/PagerTests.cs:205 (ADR-0028 re-anchor is page-local)` |
| VZ-14 | MUST | pass | L1/2: `A_candidate_is_refused_on_a_MudBlazor_version_its_floor_record_did_not_run`, `A_git_given_a_subdirectory_reads_the_paths_from_the_root`, `A_record_whose_tag_the_remote_lacks_is_refused` (+6); L3: `tests/ExGrid.Browser/scrollbar.spec.mjs:345 'the Focus stays readable with the OS doing the scaling'`; by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass |
| VZ-15 | MUST | pass | L1/2: `A_ceiling_under_the_true_height_caps_the_spacer`, `Under_the_ceiling_the_geometry_is_unchanged`; L3: `virtualisation.spec.mjs:154` |
| VZ-16 | MUST | pass | L1/2: `The_record_index_and_the_table_name_the_same_rows_and_paths`, `Told_the_root_carries_the_device_pixel_and_edges_lie_on_it`; L3: `device-pixel-225.spec.mjs:37`; by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass |
| VZ-17 | MUST | pass | L1/2: `Edges_are_the_declared_positions_rounded_once`, `Every_column_edge_lies_on_a_device_pixel`, `Told_the_root_carries_the_device_pixel_and_edges_lie_on_it`; L3: `part-c.spec.mjs:449` |
| VZ-18 | MUST | pass | L1/2: `A_known_limitation_of_the_version_is_reported_not_refused`, `A_known_limitation_only_the_table_says_is_refused`, `A_known_limitation_that_is_not_an_object_is_refused` (+4); L3: `virtualisation.spec.mjs:79`, `virtualisation.spec.mjs:113`; by hand, the remote-isolation PC: not run, a Known limitation of 0.1.0 (ADR-0202) |
| VZ-19 | MUST | pass | L1/2: `A_double_lines_middle_shows_what_its_gridline_would`, `A_number_with_a_right_line_keeps_its_device_pixel`, `No_hash_decision_moves` (+3); L3: `sheet-paper.spec.mjs:711` |
| FL-1 | MUST | pass | inspection: pass (by inspection; compile judged elsewhere) (`inspect-rows.md`); `inspect-rows.md`; `ExGrid.MudBlazor`, a Chrome built on the context alone, compiles in PRE-1 with no `InternalsVisibleTo` |
| FL-2 | MUST | pass | L1/2: `The_columns_own_filter_is_excluded`, `The_list_reflects_the_other_columns_filters` |
| FL-3 | MUST | pass | L1/2: `Too_many_degrades_to_a_condition_with_search` |
| FL-4 | MUST | pass | L1/2: `The_panel_fetches_once_and_applies_on_ok` |
| FL-5 | MUST | pass | L1/2: `tests/ExGrid.Tests/FilterCombinationTests.cs:8 Columns_combine_with_and`, `tests/ExGrid.Tests/FilterCombinationTests.cs:35 Clauses_within_a_column_can_combine_with_or` |
| FL-6 | MUST | pass | L1/2: `FL6_an_offered_operator_matches_by_its_types_semantics`, `FL6_every_operator_on_every_column_type_is_either_tested_or_refused` |
| FL-7 | MUST | pass | L1/2: `An_opaque_filter_survives_a_panel_apply` |
| FL-8 | MUST | pass | L1/2: `Applying_a_filter_clears_the_selection` |
| FL-9 | MUST | pass | L1/2: `A_list_in_force_outside_the_domain_opens_with_nothing_to_apply` |
| FL-10 | MUST | pass | L1/2: `A_hidden_checked_value_is_not_applied`, `A_search_applies_only_the_matching_checked_values`, `A_search_narrows_what_ok_applies` (+3) |
| FL-11 | MUST | pass | L1/2: `Select_all_is_tri_state_over_the_values_shown`, `Select_all_reads_and_toggles_the_values_shown`, `Select_all_reads_the_values_shown` (+1) |
| FL-12 | MUST | pass | L1/2: `ADR0080_a_press_and_alt_down_still_open_the_columns_popover`, `In_the_grid_the_mud_menu_stands_above_the_mud_panel`, `Tab_moves_into_the_filter_below` (+2); L3: `popovers.spec.mjs:572`, `popovers.spec.mjs:605` |
| FL-13 | MUST | pass | L1/2: `Add_current_selection_joins_the_filter_in_force`, `Add_is_offered_while_searching_a_value_filter`, `Added_to_the_filter_in_force_the_matches_join_it` (+3) |
| FL-14 | MUST | pass | L1/2: `A_two_clause_filter_reopens_as_two_conditions`, `An_unfinished_second_condition_leaves_the_first`, `Two_conditions_apply_as_one_spec` (+2) |
| FL-15 | MUST | pass | L1/2: `A_label_is_marked_at_its_letter_or_given_it`, `A_letter_on_a_disabled_sort_does_nothing`, `A_substituted_panel_hands_its_letters_to_the_core` (+14); L3: `popovers.spec.mjs:635`, `popovers.spec.mjs:679`, `popovers.spec.mjs:714` |
| FL-16 | MUST | pass | L1/2: `C_clears_the_filter_only_while_it_is_enabled`, `Cancel_is_the_cores_exit_and_there_is_no_clear`, `Clear_filter_clears_and_closes` (+2); L3: `popovers.spec.mjs:635` |
| SR-1 | MUST | pass | L1/2: `A_header_click_raises_the_next_sorts_list`, `A_plain_click_on_a_header_still_sorts`, `Plain_released_on_the_same_column_it_sorts`; L3: `features.spec.mjs:1130`, `sizing.spec.mjs:116` |
| SR-2 | MUST | pass | L1/2: `tests/ExGrid.Tests/SelectionKeyboardTests.cs:88 Ctrl_space_selects_whole_columns`, `tests/ExGrid.Tests/SelectionKeyboardTests.cs:78 Ctrl_shift_down_from_the_first_row_selects_the_whole_column` (+2) |
| SR-2a | MUST | pass | L1/2: `Shift_click_on_a_header_from_empty_selects_that_column`, `Shift_click_on_a_header_selects_whole_columns_and_does_not_sort`, `Shift_click_on_a_header_selects_whole_columns_from_the_focus` (+2); L3: `sizing.spec.mjs:101` |
| SR-2b | MUST | pass | L1/2: `Ctrl_shift_right_on_a_whole_column_runs_to_the_edge_whole`, `Shift_down_on_a_whole_row_keeps_every_column`, `Shift_left_past_the_focus_flips_and_stays_whole` (+2) |
| SR-2c | MUST | pass | L1/2: `A_header_shift_click_then_shift_right_never_scrolls_down`, `A_plain_arrow_from_a_whole_column_still_reveals_the_focus`, `Ctrl_backspace_still_reveals_the_focus_of_a_whole_column` (+13); L3: `headings.spec.mjs:48`, `headings.spec.mjs:71`, `headings.spec.mjs:92` (+2) |
| SR-2d | MUST | pass | L1/2: `Plain_a_click_on_another_header_ends_the_press_as_a_drag`, `Plain_a_drag_released_back_on_the_pressed_column_does_not_sort`, `Plain_a_press_on_a_header_selects_nothing` (+5); L3: `sizing.spec.mjs:116`, `sizing.spec.mjs:128`, `sizing.spec.mjs:152` (+1) |
| SR-2e | MUST | pass | L1/2: `A_column_covered_by_several_ranges_is_wholly_selected`, `Ctrl_click_from_empty_selects_the_column`, `Ctrl_click_on_a_column_adds_it_as_a_new_range` (+14); L3: `headings.spec.mjs:369`, `headings.spec.mjs:387`, `sizing.spec.mjs:180` (+2) |
| SR-3 | MUST | pass | L1/2: `The_painted_order_does_not_move_until_a_new_window_arrives` |
| SR-4 | MUST | pass | L1/2: `A_sort_that_leaves_the_order_alone_keeps_the_selection`, `A_sort_that_moves_the_order_drops_the_selection` |
| SR-5 | MUST | pass | L1/2: `tests/ExGrid.Tests/SortSemanticsTests.cs:22 Blanks_sort_last_ascending`, `tests/ExGrid.Tests/SortSemanticsTests.cs:32 Blanks_sort_last_descending` (+3) |
| SR-6 | MUST | pass | L1/2: `Aria_sort_follows_the_applied_sort` |
| ED-1 | MUST | pass | L1/2: `The_first_character_opens_the_editor_containing_it`, `The_look_renders_no_row_and_reaches_no_javascript`, `Typing_opens_one_floating_editor` |
| ED-2 | MUST | pass | L1/2: `ADR0051_where_a_reference_can_go_a_primary_modifier_arrow_points_at_the_edge`, `ADR0051_while_pointing_a_primary_modifier_arrow_moves_the_outline_to_the_edge`, `A_held_chord_left_to_an_edit_is_replayed_as_the_field_would_handle_it` (+18); L3: `declarations.spec.mjs:1355`, `declarations.spec.mjs:1542`, `declarations.spec.mjs:1564` (+12) |
| ED-3 | MUST | pass | L1/2: `Escape_escape_then_a_character_opens_an_edit_in_the_selected_cell`; L3: `features.spec.mjs:71` |
| ED-4 | MUST | pass | L1/2: `ADR0080_a_compositions_text_opens_overwrite_holding_it`, `The_first_character_opens_the_editor_containing_it`; L3: `features.spec.mjs:33`, `mud.spec.mjs:271` |
| ED-5 | MUST | pass | L1/2: `A_commit_is_an_intent_and_paints_nothing` |
| ED-6 | MUST | pass | L1/2: `The_editors_text_and_the_selection_outlive_a_change_of_the_row_under_them`, `The_text_survives_a_window_pushed_mid_edit` |
| ED-7 | MUST | pass | L1/2: `A_geometry_change_moves_the_editor_and_keeps_the_text` |
| ED-8 | MUST | pass | L1/2: `Editing_waits_for_the_row` |
| ED-9 | MUST | pass | L3: `presentation.spec.mjs:527` |
| ED-10 | MUST | pass | L1/2: `Ctrl_enter_fills_the_selection_as_one_intent` |
| ED-11 | MUST | pass | L3: `gestures.spec.mjs:268` |
| ED-12 | MUST | pass | L1/2: `A_click_away_under_a_reject_selects_nothing` |
| ED-13 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/CellEditorTests.cs:388 An_altgr_character_opens_the_editor`, `tests/ExGrid.Components/CellEditorTests.cs:400 A_lone_modifier_chord_does_not_open_the_editor`. Not asserted at layers 1–2: C# mirror: Control+Alt opens the editor and either alone opens nothing; the JS gate in ex-grid.js is only grep-pinned for the metaIsPrimary byWord rule (ShippedStylesheetTests:1711), not tested for AltGr. |
| ED-14 | MUST | pass | L1/2: `A_column_without_a_verdict_commits_exactly_as_before`, `The_verdict_is_asked_with_the_row_and_the_text` |
| ED-15 | MUST | pass | L1/2: `A_commit_that_moves_is_followed`, `A_reject_holds_the_editor_against_a_header_click`, `A_reject_holds_the_editor_against_a_header_press` (+6); L3: `features.spec.mjs:955`, `features.spec.mjs:971` |
| ED-16 | MUST | pass | L1/2: `A_flag_raises_the_intent_unchanged`; L3: `features.spec.mjs:990` |
| ED-17 | MUST | pass | L1/2: `Continuous_arrow_movement_stays_quiet`, `The_message_is_never_asked_for_while_painting`, `The_popover_opens_once_the_focus_has_stood_still`; L3: `features.spec.mjs:990` |
| ED-18 | MUST | pass | L1/2: `A_clipboard_paste_never_runs_the_verdict` |
| ED-17b | MUST | pass | L1/2: `The_pointer_coming_to_rest_opens_the_message`, `The_pointer_leaving_closes_the_message`; L3: `features.spec.mjs:1007` |
| ED-17c | MUST | pass | inspection: pass (wording note) (`inspect-rows.md`) |
| ED-19 | MUST | pass, accepted as is | L1/2: `A_refused_fill_holds_the_editor_and_enter_still_commits_the_one_cell`; L3: `tests/ExGrid.Browser/features.spec.mjs:419 'Ctrl+Enter fill across a non-editable column is refused'`. Not asserted at layer 3: Editor stands with the typed 7 after the refused fill and nothing is written; the follow-up that Enter then raises exactly one Edit Intent is not asserted at Layer 3. |
| ED-20 | MUST | pass | L1/2: `A_fill_runs_the_verdict_once_and_a_reject_stops_it` |
| ED-21 | MUST | pass | L1/2: `A_column_that_stops_being_editable_mid_edit_discards_rather_than_commits`, `A_commit_whose_handler_changes_the_columns_ends_as_committed`, `A_commit_whose_handler_hands_over_another_source_and_other_columns_is_said_at_most_once` (+11) |
| ED-22 | MUST | pass | L1/2: `A_clipboard_key_is_let_through_a_hold_and_its_event_is_held`, `A_press_on_the_rows_while_editing_holds_the_keys_after_it_until_answered`, `A_press_replayed_after_the_enter_it_followed_is_not_carried_past_by_it` (+8); L3: `circuit.spec.mjs:497`, `declarations.spec.mjs:933`, `declarations.spec.mjs:1138` (+15) |
| ED-23 | MUST | pass | L1/2: `Backspace_on_a_non_editable_cell_opens_nothing`, `Backspace_opens_an_empty_editor`, `Backspace_then_enter_commits_the_empty_text`; L3: `excel-keys.spec.mjs:97` |
| ED-24 | MUST | pass | L1/2: `Delete_over_a_non_editable_column_is_refused`, `Delete_raises_one_clear_intent_over_the_selection`; L3: `excel-keys.spec.mjs:68`, `excel-keys.spec.mjs:86` |
| ED-25 | MUST | pass | L1/2: `The_writing_keys_are_claimed_only_on_a_grid_that_edits`, `The_writing_keys_are_taken_only_on_a_grid_that_edits`; L3: `excel-keys.spec.mjs:51`, `excel-keys.spec.mjs:143` |
| ED-26 | MUST | pass | L1/2: `A_passed_on_press_marks_the_bar_only_until_the_core_has_answered_it`, `A_press_back_on_the_rows_brings_the_keyboard_back_in_the_existing_mousedown`, `A_press_back_on_the_rows_points_and_escape_then_cancels` (+7); L3: `edit-stands.spec.mjs:60`, `edit-stands.spec.mjs:96`, `edit-stands.spec.mjs:118` (+8) |
| ED-27 | MUST | pass | L1/2: `An_edit_whose_keyboard_is_elsewhere_is_drawn_with_a_1px_outline`, `On_a_sheet_the_cell_editor_draws_no_outline_while_the_keyboard_is_in_it`; L3: `edit-stands.spec.mjs:365`, `edit-stands.spec.mjs:427`, `sheet-paper.spec.mjs:336` |
| ED-28 | MUST | pass | L1/2: `A_chromes_controls_ask_through_their_contexts`, `A_reject_asks_the_module_again`, `A_request_in_the_formula_bar_asks_the_module_for_the_bar` (+7); L3: `tests/ExGrid.Browser/edit-stands.spec.mjs:471 '= typed on the left and the right pressed at once, thirty times'`, `tests/ExGrid.Browser/edit-stands.spec.mjs:497 '=1 typed at full speed and the right pressed at once'` (+1) |
| ED-29 | MUST | pass | L1/2: `DC45_F4_in_the_formula_bar_after_F2_leaves_a_caret`, `ED29_F2_after_the_edit_is_typed_in_the_cell_again_is_the_cells`, `ED29_F2_in_the_bar_goes_to_overwrite_and_home_then_commits_and_moves` (+13); L3: `declarations.spec.mjs:895`, `declarations.spec.mjs:933`, `declarations.spec.mjs:962` (+1) |
| ED-30 | MUST | pass | L1/2: `ADR0080_a_cancelled_composition_leaves_an_empty_edit_that_escape_cancels`, `ADR0080_a_cancelled_composition_opens_an_empty_edit`, `ADR0080_a_compositions_start_over_a_painted_focus_moves_nothing` (+10); L3: `key-field.spec.mjs:187`, `key-field.spec.mjs:225`, `key-field.spec.mjs:249` (+10); by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass |
| ED-31 | MUST | pass | L1/2: `A_press_is_resolved_against_the_layout_it_was_painted_under`, `A_press_on_a_held_handle_is_judged_at_the_scroll_it_was_taken_at`, `A_press_on_a_row_the_view_has_left_reveals_it` (+14); L3: `circuit.spec.mjs:529` |
| SL-1 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Tests/SelectionGestureTests.cs:85 Select_all_is_one_rectangle_over_the_whole_grid`, `tests/ExGrid.Tests/SelectionQueryTests.cs:22 CellCount_needs_no_data` (+1). Not asserted at layers 1–2: Ctrl+A is one rectangle (asserted at 100x26) and the 10^6x50 count is computed, but Ranges.Count==1 is not asserted at 10^6x50 and per-cell allocation is shown only by element/render counts. |
| SL-2 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Tests/SelectionQueryTests.cs:22 CellCount_needs_no_data`, `tests/ExGrid.Components/SelectionTests.cs:183 Selecting_the_whole_result_costs_what_eleven_cells_cost`. Not asserted at layers 1–2: Whole-result selection succeeds at 10^6x50 (count) and in the grid, but no test is named for the absence of a cap at larger sizes. |
| SL-3 | MUST | pass | L1/2: `tests/ExGrid.Components/SelectionTests.cs:286 The_selection_is_dropped_when_the_row_sequence_version_changes`, `tests/ExGrid.Components/SelectionTests.cs:308 The_selection_is_dropped_when_the_visible_columns_change` (+1) |
| SL-4 | MUST | pass | L1/2: `tests/ExGrid.Components/SelectionTests.cs:319 The_selection_survives_an_equivalent_column_list_built_again` |
| SL-5 | MUST | pass | L1/2: `A_take_out_moves_the_focus_to_the_range_made_last_wherever_cycling_had_moved_it`, `Fragments_are_listed_bottom_to_top_and_enter_visits_them_from_the_focus`, `Taking_out_a_cell_of_the_earlier_range_leaves_the_focus_in_the_range_made_last` (+2) |
| SL-6 | MUST | pass | L3: `virtualisation.spec.mjs:313`; L1/2: `tests/ExGrid.Components/SelectionTests.cs:38 A_click_paints_one_rectangle_over_the_cell_it_names`, `tests/ExGrid.Components/SelectionTests.cs:168 Selecting_does_not_repaint_a_single_row` (+1) |
| SL-7 | MUST | pass | L1/2: `SL7_selection_and_focus_outlive_the_rows_they_are_painted_over`, `SL7_uncommitted_editor_text_outlives_the_row_it_is_typed_over` |
| SL-8 | MUST | pass | L1/2: `tests/ExGrid.Tests/SelectionQueryTests.cs:22 CellCount_needs_no_data`, `tests/ExGrid.Tests/SelectionQueryTests.cs:30 CellCount_sums_areas_so_overlaps_double_count` (+1) |
| SL-9 | MUST | pass | L1/2: `Ctrl_a_selects_the_page_and_offers_the_whole_result` |
| SL-10 | MUST | pass | L1/2: `Turning_the_page_keeps_the_selection_and_shows_the_indicator` |
| SL-11 | MUST | pass, accepted as is | L1/2: `A_drag_at_the_page_boundary_stays_on_the_page`; L3: no test. Not asserted at layer 3: No Playwright test drags to a page boundary and checks the pager stays; only the bUnit PagerTests.A_drag_at_the_page_boundary_stays_on_the_page (L2, tagged SL-11) covers it. |
| SL-12 | MUST | pass | L1/2: `The_rate_rises_with_depth`, `Ticks_compound_and_the_selection_follows` |
| SL-13 | MUST | pass | L1/2: `The_auto_scroll_never_flings`, `The_ceiling_never_reaches_the_fling_threshold` |
| SL-14 | MUST | pass | L1/2: `Leaving_the_grid_stops_the_scroll`; L3: `presentation.spec.mjs:547` |
| SL-15 | MUST | pass | L1/2: `Returning_resumes_or_ends_by_the_button`; L3: `presentation.spec.mjs:547` |
| SL-16 | MUST | pass | L1/2: `Ctrl_a_keeps_the_focus_where_it_stands` |
| KB-1 | MUST | pass | L1/2: `Keys_in_another_grid_leave_this_grids_edit_standing`; L3: `tests/ExGrid.Browser/mud-app.spec.mjs:247 'the two grids on the main area stay independent'`, `tests/ExGrid.Browser/edit-stands.spec.mjs:71 'The positions grid's keys are its own (KB-1)'` |
| KB-2 | MUST | pass | L1/2: `KB2_the_script_names_no_set_of_keys`; inspection: pass (against the row as restated 2026-10-18; with a note) (`inspect-rows.md`) |
| KB-3 | MUST | pass, accepted as is | L1/2: `Command_enter_fills_where_meta_is_command`, `Meta_enter_where_meta_is_not_command_is_not_the_editors`, `The_gate_reads_a_known_limitation_against_the_tags_version` (+2); L3: `tests/ExGrid.Browser/sizing.spec.mjs:216 'Meta counts as Ctrl on a header only where Meta is Command'`, `tests/ExGrid.Browser/features.spec.mjs:419 'Ctrl+Enter fill across a non-editable column is refused'`; by hand, macOS: `verification/2026-10-18-macos-18b/report.md` at `28ab5e1bf8c1`, pass. Not asserted at layer 3: Meta+click on a header is platform-conditioned, and ControlOrMeta+Enter exercises Cmd+Enter on macOS; no test presses Win/Meta+ArrowDown off Apple or Meta+Enter doing nothing, and no macOS run is shown. |
| KB-4 | MUST | pass | L1/2: `KB4_the_mouse_and_the_keyboard_agree_on_the_primary_modifier` |
| KB-5 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Tests/SelectionKeyboardTests.cs:11 Arrow_collapses_the_selection_and_moves_from_the_focus`, `tests/ExGrid.Tests/SelectionKeyboardTests.cs:37 Shift_arrow_grows_shrinks_and_flips_around_the_focus` (+6). Not asserted at layers 1–2: Arrows, Shift/Ctrl/Ctrl+Shift arrows are tested, Home/End only as key mapping plus Ctrl+Home/Ctrl+Shift+End/Home; not every row of the ADR-0012 table has a named test. |
| KB-6 | MUST | pass | L1/2: `tests/ExGrid.Tests/SelectionCyclingTests.cs:31 Enter_cycles_column_major_and_wraps`, `tests/ExGrid.Tests/SelectionCyclingTests.cs:43 Tab_cycles_row_major_and_wraps` |
| KB-7 | MUST | pass | L1/2: `tests/ExGrid.Tests/SelectionCyclingTests.cs:147 Single_cell_enter_and_tab_clamp_at_the_edge` |
| KB-8 | MUST | pass | L1/2: `ADR0080_the_release_of_tab_ends_when_dom_focus_leaves_the_grid`, `After_the_release_an_arrow_moves_and_tab_cycles`, `An_escape_with_nothing_left_to_dismiss_releases_tab_and_raises_on_leave` (+8); L3: `features.spec.mjs:101`, `features.spec.mjs:143`, `features.spec.mjs:160` (+9) |
| KB-9 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/GridKeyboardTests.cs:152 The_first_key_on_an_empty_selection_starts_where_the_eye_is`. Not asserted at layers 1–2: First ArrowDown places the Focus on the first visible cell (row 30 after scroll); does not assert the Viewport stays unmoved (no scroll-count check). |
| KB-10 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/GoToStartTests.cs:67 Control_home_returns_the_viewport_to_the_start`, `tests/ExGrid.Components/GoToStartTests.cs:80 Home_returns_the_viewport_to_the_start_of_the_row` (+1); L3: `tests/ExGrid.Browser/circuit.spec.mjs:805 'a far reveal paints rows in every frame ... (ADR-0012, ADR-0053)'`, `tests/ExGrid.Browser/scrollbar.spec.mjs:229 'the Focus is never behind a scrollbar (ADR-0012/0013)'`. Not asserted at layer 3: Ctrl+Home on /wide (2 pinned) lands the Focus on r0c0 and scrollTop 0; scrollbar spec deliberately avoids asserting scrollLeft; no pinned-vs-unpinned agreement or Home-to-start-of-row check in a browser. |
| KB-11 | MUST | pass, accepted as is | L3: `tests/ExGrid.Browser/features.spec.mjs:1171 'Escape returns the keyboard from a descendant control to the grid (ADR-0020/0012)'`. Not asserted at layer 3: Typing in the Template Column input works and Escape returns the keyboard; arrows moving the caret and not the selection are not asserted. |
| KB-12 | MUST | pass | L1/2: `KB12_the_roots_ring_is_drawn_from_the_scripts_mark_on_a_grid_that_edits`; L3: `features.spec.mjs:1075`, `key-field.spec.mjs:584`, `key-field.spec.mjs:833` |
| KB-13 | MUST | pass | L1/2: `Pageup_moves_focus_and_viewport_back_by_the_same_rows`, `Repeated_pagedown_keeps_the_focus_position_on_screen` |
| KB-14 | MUST | pass | L1/2: `Pagedown_at_the_end_clamps_and_keeps_the_focus_visible` |
| KB-14a | MUST | pass | L1/2: `A_shrinking_box_leaves_the_focus_where_it_is_until_a_key_moves_it` |
| KB-15 | MUST | pass | L3: `features.spec.mjs:1037` |
| KB-16 | MUST | pass | `step3-static.log`: prints nothing |
| KB-17 | MUST | pass | L1/2: `A_dismissing_press_on_the_rows_hands_the_keyboard_back_after_the_close`; L3: `popovers.spec.mjs:808` |
| KB-18 | MUST | pass | L1/2: `tests/ExGrid.Components/DescendantKeyTests.cs:25 Escape_from_a_descendant_reclaims_without_releasing_tab`; L3: `tests/ExGrid.Browser/features.spec.mjs:1171 'Escape returns the keyboard from a descendant control to the grid (ADR-0020/0012)'` |
| KB-19 | MUST | pass | L3: `tests/ExGrid.Browser/features.spec.mjs:1144 'a display-only grid does not take printable keys away from the page (ADR-0010/0020)'` |
| KB-20 | MUST | pass | L1/2: `Space_on_several_actions_chooses_the_first_and_keeps_the_keyboard_on_the_root`; L3: `features.spec.mjs:1206` |
| KB-21 | MUST | pass | L1/2: `Arrows_home_and_end_choose_within_the_cell_and_clamp`, `Space_fires_the_chosen_action_once_and_leaves`; L3: `features.spec.mjs:1206` |
| KB-22 | MUST | pass | L1/2: `A_key_that_moves_nothing_still_leaves`, `A_pointer_press_leaves`, `Enter_leaves_without_firing_and_keeps_its_meaning` (+2); L3: `features.spec.mjs:1234`, `features.spec.mjs:1246` |
| KB-23 | MUST | pass | L1/2: `A_recycled_row_never_sees_the_request_again`, `A_request_is_dropped_when_the_focus_moves_first`, `Space_on_a_template_hands_its_content_one_request` (+1); L3: `features.spec.mjs:1257`, `page-follow.spec.mjs:246` |
| KB-24 | MUST | pass | L3: `features.spec.mjs:1257`; L3: `tests/ExGrid.Browser/features.spec.mjs:1257 'Space puts the caret in a Template cell's field, and Escape brings the keyboard back (KB-23/KB-24, ADR-0037)'` |
| KB-25 | MUST | pass | L1/2: `Space_on_an_editable_cell_opens_overwrite_with_a_space`, `Space_with_no_focus_only_places_it` |
| KB-26 | MUST | pass | L1/2: `The_key_gate_drops_a_repeated_space`; L3: `features.spec.mjs:1274`, `features.spec.mjs:1288` |
| KB-27 | MUST | pass | L3: `features.spec.mjs:1340` |
| KB-36 | MUST | pass | L1/2: `Pressing_an_action_moves_no_focus`, `Space_firing_an_action_moves_no_focus`; L3: `tests/ExGrid.Browser/inspectors.spec.mjs:91 RI-4`, `tests/ExGrid.Browser/inspectors.spec.mjs:119 RI-5` (+3) |
| KB-28 | MUST | pass, accepted as is | L1/2: `Alt_down_does_nothing_without_a_focus_or_without_a_menu`, `Alt_down_opens_the_column_menu_and_nothing_near_it_does`, `Alt_down_opens_the_menu_of_the_focus_column`; L3: `popovers.spec.mjs:253`; L3: `tests/ExGrid.Browser/popovers.spec.mjs:253 'Alt+↓ opens the Focus column's menu, and the menu takes the keyboard (KB-28/KB-29, ADR-0039)'`. Not asserted at layer 3: Alt+ArrowDown opens the Focus column's menu with aria-label Book and focus in it; browser taking no action of its own and the no-menu column case are not asserted (title escapes the apostrophe, so ID scan missed it). |
| KB-29 | MUST | pass | L1/2: `A_menu_opened_by_its_button_takes_the_keyboard_too`, `A_menu_opened_over_a_waiting_panel_takes_the_keyboard`, `A_menu_opened_over_another_takes_the_keyboard` (+8); L3: `popovers.spec.mjs:253`, `popovers.spec.mjs:264` |
| KB-30 | MUST | pass | L1/2: `Down_moves_to_the_next_enabled_item`, `Down_wraps_at_the_end`, `Enter_and_space_run_the_focused_item` (+12); L3: `popovers.spec.mjs:528`, `popovers.spec.mjs:556`, `popovers.spec.mjs:572` (+2) |
| KB-31 | MUST | pass | L1/2: `Asked_for_its_last_control_the_panel_focuses_cancel`, `In_the_grid_tab_moves_from_the_mud_menu_to_the_mud_panel`, `Submitting_the_condition_applies_what_ok_applies` (+5); L3: `circuit.spec.mjs:313`, `popovers.spec.mjs:605`, `popovers.spec.mjs:762` |
| KB-32 | MUST | pass | L1/2: `A_chromes_own_close_returns_the_keyboard_to_the_root`, `Closing_the_panel_returns_the_keyboard_to_the_root`, `Escape_from_inside_returns_the_keyboard_to_the_root` (+2); L3: `popovers.spec.mjs:306`, `popovers.spec.mjs:928`, `popovers.spec.mjs:975` |
| KB-33 | MUST | pass | L3: `circuit.spec.mjs:135` |
| KB-34 | MUST | pass | L1/2: `A_substituted_chrome_resolves_keys_through_the_context`, `In_the_grid_keys_typed_together_run_the_item_they_chose`, `Keys_typed_together_in_a_menu_run_the_item_they_chose`; L3: `tests/ExGrid.Browser/circuit.spec.mjs:113 'keys typed together in a menu run the item the keys chose (ADR-0039, SRV-5)'` |
| KB-35 | MUST | pass | L3: `circuit.spec.mjs:263` |
| KB-40 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/ActiveCellWiringTests.cs:75 Extending_reveals_the_extent_not_the_focus`, `tests/ExGrid.Components/ActiveCellWiringTests.cs:90 Shrinking_back_inside_the_view_scrolls_nothing` (+1); L3: `tests/ExGrid.Browser/page-follow.spec.mjs:102 'KB-45: while extending, the page follows the Extent ...'`, `tests/ExGrid.Browser/page-follow.spec.mjs:114 'KB-45: extending past the grid's own Viewport ...' (Shift+PageDown)`. Not asserted at layer 3: Browser shows the Extent followed with the Focus left behind and Shift+PageDown carrying the view; the move-back-inside-scrolls-nothing clause is only tested for the Focus (loop test at ~71), not the Extent. |
| KB-41 | MUST | pass | L1/2: `tests/ExGrid.Tests/SelectionCyclingTests.cs:197 Shift_arrow_after_enter_moves_the_edge_opposite_the_focus`, `tests/ExGrid.Tests/SelectionCyclingTests.cs:228 Shift_arrow_along_an_axis_where_the_focus_is_interior_changes_nothing`; L3: `tests/ExGrid.Browser/sheet-vs-excel.spec.mjs:264 'active cell, case 3: after Enter, Shift+arrow moves the edge opposite the Focus ...'` |
| KB-42 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Tests/ActiveCellTests.cs:80 (Ctrl+. corners)`, `tests/ExGrid.Tests/ActiveCellTests.cs:58 (Shift+Backspace collapse)` (+3); L3: `tests/ExGrid.Browser/sheet-vs-excel.spec.mjs:303 'active cell, cases 7 and 8: Ctrl+. walks the corners, Ctrl+Backspace scrolls back ..., Shift+Backspace collapses (ADR-0052)'`. Not asserted at layers 1–2: Corners, reveal and collapse are asserted in Layers 1 and 2; 'none is taken from an open editor' has no found test. Not asserted at layer 3: Corners clockwise, Ctrl+Backspace reveal-only and Shift+Backspace collapse asserted; not-taken-from-an-open-editor not asserted. |
| KB-43 | MUST | pass | L1/2: `Fragments_are_listed_bottom_to_top_and_enter_visits_them_from_the_focus` |
| KB-44 | MUST | pass | L1/2: `A_held_escape_closes_a_formula_entrys_list_and_its_repeats_leave_the_edit`, `A_held_escape_closes_a_popover_and_its_repeats_release_nothing`, `A_held_escape_raises_on_leave_once_per_press` (+1); L3: `features.spec.mjs:230` |
| KB-45 | MUST | pass | L1/2: `An_arrow_asks_the_page_to_follow_the_focus`, `Every_move_the_grid_makes_is_followed`, `Extending_past_the_viewport_still_follows_the_extent` (+3); L3: `page-follow.spec.mjs:79`, `page-follow.spec.mjs:102`, `page-follow.spec.mjs:114` (+4) |
| KB-39 | MUST | pass | L1/2: `Ctrl_y_and_ctrl_shift_z_are_forwarded_as_redo`, `Ctrl_z_is_forwarded_as_undo`, `The_gate_is_told_again_when_the_claims_change` (+2); L3: `excel-keys.spec.mjs:28`, `excel-keys.spec.mjs:143` |
| CP-1 | MUST | pass | L3: `features.spec.mjs:300`; L1/2: `tests/ExGrid.Tests/CopyRuleTests.cs:26 Select_all_over_a_million_rows_is_refused_as_too_large`, `tests/ExGrid.Components/ClipboardWiringTests.cs:86 The_cap_refuses_strictly_past_it` |
| CP-2 | MUST | pass | L1/2: `The_cap_refuses_strictly_past_it` |
| CP-3 | MUST | pass | L3: `features.spec.mjs:300`; L1/2: `tests/ExGrid.Tests/CopyRuleTests.cs:132 A_misaligned_selection_over_the_cap_reports_the_misalignment`, `tests/ExGrid.Tests/CopyRuleTests.cs:26 Select_all_over_a_million_rows_is_refused_as_too_large` (+1) |
| CP-4 | MUST | pass | L1/2: `A_selection_in_the_window_answers_both_formats_synchronously`, `Copy_carries_the_iso_form_in_text_and_the_raw_value_in_html`, `Copy_shows_the_format_in_text_and_the_raw_value_in_html`; L3: `features.spec.mjs:253` |
| CP-5 | MUST | pass | L3: `features.spec.mjs:253` |
| CP-6 | MUST | pass | L3: `declarations.spec.mjs:2162`, `features.spec.mjs:253`, `features.spec.mjs:335` (+2) |
| CP-7 | MUST | pass, accepted as is | L1/2: `A_selection_beyond_the_window_takes_the_async_route`; L3: no test. Not asserted at layer 3: No Playwright test copies a selection beyond the Window to exercise the async Range route; only Layer 2 (ClipboardWiringTests.cs:108 A_selection_beyond_the_window_takes_the_async_route) covers it. |
| CP-8 | MUST | pass | L1/2: `CP8_copy_beyond_the_window_with_headers_follows_the_column_order`, `CP8_copy_follows_the_column_order_and_leaves_hidden_columns_out` |
| CP-9 | MUST | pass | L1/2: `Vertical_segments_stack_in_position_order` |
| CP-10 | MUST | pass | L3: `declarations.spec.mjs:2162`, `features.spec.mjs:253`, `features.spec.mjs:335` (+1) |
| CP-11 | MUST | pass | L1/2: `tests/ExGrid.Tests/PasteRuleTests.cs:20`, `tests/ExGrid.Tests/PasteRuleTests.cs:133 Two_rows_into_six_tile_three_times` (+3) |
| CP-12 | MUST | pass | L1/2: `tests/ExGrid.Tests/PasteRuleTests.cs:191 A_block_into_disjoint_ranges_is_refused_even_when_each_range_would_tile`, `tests/ExGrid.Tests/PasteRuleTests.cs:202 A_disjoint_target_containing_a_single_cell_is_refused_as_disjoint` (+1) |
| CP-13 | MUST | pass | L1/2: `A_paste_never_writes_outside_the_selection` |
| CP-14 | MUST | pass | L3: `declarations.spec.mjs:2162`, `features.spec.mjs:314`, `features.spec.mjs:335` (+2) |
| CP-15 | MUST | pass | L1/2: `An_empty_selection_refuses_by_name` |
| CP-16 | MUST | pass | L1/2: `A_paste_covering_a_non_editable_column_is_refused_and_raises_no_intent`, `A_target_covering_a_non_editable_column_is_refused_whole`, `Ctrl_enter_fill_across_a_non_editable_column_is_refused`; L3: `features.spec.mjs:404`, `features.spec.mjs:419` |
| CP-17 | MUST | pass | L1/2: `A_vertically_disjoint_selection_gets_one_header_row`, `CP8_copy_beyond_the_window_with_headers_follows_the_column_order`, `Copy_with_headers_emits_one_row_of_declared_headers` (+1); L3: `features.spec.mjs:566` |
| CP-18 | MUST | pass | L1/2: `A_selection_exactly_on_the_cap_copies_plainly_and_refuses_with_headers`, `With_headers_it_approves_when_the_cap_has_room_for_the_extra_row` |
| CP-19 | MUST | pass | L3: `features.spec.mjs:566` |
| CP-20 | MUST | pass | L1/2: `A_comma_is_never_a_cell_boundary`, `A_tab_is_the_only_cell_boundary`, `Lines_of_comma_grouped_numbers_stay_one_column` |
| CP-21 | MUST | pass | L1/2: `A_paste_arrives_as_streams`; L3: `circuit.spec.mjs:66`; L3: `tests/ExGrid.Browser/circuit.spec.mjs:66 'a paste past a Server hub's message limit arrives whole (CP-21, ADR-0052)'` |
| CP-22 | MUST | pass | L1/2: `A_paste_exactly_on_the_byte_cap_is_read`, `A_paste_past_the_byte_cap_is_refused_unread` |
| CP-23 | MUST | pass | L1/2: `A_rejected_clipboard_write_is_said_in_the_consumers_words`, `A_write_the_browser_rejects_is_refused_by_name`; L3: `circuit.spec.mjs:92` |
| CP-24 | MUST | pass | L1/2: `Ctrl_d_fills_down_with_the_raw_values`, `Ctrl_r_on_one_column_fills_from_the_left`, `Fill_down_reads_the_top_row_and_writes_the_rows_below_it` (+1); L3: `excel-keys.spec.mjs:116` |
| CP-25 | MUST | pass | L1/2: `A_non_editable_source_column_is_not_refused`, `A_non_editable_target_column_is_refused_whole`, `A_source_outside_the_window_is_asked_for_and_refused_when_short` (+10); L3: `excel-keys.spec.mjs:137` |
| CP-26 | MUST | pass | L1/2: `A_copy_beyond_the_window_is_outlined_from_the_rows_it_gathered`, `A_copy_is_outlined_once_its_write_lands`, `A_refused_copy_leaves_the_outline_it_found` (+3); L3: `copied-range.spec.mjs:42`; L3: `tests/ExGrid.Browser/copied-range.spec.mjs:42 'a copy is outlined with dashes ... (ADR-0170, CP-26, UX-6)'` |
| CP-27 | MUST | pass | L1/2: `A_change_of_the_clipboard_drops_the_outline`; L3: `copied-range.spec.mjs:82`; L3: `tests/ExGrid.Browser/copied-range.spec.mjs:82 'a write by anything but the grid's own copy drops the outline ... (ADR-0170, CP-27)'` |
| CP-28 | MUST | pass | L1/2: `Escape_drops_the_outline_and_only_the_next_escape_leaves`; L3: `copied-range.spec.mjs:99` |
| CP-29 | MUST | pass | L1/2: `A_change_of_the_columns_drops_the_outline`, `A_new_row_sequence_drops_the_outline`, `An_asynchronous_copy_keeps_the_coordinates_and_text_it_was_asked_for` (+1) |
| CP-30 | MUST | pass | L1/2: `A_copied_cell_that_reads_otherwise_drops_the_outline`, `A_copy_beyond_the_window_is_outlined_from_the_rows_it_gathered`, `An_asynchronous_copy_keeps_the_coordinates_and_text_it_was_asked_for`; L3: `copied-range.spec.mjs:117` |
| CP-31 | MUST | pass | L1/2: `A_paste_leaves_the_outline`; L3: `copied-range.spec.mjs:99` |
| CP-32 | MUST | pass | L3: `copied-range.spec.mjs:142` |
| CTX-1 | MUST | pass | L3: `popovers.spec.mjs:154`, `popovers.spec.mjs:180`; L1/2: `tests/ExGrid.Components/ContextMenuTests.cs:67 A_secondary_click_outside_the_selection_moves_it_onto_that_cell`, `tests/ExGrid.Components/ContextMenuTests.cs:80 A_secondary_click_inside_the_selection_leaves_it_alone` |
| CTX-2 | MUST | pass | L1/2: `tests/ExGrid.Components/ContextMenuTests.cs:56 The_menu_holds_the_cores_clipboard_commands`, `tests/ExGrid.Components/ContextMenuTests.cs:95 A_consumers_commands_are_appended_and_receive_the_target` |
| CTX-3 | MUST | pass, accepted as is | L3: `popovers.spec.mjs:192`; L1/2: `tests/ExGrid.Components/ContextMenuTests.cs:95 A_consumers_commands_are_appended_and_receive_the_target`. Not asserted at layers 1–2: Asserts the clicked row instance (Same), column name and a single-range Selection; Row Sequence Version in the context and 'no row beyond the Window resolved' are not asserted there. |
| CTX-4 | MUST | pass | L3: `popovers.spec.mjs:202`; L1/2: `tests/ExGrid.Tests/GridKeyTests.cs:182 The_context_menu_key_and_shift_f10_both_open_the_menu`, `tests/ExGrid.Components/ContextMenuTests.cs:117 The_context_menu_key_opens_it_on_the_focus` (+1) |
| CTX-5 | MUST | pass | L3: `popovers.spec.mjs:154`; inspection: pass (`inspect-rows.md`) |
| BIG-1 | MUST | pass | L1/2: `The_largest_offset_paints_the_last_row_at_the_bottom`; L3: `virtualisation.spec.mjs:39` |
| BIG-2 | MUST | pass | L3: `virtualisation.spec.mjs:209` |
| BIG-3 | MUST | pass | L3: `virtualisation.spec.mjs:313` |
| BIG-4 | MUST | pass | L1/2: `A_million_rows_fit_at_28px_and_are_refused_by_name_at_40px` |
| BIG-5 | MUST | pass | L1/2: `A_scroll_made_before_a_moved_ceiling_was_heard_stands`, `The_first_ceiling_paints_a_scroll_the_grid_has_not_heard`, `The_first_ceiling_reads_the_browsers_offset_through_the_new_geometry`; L3: `virtualisation.spec.mjs:224`, `virtualisation.spec.mjs:257` |
| BIG-6 | OBSERVATIONAL | recorded | L3: `observational.spec.mjs:149`; CI layer 3, `metrics.json` |
| BIG-7 | OBSERVATIONAL | recorded | L3: `observational.spec.mjs:136`; CI layer 3, `metrics.json` |
| PST-1 | MUST | pass | L1/2: `A_paste_raises_one_intent`; L3: `features.spec.mjs:314` |
| PST-2 | MUST | pass | L1/2: `A_paste_target_covers_rows_outside_the_window` |
| PST-3 | MUST | pass | L3: `gestures.spec.mjs:146` |
| PST-4 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Tests/PasteRuleTests.cs:179 A_ragged_shape_is_refused`, `tests/ExGrid.Components/ClipboardWiringTests.cs:530 A_paste_covering_a_non_editable_column_is_refused_and_raises_no_intent`. Not asserted at layers 1–2: Shape mismatch (3 rows into 5) is refused with ShapeMismatch and refusals raise no intent, but no test pastes a payload 'far larger than the target' and asserts zero notifications for that case. |
| PST-5 | MUST | pass | L3: `gestures.spec.mjs:167` |
| PST-6 | OBSERVATIONAL | recorded | L3: `gestures.spec.mjs:167`; CI layer 3, `metrics.json` |
| PST-7 | MUST | pass | L1/2: `tests/ExGrid.Tests/ClipboardParseTests.cs:114 Nbsp_edges_survive_where_markup_whitespace_is_trimmed` |
| ERR-1 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Tests/CopyRuleTests.cs:126`, `tests/ExGrid.Tests/CopyRuleTests.cs:91` (+6). Not asserted at layers 1–2: Most rules have their own refusal test (empty, misaligned, cap, shape, editability, oversized fill, MinWidth, gutter); no single test enumerates all the listed rules and a both-or-neither Window/Source test was only found for 'neither' (FindTests.cs:413). |
| ERR-2 | MUST | pass | L1/2: `A_consumers_words_replace_the_built_in_ones`, `A_copy_refusal_with_a_next_step_says_it`, `A_copy_the_sheet_refuses_is_announced_once` (+10); L3: `features.spec.mjs:440` |
| ERR-3 | MUST | pass | L1/2: `tests/ExGrid.Components/VirtualisationTests.cs:118 A_null_total_with_a_nonzero_window_start_is_refused`, `tests/ExGrid.Components/VirtualisationTests.cs:126 A_window_claiming_rows_past_the_total_is_refused` (+1) |
| ERR-4 | MUST | pass | L1/2: `ERR4_a_null_row_is_refused_naming_its_position`, `ERR4_a_repeated_row_instance_is_refused_naming_its_position` |
| ERR-5 | MUST | pass | L1/2: `ERR5_a_failed_range_request_reaches_the_consumer_and_the_grid_keeps_painting`, `ERR5_a_failed_re_read_reaches_the_consumer_and_the_last_window_stays_painted` |
| ERR-6 | MUST | pass | L1/2: `ERR6_a_broken_module_does_not_stop_the_range_request`, `ERR6_a_source_is_still_asked_for_rows_when_the_module_is_broken` |
| ERR-7 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/ResourceDisposalTests.cs:81 Disposal_survives_a_circuit_that_is_going_away`, `tests/ExGrid.Components/ResourceDisposalTests.cs:94 Disposal_before_the_module_lands_still_releases_it` (+2); L3: `tests/ExGrid.Browser/memory.spec.mjs:82 'mounting and disposing the grid fifty times returns nodes and listeners to baseline (MEM-2)'`. Not asserted at layer 3: Repeated mount/dispose in a browser under the console-clean fixture, but no test deliberately disposes during an in-flight interop call. |
| ERR-8 | MUST | pass | L1/2: `tests/ExGrid.Components/ScrollbarGutterTests.cs:157 A_gutter_that_swallows_the_viewport_is_refused_by_name`, `tests/ExGrid.Tests/ViewportBoxTests.cs:97 A_gutter_that_swallows_the_viewport_is_refused_by_name` (+1) |
| ERR-9 | MUST | pass | L1/2: `ERR9_ExGrid_is_built_with_the_recorded_script_and_stylesheet`, `ERR9_the_address_is_the_hosts_fingerprinted_one_where_it_knows_one`, `ERR9_the_package_records_each_files_address_version_and_stamp` (+4); L3: `freshness.spec.mjs:176`, `shipped-files.spec.mjs:123`, `shipped-files.spec.mjs:152` |
| ERR-10 | MUST | pass | L1/2: `ERR10_a_script_that_breaks_while_it_attaches_is_refused_and_let_go`, `ERR10_a_script_that_is_not_this_builds_is_refused_before_anything_attaches`, `ERR10_a_substituted_chrome_words_the_refusal` (+4); L3: `shipped-files.spec.mjs:171`, `shipped-files.spec.mjs:200`, `shipped-files.spec.mjs:216` |
| ERR-11 | MUST | pass | L1/2: `ERR11_a_substituted_chrome_words_the_notice`, `ERR11_each_stylesheet_ends_with_the_rules_of_its_notice`, `ERR11_the_built_in_markup_brings_no_stylesheet` (+11); L3: `shipped-files.spec.mjs:262`, `shipped-files.spec.mjs:276`, `shipped-files.spec.mjs:289` |
| CON-1 | MUST | pass | L3: `mud-app.spec.mjs:431` |
| CON-2 | MUST | pass | L3: `popovers.spec.mjs:286` |
| CON-3 | MUST | pass | every layer-3 test, through the fixture (CI, `layer3/`); `console.json` |
| CON-4 | MUST | pass | L3: `presentation.spec.mjs:157` |
| CON-5 | MUST | pass | L3: `presentation.spec.mjs:582` |
| CON-6 | MUST | pass | L1/2: `Disposal_survives_a_circuit_that_is_going_away` |
| CON-7 | MUST | pass | L1/2: `tests/UnobservedTaskWatch.cs:22 UnobservedTaskWatch (assembly fixture)`, `tests/ExGrid.Components/UnobservedExceptionTests.cs:19 A_busy_session_leaves_no_unobserved_task_exception`; `layers-1-2.log`: `UnobservedTaskWatch` fails a suite on any event, and every suite passed |
| CON-8 | MUST | pass | L3: `features.spec.mjs:717`, `harness.spec.mjs:234`, `harness.spec.mjs:240` (+1) |
| PF-1 | MUST | pass | L3: `virtualisation.spec.mjs:493` |
| PF-2 | MUST | pass | L1/2: `tests/ExGrid.Components/ShippedStylesheetTests.cs:78 The_module_installs_only_the_listeners_the_allowlist_names`; inspection: pass (with a documentation caveat, unchanged) (`inspect-rows.md`) |
| PF-3 | MUST | pass | L1/2: `An_action_keeps_its_click_handler_across_renders`, `An_actions_class_costs_nothing_per_render`, `Header_buttons_keep_their_handlers_across_renders` (+9) |
| PF-4 | MUST | pass | L1/2: `A_diagonal_scroll_reads_both_axes_in_a_single_call` |
| PF-5 | MUST | pass | L1/2: `A_drag_across_the_pinned_boundary_repaints_no_row`, `Re_rendering_under_a_selection_across_the_pinned_boundary_allocates_nothing_per_cell`, `Selecting_the_whole_result_beside_pinned_columns_costs_what_a_small_range_across_them_costs` (+1) |
| PF-6 | OBSERVATIONAL | recorded | L3: `observational.spec.mjs:149`; CI layer 3, `metrics.json` |
| PF-7 | OBSERVATIONAL | recorded | L3: `observational.spec.mjs:193`; CI layer 3, `metrics.json` |
| PF-8 | OBSERVATIONAL | recorded | `measure/README.md#pf-8`, `measure/pf8/` |
| MEM-1 | MUST | pass | L1/2: `Scrolling_allocates_the_same_at_a_thousand_and_a_million_rows` |
| MEM-2 | MUST | pass | L3: `memory.spec.mjs:82` |
| MEM-3 | MUST | pass | L1/2: `Disposal_before_the_handle_lands_still_releases_everything_once`, `Disposal_before_the_module_lands_still_releases_it`, `Disposal_survives_a_circuit_that_is_going_away` (+3) |
| MEM-4 | MUST | pass | L1/2: `Disposing_releases_the_scroll_handle`, `The_alt_keys_release_is_held_in_order_by_the_roots_one_keyup_listener`, `The_layout_ceiling_is_observed_on_the_instances_probe_and_released_on_dispose` (+1); L3: `memory.spec.mjs:124`; L3: `tests/ExGrid.Browser/memory.spec.mjs:124 'disposal takes the module's listeners off the root, and the count comes back (MEM-4)'` |
| MEM-5 | MUST | pass | L3: `memory.spec.mjs:171`, the ten-minute soak with `EXGRID_SOAK=1` in CI's long run on Chrome and Edge on both hosts, passed (`layer3/`: chrome and msedge shard 2/4 on each host) |
| MEM-6 | OBSERVATIONAL | recorded | L3: `memory.spec.mjs:171`, read at the end of the four soaks; `metrics.json` |
| MEM-7 | OBSERVATIONAL | recorded | L1/2: `Bytes_per_scroll_frame_are_recorded`, `Scrolling_allocates_the_same_at_a_thousand_and_a_million_rows`; `mem-7.log` |
| DOM-1 | MUST | pass | L3: `virtualisation.spec.mjs:209` |
| DOM-2 | MUST | pass | L1/2: `ADR0008_heading_state_dom_is_independent_of_selection_size`, `A_part_that_is_the_focus_alone_has_no_tint_and_stands_no_shade_layer`, `In_each_layer_the_tints_come_before_the_boxes` (+2); L3: `virtualisation.spec.mjs:313` |
| DOM-3 | MUST | pass | L3: `virtualisation.spec.mjs:565` |
| DOM-4 | MUST | pass | L3: `features.spec.mjs:1111`, `mud-app.spec.mjs:247` |
| DOM-5 | OBSERVATIONAL | recorded | L3: `observational.spec.mjs:149`; CI layer 3, `metrics.json` |
| RR-1 | MUST | pass | L1/2: `A_paper_change_re_renders_no_row`, `The_scroll_event_after_a_reveal_renders_no_row`; L3: `mud-app.spec.mjs:477`, `mud.spec.mjs:225` |
| RR-2 | MUST | pass | L1/2: `A_row_height_change_skips_surviving_rows` |
| RR-3 | MUST | pass | L1/2: `A_cell_metrics_change_re_renders_every_visible_row_once` |
| RR-4 | MUST | pass | L1/2: `tests/ExGrid.Components/VirtualisationTests.cs:88 Scrolling_one_row_reuses_the_overlapping_rows` |
| RR-5 | MUST | pass | L1/2: `tests/ExGrid.Components/RowMemoisationTests.cs:15 Re_pushing_the_same_window_and_columns_renders_no_row_again`, `tests/ExGrid.Components/RowMemoisationTests.cs:37 Replacing_one_row_instance_repaints_only_that_row` (+1) |
| RR-6 | MUST | pass | L1/2: `tests/ExGrid.Components/HoverBandTests.cs:78 The_same_cell_again_renders_nothing` |
| RR-7 | MUST | pass | L1/2: `RR7_a_key_that_moves_nothing_renders_nothing` |
| RR-8 | MUST | pass | L1/2: `tests/ExGrid.Components/ScrollbarGutterTests.cs:129 An_unchanged_gutter_is_not_a_render` |
| RR-9 | MUST | pass | inspection: pass (`inspect-rows.md`) |
| RR-10 | MUST | pass | inspection: pass (`inspect-rows.md`) |
| RR-11 | MUST | pass | L1/2: `The_same_cell_again_renders_nothing` |
| RR-12 | MUST | pass | L1/2: `A_template_request_rerenders_only_the_focus_row`, `Only_the_engaged_row_rerenders`, `The_request_waits_until_the_cell_is_painted` |
| RR-13 | MUST | pass | L1/2: `Re_rendering_striped_rows_allocates_nothing_per_cell`, `Scrolling_one_row_with_stripes_re_renders_only_the_entering_row`, `Turning_stripes_on_re_renders_only_the_striped_rows` |
| RR-14 | MUST | pass | L1/2: `tests/ExGrid.Components/SettledMarkTests.cs:199 ADR0220_the_count_moves_and_the_rows_skip_their_render` |
| ASY-1 | MUST | pass | L1/2: `tests/ExGrid.Tests/GridSourceFetchTests.cs:138 An_answer_to_a_superseded_question_is_discarded`, `tests/ExGrid.Tests/GridSourceFetchTests.cs:346 Disposing_the_source_cancels_what_is_in_flight` |
| ASY-2 | MUST | pass | L1/2: `Every_fetchs_token_source_is_disposed_when_its_fetch_ends` |
| ASY-3 | MUST | pass | L1/2: `ASY3_a_burst_of_scroll_events_applies_only_the_last_position_in_order` |
| ASY-4 | MUST | pass | L1/2: `tests/ExGrid.Components/RangeRequestTests.cs:98 A_consumer_that_answers_immediately_is_asked_once` |
| ASY-5 | MUST | pass | L1/2: `ASY5_a_settle_firing_after_disposal_is_harmless`, `ASY5_the_settle_timer_fires_onto_the_renderers_context` |
| ASY-6 | MUST | pass | L3: `tests/ExGrid.Browser/playwright.config.mjs:123 timeout: 300_000`; `--timeout 5m` per project in Step 1; layer 3's 300 s per test |
| ASY-7 | MUST | pass, accepted as is | L3: `tests/ExGrid.Browser/settled-mark.spec.mjs:31 'ADR-0220: a key takes the mark off at once...'`, `tests/ExGrid.Browser/settled-mark.spec.mjs:54 twenty arrows` (+2); L1/2: `tests/ExGrid.Components/SettledMarkTests.cs:94 ADR0220_the_root_names_the_last_key_answered`, `tests/ExGrid.Components/SettledMarkTests.cs:107 ADR0220_a_press_told_but_not_yet_heard_holds_back_the_keys_after_it` (+5). Not asserted at layer 3: Mark off/on around keys, a 20-key burst, a reveal and an edit commit; paste, composition, copy-in-flight, a press on rows and each owed timer (data-ex-owing) are not exercised in the browser spec. |
| ST-1 | MUST | pass | L1/2: `ConsistencyTests.Five_hundred_random_operations_leave_every_invariant_standing`, seeds 20260901 and 424242 at 200 and 1,000 rows (Step 1); seed 20260901 at 1,000,000 rows, `st1.log` (Step 5) and CI's long run; zero violations |
| ST-2 | MUST | pass | L1/2: `ST2_a_source_pushing_an_empty_window_leaves_nothing_to_paint`, `ST2_after_any_sequence_an_empty_window_leaves_nothing_to_paint`, `ST2_an_edit_opened_by_typing_the_cells_own_text_is_not_untyped` (+11); L3: `sheet.spec.mjs:723`, `sheet.spec.mjs:768` |
| ST-3 | MUST | pass | L3: `presentation.spec.mjs:48`, `presentation.spec.mjs:105` |
| ST-4 | MUST | pass | L1/2: `ADR0207_a_column_the_grid_does_not_have_is_refused_by_name`, `ADR0207_a_view_state_of_another_version_is_refused`, `ADR0207_view_states_holding_the_same_view_are_equal` (+9) |
| ST-5 | MUST | pass | L1/2: `tests/ExGrid.Components/SourceSharingTests.cs:33 A_bundled_source_refuses_a_grid_on_another_circuit`, `tests/ExGrid.Components/SourceSharingTests.cs:47 Two_grids_on_one_page_still_share_a_bundled_source` (+1); see tests |
| WR-1 | MUST | pass, accepted as is | L1/2: `Applying_a_part_of_the_list_is_one_in_clause`, `Applying_everything_is_no_filter`, `Cancel_is_the_cores_exit_and_there_is_no_clear` (+6); L3: `tests/ExGrid.Browser/popovers.spec.mjs:762 'Enter in the value field applies exactly what OK applies (KB-31, ADR-0039)'`, `tests/ExGrid.Browser/popovers.spec.mjs:771 'with nothing ticked the value filter is not applied ... (WR-2)'`. Not asserted at layer 3: popovers.spec.mjs runs under both builtin and mud Chromes (VALUE_LIST/CONDITION tables): value list, search, condition, Apply/Cancel; no assertion on the Blank entry, TooMany, or FilterSpec equality across Chromes beyond row counts. |
| WR-2 | MUST | pass | L1/2: `A_blank_operator_applies_without_an_operand`, `A_condition_applies_its_typed_operand`, `A_numbers_operators_are_mudblazors_symbols` (+9); L3: `popovers.spec.mjs:771` |
| WR-3 | MUST | pass | L1/2: `ADR0130_the_figures_are_named_in_the_chromes_words`, `A_numbers_operators_are_mudblazors_symbols`, `A_refusal_is_announced_in_english_under_this_chrome` (+10) |
| WR-4 | MUST | pass | L1/2: `ADR0080_under_the_mud_chrome_the_headers_menu_buttons_are_not_tab_stops`, `Every_core_command_has_an_icon_and_a_consumers_only_when_supplied`, `In_the_grid_a_command_run_from_the_mud_menu_closes_it` (+6) |
| WR-5 | MUST | pass | L3: `mud-app.spec.mjs:450` |
| WR-6 | MUST | pass | L1/2: `Striped_turns_row_stripes_on`, `The_grids_own_stripe_rows_wins`; L3: `mud-app.spec.mjs:477` |
| WR-7 | MUST | pass | L3: `mud-app.spec.mjs:137`, `mud-app.spec.mjs:191`, `mud-app.spec.mjs:247` (+2) |
| WR-7a | MUST | pass | L3: `stretch.spec.mjs:56` |
| WR-8 | MUST | pass | L1/2: `The_Wrapper_serves_no_script_of_its_own`, `The_Wrapper_source_holds_no_script_file`, `The_Wrapper_source_names_no_interop_type`; inspection: pass (`inspect-rows.md`) |
| WR-9 | MUST | pass | L3: `mud-app.spec.mjs:431`; L1/2: `tests/UnobservedTaskWatch.cs:1 UnobservedTaskWatch (assembly fixture)`, `tests/Directory.Build.targets:50 compiles it into every bUnit test project` (+1) |
| WR-10 | MUST | pass, accepted as is | L1/2: `A_Wrapper_whose_range_cannot_be_read_is_refused`, `A_candidate_is_refused_on_a_MudBlazor_version_its_floor_record_did_not_run`, `A_later_record_in_the_floor_record_s_directory_is_refused` (+22); L3: no test; by hand, Windows or Linux: `verification/2026-10-18-linux-mudblazor-floor/report.md` at `f6b5e8fe6c4b`, pass. Not asserted at layer 3: No test asserts the MudBlazor floor or range; the Check is a Step 4 floor run plus per-version layer-2 runs recorded under verification/, not a Playwright test. |
| SRV-1 | MUST | pass | inspection: pass (`inspect-rows.md`) |
| SRV-2 | MUST | pass | every layer-3 test on the Server host, both browsers (`layer3/jobs.md`): 0 failed; the tests skipped there and not on WebAssembly are only ERR-10's two stamp tests, an exception the row names; those skipped on both hosts are the measurements run in Step 6, VZ-14's Windows block (by hand) and two `test.fixme` of `sheet-vs-excel.spec.mjs` (items 18 and 24) |
| SRV-3 | MUST | pass | L3: `tests/ExGrid.Browser/circuit.spec.mjs:394 describe 'two users, one store (SRV-3, ADR-0018)' / 'a store change reaches both, a sort reaches one, and a shared source is refused'` |
| SRV-4 | MUST | pass | every layer-3 test on the Server host, through the fixture (CI, `layer3/`) |
| SRV-5 | MUST | pass | L1/2: `A_search_with_nothing_chosen_among_its_matches_cannot_be_applied`, `A_search_with_nothing_chosen_among_its_matches_is_not_applied`, `An_echo_of_earlier_typing_is_not_written_back` (+11); L3: `circuit.spec.mjs:113`, `circuit.spec.mjs:153`, `circuit.spec.mjs:181` (+9) |
| SRV-6 | OBSERVATIONAL | recorded | L3: `measure.spec.mjs:33`; `measure/README.md`, `measure/metrics-srv6.json` |
| SRV-7 | MUST | pass | L3: `format-cells-mud.spec.mjs:344`, `format-cells.spec.mjs:188` |
| MK-1 | MUST | pass | L1/2: `Positions_beyond_the_window_are_fetched_and_lined_up`, `Positions_over_a_mixed_block_mark_all_of_it`, `Positions_over_an_all_marked_block_unmark_it` (+1); L3: `marks.spec.mjs:72` |
| MK-2 | MUST | pass | L1/2: `The_header_is_painted_from_the_counts_not_from_the_window` |
| MK-3 | MUST | pass | L1/2: `Positions_under_a_stale_sequence_version_mark_nothing`, `Positions_under_a_stale_version_mark_nothing`, `Space_in_the_mark_column_reports_the_selected_rows_once` (+1) |
| MK-4 | MUST | pass | L1/2: `A_changed_mark_repaints_only_its_row` |
| MK-5 | MUST | pass | L1/2: `A_sort_keeps_the_marks_and_drops_the_selection`, `Marks_survive_a_sort` |
| MK-6 | MUST | pass | L1/2: `After_all_a_row_fetched_later_answers_marked`; L3: `marks.spec.mjs:41` |
| MK-7 | MUST | pass | L1/2: `A_filter_change_recounts_the_marks_outside_it`, `Marks_outside_the_filter_are_kept_and_counted`, `The_count_display_names_the_marks_outside_the_filter`; L3: `marks.spec.mjs:56` |
| MK-8 | MUST | pass | L1/2: `A_row_added_after_all_is_not_marked`, `A_row_joining_the_result_after_all_is_not_marked`, `New_counts_turn_the_header_to_some`; L3: `marks.spec.mjs:95` |
| MK-9 | MUST | pass | L1/2: `A_header_press_made_under_a_pager_marks_its_page_after_the_pager_is_removed`, `A_header_press_made_with_no_pager_marks_the_whole_result_after_a_pager_is_added`, `A_header_press_marks_the_rows_its_page_held_after_the_page_size_changes` (+20) |
| DC-1 | MUST | pass | L1/2: `A_declaration_that_answers_nothing_paints_the_same_cells`, `An_empty_list_or_none_changes_nothing`, `An_unmarked_cell_is_painted_as_before` (+31) |
| DC-2 | MUST | pass | L1/2: `A_header_click_selects_columns`, `Declared_a_header_click_selects_the_whole_column`, `Declared_shift_click_on_a_header_extends_from_the_focus_column`; L3: `sheet.spec.mjs:77` |
| DC-3 | MUST | pass | L1/2: `A_row_heading_click_selects_rows`, `A_row_heading_click_selects_the_row_and_shift_extends`, `Row_headings_are_painted_beside_every_row_with_the_consumer_label` (+3); L3: `sheet.spec.mjs:77`, `sheet.spec.mjs:134` |
| DC-4 | MUST | pass | L1/2: `Row_headings_are_outside_selection_copy_select_all_and_the_cycle`, `Tab_through_a_selected_row_never_leaves_the_column_space` |
| DC-5 | MUST | pass | L1/2: `Hidden_column_headings_leave_the_addresses`, `Hiding_the_header_removes_the_band_and_the_geometry_follows`, `Without_row_headings_the_first_column_starts_at_the_edge` |
| DC-6 | MUST | pass | L1/2: `The_default_width_is_estimated_from_the_labels`, `The_stylesheet_carries_no_heading_width` |
| DC-7 | MUST | pass | L1/2: `A_second_ctrl_shift_arrow_asks_from_the_extent`, `Ctrl_arrow_asks_the_sheet`, `Ctrl_arrow_moves_to_the_answer` (+5); L3: `sheet.spec.mjs:52` |
| DC-8 | MUST | pass | L1/2: `A_block_onto_one_cell_spills_from_that_cell_as_its_top_left`, `A_spill_is_one_target_range_covering_exactly_the_block`, `A_spill_the_consumer_accepts_is_not_refused_and_selects_the_block` (+2); L3: `declarations.spec.mjs:2137`, `declarations.spec.mjs:2342` |
| DC-9 | MUST | pass | L1/2: `Without_the_declaration_the_single_cell_refusal_stands` |
| DC-10 | MUST | pass | L1/2: `A_spill_covering_a_non_editable_column_is_refused_whole`, `A_spill_covering_a_non_editable_column_is_refused`, `A_spill_past_the_last_column_is_refused_by_name` (+1) |
| DC-11 | MUST | pass | L1/2: `A_placed_range_is_selected_and_announced`, `A_stale_placement_is_dropped`, `The_name_box_moves_the_selection_and_the_focus_and_scrolls`; L3: `sheet.spec.mjs:170`, `sheet.spec.mjs:194` |
| DC-12 | MUST | pass | L1/2: `A_disjoint_or_empty_selection_has_no_handle`, `A_disjoint_selection_shows_no_handle`, `A_whole_column_dragged_right_fills_every_row_and_steps_its_lone_number` (+11); L3: `declarations.spec.mjs:2036`, `declarations.spec.mjs:2074`, `declarations.spec.mjs:2114` (+4) |
| DC-13 | MUST | pass | L1/2: `A_diagonal_drag_extends_along_one_axis`, `A_held_handle_is_grabbed_where_it_stands`, `A_whole_column_dragged_down_changes_nothing` (+9); L3: `declarations.spec.mjs:1878`, `declarations.spec.mjs:1904`, `declarations.spec.mjs:1947` (+3) |
| DC-14 | MUST | pass | L1/2: `A_fill_into_a_non_editable_column_is_refused_before_the_intent`, `A_target_covering_a_non_editable_column_is_refused_whole` |
| DC-15 | MUST | pass | inspection: pass (`inspect-rows.md`) |
| DC-16 | MUST | pass | L1/2: `A_double_click_opens_on_the_consumers_text`, `F2_opens_on_the_consumers_text`, `Typing_still_replaces_with_the_character` (+1) |
| DC-17 | MUST | pass | L1/2: `A_chrome_paints_the_list_and_calls_back`, `Escape_closes_the_list_before_it_cancels`, `Near_the_bottom_the_list_opens_above` (+6); L3: `declarations.spec.mjs:51`, `declarations.spec.mjs:94`, `declarations.spec.mjs:170` |
| DC-18 | MUST | pass | L1/2: `ADR0058_a_late_answer_for_what_tab_wrote_shows_no_list`, `A_list_goes_as_soon_as_the_text_changes`, `A_stale_answer_is_never_shown` (+1) |
| DC-19 | MUST | pass | L1/2: `A_click_writes_the_clicked_cell`, `A_press_into_the_bar_is_answered_only_once_the_gate_has_been_told_the_edit`, `A_press_into_the_bar_withdraws_the_cell_editors_focus_request` (+8); L3: `declarations.spec.mjs:631`, `declarations.spec.mjs:669`, `declarations.spec.mjs:687` (+7) |
| DC-20 | MUST | pass | L1/2: `Text_the_core_has_not_heard_can_allow_pointing`, `Text_typed_after_a_pointed_reference_ends_pointing`, `The_key_decides_from_the_text_it_carries`; L3: `declarations.spec.mjs:1084` |
| DC-21 | MUST | pass | L1/2: `Each_field_is_handed_what_the_built_in_shows`, `On_a_display_grid_the_bar_shows_the_full_value_behind_hashes`, `The_bar_follows_the_focus_with_the_label_and_the_opening_text` |
| DC-22 | MUST | pass | L1/2: `A_chromes_bar_is_the_editors_second_surface`, `Both_surfaces_agree_after_every_keystroke`, `Enter_in_the_bar_raises_one_intent` (+5); L3: `sheet.spec.mjs:421` |
| DC-23 | MUST | pass | L1/2: `Switched_off_the_geometry_is_as_before`, `The_rows_take_what_the_bar_leaves` |
| DC-24 | MUST | pass | L1/2: `On_apple_platforms_the_listener_answers_the_keys_macOS_scrolls_with`, `The_gate_claims_F4_only_while_editing_and_only_when_declared`, `The_gate_claims_declared_keys_only_from_the_cores_list` (+2); inspection: pass (`inspect-rows.md`) |
| DC-26 | MUST | pass | L1/2: `A_number_cell_in_a_text_column_hashes_when_it_does_not_fit`, `A_number_cell_in_a_text_column_is_numeric_and_hashes`, `A_text_cell_in_a_number_column_is_not_numeric_and_never_hashes` (+2) |
| DC-27 | MUST | pass | L1/2: `A_refused_fill_leaves_the_selection_on_the_source`, `An_accepted_fill_selects_the_source_and_the_target`, `An_accepted_fill_upward_selects_from_the_target` (+1); L3: `declarations.spec.mjs:1878` |
| DC-28 | MUST | pass | L1/2: `The_bar_and_the_name_box_take_what_the_browser_reports_as_already_shown`; L3: `declarations.spec.mjs:1107`, `declarations.spec.mjs:1138` |
| DC-29 | MUST | pass | L1/2: `A_cell_is_painted_with_its_own_alignment`, `A_users_alignment_paints_the_cell`, `Aligning_whole_rows_records_the_rows` (+5) |
| DC-30 | MUST | pass | L1/2: `Ctrl_Z_and_Ctrl_Y_step_through_edits_in_order`, `Declared_the_gate_claims_the_history_keys`, `The_history_keys_are_claimed_only_through_the_cores_list` (+5); L3: `declarations.spec.mjs:2428`, `declarations.spec.mjs:2467`, `declarations.spec.mjs:2509` |
| DC-31 | MUST | pass | L1/2: `A_caret_moved_under_an_open_list_is_asked_about_again`, `A_caret_moved_while_pointing_ends_the_outline`, `A_caret_only_move_asks_completion_at_the_new_caret` (+11); L3: `declarations.spec.mjs:107`, `declarations.spec.mjs:136`, `declarations.spec.mjs:710` |
| DC-32 | MUST | pass | L1/2: `A_copy_answer_is_asked_on_the_asynchronous_route`, `A_copy_answer_is_asked_on_the_synchronous_route_and_written_as_given`, `A_copy_answer_refusal_is_raised_and_announced_on_both_routes` (+2) |
| DC-33 | MUST | pass | L1/2: `A_foreign_table_without_the_mark_is_shown_text`, `A_paste_intent_marks_each_field_invariant_or_shown`, `Excel_under_a_comma_decimal_culture_marks_x_num_invariant_and_the_bare_form_shown` (+4); L3: `declarations.spec.mjs:2371`, `declarations.spec.mjs:2401` |
| DC-34 | MUST | pass | L1/2: `A_caret_moved_while_pointing_ends_the_outline`, `A_click_into_the_bar_points_from_the_clicked_caret`, `A_click_into_the_formula_bar_keeps_the_caret_where_it_was_clicked` (+2); L3: `declarations.spec.mjs:731`, `declarations.spec.mjs:782` |
| DC-35 | MUST | pass | L1/2: `One_third_reads_as_Excel_fits_it_in_a_default_column`, `The_delegate_is_given_the_content_width_and_the_metrics`, `The_painted_text_is_painted_and_the_value_is_its_accessible_name` |
| DC-36 | MUST | pass | L1/2: `A_double_click_on_an_edge_sizes_the_column_to_fit`, `A_double_click_on_an_edge_still_sizes_to_fit`, `A_drag_on_a_grip_still_resizes` (+2); L3: `declarations.spec.mjs:2533` |
| DC-37 | MUST | pass | L1/2: `tests/ExGrid.Components/CopyWithHeadersDeclarationTests.cs:52 Declared_the_menu_holds_copy_alone`, `tests/ExGrid.Components/CopyWithHeadersDeclarationTests.cs:63 Declared_the_context_menu_key_opens_copy_alone` (+3) |
| DC-38 | MUST | pass | L1/2: `A_spill_the_consumer_refuses_leaves_the_selection_where_it_was`, `A_too_narrow_excel_column_pasted_onto_one_cell_is_refused_by_name`; L3: `declarations.spec.mjs:2322` |
| DC-39 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/ActiveCellWiringTests.cs:210 The_name_box_shows_the_size_while_the_button_is_down`, `tests/ExGrid.Components/ActiveCellWiringTests.cs:227 Keyboard_extension_keeps_the_focus_label` (+1); L3: `tests/ExGrid.Browser/sheet-vs-excel.spec.mjs:251 'active cell, case 2: while a drag is held the Name Box shows 4R x 3C, and D5 stays active ... (ADR-0052)'`. Not asserted at layer 3: ExSheet drag shows 4R x 3C and returns to D5 on release; keyboard extension showing no size is only at layer 2. |
| DC-40 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Components/ExcelKeyTests.cs:272 A_fill_key_intent_names_its_source_range`, `tests/ExGrid.Components/ClipboardWiringTests.cs:244 (clipboard paste: FillSource null)` (+1). Not asserted at layers 1–2: Ctrl+D top row, Ctrl+R left column, clipboard paste and Ctrl+Enter naming none are asserted; the row above / column to the left of a range one cell deep is not. |
| DC-41 | MUST | pass | L1/2: `Ctrl_enter_names_the_cell_it_was_entered_in_and_a_paste_does_not` |
| DC-42 | MUST | pass | L1/2: `A_drag_across_row_headings_selects_whole_rows`, `Ctrl_click_on_a_row_adds_it_as_a_new_range`, `Ctrl_click_on_a_wholly_selected_row_takes_it_out` (+11); L3: `headings.spec.mjs:148`, `headings.spec.mjs:176`, `headings.spec.mjs:196` (+3) |
| DC-43 | MUST | pass | L1/2: `A_column_heading_drag_never_scrolls_down`, `A_column_heading_drag_scrolls_sideways_at_the_edge`, `A_row_heading_drag_scrolls_down_and_never_sideways`; L3: `headings.spec.mjs:217`, `headings.spec.mjs:240` |
| DC-44 | MUST | pass | L1/2: `A_drag_over_cells_shows_no_size_tip`, `A_row_heading_drag_shows_the_size_tip_at_the_extents_row`, `Over_a_resize_grip_the_pointer_places_the_extent` (+9); L3: `headings.spec.mjs:261`, `headings.spec.mjs:298`, `headings.spec.mjs:320` |
| DC-45 | MUST | pass | L1/2: `DC45_F4_hands_the_function_the_text_and_selection_its_key_carries`, `DC45_F4_in_the_formula_bar_after_F2_leaves_a_caret`, `DC45_F4_in_the_formula_bar_after_a_caret_move_selects` (+25); L3: `declarations.spec.mjs:1202`, `declarations.spec.mjs:1226`, `declarations.spec.mjs:1247` (+8) |
| DC-46 | MUST | pass | L1/2: `A_cancel_removes_every_outline`, `A_chrome_editors_text_is_outlined`, `A_commit_removes_every_outline` (+25); L3: `declarations.spec.mjs:1658`, `declarations.spec.mjs:1685`, `declarations.spec.mjs:1703` (+1) |
| DC-47 | MUST | pass | L1/2: `A_chrome_places_the_layer_it_is_handed`, `A_constant_is_drawn_uncoloured`, `A_pointed_reference_is_drawn_in_its_colour` (+8); L3: `reference-text.spec.mjs:135`, `reference-text.spec.mjs:162`, `reference-text.spec.mjs:193` (+2) |
| DC-48 | MUST | pass | L1/2: `A_caret_placed_by_the_core_is_brought_into_view`, `A_chrome_places_the_layer_it_is_handed`, `DC56_the_pointed_shade_is_one_token_per_place_in_the_palette` (+6); L3: `reference-text.spec.mjs:577`, `reference-text.spec.mjs:621` |
| DC-49 | MUST | pass | L1/2: `A_commit_tells_the_empty_set`, `An_edit_with_no_keys_left_tells_nothing_at_its_end`, `Key_colours_are_told_when_they_change` (+1) |
| DC-50 | MUST | pass | L1/2: `A_column_is_one_element_whatever_it_covers`, `A_column_listed_twice_is_outlined_twice_in_order`, `A_column_the_grid_does_not_show_is_outlined_nowhere` (+8); L3: `declarations.spec.mjs:1769`, `declarations.spec.mjs:1800` |
| DC-51 | MUST | pass | L1/2: `The_layer_is_one_run_coloured_by_highlights_of_the_grids_own`, `The_listener_gates_the_coloured_text_on_its_text_and_sets_one_class`; inspection: pass (`inspect-rows.md`) |
| DC-52 | MUST | pass | L1/2: `A_double_click_handed_over_raises_no_cell_double_click`, `A_drag_across_cells_is_handed_over_once`, `A_grid_pointed_at_shows_the_cell_pointer_and_lets_every_press_through` (+19); L3: `tests/ExGrid.Browser/pointing-scope.spec.mjs:126 'ADR-0058/SH-32: =, a press on a PV cell, *2 and Enter ...'`, `tests/ExGrid.Browser/pointing-scope.spec.mjs:150 '... =SUM(, a press on the PV header, ) and Enter ...'` (+3) |
| DC-53 | MUST | pass | L1/2: `A_pressed_cell_is_dashed_by_its_rows_key`, `A_row_that_is_not_painted_is_not_dashed_or_scrolled_to`, `Dashes_are_drawn_over_the_cell_of_the_row_named` (+6); L3: `pointing-scope.spec.mjs:916` |
| DC-54 | MUST | pass | L1/2: `A_drag_is_handed_over_after_the_press_it_began_with`, `A_press_handed_on_is_told_to_the_pointing_root_by_one_event`, `A_press_handed_on_is_written_after_the_keys_typed_before_it` (+9); L3: `pointing-scope.spec.mjs:280`, `pointing-scope.spec.mjs:338`, `pointing-scope.spec.mjs:372` (+2) |
| DC-55 | MUST | pass | L1/2: `A_cell_is_scrolled_into_view_when_asked`, `A_column_out_of_view_to_the_left_is_scrolled_into_view_across_only`, `A_column_out_of_view_to_the_right_is_scrolled_into_view_across_only` (+19); L3: `pointing-scope.spec.mjs:731`, `pointing-scope.spec.mjs:979` |
| DC-56 | MUST | pass | L1/2: `DC56_the_pointed_shade_is_one_token_per_place_in_the_palette`; L3: `reference-text.spec.mjs:458` |
| DC-57 | MUST | pass | L1/2: `A_declaration_naming_a_core_key_is_refused_by_name`, `A_declared_key_is_raised_and_moves_nothing`, `Declared_the_gate_claims_the_keys_in_both_states` (+7) |
| DC-58 | MUST | pass | L1/2: `A_bold_cells_painted_text_is_fitted_with_the_bold_widths`, `A_bold_number_that_fits_only_at_the_regular_widths_is_hashed`, `A_fill_is_painted_on_that_cell_and_covers_its_gridlines` (+12); L3: `appearance.spec.mjs:226`, `appearance.spec.mjs:278`, `sheet-paper.spec.mjs:57` (+2) |
| DC-59 | MUST | pass | L1/2: `A_dashed_line_is_a_tile_of_its_pattern`, `A_double_lines_middle_shows_what_its_gridline_would`, `A_fill_covers_its_gridline_beneath_a_line` (+10); L3: `appearance.spec.mjs:137`, `appearance.spec.mjs:175`, `appearance.spec.mjs:187` (+20) |
| DC-60 | MUST | pass | L1/2: `A_popup_of_the_contents_reaches_the_key_gate`, `A_press_into_the_formula_bar_closes_it`, `A_press_on_the_rows_closes_it` (+18); L3: `format-cells.spec.mjs:50`, `format-cells.spec.mjs:160`, `format-cells.spec.mjs:203` (+3) |
| DC-61 | MUST | pass | L1/2: `Handing_the_keyboard_on_asks_the_handle_for_the_control`, `Handing_the_keyboard_on_before_the_grid_is_attached_focuses_the_control_itself`, `Handing_the_keyboard_on_from_a_grid_that_is_gone_focuses_the_control_itself` (+6); L3: `pivot.spec.mjs:1145`, `pivot.spec.mjs:1173` |
| DC-62 | MUST | pass, accepted as is | L1/2: `A_held_escape_raises_on_leave_once_per_press`, `An_escape_from_a_control_inside_a_cell_does_not_raise_on_leave`, `An_escape_that_cancels_an_edit_does_not_raise_on_leave` (+7); L3: `tests/ExGrid.Browser/pivot.spec.mjs:395 'ADR-0070: Escape in the dialog's grid peels the grid's own layers, then closes the dialog ...'`, `tests/ExGrid.Browser/pivot.spec.mjs:473 'ADR-0070/0012: Escape held in the dialog's grid closes the dialog once, and its repeats leave the report the keyboard'`. Not asserted at layer 3: Escape dismisses the grid layer first, then leaves once, and held repeats leave once, on /pivot?details=dialog; not asserted: Tab release before the raise, every other dismissal kind, DOM focus kept, the undeclared case. |
| DC-63 | MUST | pass, accepted as is | L1/2: `A_double_click_beside_a_templates_control_names_the_template_cell`, `A_double_click_handed_over_raises_no_cell_double_click`, `A_double_click_where_no_edit_opens_is_heard`; L3: `tests/ExGrid.Browser/pivot.spec.mjs:316 'ADR-0059/0063: a double click on a value opens a tab ...'`, `tests/ExGrid.Browser/pivot.spec.mjs:395 (report Focus placed at the cell after the double click)`. Not asserted at layer 3: A double click on a /pivot value cell raises once with the Focus already placed there; Editable cell, open edit, controls in Action/Template/Mark cells and no-listener cases are layer 2 only. |
| DC-64 | MUST | pass | L1/2: `A_pinned_value_cell_carries_the_mark`, `Exactly_the_cells_whose_change_is_within_the_duration_are_marked`, `One_timer_is_rearmed_for_each_next_end_and_goes_with_the_last_mark` (+5) |
| DC-65 | MUST | pass | L1/2: `A_mark_stays_on_its_row_across_scrolls`; L3: `grid-live.spec.mjs:123` |
| DC-66 | MUST | pass | L1/2: `No_live_region_announces_a_mark`, `Nothing_in_the_core_stylesheet_transitions_or_animates`, `The_forced_colors_block_restates_the_change_highlight` (+1); L3: `grid-live.spec.mjs:163` |
| DC-25 | MUST | pass | L3: `declarations.spec.mjs:2509`, `declarations.spec.mjs:2558`, `sheets.spec.mjs:29` (+1) |
| SH-1 | MUST | pass | L1/2: `The_engine_loads_nothing_of_blazor`, `The_engine_references_only_the_base_class_library`; inspection: pass (inspect part) (`inspect-rows.md`) |
| SH-2 | MUST | pass | L1/2: `The_dom_is_bounded_at_the_top_and_at_the_far_corner`; L3: `sheet.spec.mjs:809` |
| SH-3 | MUST | pass | L1/2: `A_row_height_past_the_scroll_ceiling_is_refused_by_name`, `A_row_height_that_fits_is_taken` |
| SH-4 | MUST | pass | L1/2: `A_fill_on_a_cell_repaints_its_row_alone`, `A_fill_on_a_whole_row_repaints_that_row_alone`, `A_line_on_a_cell_repaints_the_rows_either_side_of_its_edge_alone` (+4) |
| SH-5 | MUST | pass | L1/2: `Deleting_a_referenced_cell_is_ref`, `Deleting_a_referenced_cell_makes_the_formula_ref_error`, `Entries_formats_and_alignment_move` (+3); L3: `sheet.spec.mjs:500`, `sheet.spec.mjs:539`, `sheet.spec.mjs:571` |
| SH-6 | MUST | pass | L1/2: `SH6_each_reference_form_round_trips_in_the_invariant_syntax_in_every_culture` |
| SH-7 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/ExcelCaseTests.cs:45-205 per-function [Theory] over ExcelCases/*.json`, `tests/ExSheet.Engine.Tests/ExcelCaseTests.cs:459 Every_corpus_file_is_read_by_a_theory` (+3) |
| SH-8 | MUST | pass | L1/2: `A_change_nothing_reads_recomputes_nothing`, `Entering_a_formula_recomputes_it_and_its_dependents`, `Only_dependents_of_a_change_recompute` (+5) |
| SH-9 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/ExcelCaseTests.cs:37 Errors [Theory] (ExcelCases/errors.json ERR-078..081, 083, 084, 091-093)`, `tests/ExSheet.Engine.Tests/VolatileTests.cs:50 A_computed_reference_to_itself_is_circ` |
| SH-10 | MUST | pass | L1/2: `A_number_too_wide_is_hashed_by_the_grid`, `Numbers_and_dates_are_numbers_to_the_grid` |
| SH-11 | MUST | pass | L1/2: `A_constant_is_recorded_parsed`, `A_document_opened_under_another_culture_keeps_its_numbers`, `A_typed_date_under_ja_jp_is_a_serial` |
| SH-12 | MUST | pass | L1/2: `A_whole_sheet_format_round_trips_through_the_document`, `An_unknown_version_is_refused`, `Formatting_round_trips` (+2) |
| SH-13 | MUST | pass | L1/2: `A_consumer_command_is_undone_in_its_place`, `A_fill_copy_is_one_step`, `A_fill_is_one_step` (+9); L3: `sheets.spec.mjs:29`, `sheets.spec.mjs:51` |
| SH-14 | MUST | pass | L1/2: `A_copy_carries_values_outward`, `Copying_a_formula_from_B1_to_B2_pastes_the_shifted_formula`, `Excels_invariant_number_is_read_as_that_number_under_de_DE` (+1); L3: `declarations.spec.mjs:2371` |
| SH-15 | MUST | pass | L1/2: `A_date_goes_on_by_day`, `A_formula_shifts_its_references`, `Item_1_is_refused_and_the_user_is_told` (+1) |
| SH-16 | MUST | pass | L1/2: `A_copy_reaching_a_waiting_cell_is_refused`, `A_copy_reaching_getting_data_is_refused_in_the_engines_words`, `A_new_snapshot_recalculates_only_readers` (+5); L3: `sheet.spec.mjs:599` |
| SH-17 | MUST | pass | L1/2: `ADR0180_a_seed_given_draws_the_same_whenever_the_document_is_opened`, `ADR0180_each_edit_draws_again_and_the_engine_alone_follows`, `ADR0180_the_engine_alone_computes_the_same_draws_from_a_document` (+7) |
| SH-18 | MUST | pass | L3: `sheet.spec.mjs:28`, `sheet.spec.mjs:52`, `sheet.spec.mjs:77` (+8) |
| SH-20 | MUST | pass | L1/2: `ADR0060_ADR0047_General_is_ExSheets_at_twelve_characters_or_wider`, `ADR0060_Rounding_carries_as_ExSheets_General`, `ADR0060_reading_A_minus_sign_is_not_one_of_the_eleven` (+5) |
| SH-21 | MUST | pass | L1/2: `A_formatted_column_formats_what_is_typed_later`, `A_whole_column_widens_for_every_number_it_holds`, `Aligning_whole_rows_records_the_rows` (+9) |
| SH-22 | MUST | pass | L1/2: `A_default_width_is_not_recorded`, `A_resize_is_recorded_in_the_document_as_one_step`, `A_version_3_document_opens_at_default_widths` (+11) |
| SH-23 | MUST | pass | L1/2: `A_paste_is_not_taken_for_a_fill`, `Ctrl_d_copies_a_date_rather_than_continuing_it`, `Ctrl_d_copies_a_formula_down_with_its_references_shifted` (+10) |
| SH-24 | MUST | pass | L1/2: `Delete_clears_the_selection_as_one_step_and_keeps_it`, `Delete_keeps_them`, `Delete_over_a_whole_column_clears_its_entries_and_keeps_the_format` (+1) |
| SH-25 | MUST | pass | L1/2: `A_match_in_any_row_is_found`, `A_selected_range_is_searched_and_stands`, `Ctrl_f_opens_the_find_panel` (+3) |
| SH-26 | MUST | pass | L1/2: `A_column_the_user_sized_never_widens`, `A_longer_entry_into_a_widened_column_is_undone_with_its_width`, `A_reopened_documents_widths_keep_their_kind` (+8); L3: `sheet-vs-excel.spec.mjs:788` |
| SH-27 | MUST | pass | L1/2: `Ctrl_enter_of_the_last_copys_text_is_typed_not_pasted`, `Ctrl_enter_shifts_a_formulas_references_from_the_focus`, `Ctrl_enter_shifts_relative_to_a_focus_not_at_the_top_left` (+9); L3: `sheet-vs-excel.spec.mjs:344` |
| SH-28 | MUST | pass | L1/2: `DC45_a_caret_the_user_moved_is_placed_as_a_selection`, `DC45_a_caret_where_typing_left_it_is_placed_as_a_caret_at_the_end`, `DC45_a_selection_carried_is_placed_as_the_selection_answered` (+27); L3: `declarations.spec.mjs:1329`, `declarations.spec.mjs:1492`, `declarations.spec.mjs:1506` (+3) |
| SH-29 | MUST | pass | L1/2: `A_command_given_on_hearing_the_end_comes_after_the_committed_value`, `A_linked_table_declaration_is_taken`, `A_linked_table_snapshot_is_taken` (+19); L3: `sheet.spec.mjs:619`, `sheet.spec.mjs:640`, `sheet.spec.mjs:669` |
| SH-30 | MUST | pass | L1/2: `An_unfinished_formula_is_outlined_as_far_as_it_goes`, `SH30_a_declared_table_waiting_for_its_data_is_answered`, `SH30_a_range_typed_as_far_as_its_colon_answers_its_first_corner` (+22) |
| SH-31 | MUST | pass | L1/2: `A_cancel_tells_the_empty_list`, `A_column_read_twice_in_any_case_is_told_once_as_declared`, `A_columns_colour_follows_the_references_before_it` (+7); L3: `declarations.spec.mjs:1769`, `declarations.spec.mjs:1800`, `declarations.spec.mjs:1819` (+1) |
| SH-32 | MUST | pass | L1/2: `ADR0058_a_boolean_is_written_in_capitals_and_an_error_not_at_all`, `ADR0058_a_cell_is_the_lookup_of_its_row_by_key`, `ADR0058_a_column_is_its_structured_reference` (+33); L3: `pointing-scope.spec.mjs:126`, `pointing-scope.spec.mjs:150`, `pointing-scope.spec.mjs:171` (+3) |
| SH-33 | MUST | pass | L1/2: `A_declaration_with_another_key_replaces_the_held_one`, `A_key_is_declared_recorded_and_raised`, `A_keyed_snapshot_without_a_repeat_is_taken` (+9) |
| SH-34 | MUST | pass | L1/2: `ADR0057_text_written_from_outside_straight_after_the_first_character_is_shown_selected`, `ADR0058_a_lookup_written_straight_after_the_equals_sign_is_shown_selected`, `ADR0058_the_row_a_lookup_finds_is_matched_as_xlookup_matches` (+10); L3: `pointing-scope.spec.mjs:455`, `pointing-scope.spec.mjs:520`, `pointing-scope.spec.mjs:550` |
| SH-35 | MUST | pass | L1/2: `A_caret_not_known_yet_is_waited_for`, `A_cell_the_grid_no_longer_holds_writes_nothing_and_tells_why`, `A_drag_held_from_a_header_press_takes_back_nothing_an_arrow_wrote` (+30); L3: `pointing-scope.spec.mjs:193`, `pointing-scope.spec.mjs:247`, `pointing-scope.spec.mjs:425` (+10) |
| SH-36 | MUST | pass | L1/2: `ADR0058_Q49_the_list_opens_before_what_ends_the_argument`, `ADR0058_Q53_end_at_a_references_place_writes_nothing_and_commits_nothing`, `ADR0058_Q53_home_at_a_references_place_points_at_the_rows_first_column` (+66); L3: `declarations.spec.mjs:220`, `declarations.spec.mjs:246`, `declarations.spec.mjs:275` (+7) |
| SH-37 | MUST | pass | L1/2: `ADR0058_a_range_of_columns_is_refused_by_the_grammar` |
| SH-38 | MUST | pass | L1/2: `A_cell_nothing_formats_shows_the_defaults`, `A_copy_carries_them`, `A_document_recording_both_sides_of_an_edge_shows_the_same_edges` (+37) |
| SH-39 | MUST | pass | L1/2: `The_paper_and_the_ink_default_to_white_and_black_in_every_scheme`, `The_sheet_chromes_stylesheet_leaves_the_paper_alone`, `The_wrappers_stylesheet_leaves_the_paper_alone`; L3: `part-c.spec.mjs:429`, `sheet-paper.spec.mjs:57`, `sheet-paper.spec.mjs:118` (+6) |
| SH-40 | MUST | pass | L1/2: `A_blank_cell_has_no_colour`, `A_number_beside_a_lone_text_section_has_no_colour`, `A_number_formats_colour_wins_over_the_font_colour` (+13); L3: `sheet-paper.spec.mjs:57` |
| SH-41 | MUST | pass | L1/2: `A_bold_number_that_fits_only_the_regular_widths_is_hashes`, `General_fits_a_bold_number_to_the_bold_widths`; L3: `sheet-paper.spec.mjs:694`, `sheet-paper.spec.mjs:711`, `sheet-paper.spec.mjs:733` (+1) |
| SH-42 | MUST | pass | L1/2: `A_key_excel_does_not_have_changes_nothing`, `A_key_formats_the_selection_it_carries_before_exsheet_hears_it`, `A_record_outside_a_run_directory_is_refused` (+10); L3: `format-cells-mud.spec.mjs:122`, `format-cells.spec.mjs:147`, `format-keys.spec.mjs:44` (+3); by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass |
| SH-43 | MUST | pass | L1/2: `A_key_while_exsheet_still_hears_an_edit_open_is_refused`, `CellFormatAt_answers_while_an_edit_is_open`, `In_the_formula_bar_a_formatting_key_changes_nothing` (+3); L3: `format-keys.spec.mjs:141` |
| SH-44 | MUST | pass | L1/2: `A_cells_outline_is_its_four_sides`, `A_change_keeps_what_differs_from_cell_to_cell`, `A_change_names_its_rows_and_changes_no_value` (+23) |
| SH-45 | MUST | pass | L1/2: `A2_under_a1s_thick_bottom_opens_with_a_thick_top_case_14_13`, `A_built_in_opening_under_custom_is_spelled_as_the_culture_spells_it_case_14_11`, `A_code_of_its_own_opens_as_it_was_typed_case_14_11` (+92); L3: `format-cells-mud.spec.mjs:43`, `format-cells-mud.spec.mjs:65`, `format-cells-mud.spec.mjs:87` (+24) |
| SH-46 | MUST | pass | L1/2: `Between_two_fills_the_gridline_takes_the_lower_or_right_cells`; L3: `sheet-borders.spec.mjs:84`, `sheet-borders.spec.mjs:120`, `sheet-borders.spec.mjs:142` (+10) |
| SH-47 | MUST | pass | L1/2: `The_direction_is_one_way`, `The_package_references_exsheet_the_wrapper_and_mudblazor`, `The_package_serves_no_script` (+1); inspection: pass (inspect part) (`inspect-rows.md`) |
| SH-48 | MUST | pass | L1/2: `A_sheet_shows_no_toolbar_unless_asked`, `Hiding_and_showing_keeps_the_grid`, `The_grid_is_given_what_the_toolbar_leaves` (+2); L3: `sheet-toolbar.spec.mjs:109`, `sheet-toolbar.spec.mjs:299` |
| SH-49 | MUST | pass | L1/2: `Declared_content_is_laid_out_in_markup_order`, `Items_reach_the_sheet_only_through_public_members`, `The_default_row_is_the_formatting_commands_in_order` |
| SH-50 | MUST | pass | L1/2: `A_pointer_press_does_not_take_focus`, `A_press_toggles_bold_as_one_step`, `A_pressed_choice_closes_the_list_and_gives_the_keyboard_back` (+9); L3: `sheet-toolbar.spec.mjs:132`, `sheet-toolbar.spec.mjs:153`, `sheet-toolbar.spec.mjs:306` |
| SH-51 | MUST | pass | L1/2: `A_row_whose_latest_record_is_not_a_pass_is_refused`, `Ctrl_F1_is_not_claimed_unbound`, `Ctrl_F1_toggles_a_bound_toolbar` (+1); L3: `sheet-toolbar.spec.mjs:579`; by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass |
| SH-52 | MUST | pass | L1/2: `A_consumers_letters_reach_its_item`, `A_consumers_o_beside_format_cells_is_refused_by_name`, `A_list_opened_from_the_keys_takes_the_keyboard` (+10); L3: `sheet-toolbar.spec.mjs:326`, `sheet-toolbar.spec.mjs:358`, `sheet-toolbar.spec.mjs:404` (+10); by hand, Windows: `verification/2026-10-18-windows-18b/report.md` at `28ab5e1bf8c1`, pass |
| SH-53 | MUST | pass | L1/2: `Any_other_selection_changes_nothing_and_says_why`, `Ctrl_minus_over_whole_rows_deletes_them`, `Ctrl_plus_over_whole_rows_inserts_rows_above` (+6); L3: `part-c.spec.mjs:520`, `part-c.spec.mjs:545`, `part-c.spec.mjs:558` |
| SH-54 | MUST | pass | L1/2: `On_the_paper_every_wash_multiplies_and_nothing_drawn_on_top_does`; L3: `part-c.spec.mjs:85`, `part-c.spec.mjs:116`, `part-c.spec.mjs:134` (+4) |
| SH-55 | MUST | pass | L1/2: `Normal_font_is_recorded_over_a_rows_font`, `Normal_font_sets_every_part_to_its_default`, `Normal_font_sets_the_default_font_over_a_rows` (+2) |
| SH-56 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/SheetDayTests.cs:19 Today_waits_until_the_day_is_known`, `tests/ExSheet.Engine.Tests/SheetDayTests.cs:33 A_branch_not_taken_still_waits` (+4) |
| SH-57 | MUST | pass | L1/2: `SH57_a_day_changed_away_from_midnight_shows_within_an_hour`, `SH57_readings_that_find_the_same_day_repaint_nothing_and_go_on_each_hour` |
| SH-58 | MUST | pass | L1/2: `tests/ExGrid.Components/BrowserTimeZoneTests.cs:31 A_grid_no_one_asks_does_not_ask_the_browser`, `tests/ExGrid.Components/BrowserTimeZoneTests.cs:39 A_listening_consumer_is_told_the_zone` (+2); L3: `tests/ExGrid.Browser/sheet-today.spec.mjs:32 "ADR-0121/0122: TODAY() is the day in the browser's zone, Pacific/Kiritimati / Pago_Pago"` |
| SH-59 | MUST | pass | L1/2: `ADR0124_indirect_follows_a_cell_its_text_does_not_name`, `ADR0124_indirect_r1c1_text_to_its_own_cell_is_circ`, `ADR0124_indirect_reads_a_cell_computed_in_the_same_recalculation` (+6) |
| SH-60 | MUST | pass, accepted as is | L1/2: `tests/ExSheet.Components/SheetDayWiringTests.cs:134 Now_moves_on_each_minute`, `tests/ExSheet.Engine.Tests/VolatileTests.cs:73 Now_answers_the_moment_it_is_given` (+2); L3: `tests/ExGrid.Browser/sheet-today.spec.mjs:38 "ADR-0124: NOW() is the moment in the browser's zone"`. Not asserted at layer 3: Only NOW() in the browser's zone on a page; no layer-3 check of #GETTING_DATA, the minute turning, or the Consumer's zone for NOW. |
| SH-61 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/LinkedTableTests.cs:518 A_key_of_several_columns_is_declared`, `tests/ExSheet.Engine.Tests/LinkedTableTests.cs:540 A_repeated_key_of_several_columns_is_refused` (+4) |
| SH-62 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/SpillTests.cs:23 A_range_spills_from_its_anchor`, `tests/ExSheet.Engine.Tests/SpillTests.cs:37 A_spill_is_not_recorded_and_reopens` (+5) |
| SH-63 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/SpillTests.cs:229 A_spill_reference_reads_the_spill_range`, `tests/ExSheet.Engine.Tests/SpillTests.cs:256 A_spill_reference_to_no_spill_is_ref` (+2) |
| SH-64 | MUST | pass | L1/2: `tests/ExSheet.Engine.Tests/ExcelCaseTests.cs:186-204 filter/unique/sort/sortby/sequence/transpose Theories`, `tests/ExSheet.Engine.Tests/ExcelCases/sumproduct.json` (+4) |
| SH-65 | MUST | pass | L1/2: `tests/ExSheet.Components/SpillWiringTests.cs:27 A_spill_paints_its_values`, `tests/ExSheet.Components/SpillWiringTests.cs:35 The_bar_shows_the_anchors_formula_dimmed` (+4) |
| SH-66 | MUST | pass | L1/2: `ADR0180_a_change_draws_again_and_the_clock_does_not`, `ADR0180_a_document_records_no_seed_and_reopens_at_zero_changes`, `ADR0180_a_let_binding_is_one_draw` (+11) |
| SH-67 | MUST | pass | L1/2: `ADR0181_a_bound_functions_name_followed_by_a_parenthesis_is_still_its_call`, `ADR0181_a_bound_range_follows_a_change_inside_it`, `ADR0181_a_cycle_through_an_unused_binding_is_circ` (+11) |
| SH-68 | MUST | pass | L1/2: `ADR0047_SH68_a_call_anywhere_in_a_cells_formula_is_nested`, `ADR0047_SH68_a_call_in_a_branch_never_taken_is_nested_by_its_text`, `ADR0047_SH68_a_cell_spilled_from_a_formula_that_calls_subtotal_is_nested` (+10) |
| SH-19 | OBSERVATIONAL | recorded | L3: `measure-sheet.spec.mjs:108`; `measure-coreclr.log` (CoreCLR); `measure/README.md`, `measure/metrics-sh19.json` (circuit); explicit measurements, not run in Step 1, as Step 2 allows |
| FD-1 | MUST | pass | L1/2: `Ctrl_f_from_a_column_menu_keeps_the_keyboard_on_the_root_until_the_field_takes_it`, `Ctrl_f_from_a_column_menu_opens_the_find_panel_in_its_place`, `Ctrl_f_from_the_context_menu_opens_the_find_panel_in_its_place` (+3); L3: `find.spec.mjs:29`, `find.spec.mjs:105`, `find.spec.mjs:120` (+4) |
| FD-2 | MUST | pass | L1/2: `Ctrl_f_opens_the_find_panel`, `Ctrl_f_with_nothing_to_search_is_refused`; L3: `find.spec.mjs:216` |
| FD-3 | MUST | pass | L1/2: `A_press_on_the_rows_closes_the_panel`, `Ctrl_f_opens_the_find_panel`; L3: `find.spec.mjs:29`, `find.spec.mjs:172` |
| FD-4 | MUST | pass | L1/2: `A_step_asks_with_the_focus_the_columns_and_the_version`, `A_step_over_a_range_searches_it_and_keeps_it`; L3: `find.spec.mjs:79` |
| FD-5 | MUST | pass | L1/2: `A_bound_in_memory_source_answers`, `A_found_cell_becomes_the_focus_and_is_revealed`, `A_match_outside_the_window_is_scrolled_to` (+1); L3: `find.spec.mjs:29`, `find.spec.mjs:67`, `find.spec.mjs:79` |
| FD-6 | MUST | pass | L1/2: `A_new_step_cancels_the_one_that_is_out`, `An_answer_under_a_stale_version_moves_nothing`, `Not_found_is_refused_and_shown`; L3: `find.spec.mjs:94` |
| FD-7 | MUST | pass | L1/2: `A_lone_match_finds_itself`, `A_scope_confines_the_walk`, `A_step_walks_by_rows_from_the_cell_after_from` (+6) |
| FD-8 | MUST | pass | L1/2: `Ctrl_f_from_a_column_menu_opens_the_find_panel_in_its_place`, `Escape_closes_and_the_text_survives`, `The_panel_is_placed_inside_the_grids_box`; L3: `find.spec.mjs:185`, `find.spec.mjs:199` |
| FD-9 | MUST | pass | L1/2: `A_source_without_find_says_it_cannot`, `The_fetching_source_finds_through_its_delegate` |
| FD-10 | MUST | pass | L1/2: `On_find_beside_a_source_is_refused_by_name` |
| FD-11 | MUST | pass | L1/2: `A_column_that_left_while_the_step_was_out_is_order_changed`, `An_answer_outside_the_request_throws_naming_it` |
| PV-1 | MUST | pass | L1/2: `The_engine_references_only_the_base_class_library_and_the_data_package` |
| PV-2 | MUST | pass | L1/2: `tests/ExPivot.Components/PivotRenderingTests.cs:32 The_report_is_one_grid_with_its_labels_pinned`, `tests/ExPivot.Components/PivotRenderingTests.cs:64 Roles_paint_as_row_kinds` (+4) |
| PV-3 | MUST | pass, accepted as is | L1/2: `A_date_is_its_clock_value`, `A_non_finite_double_is_the_error_item`, `A_number_is_its_value_in_every_slice` (+2); L3: `tests/ExGrid.Browser/pivot-csv.spec.mjs:252 "ADR-0060: a number Item's label in General paints at most eleven characters, as Excel labelled the same file"`. Not asserted at layer 3: Only the number-Item-label-in-General clause is read in a browser; case-insensitive text, dates, Blank, and ordering clauses are not at layer 3. |
| PV-4 | MUST | pass | L1/2: `ADR0130_money_is_summed_exactly`, `A_question_over_columns_boxes_nothing`, `An_exact_sum_past_decimals_range_is_a_double` (+5) |
| PV-5 | MUST | pass | L1/2: `tests/ExPivot.Engine.Tests/HiddenItemAndCubeTests.cs:11 A_hidden_row_item_leaves_the_totals`, `tests/ExPivot.Engine.Tests/HiddenItemAndCubeTests.cs:32 A_hidden_column_item_leaves_its_column_and_the_row_totals` (+2) |
| PV-6 | MUST | pass | L1/2: `tests/ExPivot.Engine.Tests/RowFormTests.cs:19-299 (Compact/Outline/Tabular, subtotals top/bottom/off, grand total, collapsed, Values in rows, no row field)`, `tests/ExPivot.Engine.Tests/ColumnTests.cs:10 (grand total column, subtotals off)` (+2) |
| PV-7 | MUST | pass, accepted as is | L1/2: `ADR0060_PV7_A_copy_carries_the_full_precision_number`, `ADR0060_PV7_A_copy_carries_the_whole_number`, `ADR0060_PV7_A_number_Items_copy_carries_the_number_in_full`; L3: `tests/ExGrid.Browser/pivot-csv.spec.mjs:220 'ADR-0060: a value in General paints at most eleven characters, as Excel's PivotTable showed the same file'`, `tests/ExGrid.Browser/pivot-csv.spec.mjs:252 (number Item label in General)` (+1). Not asserted at layer 3: Eleven-character General, number-Item label and full-precision copy of the header are covered; a value cell copy and the refusal of a format the engine cannot write are not at layer 3. |
| PV-8 | MUST | pass | L1/2: `tests/ExPivot.Engine.Tests/LayoutEditTests.cs:22-164 (tick, untick, place, move, remove, Σ Values only Rows/Columns)`, `tests/ExPivot.Components/FieldListTests.cs:30 Ticking_places_fields_and_raises_the_layout` (+2) |
| PV-9 | MUST | pass | L1/2: `A_failed_refresh_is_a_stale_report_under_mudblazor`, `Defer_through_the_chrome`, `Filter_keeps_the_earlier_items_listed_while_the_new_ones_are_on_their_way` (+11); L3: `tests/ExGrid.Browser/pivot.spec.mjs:128 for (chrome of ['builtin','mud']) loop: 130 field dragged onto an Area, 141 entry reorder/remove, 896 command from a field's menu moves the field, 968 Layout menu choice` |
| PV-10 | MUST | pass | L3: `pivot.spec.mjs:214`, `pivot.spec.mjs:236`, `pivot.spec.mjs:263` |
| PV-11 | MUST | pass | L1/2: `A_late_dismissal_leaves_the_replacement_surface_open`, `Every_select_names_the_element_that_takes_its_keyboard`, `The_layout_menu_takes_the_keyboard`; L3: `pivot.spec.mjs:743`, `pivot.spec.mjs:759`, `pivot.spec.mjs:779` (+3) |
| PV-12 | MUST | pass | L1/2: `The_bands_filter_opens_under_the_toolbar`, `The_toolbar_makes_the_built_ins_layout`; L3: `tests/ExGrid.Browser/pivot.spec.mjs:923 'ADR-0061: the report filter band filters the report'`, `tests/ExGrid.Browser/pivot.spec.mjs:943 "ADR-0061: the band's Filter… opens under the Pivot Toolbar, over the report, and a press beside it closes it"` (+1) |
| PV-13 | MUST | pass, accepted as is | L1/2: `A_change_of_values_keeps_the_selection`; L3: `tests/ExGrid.Browser/pivot.spec.mjs:303 'ADR-0059: the − button collapses an Item, and the Focus stays on it'`. Not asserted at layer 3: The ± button collapses and keeps the Focus; expand via Context Menu or double click on the label, and a values-only refresh keeping Row Sequence Version and Selection, are not at layer 3. |
| PV-14 | MUST | pass | L1/2: `A_bundled_tab_survives_a_refresh`, `A_consumer_that_listens_takes_the_records`, `A_new_tab_takes_the_keyboard` (+21); L3: `tests/ExGrid.Browser/pivot.spec.mjs:316 "a double click on a value opens a tab at the report's foot, titled by the cell ..."`, `tests/ExGrid.Browser/pivot.spec.mjs:373 "Show Details opens ExPivot's dialog when the page asks for one ..."` (+2) |
| PV-15 | MUST | pass | L1/2: `A_stale_change_renders_no_grid_row`, `The_pane_and_the_toolbar_do_not_render_the_grid`, `The_pane_does_not_render_the_tabs` |
| PV-16 | MUST | pass | L1/2: `A_query_round_trips` |
| PV-17 | MUST | pass | L1/2: `tests/ExPivot.Components/ReportCommandTests.cs:166 Two_pivots_are_independent` |
| PV-18 | MUST | pass, accepted as is | L1/2: `tests/ExPivot.MudBlazor.Tests/WrapperStylesheetTests.cs:43 Every_visual_token_is_mapped_onto_the_palette`, `tests/ExPivot.MudBlazor.Tests/WrapperStylesheetTests.cs:55 The_wrapper_sets_no_geometry_token` (+2); L3: `tests/ExGrid.Browser/pivot.spec.mjs:1227 'ADR-0062/0030: the palette reaches the pane, the entries and the − button, light and dark'`, `tests/ExGrid.Browser/pivot-live.spec.mjs:196 (stale value colour, built-in and mud, light and dark)`. Not asserted at layer 3: Palette reaches the pane, entries and − button in both schemes and the stale token is probed under the Wrapper mapping; not every Visual Token is read in a browser. |
| PV-19 | MUST | pass | L1/2: `tests/ExPivot.MudBlazor.Tests/MudPivotChromeTests.cs:372 The_grid_menu_speaks_ExPivots_words`, `tests/ExPivot.MudBlazor.Tests/MudPivotChromeTests.cs:389 The_consumers_words_reach_the_grid_menu` |
| PV-20 | MUST | pass | L3: `grid-live.spec.mjs:288`, `pivot-db.spec.mjs:298`, `pivot-db.spec.mjs:318` (+1) |
| PV-21 | OBSERVATIONAL | recorded | L3: `measure-pivot.spec.mjs:265`, `measure-pivot.spec.mjs:379`, `measure-pivot.spec.mjs:416` (+2); `measure-coreclr.log`; `measure/README.md`, `measure/ab/pv21-table.txt`; explicit measurements, not run in Step 1, as Step 2 allows |
| PV-22 | MUST | pass | L1/2: `ADR0066_Items_are_over_all_the_data_with_the_Blank_last_and_the_total`, `ADR0066_PV22_Items_are_the_reference_Items`, `ADR0066_PV22_an_unknown_field_and_an_unoffered_part_are_refused_in_the_same_words` (+10) |
| PV-23 | MUST | pass | L1/2: `ADR0066_PV23_after_a_live_tick_the_old_version_is_refused_and_the_answers_follow_the_data`, `A_late_listing_older_than_the_one_in_view_is_set_aside`, `A_late_listing_that_is_the_newest_stays_in_view` (+18) |
| PV-24 | MUST | pass | L1/2: `A_layout_with_an_aggregation_not_offered_is_refused_by_name`, `A_part_no_offered_aggregation_reads_is_refused_without_asking`, `A_refusal_is_an_error_mud_alert` (+3) |
| PV-25 | MUST | pass | L1/2: `A_failed_first_question_keeps_the_layout`, `A_failure_leaves_the_report_as_it_was_and_says_so`, `A_further_change_cancels_the_question_in_flight` (+3) |
| PV-26 | MUST | pass | L1/2: `A_change_the_held_answer_lays_out_asks_nothing`, `A_filters_field_that_hides_nothing_asks_nothing`, `A_gesture_from_the_answer_held_supersedes_the_build` (+3) |
| PV-27 | MUST | pass | L1/2: `A_cancelled_question_stops_at_the_next_slice`, `A_question_cancelled_before_it_starts_reads_nothing`, `A_slice_ends_when_its_budget_is_spent` (+3) |
| PV-28 | MUST | pass | L1/2: `A_report_gesture_while_deferring_is_applied_and_kept`, `Defer_holds_the_report_until_update`, `Defer_makes_the_built_ins_layout` (+4) |
| PV-29 | MUST | pass | L1/2: `A_layout_over_the_column_cap_is_refused_by_name`, `A_layout_over_the_leaf_cap_is_refused_by_name`, `A_layout_over_the_row_cap_is_refused_by_name` (+5) |
| PV-30 | MUST | pass | L1/2: `A_failed_refresh_is_a_stale_report`, `A_layout_choice_changes_the_report`, `Escape_and_the_backdrop_close_the_layout_menu` (+11); L3: `tests/ExGrid.Browser/pivot.spec.mjs:907 'the Pivot Toolbar above the report holds the report filter band on its left, then Layout and the Field List's toggle'`, `tests/ExGrid.Browser/pivot.spec.mjs:968 'the Layout menu opens under its button ...'` (+3) |
| PV-31 | MUST | pass | L1/2: `A_declared_item_order_comes_first`, `A_key_function_that_throws_is_refused_by_name`, `A_numbers_key_is_given_the_number_and_keys_are_of_one_type` (+7) |
| PV-32 | MUST | pass | L1/2: `A_part_is_a_field_like_any_other`, `A_part_is_of_a_date_field`, `A_part_over_untyped_accessors_answers_alike` (+8) |
| PV-33 | MUST | pass | L1/2: `A_copy_refused_on_the_report_is_said_in_japanese`, `A_paste_refused_on_the_report_is_said_in_japanese`, `Every_id_the_pivot_uses_has_a_japanese_word` (+6) |
| PV-34 | MUST | pass | L1/2: `A_batch_applied_while_the_held_answer_is_assembled_waits_for_it`, `Every_leaf_of_a_folded_answer_equals_a_fresh_aggregation` |
| PV-35 | MUST | pass | L1/2: `A_batch_during_the_build_waits_for_it`, `A_change_of_values_keeps_the_selection`, `A_negative_interval_is_refused` (+10) |
| PV-36 | MUST | pass | L1/2: `A_change_the_format_hides_is_not_marked`, `A_layout_change_with_new_data_in_the_same_redraw_marks_nothing`, `A_mark_lasts_from_the_version_that_changed_the_cell` (+9); L3: `pivot-live.spec.mjs:153`, `pivot-live.spec.mjs:276` |
| PV-37 | MUST | pass | L1/2: `A_failed_refresh_before_the_first_report_is_said_on_the_toolbar`, `A_failed_refresh_is_a_stale_report_under_mudblazor`, `A_failed_refresh_is_a_stale_report` (+17); L3: `pivot-live.spec.mjs:196` |
| PV-38 | MUST | pass | L1/2: `A_notice_of_the_version_on_screen_asks_nothing`, `A_servers_notice_asks_again_for_the_whole_answer`, `Refresh_asks_a_servers_source_again_once` (+1) |
| PV-39 | MUST | pass | L1/2: `An_escape_that_closes_the_records_menu_leaves_the_dialog_standing`, `Closing_a_tab_while_the_reports_tab_is_selected_gives_the_report_the_keyboard`, `Closing_the_last_details_tab_behind_the_report_gives_the_report_the_keyboard_back` (+5); L3: `tests/ExGrid.Browser/pivot.spec.mjs:432 'Escape in the dialog's grid, then ↓ typed at once ...' (80 ms)`, `tests/ExGrid.Browser/pivot.spec.mjs:456 'Escape on the dialog's Close, then ↓, → typed at once ...' (80 ms)` (+3) |
| PV-40 | MUST | pass | L1/2: `A_batch_applied_while_the_held_answer_is_assembled_waits_for_it`, `A_batch_during_the_build_waits_for_it`, `A_gesture_during_the_build_supersedes_the_question` (+17) |
| PV-41 | MUST | pass | L1/2: `A_new_details_tab_takes_the_keyboard_from_the_reports_grid`, `A_new_tab_takes_the_keyboard`, `Closing_a_tab_before_the_selected_one_keeps_the_selection` (+6); L3: `pivot.spec.mjs:592`, `pivot.spec.mjs:616` |
| PV-42 | MUST | pass | L1/2: `A_key_prints_its_role_value_field_and_items`, `A_redraw_that_adds_an_item_keeps_every_painted_keys_component`, `A_reports_row_keys_are_distinct` (+4) |
| PV-43 | MUST | pass | L1/2: `Reading_every_rows_key_allocates_nothing` |
| PV-45 | MUST | pass | L1/2: `The_rows_that_render_are_the_painted_rows_whose_text_changed` |
| PV-47 | MUST | pass | L1/2: `A_redraw_out_of_memory_is_a_stale_report_under_mudblazor`, `A_redraw_out_of_memory_while_the_cube_is_made_leaves_it_stale`, `A_redraw_out_of_memory_while_the_report_is_made_leaves_it_stale` (+1) |
| PV-48 | OBSERVATIONAL | recorded | `measure/README.md`, `measure/ab/pv48-table.txt` |
| PV-49 | MUST | pass | L1/2: `tests/ExPivot.Components/StaleValueCellsTests.cs:207 ADR0067_The_mark_renders_no_row_of_the_grid`, `tests/ExPivot.Components/StaleValueCellsTests.cs:247 ADR0067_The_stylesheet_paints_the_mark_through_its_token_on_the_reports_value_cells` (+6); L3: `tests/ExGrid.Browser/pivot-live.spec.mjs:196 'PV-37/ADR-0067/UX-8: a server cut off leaves a Stale Report whose value cells paint muted and readable ... (chrome, scheme)'` |
| PV-50 | MUST | pass | L1/2: `tests/ExPivot.Components/FieldListTests.cs:372 Keys_typed_on_an_entry_while_its_menu_opens_are_the_menus`, `tests/ExPivot.Components/FieldListTests.cs:391 Escape_and_enter_typed_while_a_menu_opens_close_it_and_open_it_again` (+5); L3: `tests/ExGrid.Browser/pivot.spec.mjs:689 'with a 150 ms round trip, Enter on a field's entry and Enter typed at once press the menu's first command'`, `tests/ExGrid.Browser/pivot.spec.mjs:717 'with a 150 ms round trip, letters typed straight after Enter on a report filter are typed into its Filter…'s search'` |
| DA-1 | MUST | pass | `pre-3.log`, the package check |
| DA-2 | MUST | pass | L1/2: `No_public_member_changes_a_snapshot` |
| DA-3 | MUST | pass | L1/2: `Text_is_exact_and_two_spellings_are_two_entries` |
| DA-4 | MUST | pass | L1/2: `Records_are_kept_by_reference_in_order` |
| DA-5 | MUST | pass | L1/2: `A_build_yields_between_slices_and_reports_its_progress` |
| DA-6 | MUST | pass | L1/2: `A_value_of_the_wrong_kind_fails_the_build_naming_row_and_column`, `A_value_that_cannot_be_read_fails_the_build_by_name` |
| DA-7 | MUST | pass | L1/2: `A_quoted_separator_is_part_of_the_field` |
| DA-8 | MUST | pass | L1/2: `Only_the_opt_in_encoding_refers_to_the_code_pages`, `The_package_is_trimmable_so_a_browser_never_downloads_the_code_pages` |
| DA-9 | MUST | pass | L1/2: `A_suggestion_applies_nothing_until_it_is_handed_back` |
| DA-10 | MUST | pass | L1/2: `tests/ExGrid.Data.Tests/DataReaderTests.cs:78 Each_column_is_read_by_its_own_type`, `tests/ExGrid.Data.Tests/DataReaderTests.cs:125 A_column_of_another_type_is_refused_by_name` (+2) |
| DA-11 | MUST | pass | L1/2: `Only_a_text_or_an_integer_column_can_be_the_key` |
| DA-12 | MUST | pass | L1/2: `The_next_snapshot_shares_the_segments_before_it` |
| DA-13 | MUST | pass | L1/2: `Every_kind_round_trips_with_its_blanks_captions_key_and_version` |
| DA-14 | MUST | pass | L1/2: `Pyarrows_delta_dictionaries_are_read_under_the_snapshots_rules`, `Utf8_is_text` |
| DA-15 | MUST | pass | L1/2: `The_stream_is_uncompressed_and_each_kind_has_its_arrow_type` |
| DA-16 | MUST | pass | `pre-3.log`, `tests/ExGrid.PackageSmoke/check.sh` |
| DA-17 | OBSERVATIONAL | recorded | L3: `measure-pivot.spec.mjs:265`, `measure-pivot.spec.mjs:537`, `measure-pivot.spec.mjs:598` (+1); `measure-coreclr.log`; `measure/README.md`, `measure/ab/pv21-table.txt`; explicit measurements, not run in Step 1, as Step 2 allows |
| DA-18 | MUST | pass, accepted as is | L1/2: `tests/ExGrid.Data.Tests/AggregateAccumulatorTests.cs:20-141 (definitions, finishing from parts)`, `tests/ExPivot.Engine.Tests/LeafAggregateTests.cs:54 Every_total_from_the_leaves_is_the_aggregation_of_its_records` (+2). Not asserted at layers 1–2: Definitions are tested in ExGrid.Data and ExPivot tables are checked per Aggregation, but no test asserts that both ExPivot.Engine and the Selection Summary answer through the same ExGrid.Data accumulators ("inspect the references"). |
| SM-1 | MUST | pass | L1/2: `ADR0130_a_request_carries_the_ranges_the_version_the_columns_and_the_figures`, `ADR0130_only_the_figures_asked_are_answered`, `SM1_a_request_names_the_visible_columns_in_the_current_order` |
| SM-2 | MUST | pass | L1/2: `ADR0130_a_new_selection_never_shows_the_previous_answer`, `ADR0130_an_answer_read_under_another_order_is_dropped`; L3: `selection-summary.spec.mjs:196`, `selection-summary.spec.mjs:255` |
| SM-3 | MUST | pass | L1/2: `ADR0130_an_edit_handed_over_asks_again`, `ADR0130_rows_changing_under_the_selection_ask_again` |
| SM-4 | MUST | pass | L1/2: `ADR0130_a_decline_shows_its_reason`, `ADR0130_a_million_rows_are_summed`, `ADR0130_the_in_memory_source_never_declines`; L3: `selection-summary.spec.mjs:289` |
| SM-5 | MUST | pass | L1/2: `ADR0130_an_existing_source_reports_that_it_cannot_summarise`, `ADR0130_on_summarize_beside_a_source_is_refused`, `ADR0130_the_fetching_source_summarises_through_its_delegate` (+1) |
| SM-6 | MUST | pass | L1/2: `ADR0130_a_bound_source_answers`, `ADR0130_a_cell_under_two_ranges_counts_once`, `ADR0130_a_column_with_no_value_is_blank` (+6); L3: `selection-summary.spec.mjs:64` |
| SM-7 | MUST | pass | L1/2: `ADR0130_an_error_among_the_cells_leaves_count_and_numerical_count`, `ADR0130_an_error_among_the_values_leaves_only_the_two_counts`, `ADR0130_an_error_beside_merged_parts_leaves_only_the_two_counts` (+2); L3: `selection-summary.spec.mjs:109`; L3: `tests/ExGrid.Browser/selection-summary.spec.mjs:109 "an error among the cells leaves Count and Numerical Count, as Excel's status bar (SM-7, ADR-0130, run 17 b5 and b6)"` |
| SM-8 | MUST | pass | L1/2: `ADR0130_the_sql_summary_answers_as_the_reference` |
| SM-9 | MUST | pass | L1/2: `ADR0130_no_menu_without_a_binding`, `ADR0130_the_figures_menu_reports_the_choice`, `ADR0130_the_figures_wear_the_active_cells_format_across_mixed_formats` (+1); L3: `selection-summary.spec.mjs:44`, `selection-summary.spec.mjs:73` |
| SM-10 | MUST | pass | L1/2: `ADR0130_the_figures_are_not_announced` |
| SM-11 | MUST | pass | L1/2: `ADR0130_each_figure_is_a_caption_with_the_cores_text`; L3: `selection-summary.spec.mjs:44`, `selection-summary.spec.mjs:196`, `selection-summary.spec.mjs:255` (+1) |
| SM-14 | MUST | pass | L1/2: `ADR0130_switched_off_a_listener_is_still_told`, `ADR0130_switched_off_nothing_is_shown_or_asked`, `ADR0130_switched_off_the_report_shows_no_summary` (+1) |
| SM-12 | MUST | pass | L1/2: `ADR0130_an_error_leaves_the_counts_and_hides_the_sums`, `ADR0130_formula_values_and_spilled_cells_are_summed` |
| SM-13 | MUST | pass | L1/2: `ADR0130_labels_are_counted`, `ADR0130_subtotals_are_summed_with_the_rest`, `ADR0130_the_report_cells_are_summed_with_the_grand_total` |
| SM-15 | MUST | pass | L1/2: `ADR0130_a_change_inside_the_selection_asks_again`, `ADR0130_a_change_outside_the_selection_moves_no_figure`, `ADR0130_a_changed_row_count_asks_again` (+5) |
| LV-1 | MUST | pass | L1/2: `A_changed_row_keeps_its_component_and_renders_once`, `A_changed_row_that_moved_keeps_its_component`, `A_row_key_equal_to_a_placeholders_position_does_not_clash_with_it` (+5) |
| LV-2 | MUST | pass | L1/2: `A_later_window_with_a_repeated_key_is_refused`, `A_null_row_key_is_refused_by_name`, `A_repeated_key_from_a_source_that_does_not_vouch_is_refused` (+4) |
| LV-3 | MUST | pass | L1/2: `A_changed_key_not_held_is_refused_and_nothing_is_applied`, `A_changed_row_keeps_its_place_and_an_added_row_goes_at_the_end`, `A_changed_row_that_is_the_instance_held_is_refused` (+8) |
| LV-4 | MUST | pass | L1/2: `A_list_with_a_repeated_key_is_refused`, `A_new_order_is_gathered_and_a_later_change_marks_only_its_cells`, `A_new_order_the_sort_does_not_show_changes_nothing` (+6) |
| LV-5 | MUST | pass | L1/2: `The_incremental_requery_equals_the_reference_over_random_batches`; L3: `grid-live-local.spec.mjs:65` |
| LV-6 | MUST | pass | L1/2: `A_change_after_a_quiet_interval_is_applied_at_once`, `A_change_from_another_thread_is_published_on_the_sources_context`, `A_new_order_is_gathered_and_a_later_change_marks_only_its_cells` (+8) |
| LV-7 | MUST | pass | L1/2: `A_batch_that_moves_no_row_keeps_the_version`, `A_batch_that_moves_one_row_moves_the_version`, `A_key_removed_and_added_again_at_its_position_keeps_the_version` (+3); L3: `grid-live-local.spec.mjs:65` |
| LV-8 | MUST | pass | L1/2: `ADR0141_a_trade_cancelled_by_name_leaves_and_is_told`, `ADR0141_a_trade_cancelled_by_name_moves_the_order_token`, `ADR0141_the_order_token_moves_exactly_when_a_trade_is_cancelled_or_booked` (+10); L3: `grid-live.spec.mjs:326` |
| LV-9 | MUST | pass | L1/2: `A_change_the_format_hides_is_not_marked`, `A_change_time_is_kept_for_a_bounded_time`, `A_changed_cell_is_marked_and_the_others_are_not` (+24); L3: `grid-live-local.spec.mjs:35` |
| LV-10 | MUST | pass | L1/2: `A_grid_key_other_than_the_sources_is_checked_by_the_grid`, `A_keyed_source_names_its_rows_and_vouches`, `A_pushed_vouch_without_a_row_key_is_not_taken` (+14) |
| LV-11 | MUST | pass | L1/2: `A_baseline_owed_while_no_row_stands_at_the_editors_place_is_kept_and_a_change_is_told`, `A_baseline_owed_while_the_edited_row_is_out_of_the_window_is_never_read_from_another_row`, `A_change_after_the_answer_under_the_same_editor_is_told` (+25); L3: `write-lands.spec.mjs:88`, `write-lands.spec.mjs:115`, `write-lands.spec.mjs:134` (+2) |
| LV-12 | MUST | pass | L1/2: `A_change_to_any_cell_of_the_pressed_row_refuses_nothing`, `A_pointer_press_on_an_action_aimed_at_the_replaced_source_is_refused`, `A_press_taken_on_the_new_sources_paint_acts` (+27); L3: `write-lands.spec.mjs:150` |
| LV-13 | MUST | pass | L1/2: `A_clear_over_a_changed_target_lands`, `A_ctrl_enter_fill_after_the_order_moved_is_refused_and_keeps_the_editor`, `A_ctrl_enter_fill_asks_for_the_gathered_change_first_and_lands` (+29); L3: `write-lands.spec.mjs:176`, `write-lands.spec.mjs:208`, `write-lands.spec.mjs:233` (+3) |
| LV-14 | MUST | pass | L1/2: `A_paste_aimed_under_an_order_that_has_moved_since_is_refused`, `A_paste_taken_on_a_render_long_replaced_under_the_same_order_lands`, `A_render_that_changes_no_painted_cell_keeps_its_name` (+6); L3: `write-lands.spec.mjs:176` |
| LV-16 | MUST | pass | L1/2: `A_commit_inside_GridSource_Froms_gather_interval_lands_and_keeps_the_gathered_change`, `A_commit_inside_a_gather_interval_lands_with_the_newest_row`, `A_ctrl_enter_fill_asks_for_the_gathered_change_first_and_lands` (+22) |
| LV-17 | MUST | pass | L1/2: `A_baseline_owed_while_no_row_stands_at_the_editors_place_is_kept_and_a_change_is_told`, `A_baseline_owed_while_the_edited_row_is_out_of_the_window_is_never_read_from_another_row`, `A_change_after_the_users_own_write_is_told_alike_in_either_order` (+21); L3: `write-lands.spec.mjs:274`, `write-lands.spec.mjs:304` |
| LV-18 | MUST | pass | L3: `tests/ExGrid.Browser/grid-live-local.spec.mjs:35 'a million trades in this process; a Change Batch reaches the screen and marks what changed, and a pause stops it'`, `tests/ExGrid.Browser/grid-live-local.spec.mjs:65 'under a sort by P&L, the painted rows stay in order as batches move the values'` (+1) |
| LV-19 | MUST | pass | L1/2: `A_later_commit_after_a_refusal_raises_the_intent_again`, `A_press_made_while_a_refused_commit_is_heard_is_answered_as_after_the_refusal`, `A_refusal_after_the_handler_awaited_still_holds_the_editor` (+5) |
| LV-20 | MUST | pass | L1/2: `A_baseline_owed_while_the_edited_row_is_out_of_the_window_is_never_read_from_another_row`, `Every_commit_refusal_is_worded_with_its_cell`, `Space_on_an_action_whose_order_the_gathered_change_moved_is_refused` (+16); L3: `write-lands.spec.mjs:422` |
| LV-21 | MUST | pass | L1/2: `The_grid_holds_no_string_for_the_notice_and_adds_nothing_to_the_row`; L3: `write-lands.spec.mjs:88` |
| LV-22 | MUST | pass | L1/2: `ADR0153_a_baseline_window_evicted_by_publication_does_not_outlive_the_call`, `ADR0160_ADR0153_LV22_after_live_redraws_only_what_the_report_source_keeps_is_alive`, `ADR0160_LV22_the_grid_holds_no_display_row_of_an_earlier_window` (+4) |
| LV-23 | OBSERVATIONAL | recorded | `measure/README.md`, `measure/ab/lv23-table.txt` |
| LV-24 | MUST | pass | L1/2: `Incremental_windows_equal_fresh_reports`; L3: `pivot-db.spec.mjs:73`, `pivot-db.spec.mjs:88` |
| LV-25 | MUST | pass | L1/2: `tests/ExPivot.Components/ReportWindowTests.cs:14 ADR0151_A_remote_report_renders_a_window_with_the_complete_extent`, `tests/ExPivot.Components/ReportShrinkTests.cs:241 ADR0151_A_report_that_ends_inside_the_Window_stays_on_screen_until_its_clamped_Window_lands` (+4); L3: `tests/ExGrid.Browser/pivot-db.spec.mjs:73 'LV-24/ADR-0151: a server report scrolls beyond its first Window with bounded rendered rows'`, `tests/ExGrid.Browser/pivot-db.spec.mjs:88 'LV-24/ADR-0151: a report that shrinks under the Window scrolled to shows its last rows, and scrolls on'` (+1) |
| LV-26 | MUST | pass | L1/2: `ADR0152_a_throwing_registered_order_key_is_refused_by_name_over_json`, `LV26_a_changed_order_key_policy_asks_again_and_reorders`, `LV26_a_changed_policy_that_throws_is_refused_by_name_and_the_shown_report_stays` (+8) |
| LV-27 | MUST | pass, accepted as is | L1/2: `tests/ExPivot.Engine.Tests/WindowChangesDigestTests.cs:42 A_fixed_window_has_a_pinned_digest`, `tests/ExPivot.Engine.Tests/WindowChangesDigestTests.cs:160 Window_Changes_with_no_digest_are_recovered` (+6); L3: `tests/ExGrid.Browser/pivot-live.spec.mjs:276 "the server's changing data marks the values it changed ..."`, `tests/ExGrid.Browser/pivot-db.spec.mjs:73 (server report scroll)`. Not asserted at layer 3: Server-path Window Changes are exercised end to end, but no layer-3 test targets digest verification, mismatch recovery or a stale fallback. |
| LV-28 | MUST | pass | L3: `pivot-db.spec.mjs:268`; L1/2: `tests/ExPivot.Engine.Tests/ReportProtocolTests.cs:108 ADR0152_offscreen_change_advances_metadata_and_versioned_operations_never_rebind`, `tests/ExPivot.Engine.Tests/ReportProtocolTests.cs:306 ADR0152_remote_operations_reject_answers_for_a_different_report_version` (+3) |
| LV-29 | MUST | pass | L1/2: `Incremental_windows_equal_fresh_reports` |
| LV-30 | MUST | pass | L1/2: `ADR0153_One_stable_delegate_reads_immutable_row_changes`, `Incremental_windows_equal_fresh_reports` |
| LV-31 | MUST | pass | L1/2: `ADR0153_A_full_refresh_source_is_asked_to_refresh_on_a_notice` |
| LV-32 | MUST | pass | L1/2: `A_commit_answered_with_another_document_is_never_said_discarded`, `A_commit_whose_handler_hands_over_another_source_and_other_columns_is_said_at_most_once`, `A_commit_whose_handler_hands_over_another_source_ends_as_committed` (+9) |
| LV-33 | MUST | pass | L1/2: `A_key_that_moves_with_nothing_selected_aimed_at_a_replaced_source_places_no_focus`, `Ime_text_composed_with_nothing_selected_against_a_replaced_source_opens_nothing_and_is_said`, `Typing_with_nothing_selected_aimed_at_a_replaced_source_over_a_cell_that_does_not_edit_is_dropped_unsaid` (+2) |
| LV-34 | MUST | pass | L1/2: `ADR0182_LV34_A_Window_read_ahead_is_clamped_into_a_report_that_shrank`, `ADR0182_LV34_A_question_in_flight_that_does_not_cover_the_rows_on_screen_is_superseded`, `ADR0182_LV34_A_remote_source_reads_sixty_rows_ahead_by_default_and_a_local_one_none` (+19); L3: `pivot-db.spec.mjs:130`, `pivot-db.spec.mjs:193` |
| LV-15 | OBSERVATIONAL | recorded | L3: `measure-live.spec.mjs:93`; `measure/README.md`, `measure/ab/lv15-table.txt` |
