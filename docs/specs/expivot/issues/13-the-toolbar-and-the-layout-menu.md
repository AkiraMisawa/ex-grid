# 13: The toolbar and the Layout menu

Status: ready-for-agent

**What to build:** the toolbar above the report (ADR-0060).

- **On the left:** the report filter band, moved into the toolbar.
- **On the right, in this order:**
  - **Layout ▾**, offering Excel's Design tab choices under Excel's names, with the current choice
    marked and a choice that changes nothing disabled;
  - **Refresh**, only when the source can be refreshed;
  - **the pane's toggle**, bound through `@bind-ShowFieldList`.
- **The Context Menu** still offers Show / Hide Field List.

**Blocked by:** 12

- [ ] PV-30, PV-12
- [ ] The Layout menu's choices, as layer-1 tests against `PivotLayoutEdits`

## Comments
