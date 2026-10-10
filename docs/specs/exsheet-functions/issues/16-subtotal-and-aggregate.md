# 16: SUBTOTAL and AGGREGATE, while a Sheet cannot hide a row

Status: ready-for-agent

**What to build:** `SUBTOTAL` and `AGGREGATE` under
[ADR-0047](../../../adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)'s
note of 2026-10-10, "The catalogue's last Decide rows".

**Blocked by:** none.

- [ ] `SUBTOTAL` with function numbers 1–11 and 101–111. While no row can be hidden, the two give the
      same answers, as they do in Excel over such a sheet. A test named for ADR-0047 fails on the day a
      row can be hidden, and its message says to revisit this (SH-37's pattern)
- [ ] `AGGREGATE`'s reference form and array form, with its options 0–7. The options that skip hidden
      rows give what the others give. Function numbers for functions the engine supports are
      answered; the rest are `#VALUE!`
- [ ] Nested calls (SH-68):
      - a cell whose whole Formula is a `SUBTOTAL` is skipped by `SUBTOTAL`;
      - a cell whose whole Formula is a `SUBTOTAL` or an `AGGREGATE` is skipped by `AGGREGATE`'s
        options 0–3;
      - any other call of either in the range makes the result `#VALUE!`
- [ ] Corpus cases: Microsoft's documented cases as `documented`, and the open ones as `uncertain`
      for the next Windows run. That run also asks what Excel counts as nested
- [ ] The catalogue rows Supported. The Docs Site's Formulas page lists them from the declared set,
      and says what is refused

## Comments

2026-10-10: written with the user's decision, ADR-0047's note of 2026-10-10.
