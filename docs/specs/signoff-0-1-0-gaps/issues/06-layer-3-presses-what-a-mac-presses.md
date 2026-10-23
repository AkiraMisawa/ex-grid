# 06: Layer 3 presses what a Mac presses

Status: ready-for-agent
Roadmap: internal

**Why:** the macOS runs of 2026-10-18 (`verification/2026-10-18-macos-18/`) found two gaps:
`sheet-vs-excel.spec.mjs`'s clipboard tests press Control+C and Control+V, which fail on macOS and pass
with Meta; and no layer-3 test presses Command+Enter in the Sheet's editor (KB-3's macOS row read it once
by a scratch copy).

**What:** the clipboard tests press `ControlOrMeta`, and a test of Cmd+Enter in the Sheet's editor, run
on a Mac with `--repeat-each=3` before it goes in (AGENTS.md, "What counts as verified").

## Comments

- 2026-10-22: the tests are written and pass on Linux, where the Apple-only ones skip by name. The run on a Mac with `--repeat-each=3` is what remains, and it cannot be made from a Linux container.
