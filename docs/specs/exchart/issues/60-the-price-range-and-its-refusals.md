# 60: The Price Range and its refusals

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0300](../../../adr/0300-a-price-range-is-one-bar-a-row-refused-where-its-values-disagree.md).

- [ ] `SeriesKind.PriceRange` and its bars (open, high, low, close, optional volume); a row where `low > min(open, close)` or `max(open, close) > high` is refused naming the row and the values
- [ ] A row missing any of the four is a gap; a series with no open at all is High-Low-Close; negative values are drawn
- [ ] The close stands as the series' point, so its domain, positions, CSV and the bridge read it as a Level's
