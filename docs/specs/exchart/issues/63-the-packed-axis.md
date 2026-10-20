# 63: The Packed axis

Status: ready-for-agent

**Why:** [ADR-0301](../../../adr/0301-candles-stand-on-calendar-time-unless-the-consumer-packs-the-sessions.md).

- [ ] `ChartAxis.Packed`: each candle position one even slot, ticks the real dates, the title says the closed days are packed
- [ ] On a packed axis a point of any series at a position no candle has is refused by name

**Blocked by:** 61.

Roadmap: internal
