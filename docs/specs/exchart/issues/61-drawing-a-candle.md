# 61: Drawing a candle

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0300](../../../adr/0300-a-price-range-is-one-bar-a-row-refused-where-its-values-disagree.md).

- [ ] Rising (close above its own open) hollow, falling filled, unchanged one horizontal line; a High-Low-Close bar has a close tick and no body
- [ ] Rise and fall colours are Visual Tokens, by default the series' colour slot
- [ ] A candle as wide as its period on calendar time; the value axis takes the highs and lows and leaves out zero, with the break mark
- [ ] Rendered as components that skip their render (ADR-0252)

**Blocked by:** 60.
