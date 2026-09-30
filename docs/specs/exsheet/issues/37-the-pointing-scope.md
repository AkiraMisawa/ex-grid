# 37: The Pointing Scope

Status: ready-for-agent

**What to build:** ExSheet's half of ADR-0058: "The Pointing Scope", "What is written" and "The
keyboard". The Scope lives in the ExSheet package. It registers Sheets and grids, declares a grid
pointed at while a Sheet of its Scope points (ticket 34's declaration), and turns a press that a grid
hands over into the text the Sheet writes, or into a reason.

**Blocked by:** Tickets 34 (the grid's declaration) and 36 (the key)

The Scope:

- [ ] A Pointing Scope object the Consumer makes, and passes to each ExSheet and to each registered
      ExGrid. A grid is registered with the Linked Table it shows and its column correspondence (a
      grid column with the same name as its table column needs no entry). The key comes from the
      declaration of the Sheet that points (SH-32)
- [ ] A registered grid is pointed at while a Sheet of the Scope holds the keyboard, has an edit
      open and is in Point, and while the grid holds no open edit of its own. Another Sheet is never
      pointed at (SH-32, SH-35)
- [ ] When the keyboard leaves the Sheet that points, no grid is pointed at, and the edit stands
      (SH-35, ED-26)

What is written:

- [ ] A cell: `XLOOKUP(<key>, T[<key column>], T[<column>])`, with the key written as Excel writes a
      constant of its kind (text quoted with `"` doubled; a number in the invariant form;
      `TRUE`/`FALSE`). A column header: `T[<column>]`. Always the table's column names (SH-32)
- [ ] Written where Point writes; a further press, on a registered grid or on the Sheet, replaces
      what this Point wrote (SH-32)
- [ ] More than one cell, several columns, a Header Group's rectangle, a column the table does not
      have, a table declared without a key, or a blank key: nothing is written, and ExSheet tells its
      Consumer the reason through a new notification (SH-32)

The keyboard:

- [ ] After a press on a registered grid, the arrow keys and Shift+arrows move nothing, and the
      Consumer is told that pointing goes on by a press. F4 changes nothing. The Name Box names the
      edited cell (SH-35)

The DemoHost:

- [ ] `/sheet` registers its positions grid in a Scope and drops `OutlinePositions`. `/sheets` gives
      each side its own Scope. Each shows the notification's reason in a status line (SH-32)

Tests:

- [ ] Layer 2, one per row of ADR-0058's table, and a press on a grid in no Scope; a registered grid
      with its own open edit; the keyboard leaving and coming back (SH-32, SH-35)
- [ ] Layer 3 on `/sheet` and `/sheets`, both hosts: `=`, a press on a PV cell, `*2`, Enter shows
      that row's PV doubled; `=SUM(`, a press on the PV header, `)`, Enter shows the column's sum; on
      `/sheets`, a press on the right's grid while the left Sheet points is an ordinary press. On the Server host with 150 ms injected, a press within the
      round trip after `=` is an ordinary press, and the edit stands (SH-32, SH-35)
