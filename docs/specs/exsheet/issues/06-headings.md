# 06: Headings: header click selects, Row Headings, and hiding them

Status: ready-for-agent

**What to build:** The first ADR-0050 declaration, in the core and wired by ExSheet. A header click selects the
column. Row Headings are a pinned band beside the rows, outside the column index space, labelled by
the Consumer: a click selects the row and Shift+click extends. The corner selects all. Either
Heading can be hidden, and hiding the column header is also available to a plain ExGrid.

**Blocked by:** 02

- [ ] Off by default: an existing ExGrid's header click still sorts (existing suites green)
- [ ] On: click, Shift+click and the corner select as stated; nothing sorts (ADR-0050)
- [ ] Row Headings are not in Selection, copy, Ctrl+A or the Enter/Tab cycle
- [ ] Either Heading hides; the Sheet still addresses `A1`
- [ ] The band's width is resolved geometry, not a stylesheet literal (ADR-0027/0028)

## Comments
