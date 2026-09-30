# 41: What waits for the ninth Windows run

Status: needs-info

**What to build:** the parts of ADR-0058 that are built once Excel has been observed, in
[verify-on-windows-9.md](../verify-on-windows-9.md). Each item's decision is already recorded; only
Excel's behaviour is missing.

**Blocked by:** The ninth Windows run, Part A

- [ ] The arrow keys after pointing into a registered grid: if Excel moves inside the other workbook,
      the pointed position moves inside the grid, and the text is rewritten for the new row;
      Shift+arrow is refused as a range is (ADR-0058, a1). Otherwise keep what ticket 37 built
- [ ] The list after `Table[`: the table's column names only, even where Excel also offers `#All`,
      `#Data`, `#Headers`, `#Totals` and `@` (ADR-0058)
- [ ] F3, if Excel's Paste Name lists tables
- [ ] Backspace back into a name, if Excel lists again
- [ ] `search_mode`'s texts corrected to what the run saw (ticket 39)
- [ ] The Name Box while pointing into a registered grid, if Excel's differs from "the edited cell"
- [ ] Each difference the run finds is written into ADR-0058 first, as ADR-0057's "What the eighth
      Windows run settled" was
