# ExGrid — guide for AI agents

An Excel-like grid component for Blazor. **The specification is settled; there is no
implementation yet.**

The products are **ExGrid** (display-oriented, the one that is specified) and **ExSheet**
(edit-oriented, later). `Ex` is a prefix that names the claim — Excel-like operability — in the
same position where `ag-grid` puts `ag` = "AGnostic". Both live in one repository and ship as
separate packages ([ADR-0019](docs/adr/0019-one-repository-many-packages.md)).

This document holds only what you will get wrong without being told. It does not restate the
specification.

---

## Two rules that override convenience

### 1. Everything committed to this repository is written in English

Conversation with the user may be in any language. **Anything that lands in the repository —
documents, ADRs, code, comments, commit messages, test names, UI strings — is English.**

Domain terms stay as `CONTEXT.md` defines them (Focus, Anchor, Overlay, Window, Consumer,
Chrome, Overwrite, Caret, …); do not translate those.

### 2. JavaScript is allowlisted, not "minimised"

JS is used only where Blazor genuinely cannot do the job, or where a **recorded measurement**
shows the Blazor-side approach is too slow. There are currently **three** permitted uses:
capture-phase `keydown`, reading/setting scroll offsets, and the clipboard.

**Anything else needs a new ADR.** See
[ADR-0021](docs/adr/0021-javascript-is-allowlisted-not-minimised.md), which also lists what
deliberately stays out of JS (popovers, text measurement, focus, overlay geometry) so you do
not re-derive it.

---

## Read these first

| | Contents |
|---|---|
| `CONTEXT.md` | **Glossary.** No implementation detail. `_Avoid_` lists words you must not use |
| `docs/adr/` | **Decisions and their reasons.** 23 of them. The implementation follows these |
| `spikes/render-bench/README.md` | Render-cost measurement harness (disposable) |

**Rules:**

- **Read the relevant ADR before changing behaviour.** The reason is written down. Changing
  something without knowing the reason usually walks back into an option that was already
  rejected.
- **If a decision changes, rewrite the ADR.** Do not let the implementation drift away from it
  silently. ADR-0001 / 0003 / 0005 / 0008 were each rewritten mid-design, and the predictions
  that turned out wrong are recorded as wrong.
- **A new term goes into `CONTEXT.md`.** The glossary leads the implementation, it does not
  trail it.

## Toolchain

**`dotnet` is not on `PATH`. It is managed by nix.**

```sh
nix develop -c dotnet build ...
nix develop -c dotnet test ...
nix develop .#browser -c node ...   # when a headless Chromium is needed
```

- **Shipped packages target `net8.0`** (single-target; newer runtimes load it as-is —
  [ADR-0022](docs/adr/0022-packages-target-net8-and-run-on-everything-newer.md)). The **SDK**
  is .NET 10 — SDK version and target framework are independent. The code is therefore C# 12;
  do not raise `LangVersion`
- **Flakes only see git-tracked files.** A new file must be `git add`-ed before the build can
  see it (committing is not required)
- **Do not commit or push unless asked**

## The spine of the design — how to decide when unsure

The principles that run through all 23 ADRs. **A new decision that follows these will not
collide with the existing ones.**

1. **Rather than be quietly wrong, say it cannot be done.** This component displays money and
   risk numbers. Refuse, or show it unreadably, before producing something that looks
   plausible but is not. (Copy is never truncated / paste never spills outside the selection /
   selection is dropped when the order changes / a number that does not fit becomes `####`)
2. **Immutable base plus a thin diff.** Layer on top; never rewrite in place. (Overlay,
   selection painting, Row Identity)
3. **The grid neither holds nor executes.** Not the data, not sorting, not filtering, not
   grouping — all Consumer-side. The grid notifies.
4. **Chrome renders and calls back; the core decides meaning.** Swapping Chrome must not change
   behaviour.
5. **Selection is cheap, so it is not capped. Caps belong on what cannot be executed.**

## Traps that are hard to spot

Things actually hit during this design, and things the measurements exposed. **All of them are
the kind that still look correct on screen**, so review will not catch them.

### Blazor / Razor

- **Write `ShouldRender()` by hand.** Blazor's automatic parameter change detection only reports
  "unchanged" for value/immutable types. A mutable reference type such as `Row` is treated as
  "may have changed" **even when the reference is identical**, so memoisation does not happen
  (measured; ADR-0003).
- **Cache delegates passed as parameters in a field.** A method group creates a new instance on
  every render and slips past the `ShouldRender()` comparison every time.
- **`StateHasChanged()` can complete the render synchronously.** A field set just before it may
  already have been cleared by `OnAfterRender` when you read it back — this produced a real
  `NullReferenceException`. Copy to a local first.
- **Two name collisions exist.** A Razor page class with the same name as the root namespace
  shadows the namespace (`Bench.razor` in namespace `Bench` → CS0426). An enum named
  `RenderMode` collides with `Microsoft.AspNetCore.Components.Web.RenderMode`, which
  `_Imports.razor` pulls in.

### Specific to this component

- **Row height must go through a C# parameter.** Changing it in CSS alone makes the
  virtualisation arithmetic, the selection overlay and the editor position **drift slightly
  out of alignment** (ADR-0013 / 0016).
- **The capture-phase key listener attaches to the instance root, not `document`.** On
  `document`, every grid on the page reacts to every keystroke. The bubble phase is too late —
  the editor has already handled the key (ADR-0010 / 0018).
- **CSS classes take an `ex-` prefix; JS is a module returning per-instance handles.**
  `spikes/render-bench` uses `.r` `.c` `.sel` and `window.bench` as a **bad example** on
  purpose (it is disposable). Do not carry that into product code (ADR-0018).

### Environment

- **`pkill -f "Bench.Host"` kills the calling shell**, because the pattern matches the shell's
  own command line. Stop the spike host with `fuser -k 5199/tcp`.

## Tests

**Three layers. All three gate. Performance does not gate.**

| Layer | Tool | Covers |
|---|---|---|
| 1. Pure logic | xUnit | Selection rectangle arithmetic, Anchor/Focus, Enter/Tab cycling, paste shape rules, copy refusal rules, overflow decisions, Auto width, row sequence version |
| 2. Component | bUnit (no browser) | Which rows get rendered, and **whether row memoisation actually skips** (count renders) |
| 3. Browser | CDP driver | Capture-phase keys, clipboard, popovers, multiple-instance independence |

**Rules:**

- **Put the ADR number in the test name.** A failure then says which decision was violated.
  ```csharp
  [Fact]  // ADR-0011: selection is dropped when the sort order changes
  public void Selection_is_dropped_when_sort_changes() { … }
  ```
- **The ADRs already contain the cases in prose.** The Enter/Tab cycling diagram, the paste
  shape table, the overflow rules — they transcribe almost directly.
- **`GridSource.From` is the reference implementation of filter and sort semantics**, and its
  behaviour is the specification (ADR-0001). Pin null ordering, case sensitivity and culture
  exhaustively.
- **Never gate on performance.** It swings with the environment — the same measurement gave a
  maximum of 11.7 ms headless and 19.7 ms on real hardware. Accumulate the result JSON from
  `spikes/render-bench` and watch the trend instead.

## Measure before claiming anything about performance

**Two predictions were wrong during this design.** Both are recorded as wrong in the ADRs.

- "Making cells components is slow" → held only under half the conditions, and inverted under
  the other half (ADR-0003)
- "Painting selection with per-cell classes is heavy while dragging" → the median was light;
  what mattered was **how many rows change membership at once** (ADR-0008)

`spikes/render-bench` still works. **Add a mode and measure rather than asserting from
reasoning.**

## Do not

- **Do not add MudBlazor, Fluxor or similar dependencies to the core.** Integrations live in
  separate packages (`ExGrid.MudBlazor`, `ExGrid.Fluxor`). Sharing a repository is not the same
  as mixing dependencies — **enforce it structurally through project reference direction**.
  Browser-specific API use is likewise confined to popovers and the clipboard
  (ADR-0017 / 0018 / 0021).
- **Do not turn `CONTEXT.md` into a specification or a scratchpad.** It is a glossary and
  nothing else.
- **Do not quietly rewrite an ADR.** When a decision changes, leave what changed and why in the
  text (ADR-0005's "the rationale was replaced once" is the model).
