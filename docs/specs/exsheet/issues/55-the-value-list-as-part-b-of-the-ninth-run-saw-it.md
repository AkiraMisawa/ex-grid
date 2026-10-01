# 55: The value list as Part B of the ninth run saw it

Status: ready-for-agent

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", its first and third
bullets (Q49, Q51), and ADR-0051's note of 2026-10-01. Part B of the ninth run
(`verification/2026-10-01-windows-9/pointing-scope.md`, "Ask Excel too (1)", cases x1, x4–x6)
contradicted two readings ticket 44 was built on.

**Blocked by:** None. It shares nothing with ticket 56 but the Sheet's test files; keep to the
completion tests.

- [ ] The value list opens only while nothing of the argument stands after the caret. With the caret
      before a value (`=XLOOKUP(1,A2:A4,B2:B4,,|1)`, reached with F2 and ←←) or inside one
      (`,,-|1)`), nothing is listed. The argument's hint still shows. Tab with no list open does what
      it does anywhere else in the edit (in Caret, it commits and moves to the next cell). The
      listing rule elsewhere is unchanged: a value typed whole lists that value alone, any other text
      lists every value, the first selected (SH-36)
- [ ] With a list open over Point (the caret where a Reference can go, the edit not in Caret), `Home`,
      `End` and the four Shift+arrows close the list and do what Point does with them without a list:
      `=XLOOKUP(1,A2:A4,B2:B4,,` then `Home` points at A10; Shift+→ points at `D10:E10`, as Excel's
      does. `End` does what Point does with it today; say what that is in the comments. The key
      listener's gate learns it as it learns ← and → there (`completionOverPoint`); no listener is
      added and no layout is read (ADR-0021) (SH-36)
- [ ] In a list of names, and with the edit in Caret, `Home`, `End` and the Shift+arrows stay the
      editor's, as today (SH-36)
- [ ] ↑, ↓, Tab and Escape at an open list, and ← and → over Point, are unchanged (SH-36)
- [ ] Layer 1 for the listing rule (the caret before and inside a value); Layer 2 for the keys over
      Point and in a list of names; Layer 3 on `/sheet` under both Chromes: x1 (no list after F2 ←←,
      and Tab commits) and x4/x6 (Home and Shift+→ point and close the list) (SH-36)

## Comments
