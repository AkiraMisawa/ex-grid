# 280: The editor's ⌘+←/→ is pressed by a test on a Mac

Status: ready-for-agent
Roadmap: internal

**Found:** on 2026-10-18, by run 18's macOS half (`verification/2026-10-18-macos-18/report.md` on
`release/0.1`, difference 2).

**Seen:** `run-18/f4-enter-mode.md` item 9 names, for an Apple platform, Option+←/→ moving the Caret by a
word and ⌘+←/→ going to the text's ends (ED-2). The two ED-2 tests in `features.spec.mjs` press Option for
a word (`wordKeys`) and never press ⌘+←/→, so layer 3 on a Mac does not read the second half, and run 18
did not read it either.

**Why it matters:** KB-3's macOS row is a layer-3 run on a Mac. A key the suite never presses there holds
only by reasoning, which is the gap the row exists to close.

- [ ] A layer-3 test that presses `Meta+ArrowLeft` and `Meta+ArrowRight` in the Cell Editor on an Apple
      platform, held behind F2 and not, and reads the Caret at the text's start and end (ED-2, KB-3).
      Elsewhere it is skipped by name, as `wordKeys` already branches.
- [ ] Run on the Mac by the next record of KB-3.

**Done alongside, in the same branch:** difference 1 and 3 of that report. `sheet-vs-excel.spec.mjs`
pressed `Control+Enter`, `Control+C` and `Control+V` literally, so on a Mac its Ctrl+Enter tests asked
Control+Enter rather than Command+Enter, and its seven clipboard tests failed. They now press
`ControlOrMeta+…`, which is Control everywhere CI runs and Command on a Mac.

## Comments

- 2026-10-22: the tests are written and pass on Linux, where the Apple-only ones skip by name. The run on a Mac with `--repeat-each=3` is what remains, and it cannot be made from a Linux container.
