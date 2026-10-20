# 66: Intervals on a Level

Status: open

**Why:** [ADR-0303](../../../adr/0303-an-interval-belongs-to-a-series-and-says-what-it-means.md).

- [ ] A series' Interval: bounds per position and a required meaning; a lower above its upper, a bound at a position the series has no point, and a missing meaning are refused
- [ ] Whiskers or a band (an appearance choice); a band joins only neighbours; past one point a pixel whiskers become the band, thinned to the lowest lower and highest upper
- [ ] The value axis takes the bounds; the legend, tooltip, Notes and CSV carry the meaning; a point with a missing bound says `(no interval)`

Roadmap: internal
