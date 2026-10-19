# Responsive layout and touch: implementation entry point

The design is accepted and its browser-interop prerequisites are registered. Implementation
may begin. This page is a handoff to the agreed decisions, not a claim of mobile support or a
replacement for their acceptance criteria. No product implementation was made in this preparation.

## Read before implementing

- [ADR-0045](../../adr/0045-exgrid-is-a-desktop-grid.md) is the complete behavior contract:
  ExGrid, ExSheet and ExPivot, both Chromes, both hosting models, and Q1–Q27 including revised Q9.
- [Definition of Done, section 33](../../definition-of-done.md#33-responsive-layout-and-touch-rt)
  contains RT-1 through RT-19 and the real-device completion boundary.
- [ADR-0021](../../adr/0021-javascript-is-allowlisted-not-minimised.md) now permits fourteen
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
At the design checkout, its implementation tickets are still open. Check their current status
and reuse completed work before changing the same code:

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
complete mobile support before all required RT criteria and real-device records hold. This
documentation-only preparation changes neither product behavior nor Docs Site support claims,
so it adds no release Change Note or runtime Example.

## Reservation record

The 0260–0269 block for `codex/responsive-touch-design` was reserved on main in commit
`cdf69d6aa0f8c119cf6b39ce850c9f0e1afe05d9`, after the user's explicit authorization to push that
reservation alone. ADR-0260 through ADR-0263 use that block. The earlier unnumbered browser-interop
draft has been replaced by these records; it is no longer an outstanding prerequisite.
