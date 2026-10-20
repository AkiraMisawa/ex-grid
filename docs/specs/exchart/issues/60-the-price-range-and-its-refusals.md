# 60: The Price Range and its refusals

Status: done
Roadmap: internal

**Why:** [ADR-0300](../../../adr/0300-a-price-range-is-one-bar-a-row-refused-where-its-values-disagree.md).

- [x] `SeriesKind.PriceRange` and its bars (open, high, low, close, optional volume); a row where `low > min(open, close)` or `max(open, close) > high` is refused naming the row and the values
- [x] A row missing any of the four is a gap; a series with no open at all is High-Low-Close; negative values are drawn
- [x] The close stands as the series' point, so its domain, positions, CSV and the bridge read it as a Level's
