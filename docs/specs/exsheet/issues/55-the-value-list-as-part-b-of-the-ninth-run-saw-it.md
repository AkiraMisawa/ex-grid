# 55: The value list as Part B of the ninth run saw it

Status: done

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", its first and third
bullets (Q49, Q51), and ADR-0051's note of 2026-10-01. Part B of the ninth run
(`verification/2026-10-01-windows-9/pointing-scope.md`, "Ask Excel too (1)", cases x1, x4–x6)
contradicted two readings ticket 44 was built on.

**Blocked by:** None. It shares nothing with ticket 56 but the Sheet's test files; keep to the
completion tests.

- [x] The value list opens only while nothing of the argument stands after the caret. With the caret
      before a value (`=XLOOKUP(1,A2:A4,B2:B4,,|1)`, reached with F2 and ←←) or inside one
      (`,,-|1)`), nothing is listed. The argument's hint still shows. Tab with no list open does what
      it does anywhere else in the edit (in Caret, it commits and moves to the next cell). The
      listing rule elsewhere is unchanged: a value typed whole lists that value alone, any other text
      lists every value, the first selected (SH-36)
- [x] With a list open over Point (the caret where a Reference can go, the edit not in Caret), `Home`,
      `End` and the four Shift+arrows close the list and do what Point does with them without a list:
      `=XLOOKUP(1,A2:A4,B2:B4,,` then `Home` points at A10; Shift+→ points at `D10:E10`, as Excel's
      does. `End` closes the list and writes nothing (Q53, below). The key
      listener's gate learns it as it learns ← and → there (`completionOverPoint`); no listener is
      added and no layout is read (ADR-0021) (SH-36)
- [x] At a Reference's place with no outline standing, list or no list, `Home` starts pointing at the
      row's first column (`=SUM(` then `Home` writes `A10`) and `End` writes nothing and asks for no
      commit (ADR-0058, Q53, decided 2026-10-01 while this ticket was built). With an outline
      standing, both move it as before (SH-36)
- [x] In a list of names, and with the edit in Caret, `Home`, `End` and the Shift+arrows stay the
      editor's, as today (SH-36)
- [x] ↑, ↓, Tab and Escape at an open list, and ← and → over Point, are unchanged (SH-36)
- [x] Layer 1 for the listing rule (the caret before and inside a value); Layer 2 for the keys over
      Point and in a list of names; Layer 3 on `/sheet` under both Chromes: x1 (no list after F2 ←←,
      and Tab commits) and x4/x6 (Home and Shift+→ point and close the list) (SH-36)

## Comments

2026-10-01, implemented on `agent/ps-55`.

- **The listing rule (Q49)**, in `FormulaEntry.CompleteValue`. The value list now opens only while
  nothing of the argument stands after the caret: after the caret there may be white space, then
  `,`, `)` or the end of the text, and nothing else. With the caret before a value (`,,|1)`,
  `,,|-1)`, `,,| 1)`, `,,|1,0)`, `0,|-2`) or inside one (`,,-|1)`, `0,-|2`), nothing is listed;
  the hint is asked for separately (`FormulaEntry.HintAt`), so `[match_mode]` still shows. A value is
  no longer extended past the caret, so the span accepting replaces is what was typed. Elsewhere the
  rule is ticket 44's: a value typed whole lists that value alone, anything else every value. White
  space after the caret before `,` or `)` was already read as nothing of the argument, and still is;
  Excel was not asked about it.
- **Tab with no list** reaches what it does anywhere in the edit: after F2 ←← in Caret it commits
  `=XLOOKUP(1,A2:A4,B2:B4,,1)` and moves to E10. Nothing was changed for that; the list no longer
  stands in its way.
- **A list open over Point (Q51).** The gate's `completionOverPointKeys` in `ex-grid.js` gains `Home`,
  `End` and the four Shift+arrows beside ← and →; no listener is added and no layout is read
  (ADR-0021). `OnCompletionKey` closes the list for each of them when it is open over Point and leaves
  the key to `OnPointKey`, as it did ← and →. Shift+→ at `,,` on D10 writes `D10:E10`: `OnPointKey`
  already started an outline on the edited cell for a Shift+arrow and extended it, but the gate never
  forwarded one while a list was open. In a list of names and in Caret the gate's set is
  `completionKeys`, so these keys stay the editor's; one claimed all the same by a gate not yet told
  writes nothing and commits nothing.
- **What `End` did in Point, and Q53.** Before this ticket, Point moved an outline that stood with
  `Home` and `End` and started none: with no outline standing they fell through to Overwrite's
  commit and move (ADR-0012). On the Sheet at D10, `=SUM(` then `Home` or `End` asked for a commit,
  which the Sheet's verdict refused ("This Formula cannot be read at character 6: the Formula ends
  too early."); the edit stayed open and nothing was pointed. At an open value list, both only
  closed the list, through the fallback for a key claimed by a gate not yet told. That made "do what
  Point does with them" unable to give x4's A10, and the user decided Q53 (ADR-0058): at a
  Reference's place with no outline standing, list or no list, `Home` starts an outline on the edited
  cell and moves it to the row's first column, and `End` is claimed and does nothing more (it closes
  a list it was given, and asks for no commit). With an outline standing, both move it to the row's
  first or last column, as before. Where no Reference can go, `Home` is still Overwrite's commit and
  move. The change is in `OnPointKey`; `ExGrid.PointAt`'s documentation says it.

Layer 1: `CompletionTriggerTests` (`ADR0058_Q49_…`: 8 cases with the caret before or inside a value,
and 5 where the list still opens before what ends the argument; `The_list_replaces_the_whole_value`,
which pinned the replaced reading, is gone). Layer 2, ExGrid: `CompletionOverPointTests` (the four
Shift+arrows from B2, Home writing `A2` and End writing nothing at a list over Point, and Home, End
and Shift+arrows left alone in a list of names and in Caret) and `PointModeTests` (Q53 without a
list: Home `=` → `=A2`, End writing nothing with no intent and no Selection change, Home and End
moving an outline that stands, Home committing where no Reference can go); `ShippedStylesheetTests`
pins the gate's new set. Layer 2, the Sheet: `CompletionTriggerWiringTests` (x1 with F2, the caret
reported back to 24 and Tab committing to E10; the caret inside a value; x4 writing `…,,A10` in the
pointed look with the Name Box on A10; x5 closing the list with no message shown; `=SUM(` then Home
and End; Shift+→ writing `D10:E10`; Home, End and Shift+→ in a list of names). Each test that pins a
change was red before it. Layers 1 and 2: 826 + 2082 + 88 + 1165 (1 skipped) + 370, all passing.

Layer 3, `declarations.spec.mjs`, "SH-36 under the builtin/mud Chrome", three new tests per Chrome:
x1 (F2 ←←, no list, the hint, Tab commits to E10 and D10 holds the Formula); x4 and x5 (Home writes
`A10`, pointed, the Name Box on A10 and the outline in the pinned layer, since `/sheet` pins column
A; End closes the list, writes nothing and shows no message; the same two keys after `=SUM(`); and
x6 (Shift+→ writes `D10:E10`, pointed, and Home in a list of names moves the caret to 0). Run
headless on macOS with `--project=chrome` and `--grep "SH-36|DC-19|DC-28|DC-31|ED-29"`: 28 of 28 on
WebAssembly and 28 of 28 on the Server host. The first run failed x4 under both Chromes on the
outline's layer, which the test had looked for in the scrolling layer; the assertion was corrected.
Edge and the full run are CI's.
