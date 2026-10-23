# 40: One legend entry for quotes and their Interpolated curve

Status: done
Roadmap: ExChart — quotes and the curve drawn through them show one legend entry, which hides both.

**Why:** [ADR-0257](../../../adr/0257-notes-and-interpolated-series-are-the-consumers-said-as-such.md)
says the quotes (markers) and the curve declared Interpolated, sharing a colour key, show **one** legend
entry. `ChartScene.Legend` writes an entry per series, so the Docs Site's "Series and axes" Example shows
"Zero rate" twice (found writing that page, 2026-10-20).

- [x] The legend writes one entry for a colour key shared by quotes and an Interpolated series of the same panel
- [x] Clicking it hides both, and `OnSeriesVisibilityChanged` names the key it raised
- [x] Layer 2 test naming ADR-0257; the Docs page says "The legend shows one entry" again

## Comments

- 2026-10-22: built. The entry is keyed by the quotes' identity rather than the colour key, so a key never names another series; two quote series of one colour key keep an entry each, since which one the curve belongs to is not guessed; showing the entry again shows every series it stands for (`ChartScene.LegendMembers`).
