# 78: A press into the Name Box selects its text

Status: ready-for-agent

**What to build:** ADR-0051, the Name Box bullet, as decided with the user on 2026-10-01. The fifteenth
Windows run (i7) found ExSheet's press into the Name Box leaving the caret after `D10`, so what was
typed (a composition, `かな`) was appended: `D10かな`. Excel's press selected `D10`, and the composition
replaced it.

**Blocked by:** None.

- [ ] A press into the Name Box selects its whole text, the built-in Chrome's and the MudBlazor
      Chrome's alike, so what is typed replaces it. A second press, or a drag, inside it places the
      caret or selects as the field does (only the press that gives it the keyboard selects all)
- [ ] Nothing else of the Name Box changes: Enter still goes to the address, Escape still gives the
      keyboard back, and a Reject met by a press into it still takes the keyboard out of it
      (ADR-0021, 2026-10-01)
- [ ] No listener is added, and no layout is read (ADR-0021): the selection is set where the field's
      focus is already handled
- [ ] Layer 2 for the focus path; Layer 3 on `/sheet` under both Chromes, on both hosts: press the
      Name Box, type `B2`, Enter: the Focus is on B2

## Comments
