# 147: `ExSheet.MudBlazor`'s Format Cells shows every tab

Status: done

**What to build:** Part C saw `ExSheet.MudBlazor`'s Format Cells open on Border with Font and Fill
scrolled out of its tab strip. All five tabs are shown whichever it opens on.

**Blocked by:** None (can start immediately)

- [x] **Opened on Border, Number to Fill are all in view**, with no scroll buttons.
- [x] **Layer 3** under the Mud Chrome.

## Comments

2026-10-02, claude/exsheet-part-c. `MinimumTabWidth="0px"` on the tabs. Layer 3:
`format-cells-mud.spec.mjs`, which fails without it (Number scrolled to x −62).
