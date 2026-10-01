# 74: The value list as the thirteenth run saw it

Status: ready-for-agent

**What to build:** ADR-0058, "What the thirteenth Windows run settled", its first two bullets (Q54,
Q55), and SH-36 as rewritten on 2026-10-01. The thirteenth run
(`verification/2026-10-01-windows-13/pointing-scope.md`, Part A groups 1 and 2, and the additions that
typed them into ExSheet) found Excel listing the values for text ExSheet listed names for, or nothing.

**Blocked by:** None.

- [ ] At an argument whose values are a fixed list, text that is not a number lists every value, with
      the first selected: at `match_mode`, `A1`, `1+`, `X`, `AV`, `Positions` and `"` each list the five
      values, `0 - Exact match` selected; at `search_mode`, `A` lists its four. Letters there list no
      function and no table (SH-36)
- [ ] A number lists the value it is, alone and selected, or nothing when it is no value: `4` at
      `match_mode` and `5` at `search_mode` list nothing; `-1` lists `-1 - Exact match or next smaller
      item` alone, as today. The caret rule of ticket 70 stands: with the caret before or inside a
      value, nothing is listed (SH-36)
- [ ] White space after the caret is something of the argument: with the caret before two spaces
      (`=XLOOKUP(1,A2:A4,B2:B4,,  )`, F2, ←←←) nothing is listed, and Tab commits the Formula with the
      spaces kept and moves on, as Tab does with no list open (SH-36)
- [ ] Tab on a listed value writes it over the whole typed text of the argument (`X` then Tab writes
      `0`), and closes the list; the list is not opened again on what Tab wrote (SH-36)
- [ ] Outside a value-list argument nothing changes: a letter after `=`, an operator, `(` or `,` lists
      functions and Linked Tables by prefix, `Table[` lists columns, and nothing is listed after `=`, an
      operator, `(` or `,` before anything is typed (SH-36)
- [ ] Layer 1 for the rule (each text of the run, at both arguments, and the spaces); Layer 2 for Tab
      over typed text; Layer 3 on `/sheet` under both Chromes: `,,X` lists the five values, `0`
      selected, and Tab writes `0`; `,,4` lists nothing; the spaces case commits (SH-36)

## Comments
