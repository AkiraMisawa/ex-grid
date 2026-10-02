# 02: The mark in a real browser

Status: done

**What to build:** layer-3 tests on `/grid-live` (ExPivot's ticket 20 builds the page).

- **DC-65:** a mark stays on its cell across a scroll that recycles the row elements.
- **DC-66:** UX-6's check passes with marks painted, under both Chromes.

**Blocked by:** 01, and `/grid-live`

- [x] DC-65
- [x] DC-66

## Comments

2026-10-01: `grid-live.spec.mjs`, under ExGrid's own stylesheet and ExGrid.MudBlazor's, on both
hosts. DC-65 scrolls three rows, and far away and back, so that new rows take new elements, and
the mark stays on its trade's cell. DC-66 runs UX-6's check with marks painted: no transition or
animation, the forced-colors rule restating the mark, and the live region unchanged. One of
DC-65's checks cannot fail today: ExGrid keys row elements by row instance, so no element ever
paints another trade.
