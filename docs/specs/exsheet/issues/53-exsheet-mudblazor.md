# 53: `ExSheet.MudBlazor`: Format Cells as a `MudDialog`

Status: ready-for-agent

**What to build:** the package of [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) and ADR-0019's note of 2026-09-30.

**Blocked by:** None (52 is in)

- [ ] **A new project, `src/ExSheet.MudBlazor`** (SH-47).
  - It references `ExSheet`, `ExGrid.MudBlazor` and MudBlazor.
  - XML doc comments on every public member.
  - `tests/ExGrid.PackageSmoke/check.sh` packs it.
  - It stays out of `release.yml` as ExSheet does.
- [ ] **Its Format Cells Chrome is a `MudDialog`** (SH-45).
  - More Colours is MudBlazor's colour picker.
  - The tabs switch with the arrow keys.
  - On closing it calls the core's focus function.
  - It adds no script.
- [ ] The DemoHost's MudBlazor Sheet page uses it.
- [ ] Inside `ExGrid.*` namespaces, write `@using global::MudBlazor` (AGENTS.md, name collisions).
- [ ] Layer 2 under bUnit with MudBlazor; Layer 3 under the MudBlazor Chrome on both hosts.
