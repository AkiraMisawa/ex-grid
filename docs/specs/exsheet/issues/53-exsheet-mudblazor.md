# 53: `ExSheet.MudBlazor`: Format Cells as a `MudDialog`

Status: done

**What to build:** the package of [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) and ADR-0019's note of 2026-09-30.

**Blocked by:** None (52 is in)

- [x] **A new project, `src/ExSheet.MudBlazor`** (SH-47).
  - It references `ExSheet`, `ExGrid.MudBlazor` and MudBlazor.
  - XML doc comments on every public member.
  - `tests/ExGrid.PackageSmoke/check.sh` packs it.
  - It stays out of `release.yml` as ExSheet does.
- [x] **Its Format Cells Chrome is a `MudDialog`** (SH-45).
  - More Colours is MudBlazor's colour picker.
  - The tabs switch with the arrow keys.
  - On closing it calls the core's focus function.
  - It adds no script.
- [x] The DemoHost's MudBlazor Sheet page uses it.
- [x] Inside `ExGrid.*` namespaces, write `@using global::MudBlazor` (AGENTS.md, name collisions).
- [x] Layer 2 under bUnit with MudBlazor; Layer 3 under the MudBlazor Chrome on both hosts.

## Comments

*(2026-10-01, agent cf-53, built; written into the ticket by the orchestrator from the agent's report.)*
`src/ExSheet.MudBlazor`, Format Cells as a `MudDialog`.

- **The package (SH-47).**
  - It references ExSheet, ExGrid.MudBlazor and MudBlazor 9.0.0, the Wrapper's floor. It depends on
    exactly the ExSheet and Wrapper versions it is built with (ADR-0042).
  - Public surface: `MudSheetChrome : ISheetChrome` (with `Default`, and `Grid`, a `MudGridChrome`
    defaulting to `MudGridChrome.Default`); the components `MudFormatCellsFrame` and
    `MudFormatCellsDialog` (public, as Razor components are, like ExGrid.MudBlazor's); and
    `_content/ExSheet.MudBlazor/mud-ex-sheet.css`. The MudTabs subclass is internal.
  - Every product `using` is written with `global::`: the root namespace `ExSheet.MudBlazor` collides as
    `ExGrid.*` does.
  - `check.sh` packs it into the ExSheet feed and checks its dependencies (exactly ExSheet and
    ExGrid.MudBlazor at the version, MudBlazor 9.0.0), that it ships no script, and that its stylesheet
    is there. It also checks that ExGrid.MudBlazor depends on no ExSheet package, and publishes the
    README's page. `release.yml` is untouched.
- **The Chrome (SH-45).**
  - Every grid seam is forwarded to `Grid`. A layer-2 test fails if a seam falls through to the
    interface default.
  - The dialog is shown through `IDialogService`, at page level, outside the root. Its contents come
    from `FormatCellsOffer` and its choices go through `FormatCellsDraft`.
  - More Colours is `MudColorPicker` in HEX mode, opaque. The tabs are `MudTabs` with automatic
    activation: the arrows, Home and End show the tab they reach.
  - Enter in a field is OK. Escape, the backdrop and the × are a Cancel. MudBlazor's focus trap keeps
    Tab inside. No script.
- **Found.**
  1. MudBlazor's dialog hands the keyboard back to what held it at opening. Opened from a page's
     button, that is the button, and `ReturnKeyboard` was then declined. The frame now first focuses an
     empty element of its own, removed once the dialog has the keyboard, so on closing focus is on
     nothing and the core grants the hand-back. `ReturnKeyboard` is called however the dialog closed,
     behind the render that removes it.
  2. Two arrows typed together on a circuit ended on the wrong tab (layer 3, Server). The tabs now
     count from the tab shown, as the built-in Chrome's do. A layer-2 test fails without the fix.
  3. Without a `MudDialogProvider`, MudBlazor answers after 5 s as if shown. Format Cells is now
     refused by name and cancelled.
  4. Under ExGrid.MudBlazor's Chrome the Context Menu read `exsheet.format-cells`, because commands
     are worded from their id with the id as the fallback. `SheetCommandIds.EnglishFor` is now public,
     and `MudSheetChrome` words ExSheet's commands with it, beneath the Consumer's Label. This affected
     every ExSheet command on `/sheet?chrome=mud`.
- **DemoHost.** `/sheet?chrome=mud` and `/sheets?chrome=mud` use `MudSheetChrome.Default`
  (`DemoChrome.ForSheet`). `DemoChromeAssets` adds a `MudDialogProvider`, and `mud-ex-sheet.css` on the
  Sheet pages.
- **Tests.**
  - Layers 1 and 2, all passing on the agent's tree: ExGrid.Tests 857, ExSheet.Engine.Tests 2107,
    ExGrid.MudBlazor.Tests 88, ExGrid.Components 1094 and one skipped, ExSheet.Components.Tests 414,
    and ExSheet.MudBlazor.Tests 34 (new, bUnit with MudBlazor 9.0.0). After the merge with tickets 57
    and 58: 2137, 857, 88, 1094 + 1, 433 and 34.
  - Layer 3: `format-cells-mud.spec.mjs` (10, new) and `format-cells.spec.mjs` pass on both hosts,
    Chrome, headless on a Mac. edit-stands, declarations, selection-look and reference-text with
    `--grep "mud|MudBlazor"`: 37 passed and 2 skipped on WebAssembly, 38 passed and 1 skipped on
    Server. The full run is CI's.
  - Package smoke passed, run before the tab fix (a C# change only).
- **Left open.** A real IME in the Custom code field under MudBlazor was not tried.
  (`format-cells.spec.mjs` is now listed in `tests/ExGrid.Browser/README.md`.)

*(2026-10-01, agent cf-53, follow-up.)* Horizontal and Underline are dropdowns (`MudSelect`), as
Excel's and the built-in Chrome's are. The first build drew them as radio groups, on the expectation
that Escape in an open list would also reach the dialog and close it. That expectation was never
tried, and MudBlazor's source shows it to be wrong: while its list is open, `MudSelect` stops Escape
at its own element (`stopDown`), so the dialog does not hear it, and the next Escape is the dialog's
Cancel. Layer 3 holds it to that on both hosts (`format-cells-mud.spec.mjs`). ExSheet.MudBlazor.Tests
has 35 tests (one new: the two dropdowns list Excel's choices and open on the Focus cell's).

*(2026-10-01, agent cf-53, follow-up.)* The keyboard's hand-back on closing is ordered by the
browser, not by the server. The follow-up's layer-3 run caught a Custom code entered with Enter
leaving the keyboard on nothing on the Server host (three runs in five). The core's focus function
reached the browser before the render that removed the dialog, while DOM focus was still inside
the dialog, and was declined. A yield on the server does not order a request behind a render's
arrival on a circuit.
- The dialog now closes itself first: on OK too, unless the draft holds a refusal, which still
  keeps it open with the reason.
- The frame then draws once more and waits for that render's `OnAfterRenderAsync`. Blazor runs it
  once the browser has applied the render, so the dialog's removal is applied by then.
- Only then does the frame set OK's change (or the Cancel) and hand the keyboard back.
- After the fix, the failing test passed ten runs in ten on Server, and `format-cells-mud.spec.mjs`
  passed three times over (33 of 33). WebAssembly: 21 of 21 with `format-cells.spec.mjs`.
- Closings that ExSheet starts itself (the Selection moved under Format Cells, a new opening) still
  hand back after a yield only. No user gesture reaches them while the modal dialog stands.

