# 03: Each Series Kind is drawn as what it is

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0253](../../../adr/0253-every-series-declares-its-series-kind-and-its-drawing-follows.md).

- [ ] A series without a Series Kind is refused by name
- [ ] Level: points, joined only to neighbours in the data; Rate over an interval: (tᵢ₋₁, tᵢ] or [tᵢ, tᵢ₊₁), filled where it takes the value, open where it does not, none where the next level is equal, no riser; Amount per period: a step, axis from zero
- [ ] A missing value is a gap, never a zero; nothing is drawn beyond the data
- [ ] Layer-1 tests transcribe the proof of concept's cases (exposure CVA, CDS hazard rates, forwards)

**Blocked by:** 02.
