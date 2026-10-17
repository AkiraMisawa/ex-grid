# 220: A Tab typed straight after Escape moves the focus, as one typed slowly does

Status: done

**Found:** by the spec lint (#137, ADR-0220 §6). Eleven waits in `key-field.spec.mjs` and one in
`pivot.spec.mjs` sit between an Escape and the Tab or Shift+Tab that follows it. They are waits
between inputs, which ADR-0220 §2 forbids, so the lint asked for them to go.

**Seen:** replaced by `circuitQuiet()`, the Server host passed, and WebAssembly did not: "A11Y-4 /
ADR-0080: Tab from before the first grid, which edits, reaches its field past every ▾, and the next
Tab leaves the grid; Shift+Tab likewise" failed under both Chromes, the Shift+Tab typed straight
after Escape leaving `#before-grid` unfocused (local run, Linux, headless, `--project=chrome`,
2026-10-17). `circuitQuiet()` has nothing to wait for on WebAssembly, so the Tab followed the Escape
at machine speed. The waits are back, each allowed with this suspicion.

**Today:** Escape releases Tab (KB-8, ADR-0012 rewritten 2026-10-01). The release is the Escape's
answer, and a Tab typed before it is held behind the Escape; script cannot move DOM focus for a held
Tab (ADR-0010, ADR-0021), so the Tab is dropped. `pivot.spec.mjs`'s comment says the same of an
ExPivot tab's grid.

**Why it is a defect:** the outcome depends on how fast the two keys are typed (the spine's sixth
principle, ADR-0220 §2): typed slowly, Tab leaves the grid; typed at once, nothing happens.

**Decision needed:** what a Tab held behind an Escape means at its turn — refused visibly, the
release taken without a round trip, or another way that makes the outcome the same at any speed.

- [ ] Decide, and record it in ADR-0012 or ADR-0021.
- [ ] Remove the twelve waits; `key-field.spec.mjs` and `pivot.spec.mjs` type Escape and Tab at
      once, and pass on both hosts, 20 runs each.

## Comments

2026-10-17, claude/races-220-224. Triaged: needs a decision before a fix. The options and a
recommendation are in ADR-0230 section 1, proposed and not decided. The user decides; the fix
follows on its own branch.

2026-10-17. Decided with the user: ADR-0230 section 1, as recommended. Ready to build.

2026-10-17. Done: the Settled Stamp says whether an Escape leaves (`data-ex-escape-leaves`), and the gate takes the release at the keydown once every input that could add a layer is answered. A held Tab the release meets is refused aloud (`KeyRefusalLabelIds.TabBeforeRelease`). The eleven waits in `key-field.spec.mjs` and the one in `pivot.spec.mjs` are gone; the three after a press read `settled(grid)` first.
