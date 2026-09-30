# 44: Measure per-cell appearance and a border layer

Status: ready-for-agent

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "How it is painted: measured first", which keeps ADR-0046's precondition.
Add modes to `spikes/render-bench` that measure what a Fill, a Font and Borders cost per painted
cell, and bring the numbers back. **The choice is the user's.** The agent reports the results with
a recommendation. The orchestrator puts them to the user, and records the decision as an addendum
to ADR-0063. The agent does not edit the ADR.

**Blocked by:** None (can start immediately)

- [ ] **Three Font and Fill modes.** In each, a share of the painted cells (10%, 50% and 100%) carries
      a colour, a background and bold, drawn from N distinct formats (1, 16 and 256):
  - `CellFormatInline`: an inline `style` on each cell.
  - `CellFormatVars`: per-cell custom properties, read by one static rule.
  - `CellFormatClasses`: interned classes, one per distinct format, written into a generated
    `<style>`.
- [ ] **Two Border modes**, over the same shares:
  - `BorderLayer`: a layer over the rows, one element per run of the same line along a painted row
    or column, as the selection overlay draws ranges (ADR-0008).
  - `BorderInCell`: lines inside each cell's box.
- [ ] **The measurements**:
  - scroll frame cost (median, p95 and max), measured as the existing modes are;
  - renders per scroll step;
  - the time for one format change over a whole column.
- [ ] Rows still skip their render when nothing of theirs changed, in every mode (P1–P9, ADR-0027).
- [ ] The result JSON is kept beside the existing results, and `README.md` lists the modes.
- [ ] **The report**: the numbers, and a recommendation for Font and Fill and one for Border. If
      the layer is too costly, say so: the fall-back (lines inside each cell) is the user's call
      (ADR-0063).
