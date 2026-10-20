# Responsive layout and touch: implementation entry point

The design is accepted and its browser-interop prerequisites are registered. Implementation
may begin. This page is a handoff to the agreed decisions, not a claim of mobile support or a
replacement for their acceptance criteria. Implementation is underway; the verified partial deliveries are recorded below.

## Read before implementing

- [ADR-0045](../../adr/0045-exgrid-is-a-desktop-grid.md) is the complete behavior contract:
  ExGrid, ExSheet and ExPivot, both Chromes, both hosting models, and Q1–Q27 including revised Q9.
- [Definition of Done, section 34](../../definition-of-done.md#34-responsive-layout-and-touch-rt)
  contains RT-1 through RT-19 and the real-device completion boundary.
- [ADR-0021](../../adr/0021-javascript-is-allowlisted-not-minimised.md) now permits nineteen
  scoped uses. The four touch additions are accepted, not implemented:

| Browser responsibility | Decision |
|---|---|
| Pointer lifecycle, gesture candidates and initial capability hint | [ADR-0260](../../adr/0260-touch-input-reports-browser-facts-and-keeps-command-meaning-in-csharp.md) |
| Keyboard preparation during an activating edit gesture | [ADR-0261](../../adr/0261-touch-editing-prepares-the-keyboard-during-user-activation.md) |
| Visual viewport notifications for external frames | [ADR-0262](../../adr/0262-external-frames-follow-visual-viewport-notifications.md) |
| Registered Field List target lookup during active touch drag | [ADR-0263](../../adr/0263-pivot-touch-drag-resolves-only-registered-live-targets.md) |

The source proposal informed the interview. Where it differs, the accepted ADRs and Definition
of Done govern; its instructions or milestone minimums are not additional authority.

## Start and dependency order

Container responsiveness can be implemented and verified against RT-1 independently of new input
paths. For touch input, first establish the relevant owner's
[Input Order](../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md).
Upstream main `04530f84` includes core ticket 02 (PR #172). It was merged here as `3eb036fa`;
tickets 03–07 remain open. Reuse that queue and preserve ADR-0240’s separate-PR verification gates:
normal CI plus repeated and widened runs of related input tests. The whole-suite hunt continues
asynchronously and does not delay the next stage (user decision, 2026-10-20).

1. [Core input order](../input-order/issues/02-the-core-takes-input-one-at-a-time-by-number.md)
   and [field text with the next input](../input-order/issues/03-field-text-rides-with-the-next-input.md)
   provide the order and text contract on which touch candidates depend.
2. [ExSheet](../input-order/issues/04-exsheet-on-the-input-order.md),
   [ExPivot](../input-order/issues/05-expivot-on-the-input-order.md) and
   [Wrappers](../input-order/issues/06-wrappers-on-the-input-order.md) establish ownership for
   their respective input paths and external surfaces.
3. Follow the existing Input Order plan through its
   [consolidation and differential verification](../input-order/issues/07-local-fixes-removed-and-differential-test.md).
   Touch work must not create another pending-edit state or parallel queue.

These are implementation dependencies, not unresolved policy or a reason to reopen the design
interview. Prepare the necessary foundation as part of implementation, then add the agreed
touch paths to it. A branch claiming a completed touch path must include its owner's foundation.

Test through the existing command semantics wherever possible: Selection arithmetic, Fill
validation and commit, Pointing Scope, column notifications and Pivot placement. Add browser
coverage for the new gestures and lifecycle boundaries; do not duplicate the product's logic in
the input bridge or Chrome. Public API names, thresholds, hit areas, movement-command wording
and responsive breakpoints are implementation choices to document and test against the ADRs.

## Verification and delivery

The supported matrix is all three applicable products, Built-in and MudBlazor Chrome, Server
and WebAssembly, with desktop regression plus Android Chrome and Safari on iPhone/iPad under
ADR-0045's version policy. Emulation cannot discharge real-device native panning, software
keyboard, Japanese IME, clipboard, pinch and rotation. Keep unrun cases visibly unverified.

Run the logic/component suites and the targeted browser tests under the repository's local
scope rules; each added or rewritten browser test runs individually with `--repeat-each=3`
on one host and Chrome. Full browser coverage belongs to CI. Performance readings are retained
as evidence, never invented from the design or used as a timing gate.

Each Consumer-visible implementation includes its Docs Site prose and runnable Examples and
its Change Notes. Intermediate deliveries describe their exact partial scope; they do not claim
complete mobile support before all required RT criteria and real-device records hold. The initial documentation-only preparation made no runtime support claim. Subsequent product
changes include their own Examples and Change Notes.

## Reservation record

The 0260–0269 block for `codex/responsive-touch-design` was reserved on main in commit
`cdf69d6aa0f8c119cf6b39ce850c9f0e1afe05d9`, after the user's explicit authorization to push that
reservation alone. ADR-0260 through ADR-0263 use that block. The earlier unnumbered browser-interop
draft has been replaced by these records; it is no longer an outstanding prerequisite.

## Implementation checkpoint, 2026-10-20

- `6cc6b24e` lets the MudBlazor grid paper shrink with its container and adds the Docs Site
  examples and product notes. Its isolated logic/component suite passed (14,020 passed,
  11 skipped); the added browser test passed three times and the Docs examples were exercised.
- `dc29ec0a` adds responsive family coverage: ExGrid, ExSheet and ExPivot under both Chromes,
  flex/grid/split containers and widths 240/320/640/800, including immediate Focus and Overlay
  geometry after resize. The six new browser cases each passed three times on Chrome WebAssembly.
- The earlier alternative Input Order prototype is preserved in the stash named
  `responsive-touch local input-order prototype before upstream reconciliation`. It is not
  the implementation baseline and must not be applied wholesale over upstream's queue.
- The user selected the bounded-retention policy for queued row commands: continuous presence
  in the current Window is required; leaving just the painted slice is harmless. ADR-0160,
  ADR-0142, ADR-0043 and ADR-0240 record that decision. The row Action/Mark follow-up closes
  paths shown to bypass upstream's queue, including recovery of discarded Blazor clicks.
- The row follow-up passed 24 targeted component cases. The final solution-wide logic/component
  run passed 14,180 tests with 11 skipped; the solution build had no warnings or errors. On Chrome
  Server, each of the four new browser cases passed three times, and the existing LV-12 discarded
  Action click case passed. The Docs Site's queued-row example was run under both Chromes with no
  browser warnings or errors. Standards and Spec reviews have no unresolved findings. These are
  local results; the full CI browser matrix, hunt and widened run remain outstanding.
- Draft PR #187 carries this partial delivery. Its first CI run (`39339605690`) passed the
  logic/component and package checks, but exposed missing demo-index registrations and an RT-1
  test that assumed the Sheet's final test column was initially painted. The navigation fix
  passed RI-1 and RI-3 locally. RT-1 now reveals that column before reading its width, and reads
  its identity and width atomically so a recycled cell node cannot supply its neighbour's geometry.
  All four queued-row browser cases passed three times on each host/browser combination in that
  first CI run. After correction, each of the six RT-1 family cases passed three times locally on
  Chrome WebAssembly, and all 127 spec-lint checks passed. Standards and Spec reviews found no
  outstanding issues. The responsive correction still requires its own CI matrix before the gate closes.

The new pointer bridge, touch editing and keyboard preparation, external frames, and Pivot touch
placement are not implemented. Full RT coverage, input-order stages 03–07, CI gates and the
Android/iPhone/iPad real-device records remain required before mobile support can be called done.
On 2026-10-20 the user authorized work-branch pushes, Draft PR creation and CI/hunt verification
for this implementation and its later stages. Direct pushes to main and merging are not included.

## Related input verification for the row-command follow-up

Chosen before its targeted run, under the revised ADR-0240 §7:

- `row-command-order.spec.mjs`: the changed Action/Mark paths, refusal and held-key ordering under both Chromes.
- `write-lands.spec.mjs`: recovery of a discarded Action click (LV-12), and neighbouring press/commit paths against changing rows.
- `marks.spec.mjs`: Mark command semantics and identity across filtering and live updates.
- `keyboard-return.spec.mjs`: the shared browser hold and keyboard hand-back lifecycle.
- `settled-mark.spec.mjs`: completion reporting for numbered inputs, editing and reveal.
- `circuit.spec.mjs`: held keys, claimed menu commands, presses and editing across a Server round trip.

Run these ten times, plus once with the widened settings, on both hosts and browsers through
`hunt.yml`'s existing `specs` input. A separate verification ref keeps this targeted dispatch from
cancelling the already-running whole-suite hunt. Normal CI passed at implementation revision
`38c57a6a` ([run 39342985942](https://github.com/AkiraMisawa/ex-grid/actions/runs/39342985942));
the policy update changes documentation only. The full hunt at that implementation revision is
[run 39344122821](https://github.com/AkiraMisawa/ex-grid/actions/runs/39344122821) and is not the
next-stage gate. The selected input run must pass before ticket 03 begins.
