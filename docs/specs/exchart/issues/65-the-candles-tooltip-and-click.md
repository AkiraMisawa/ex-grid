# 65: A candle's tooltip and click

Status: done
Roadmap: internal

**Why:** [ADR-0300](../../../adr/0300-a-price-range-is-one-bar-a-row-refused-where-its-values-disagree.md), [ADR-0302](../../../adr/0302-past-the-plots-pixels-candles-merge-into-the-markets-own-periods.md).

- [x] The tooltip gives O, H, L, C, the change from the previous close and the volume; a merged candle its period and row count; unchanged says so
- [x] A click on a single candle goes to its row; on a merged candle it raises a narrowing to its period

**Blocked by:** 64.
