# 05: Rewrite the LV rows, and PV-50's keys, for the Script Core's ExPivot

Status: needs-triage
Roadmap: internal

**Why:** LV-24 to LV-28, LV-30, LV-31 and LV-34 judge the server report Window path that ADR-0370 section 3 ended:
the clauses only that path had, its components' render counts and its browser scenarios, are not met, since those
components no longer exist. A server's ExPivot is now the Leaf Aggregate boundary's. PV-50's keys typed on a
menu's or panel's button while it opens, Escape and Tab among them, are not checked over the Script Core. The user
made both Known limitations of 0.2.0 on 2026-11-07, the rewrite owed with ExPivot.Engine's removal (the
[sign-off](../../../../verification/2026-11-09-linux-signoff/results.md)).

**What:** each LV row restated for the Leaf Aggregate boundary, saying what it asks of a server whose data moves
on now, and a test for each clause; PV-50's Escape and Tab, and the no-report panel, checked in layer 3 over the
Script Core. Restating a criterion is the user's decision (CLAUDE.md: "Do not weaken a requirement"), so the
rewritten rows go to the user together before they land. With them, measure LV-15's grid frame: at 0.2.0 it comes
a frame after the status line's, 101 ms after Apply against rc.3's 84 in one container (the sign-off's Step 6).
Then the Known limitation goes from the Docs Site's "A server whose data moves on" section.
