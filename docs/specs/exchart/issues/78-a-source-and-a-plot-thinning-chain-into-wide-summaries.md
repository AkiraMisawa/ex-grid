# 78: A source's and a plot's thinning chain into wide summaries

Status: needs-triage
Roadmap: ExChart — a chart read through a Chart Source says, at each pixel, what that pixel held.

**Why:** `Thinning.Through` joins a source pixel and a drawn pixel whose spans overlap into one region, and
keeps joining while the next overlaps the region so far. Read through a Chart Source at the Docs Site's
"Following the data" Example (200,000 ticks, 2026-10-22), a tooltip said "178 points, 94833–96832" where one
pixel is about 46 seconds wide: a region of about 2,000 seconds. Nothing is counted twice and nothing is
wrong ([ADR-0344](../../../adr/0344-a-chart-source-answers-thinned-points-and-a-board-charts-a-server-input-through-it.md)
asks for no row counted twice and no two summaries overlapping), but the summary is much coarser than the
pixel it is read at.

- [ ] Decide whether a region may be split at the drawn pixel's edges, attributing a source pixel's count to the drawn pixel its kept points fall in, or whether the width is the price of never counting a row twice
- [ ] A layer-1 test of the chained case, naming ADR-0344

## Comments

- 2026-10-25: the user agreed to leave it past 0.2.0: nothing is counted twice, the summary is only coarse, and fixing it first needs the choice above.
