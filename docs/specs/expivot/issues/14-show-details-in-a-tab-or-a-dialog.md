# 14: Show Details in a tab or a dialog

Status: ready-for-agent

**What to build:** the three destinations of Show Details (ADR-0058).

- **A tab at the report's foot, by default.** It is closable, titled by the cell, not part of the
  layout, and holds an ExGrid of the Pivot Fields fetched in pages from `DetailsAsync` under the
  report's Source Version.
- **A dialog**, when the Consumer asks for one.
- **The Consumer**, when it listens to `OnShowDetails`.

A tab whose version the source can no longer answer says that the data has changed.

**Blocked by:** 12

- [ ] PV-14, in layer 2 and on `/pivot` in layer 3

## Comments
