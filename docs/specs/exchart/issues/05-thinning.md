# 05: Thinning past the plot's pixels

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0254](../../../adr/0254-past-the-plots-pixels-a-series-is-thinned-to-its-minimum-and-maximum.md).

- [ ] First, last, minimum and maximum per device pixel column, only when a series has more points than pixels
- [ ] Steps thinned by their levels; gaps kept
- [ ] What a thinned pixel holds (from–to, min, max, count) available for the tooltip
- [ ] Redone from the full series when the width or the range changes

**Blocked by:** 03.
