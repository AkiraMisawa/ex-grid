# 35: The colour bar, and colours that diverge from a centre

Status: done
Roadmap: ExChart — a surface shows its colour bar, and colours a P&L from zero

**Why:** [ADR-0310](../../../adr/0310-a-surface-shows-its-colour-bar-and-its-colours-may-diverge-from-a-centre.md).

- [x] Every surface draws its colour bar beside the plot, in its files too
- [x] `SurfaceDefinition.Centre`: bands symmetric about it, the middle one neutral, the centre labelled on the bar
- [x] `--ex-chart-diverge-0` to `-8` Visual Tokens, red below, blue above
- [x] On a logarithmic vertical scale the colours diverge from the centre's logarithm

**Blocked by:** 30, 31.
