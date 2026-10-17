# 222: A press into the Name Box after a row press keeps what is typed into it

Status: ready-for-agent
Roadmap: ExSheet — Text typed into the Name Box straight after pressing a row is kept whole

**Found:** by the spec lint (#137, ADR-0220 §6). In `declarations.spec.mjs`, "ADR-0021: a row press's
late hand-back does not take the keyboard from the Formula Bar or the Name Box pressed after it, on a
150 ms circuit", a wait sits between the press into the Name Box and the typing (~line 1002).

**Today:** the Name Box's press is not held among the keys, so a render answering the row press can
rewrite the box's text after the user has started typing; the test waits for the round trips before
it types. `circuitQuiet()` would be the state, but it would still sit between two inputs.

**Why it is a defect:** typed at once, the text typed can be overwritten by a render the user did not
wait for (ADR-0220 §2; the same family as ticket 202 for the Formula Bar).

- [ ] Hold the Name Box's press, or keep what it is pressed on, as ticket 202 did for the bar.
- [ ] Remove the wait; the test types at once and passes on both hosts, 20 runs each.

## Comments

2026-10-17, claude/races-220-224. Triaged: needs a decision before a fix. The options and a
recommendation are in ADR-0230 section 3, proposed and not decided. The user decides; the fix
follows on its own branch.

2026-10-17. Decided with the user: ADR-0230 section 3, as recommended. Ready to build.
