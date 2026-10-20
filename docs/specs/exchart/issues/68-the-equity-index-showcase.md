# 68: The Equity index daily bars Showcase

Status: done

**Why:** grilling Q9 (2026-10-20); the Showcases of [ADR-0280](../../../adr/0280-exchart-is-shown-in-five-showcases-and-a-chart-section-of-nine-pages.md).

- [x] Ten years of a synthetic index (fixed seed, one crash day) in a grid beside candles, volume and a 25-day moving average the page computes
- [x] Toggles: calendar or packed axis; shape only, Japanese red and blue, Western green and red
- [x] The whole range merges to months; a narrowing returns days; a click goes to the row
- [x] Handed to the shipping thread, which owns the Showcases and the README's GIFs; the DemoHost's `/chart-candles` holds the chart, the toggles and the data generator to build it from (2026-10-20)

**Blocked by:** 62–65.
