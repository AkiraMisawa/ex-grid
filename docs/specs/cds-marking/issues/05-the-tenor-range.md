# 05: The tenor range

Status: ready-for-agent

**What to build:** the page's tenor range (spec, "Tenors").

- **The setting:** one range for the whole page, chosen from the standard tenors, stored on the
  server and recorded when changed.
- **Columns:** the Reference tabs, the Bespoke list, the definition area and the files follow it.
- **Narrowing:** the definition's column is removed, and a Formula that read it shows `#REF!`, which
  the readiness panel lists.
- **Widening:** Reference Curves take the quote or the Auto-quote. Bespoke Points start empty, and
  the readiness panel lists them.

**Blocked by:** 04 (and 07 for the Bespoke columns)

- [ ] Layer 1: narrowing and widening on a stored book give the errors above and no others
- [ ] Layer 3: changing the range changes every tab's columns
