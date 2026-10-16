# 200: Keys typed straight after the Name Box's Enter reach the grid

Status: done

**What to build:** a defect found while writing `tests/ExGrid.Browser/f4-enter-mode.spec.mjs` (PR #128),
on the Server host only. The cause is in ExGrid's core, not ExSheet: the Name Box is ExGrid's
(ADR-0051), and any ExGrid with `ShowFormulaBar` met it. The keys typed straight after the Name
Box's Enter must reach the grid, against the Focus the Name Box placed, as they do when typed slowly
(ADR-0220, the spine's sixth principle; ADR-0010's hold).

**Today (before the fix):** the Name Box's Enter is its form's implicit submission (ADR-0051). The
core answers it by handing the keyboard back to the root (`ReclaimFocusAsync(fromField: true)`) and
then letting the Consumer place the Selection (`OnNameBoxEntered` → `PlaceSelectionAsync`). On a
circuit both land a round trip after the Enter. The capture-phase listener held nothing behind it,
so `abc` typed at once went into the Name Box, which still held DOM focus; the hand-back then took
the keyboard to the Keyboard Field, and the letters were lost. No edit opened. Reported as "when the
Name Box names the cell that is already the Focus", because a test that names another cell waits for
the Focus to move before it types; the race is the same for any name. Measured with
`hand-back-races.spec.mjs` on the Server host, `--project=chrome --repeat-each=10`: 9 of 10 runs
failed naming the Focus, 9 of 10 naming another cell. WebAssembly answers the submission inside the
Enter's own task, and was not affected.

**The fix:** the listener holds the keys typed after an Enter in this grid's Name Box, in order,
until the core has answered the submission, and then hands them on where the keyboard is. The
answer is the core's: `ExGrid.NameBoxAnsweredAsync` completes once the submission the core heard
last has been answered in full (the hand-back and the Consumer's placement), and the listener asks
it from the task after the Enter's, so the submission has always reached the core first. A held
Enter replayed into the Name Box waits the same way. This is the first allowlist entry's hold
(ADR-0021); no listener is added, and nothing is measured or timed.

- [x] **Layer 3:** `hand-back-races.spec.mjs`, "keys typed straight after the Name Box's Enter names
      the Focus open an edit on it" and "… names another cell open an edit on that cell": 20 of 20
      on the Server host, 10 of 10 on WebAssembly, `--project=chrome`.
- [x] **Layer 2:** `FormulaBarEditingTests`: the Name Box's Enter is answered only once the
      Consumer has placed the Selection; an Enter that submitted nothing finds the answer given.
- [x] **The F4 spec drives the natural gesture:** answer 7's loop goes back to D10 by the Name Box,
      as the run went back, where it had pressed the cell to dodge this race.

## Comments

2026-10-16, claude/settled-races. Reproduced, root-caused and fixed in ExGrid's core.
