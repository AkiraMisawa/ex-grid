# Verification — 2026-09-01

**Scope: a full layer-1/2 run, a chrome-only layer-3 run, and the static inspections.**
This is not the complete release verification of `docs/definition-of-done.md` §22 — the
criteria that need Edge, Windows/Linux, CDP metrics or the randomised consistency run are
recorded as `blocked` below, each with its reason. A criterion that could not be verified
is not passed.

## Environment

- macOS 26.6.2 (Apple Silicon), .NET SDK 10.0.203 (nix), Google Chrome 152.0.7977.65
- Microsoft Edge: **not installed on this machine** — the `msedge` project exists
  (PRE-5) and fails by name here, which ADR-0026 calls the honest outcome
- Platform note: macOS draws overlay scrollbars; the scrollbar suite forces
  layout-occupying bars itself, but VZ-10's "on Windows and Linux" clause is not
  discharged by this machine

## Results

| Layer | Command | Result |
|---|---|---|
| Build | `nix develop -c dotnet build ExGrid.slnx` | 0 warnings, 0 errors (PRE-1) |
| 1 | `layer1.log` | **407 passed, 0 failed, 0 skipped** |
| 2 | `layer2.log` | **292 passed, 0 failed, 0 skipped** |
| 3 (chrome) | `layer3.log` | **36 passed, 0 failed** — plus gestures (real-mouse reorder and resize, the off-screen paste, the 10MB paste, the IME guard, UX-9/10) — scrollbar (DPR 1/1.25/2, forced classic bars), features (editor, clipboard, keys, popovers, two instances), presentation (tokens, computed geometry, transitions, forced colors, dark scheme, the RTL island, the editor's box, the runaway-scroll stop, the error UI), virtualisation (far corner at 10⁵ rows, element-count stability, overlay economy) |

## Static inspections (DoD §22 step 3)

- **PF-1**: no `getBoundingClientRect` / `clientWidth` / `offsetWidth` / `scrollIntoView`
  in `src/` outside comments — pass.
- **PF-2**: `ex-grid.js` holds exactly the four allowlisted uses — capture-phase keydown
  (now mode-gated for the editor), scroll offsets, the clipboard events + async write,
  and the ResizeObserver report (gutter + content-box size; ADR-0028 grows the report,
  not the list) — pass.
- **PRE-4**: no `Mud` / `Fluxor` under `src/` — pass.
- **FN-15**: no ordering or predicate evaluation over the Window in the component. The
  one `Where(` in `ExGrid.razor` orders the filter panel's *checked values* for the In
  clause it emits, not the Window — pass, noted.
- **KB-16**: `grep -ri page src/ExGrid/Keys/` matches only the browser's own
  `PageUp`/`PageDown` key strings — pass.
- **DIR-4**: no CSS logical property — pass.
- **PRE-6**: every file the build needs is git-tracked (all new files `git add`-ed).

## Console capture (CON)

Both layer-3 spec files fail any test on a console `error` or an uncaught page error;
the 36-test run emitted none (CON-1/2 for the scenarios covered). The one message this
surfaced and fixed: the DemoHost's missing favicon 404 — a host defect, not the grid's.

## Pass / blocked, by DoD section

- **PRE**: pass, except PRE-5's *runtime* half — the config declares both browsers;
  only `chrome` could run here. `blocked: Edge not installed`.
- **FN**: FN-1..15 pass (layers 1/2). FN-16 pager pass (L2). FN-17/18 pass (L2 stub
  Chrome). FN-19 pass (L1 + the status line). FN-20 pass (`GetFocusedValue()`: the raw
  value behind `####`, pinned at L2).
- **UX**: UX-1 pass (no colour parameter; stylesheet lengths are token reads — the
  pager/status bar's 2–4px paddings are visual, not grid geometry). UX-2 pass for the
  supported override routes (chrome), with the descendant-re-declaration hole measured
  and recorded as a correction in ADR-0029 rather than passed over. UX-3 pass (chrome:
  computed row/band/padding equal the resolved metrics). UX-4 pass by construction
  (ADR-0029's lists updated alongside every class added). UX-5's paint half pass
  (chrome); its zero-.NET-renders half holds by RR-1 at L2. UX-6 pass (chrome). UX-7
  pass (chrome, forced-colors emulation: every state distinguishable). UX-8 pass
  (chrome, dark emulation, contrast ≥ 4.5). **UX-9 pass — after a real fix**: the
  default `--ex-focus-outline` fallback was Chrome's `Highlight`, which measured
  1.99:1 against `Canvas`; the fallback is now `CanvasText`, which contrasts by
  construction in both schemes. UX-10 pass (chrome: the token narrows the bar to 8px,
  the gutter follows, the Focus stays readable). UX-11 pass (chrome).
- **A11Y**: 1/2/3/5/7/8/11/12/13/14 pass (L2); 4 pass (chrome); 6 L2-half pass;
  9/10 pass at L2 with the fake clock — the real-screen-reader wording remains open by
  ADR-0033's own note.
- **HG**: HG-1..17 pass — 1..13/16/17 at L1/L2, and 14/15 with a real mouse
  (chrome: a leaf drag clamps at its group's edge and never escalates; a grabbed
  rectangle moves the whole group; the declaration survives both).
- **DIR**: DIR-1 pass (L2), DIR-4 pass, DIR-2/3 pass (chrome: the overlay lands on
  its cell inside an RTL ancestor; an Arabic value renders in place).
- **VZ**: VZ-2..9, 11, 12 pass (L1/L2); VZ-1 chrome-shaped pass; VZ-10 pass on the
  forced-bar suite, `blocked` for the Windows/Linux clause.
- **FL / SR**: pass (L1/L2 + SR-1 on chrome).
- **ED**: ED-1, 4..8, 10 pass (L2); ED-2/3/4 pass with real keys (chrome); ED-9 pass
  (chrome: the editor's box equals the cell's within 1px). ED-11's listener guard
  pass (chrome: synthetic composing keydowns and keyCode 229 pass through untaken);
  a run with a real IME remains worth doing where one is installed.
- **SL**: SL-1..13 pass (L1/L2); SL-14/15 pass (chrome: the runaway scroll stops at
  the pointer's leaving, and a released return stays stopped).
- **KB**: KB-1..14, 16 pass; KB-15 pass (chrome); KB-8/12 pass (chrome); KB-17 pass
  (added with the popover-dismissal fix: L2 `FilterChromeTests` toggle/Escape/click-away,
  no blur on close; chrome asserts Escape and the ▾ re-click on the live panel).
- **CP / PST**: CP-1..15 pass across L1/L2, with CP-4/5/6/10/14-read on chrome
  (real clipboard, both formats). PST-1/2/4 pass (L1/L2); PST-3 pass (chrome: the indicator showed, the off-screen paste applied); PST-5 pass (chrome: a 10.4MB TSV paste, next key answered in 6ms; PST-6 recorded in metrics.json).
- **ERR**: ERR-1..8 pass (each refusal named; L1/L2).
- **CON**: CON-1/2/4/8 hold across the 36-test run; CON-5 pass (the error UI stays
  `display: none`); CON-7 pass (`UnobservedExceptionTests`: a busy session, forced
  collection, zero unobserved Task exceptions); CON-3/6 not separately instrumented —
  `blocked: instrumentation not yet written`.
- **PF / MEM / DOM / RR / ASY / ST**: RR-1..10 pass (L2). ASY-1..6 pass. ST-2/4 pass.
  **ST-1 pass** (`ConsistencyTests`: 500 randomised operations, recorded seeds, at 200
  and at 100,000 rows). ST-3 pass (chrome, with UX-3). **MEM-1 pass**
  (`AllocationTests`: steady-state scrolling allocates the same at 10³ and 10⁶ rows).
  DOM-2/3-shaped and DOM-4 pass (chrome). DOM-5, BIG-7 and PST-6 are **recorded** in
  `metrics.json` (246 elements / 210 cells / 21 rows on /wide; 374ms mount to first
  painted row at 10⁵ rows; 2.27s to parse a 10.4MB paste with the next key at 6ms).
  **MEM-2..7, PF-3..8, BIG-2..6: `blocked: measurement harness not yet written`** —
  no threshold was invented to stand in for them.

## §21

No open question. Reservations settled this run: **a copy over an Action Column**
(empty cell — recorded in ADR-0005 and in §21.11's table). Still reserved, triggers
unfired: the fill handle, right-click/context menu, `--ex-selection-outline`, the
Wrapper seam order, ExSheet's shape, the column band. The editor's own classes/tokens
(`ex-editing`, `ex-editor`, `--ex-editor-*`) were settled with the editor, as their
trigger said.

## Verdict

**Not release-ready**, and the gap is now cross-platform verification and the
remaining instrumentation rather than implementation: every ADR-specified feature of
the core is implemented and exercised by at least one layer, the randomised
consistency run and the allocation invariant now pass, and the layer-3 suite covers
the interaction, presentation and virtualisation surfaces on Chrome. What the DoD
still requires and this run could not give: an Edge run, a Windows or Linux run
(VZ-10's real clause), the CDP-metrics instrumentation (MEM-2..7, PF-6/7, BIG-6),
a real-IME session (ED-11's stronger half), and the CON-3/6 host instrumentation.

## Post-review fix round (same day)

A 17-candidate adversarial review of the uncommitted drop confirmed every candidate;
all 17 were fixed the same day. The figures above therefore superseded: **layer 1
412 pass, layer 2 305 pass, layer 3 38 pass on chrome** (two flaky failures under a
loaded machine — the DPR-loop scrollbar test and the real-mouse gestures — passed on
serial re-run; recorded here rather than smoothed over).

New or re-verified criteria from the fixes:

- **VZ-13** pass (L2: a 2M-row paged result binds; the re-anchor writes page-local
  offsets — both directions were defects).
- **FL-9** pass (L2: a shifted domain narrows the In-list; the count comparison that
  silently removed the filter is gone).
- **ED-12** pass (L2: click-away commits — another cell and the header; the editor's
  input stops propagation, pinned by the MissingEventHandler assertion).
- **ED-13** pass (L2 + inspection: AltGr admits printables in both gates).
- **KB-18** pass (L2 + chrome on /cells: Escape returns from the Note control to the
  grid; typing in the control is untouched).
- **KB-19** pass (chrome on /cells: a display-only grid leaves printable keys to the
  page, unprevented).
- **SL-16** pass (L1 + L2: Ctrl+A — paged or not — moves neither Anchor nor Focus).
- **PST-7** pass (L1: &nbsp;-preserved spaces survive the HTML parse).
- **CON-8** re-verified with the widened wording (the copy failure paths now report;
  the healthy-path runs stay silent — zero console errors across all 38 chrome tests).
- **KB-8/17** re-verified: Escape's popover layering now travels the capture path
  (the bubble handler is gone), marked from-descendant; same observable behaviour.

### The 18th fix (found by stress, not by the review)

Repeat-running the zoom suite (`scrollbar.spec.mjs --repeat-each`) failed ~1 run in 3:
Ctrl+End after Ctrl+Home, faster than one scroll-event round-trip, left the view at the
top with the Focus 100,000 rows away and nothing armed to recover. Root cause: the
reveal's no-op check compared its target to the scroll-EVENT mirror while the previous
reveal's write was still in flight. Fixed with expected-offset tracking (the newer of
last event and last write); the "a move inside the viewport scrolls nothing" economy
still holds — its three tests pass unchanged, plus a new in-flight regression test in
`GoToStartTests`. After the fix: **layer 2 306 pass; layer 3 38/38, and the zoom suite
15/15 under `--repeat-each=5`** (previously flaking, which had been mis-read as machine
load — recorded here as the correction).
