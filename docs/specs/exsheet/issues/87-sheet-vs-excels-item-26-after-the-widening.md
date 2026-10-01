# 87: `sheet-vs-excel` item 26 after a Number Format widens the column

Status: done

**What to build:** a test ticket 48 found stale. Since ticket 58, the page's *Format selection as #,##0.00*
(`SetCellFormatAsync`) widens a column still at the standard width, as a formatting key does. So item 26 of
`sheet-vs-excel.spec.mjs`, which expected `####`, now sees the number. That follows ADR-0071's reading that
`SetCellFormatAsync` widens. The fourteenth Windows run (case 8) observes it for Format Cells.

**Blocked by:** None (can start immediately)

- [x] **Item 26 checks what Excel does for its own case.** If its point is `####`, set the column's width
      first, as a user would, so nothing widens. If its point is the format, expect the widening.
- [x] **Say which in the test's comment**, and cite ADR-0071's reading and SH-26.

## Comments

2026-10-01, agent cf-86.

**Item 26's point is `####`.** Excel's case (verification/2026-09-27-windows-excel/behaviours.md, item
26) is three values at a 4-character width, all `####`: `123456` in General, `12345` as `0.00`, and a
date. So the item now sizes the column first, as a user does, and nothing widens.

- **What it does.** It drags E's resize grip left by half the default width, which is about Excel's
  four characters. Then it enters `123456` in E1, `12345` in E2 with the page's *Format selection as
  #,##0.00*, and `9/26/2026` in E3. All three show `####`, the column keeps the width the user gave
  it, and E2's Entry is still `12345`.
- **Why sizing first is the case.** A column the user sized is never widened, by an entry (SH-26) or
  by a Number Format. ADR-0071's reading ("Readings until the fourteenth Windows run") is that Format
  Cells' OK and `SetCellFormatAsync` widen only as a formatting key does, which is only a column the
  user has not sized. The test's comment cites both.
- **The date is asked now.** The old comment said `/sheet` could not narrow a column. Since ExSheet
  declares `OnColumnWidthChanged`, a user's grip records a width, so Excel's date case is asked too.
- **The page's format is `#,##0.00`.** Excel's case used `0.00`. Neither fits, and the comment says
  so.
- **Checked the other way.** Without the drag, on this tree, E1 shows `123456`, E2 `12,345.00` and
  E3 `9/26/2026`, and the column widens from 99 to 104 px. So the column the user sized is what makes
  the `####`.

Layer 3, headless on this Mac, `--project=chrome`: item 26 passed on WebAssembly and on Server.
