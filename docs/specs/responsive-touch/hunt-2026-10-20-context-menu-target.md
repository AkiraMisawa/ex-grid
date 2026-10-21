# A secondary press keeps its target when the Consumer moves the grid

Status: ready-for-agent
Roadmap: internal

Found in the related-input gate for ADR-0240 ticket 03, run
[39508352025](https://github.com/AkiraMisawa/ex-grid/actions/runs/39508352025), widened
WebAssembly Chrome shard 2, job 118924598091, implementation `23a08d38`.

The typing-then-menu test expected `Novak` from row 3 but copied `Ishikawa` from row 2.
The original trace shows the secondary mousedown at viewport offset y=84, row 3 at 24 pixels
per row. Its commit grew the Consumer's status line, moving the grid down before contextmenu.
The menu used that later event's offsets and selected row 2. Copy itself used that selection
correctly. The [original Input Journal](evidence/hunt-39508352025-menu-input-journal.json)
records the press, commit's answer, menu claim and copy in order; this was a lost target,
not a typing or copy completion failure.

The regression now moves the Consumer's status on the secondary mousedown, through `alterPage`
with cleanup, so the same layout transition occurs every time. It retains the uninterrupted
sequence of typing, right-click and Enter and reads once after the Settled Mark. Before the
fix it reproduced the same `Novak` versus `Ishikawa` mismatch three times out of three on
WebAssembly Chrome with CPU throttled by four.

The existing press descriptor now rides with contextmenu, immediately or through the existing
hold. `PressTakenAt` carries it instead of the generic claim telling, and the rows' handler
uses the existing paint validation. No message, listener, geometry measurement or queue is added.
ADR-0036 records the implementation correction.

After the fix, the two changed cases, macOS Control-primary variants and adjacent no-edit
right-click cases passed all 18 executions (three repetitions, both Chromes, WebAssembly
Chrome on macOS, CPU throttled by four). Control-primary retains ADR-0012's existing added
range, and ADR-0036 preserves both selected cells when the menu opens inside the Selection.
The native macOS variants are skipped on Linux; the ordinary secondary-button cases run there.

The full layers 1 and 2 passed 14,497 tests with 11 skips; the final shipped-script and
spec-lint checks passed 66 and 137 tests. The solution built with zero warnings/errors.
The Docs Site's field-text example was exercised under both Chromes, including editing then
right-click and Copy, with no browser complaints. Standards and Spec reviews have no remaining
findings. CI remains the stage gate; newly observed pointing and heading-drag failures are
tracked separately and are not claimed resolved by this menu fix.
