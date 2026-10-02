# 13: The Pivot Toolbar and the Layout menu

Status: done

**What to build:** the Pivot Toolbar above the report (ADR-0061).

- **On the left:** the report filter band, moved into the Pivot Toolbar.
- **On the right, in this order:**
  - **Layout ▾**, offering Excel's Design tab choices under Excel's names, with the current choice
    marked and a choice that changes nothing disabled;
  - **Refresh**, only when the source can be refreshed;
  - **the pane's toggle**, bound through `@bind-ShowFieldList`.
- **The Context Menu** still offers Show / Hide Field List.

**Blocked by:** 12

- [x] PV-30, PV-12
- [x] The Layout menu's choices, as layer-1 tests against `PivotLayoutEdits`

## Comments

2026-10-01: Built. The Pivot Toolbar stands above the report: the report filter band on its left;
Layout ▾, Refresh (only when the source's features say it can be refreshed) and the Field List's
toggle (`@bind-ShowFieldList`) on its right. Its popups open under it, over the report, with a
backdrop; Escape, Cancel and the backdrop close them, and the keyboard goes back to the button.
The Layout menu's choices are `PivotLayoutChoice`, applied by `PivotLayoutEdits.Choose`, marked
by `IsChosen` and disabled where `Changes` says they would change nothing (layer 1:
`LayoutMenuTests`). The Chrome draws the Pivot Toolbar through `IPivotChrome.Toolbar`, and the
Layout menu through the menu surface. The Context Menu keeps Show / Hide Field List. Layer 3 runs
the Pivot Toolbar, the band, the Layout menu and the toggle on the WebAssembly host under both
Chromes.
