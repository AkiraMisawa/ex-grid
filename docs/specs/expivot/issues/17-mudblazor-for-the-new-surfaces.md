# 17: MudBlazor for the new surfaces

Status: ready-for-agent

**What to build:** `MudPivotChrome` for the surfaces the grilling added (ADR-0061).

- **The toolbar and the Layout menu.**
- **Defer Layout Update:** a `MudCheckBox` and a `MudButton`.
- **The Details tabs:** `MudTabs`, placed at the bottom.
- **The dialog's content**, inside ExPivot's own frame.
- **The Stale Report's notice:** a `MudAlert`.
- **The tokens** in `mud-ex-pivot.css`. The Change Highlight's token is mapped in `mud-ex-grid.css`.

**Blocked by:** 13, 14, 15

- [x] PV-9 and PV-18 for the new surfaces
- [x] PV-19 still green

## Comments

2026-10-01: Built. `MudPivotChrome` now draws every surface ExPivot hands a Chrome; none is left
to the built-in markup.

- **The toolbar** (`MudPivotToolbar`): the Mud band on its left; Layout ▾, Refresh (when the
  source can be refreshed) and the pane's toggle on its right, as text `MudButton`s with Material
  icons, in ExPivot's order and with its roles — `aria-haspopup` and `aria-expanded` on Layout ▾,
  `aria-pressed` on the toggle, which is shaded while the pane is shown. Layout ▾'s wrapper is
  positioned, so ExPivot's menu frame opens under it, drawn by `MudPivotMenu`. A refusal is an
  error `MudAlert` with the built-in notice's `role="alert"`.
- **The Details tabs** (`MudPivotDetailsTabs`): `MudTabs` at the bottom, the PivotTable tab first,
  each Show Details tab closable by a `MudIconButton` beside it, named in ExPivot's words.
  ExPivot owns the selection: `ActivePanelIndex` is ExPivot's, and an activation (pointer, Enter,
  Space) goes back through `PivotDetailsTab.Select`. MudTabs keeps its active index by position
  and moves it by its own rules when a panel is added or removed, so it is keyed by the tabs' ids
  and made anew when they change; unkeyed, closing a tab before the selected one selected the
  wrong tab (a test pins it). `FocusRequest` focuses MudTabs' tab element. The tabs render only
  when what they show changes, since each MudTabs render measures its tabs again.
- **The dialog's content** (`MudPivotDetailsDialog`): the cell's title as an h6 `MudText`, the
  records, and a Close `MudPivotButton` that takes the keyboard, inside ExPivot's frame. There is
  no `MudDialog`.
- **The tokens:** the dialog frame's edge, corners and shadow became Visual Tokens of ExPivot's
  (`--ex-pivot-dialog-border-color`, `--ex-pivot-dialog-radius`, `--ex-pivot-dialog-shadow`,
  defaulting to today's look), mapped under `.mud-ex-grid` to a MudDialog's: no edge, the theme's
  corners, elevation 24. The toolbar, tabs and dialog content are laid out by `mud-ex-pivot-*`
  rules of the Wrapper's own; none names a class of ExPivot's or ExGrid's.

Three things MudTabs does not allow, and how they are met:

- Its tab element carries an id of its own, so it cannot be `PivotDetailsTab.Id`. The tab's title
  inside it carries that id instead, and labels ExPivot's panel; `PivotDetailsTab.Id`'s doc says
  so, and `pivot.spec.mjs` accepts the tab or its title as the label.
- It draws a tabpanel for the active tab. ExPivot places the records over the report, so MudTabs'
  own panels are rendered empty and `hidden`.
- Its slider is positioned from the tabs' widths alone, and the close buttons stand beside the
  tabs, not inside them, where their names would join the tab's. The slider is hidden; the
  selected tab is marked by MudTabs' active colour and weight. Its tablist takes no name, so the
  MudTabs root is a group named "Sheets", as the built-in tablist is.

Also built here, from "Refined while building it": a failed Refresh is a Stale Report whose Retry
refreshes again, and a failed layout question goes back to the report's layout, said on the
toolbar (ADR-0066); while a new Source Version's Items are on their way, the band and Filter…
keep the earlier version's in view, OK enabled, the list marked busy
(`PivotItemFilterContext.IsUpdating`) (ADR-0065).

Layer 2: `MudPivotToolbarTests`, `MudPivotDetailsTests`, `MudPivotLiveItemsTests` and
`MudPivotStaleReportTests` in `tests/ExPivot.MudBlazor.Tests` (52); `AskingTests`,
`ToolbarAndDeferTests` and `LiveDataTests` in `tests/ExPivot.Components` (175). Layer 3:
`pivot.spec.mjs` on the WebAssembly host in Chromium, under both Chromes.
