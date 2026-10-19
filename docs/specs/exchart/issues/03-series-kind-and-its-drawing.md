# 03: Each Series Kind is drawn as what it is

Status: done

**Why:** [ADR-0253](../../../adr/0253-every-series-declares-its-series-kind-and-its-drawing-follows.md).

- [x] A series without a Series Kind is refused by name
- [x] Level: points, joined only to neighbours in the data; Rate over an interval: (tᵢ₋₁, tᵢ] or [tᵢ, tᵢ₊₁), filled where it takes the value, open where it does not, none where the next level is equal, no riser; Amount per period: a step whose height is the amount per year, axis from zero; a Level axis from zero unless the series declares otherwise, with a break mark at its foot when it does not
- [x] A sampled function evaluated about once per device pixel, always at each Segment boundary and quote, never joined across a boundary (ADR-0257)
- [x] A missing value is a gap, never a zero; nothing is drawn beyond the data
- [x] Layer-1 tests transcribe the proof of concept's cases (exposure CVA, CDS hazard rates, forwards)

**Blocked by:** 02.
