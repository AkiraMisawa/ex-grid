# 223: Keys typed at once after held keys, and Ctrl+Z after typing, land as typed slowly

Status: done

**Found:** by the spec lint (#137, ADR-0220 §6). Two tests in `declarations.spec.mjs` wait between
inputs until every answer has landed:

- "DC-28/ED-22: after keys held behind F2 on a 150 ms circuit, the next key lands at the caret they
  left" types its next key once every answer to the held keys has landed (~line 1140).
- "DC-30/ADR-0007: with an edit open, Ctrl+Z undoes uncommitted typing" presses Ctrl+Z once every
  render answering the typing has landed (~2465).

`circuitQuiet()` is the state each waits for, but it would still sit between two inputs.

**Not yet known:** whether either fails typed at once. Each wait is a suspicion, not a finding.

- [x] Remove each wait and run the test 20 times on each host. If it passes, the wait goes and this
      ticket closes; if not, the race is fixed in the product, and recorded here.

## Comments

2026-10-17, claude/races-220-224. Both waits removed. Typed at once, each test passed 20 of 20 on the
Server host and 20 of 20 on WebAssembly (`--project=chrome`, headless, Linux under WSL2). Neither wait
hid a race, so the ticket closes with no change to the product.
