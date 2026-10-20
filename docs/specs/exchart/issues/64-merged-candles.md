# 64: Merged candles

Status: open

**Why:** [ADR-0302](../../../adr/0302-past-the-plots-pixels-candles-merge-into-the-markets-own-periods.md).

- [ ] Past three device pixels a candle, merge into weeks, months, quarters, years (the first that fits): first open, highest high, lowest low, last close, summed volume
- [ ] The axis title names the period; the CSV keeps every row; narrowing returns the rows
- [ ] `MergeTo` fixes the period; a fixed period that does not fit draws nothing and says so

**Blocked by:** 61.

Roadmap: internal
