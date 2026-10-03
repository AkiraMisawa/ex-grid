# 08: The definition area

Status: ready-for-agent

**What to build:** the ExSheet under the Bespoke list, and the switchable Reference grid beside it,
in one Pointing Scope (spec, "Bespoke Proxies").

- **The layout:** row 1 holds the labels (Ticker, Entity, Ccy, Seniority, DocClause, Recovery and the
  tenors), row 2 the tenor years, and the proxies sit from row 3. The area declares `Cds` and `Rec`
  and is pushed every snapshot.
- **Moving proxies in:**
  - **Edit** moves the selected proxies in, and locks them; the list shows who holds a lock.
  - Taking a lock over needs a reason, and is recorded.
  - **New** adds an empty row.
  - **Template forms** write a row's Formulas.
- **Ending an edit:**
  - **Save** checks each row and writes it back as a new version.
  - **Apply for today** stores the row's Entries as that day's Override of the definition.
  - **Discard** writes nothing and releases the locks.
- **Save refuses a Reference outside the row.** It reads `FormulaEntry.References` for every Entry,
  and refuses, by name, any Reference outside the row or the two heading rows.
- **A committed Formula that reads an unmarked curve** opens the add-to-Marked-set dialog. Its Read
  Set (ticket 01) is evaluated on the area's Sheet. Declined, the Formula stays and the readiness
  panel lists it.
- **Bulk interpolation** fills the selection's empty cells, row by row, as one undo step, and never
  overwrites.

**Blocked by:** 07

- [ ] Layer 1: Save refuses a cross-row Reference; bulk interpolation fills as the spec says
- [ ] Layer 3 on both hosts: pointing at a Reference grid's cell while a Formula is open writes the
  `XLOOKUP` by key (ADR-0058); a Save shows the new values in the list; a second user sees the lock
- [ ] A proxy edited and saved in the area gives the same Values in the list (one implementation)
