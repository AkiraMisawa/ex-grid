# 224: The Excel-comparison probes type at machine speed

Status: done

**Found:** by the spec lint (#137, ADR-0220 §6). `sheet-vs-excel.spec.mjs` paces every press, type and
click by `PACE_MS`, a person's pace kept to match the by-hand comparison with Excel. Its own comment
gives the reason: on the Server host a key or click sent before the previous answer landed in the
wrong cell (`verification/2026-09-27-windows-excel/typing-probe-2.mjs`). The five places are allowed,
naming that suspicion.

**Since:** ticket 201 (a press straight after Enter lands on the cell pressed) and ticket 200 (keys
typed after the Name Box's Enter reach the grid) fixed the races of that family on 2026-10-16. The
pacing may now hide nothing.

- [x] Set `PACE_MS` to 0 and run the file 20 times on each host. If it passes, remove the pacing; if
      not, each failure is a race of its own, ticketed and fixed in the product (ADR-0220 §2).

## Comments

2026-10-17, claude/races-220-224. `PACE_MS` set to 0, Server host, `--project=chrome`, headless,
Linux under WSL2: of 84 runs (two each), 13 failed, in eight tests. None was a product race. Each
read the page once, too early:

- `entryOf` read the Formula Bar straight after its press, before a paste or a commit before it had
  been answered (items 16 and 19: an empty bar);
- `dragFillHandle` read the fill handle's box before the Selection the inputs before it left had
  painted it, so the drag began elsewhere and filled nothing (item 20's three fills, and run 18a's);
- item 11 read the caret straight after the two arrows held behind F2 (3 for 1);
- item 2's press read F6's box while a reveal the keys before it asked for could still move the
  cells, and pressed G6 (2 of 20 on the Server host). Read once the grid had settled, it passed 40
  of 40. The scroll itself was not traced, so this cause is inferred from that result.

Each now reads after the Settled Mark (`settled(grid)`, ADR-0220 §2), and the pacing is gone: the
keyboard is no longer slowed, and no press or drag waits a fixed time. Typed at machine speed, the
file passed 840 of 840 on the Server host and 840 of 840 on WebAssembly (20 runs of its 42 tests
that run; `--project=chrome`, headless).
