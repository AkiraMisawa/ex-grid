# ExGrid — guide for AI agents

An Excel-like grid component for Blazor. **The specification is settled; implementation
is underway** (pure-logic core, first component layer, and the demo host exist).

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
shows the Blazor-side approach is too slow. There are currently **five** permitted uses:
capture-phase `keydown`, reading/setting scroll offsets, the clipboard, a `ResizeObserver`
reporting the Scrollbar Gutter, and a `mousemove` listener that reports the pointer **only when
it moves onto another row, and when it comes to rest** (the hover band, ADR-0029; the error
popover, ADR-0034 — a Blazor handler would be a wire round trip per frame on Server). The last
two turn on a distinction worth keeping: the grid never **measures** (a synchronous read it
performs, on the path to a paint), it is **told** when something the browser already knows has
changed or settled.

**Anything else needs a new ADR.** See
[ADR-0021](docs/adr/0021-javascript-is-allowlisted-not-minimised.md), which also lists what
deliberately stays out of JS (popovers, text measurement, focus, overlay geometry) so you do
not re-derive it.

---

## Read these first

| | Contents |
|---|---|
| `CONTEXT.md` | **Glossary.** No implementation detail. `_Avoid_` lists words you must not use |
| `docs/adr/` | **Decisions and their reasons.** 36 of them. The implementation follows these |
| `docs/definition-of-done.md` | **The exit criteria.** What "finished" means, as pass/fail criteria tied to ADRs, plus what is still open |
| `spikes/render-bench/README.md` | Render-cost measurement harness (disposable) |

**Rules:**

- **The ADRs and `docs/definition-of-done.md` are authoritative.** Where code, a comment, a plan
  or a conversation disagrees with them, they win, and the disagreement is a defect in the other
  thing. **A major architectural decision is made by writing an ADR**, not by writing code that
  implies one.
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
nix develop .#browser -c npx playwright test   # layer 3, from tests/ExGrid.Browser
```

- **Shipped packages target `net8.0`** (single-target; newer runtimes load it as-is —
  [ADR-0022](docs/adr/0022-packages-target-net8-and-run-on-everything-newer.md)). The **SDK**
  is .NET 10 — SDK version and target framework are independent. The code is therefore C# 12;
  do not raise `LangVersion`
- **Flakes only see git-tracked files.** A new file must be `git add`-ed before the build can
  see it (committing is not required)
- **Do not commit or push unless asked**

## The spine of the design — how to decide when unsure

The principles that run through all 36 ADRs. **A new decision that follows these will not
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
- **Three name collisions exist.** A Razor page class with the same name as the root namespace
  shadows the namespace (`Bench.razor` in namespace `Bench` → CS0426). An enum named
  `RenderMode` collides with `Microsoft.AspNetCore.Components.Web.RenderMode`, which
  `_Imports.razor` pulls in. And inside any namespace nested under `ExGrid` — the DemoHost,
  the Wrapper itself — a bare `@using MudBlazor` resolves to `ExGrid.MudBlazor`, so `Color`
  and `Typo` vanish: write `@using global::MudBlazor`.

### Specific to this component

- **No colour is a C# parameter, and no pixel is only a stylesheet value.** Appearance travels
  as Visual Tokens the stylesheet reads; geometry is emitted inline on the instance root from the
  resolved Grid Metrics (ADR-0027/0028). A stylesheet literal paired with a C# constant by a
  "must move together" comment is the defect that split replaces — do not add another pairing.
- **Row height must go through a C# parameter.** Changing it in CSS alone makes the
  virtualisation arithmetic, the selection overlay and the editor position **drift slightly
  out of alignment** (ADR-0013 / 0016).
- **The capture-phase key listener attaches to the instance root, not `document`.** On
  `document`, every grid on the page reacts to every keystroke. The bubble phase is too late —
  the editor has already handled the key (ADR-0010 / 0018).
- **CSS classes take an `ex-` prefix; JS is a module returning per-instance handles.**
  `spikes/render-bench` uses `.r` `.c` `.sel` and `window.bench` as a **bad example** on
  purpose (it is disposable). Do not carry that into product code (ADR-0018).
- **A scrollbar takes about 15px out of the Viewport on Windows and Linux, and 0 on macOS.**
  Every geometry bug this causes is invisible on the development machine. The gutter is
  reported by the browser and subtracted in `ViewportBox` (ADR-0013 / 0021) — never assumed,
  never measured once at attach (`overflow: auto` shows no bar until the content overflows,
  so attach is the moment the answer is 0).

### Environment

- **`pkill -f "Bench.Host"` kills the calling shell**, because the pattern matches the shell's
  own command line. Stop the spike host with `fuser -k 5199/tcp`.
- **Headless Chrome on macOS keeps overlay scrollbars on the horizontal axis** whatever the CSS
  asks for, so a scrollbar test written there passes without testing anything. Layer 3 runs
  headed for that reason (ADR-0026).

## What counts as verified

**A green build is not a result.** `dotnet build` and `dotnet run` prove that the code compiles and
starts; they say nothing about whether it does what an ADR decided.

```sh
nix develop -c dotnet test ExGrid.slnx      # layers 1 and 2, both suites
```

- **A UI-facing change is not verified until a real browser has exercised it.** Layers 1 and 2
  cannot see a sticky header slipping, a scrollbar eating the last column, or a key the browser
  took first — which is exactly why layer 3 exists, and why the worst bugs in this project were
  invisible to suites that were passing at the time. Use `tests/ExGrid.Browser`, and read the
  layer 3 rules below before trusting a pass.
- **An unexpected console message or runtime exception is a failure**, not noise to scroll past.
  This component displays money; something the browser is complaining about may be something the
  reader is already seeing wrong.
- **The virtualisation and performance invariants hold, or the change is wrong.** The DOM does not
  grow with the row count, rows still skip their render, and nothing per-cell reaches JavaScript
  (P1-P9 in ADR-0027; the Definition of Done states them as criteria you can run).

## Tests

**Three layers. Layers 1 and 2 gate; layer 3 is run by hand (there is no CI). Performance
never gates.**

| Layer | Where | Tool | Covers |
|---|---|---|---|
| 1. Pure logic | `tests/ExGrid.Tests` | xUnit | Selection rectangle arithmetic, Anchor/Focus, Enter/Tab cycling, paste shape rules, copy refusal rules, overflow decisions, Auto width, row sequence version |
| 2. Component | `tests/ExGrid.Components` | bUnit (no browser) | Which rows get rendered, and **whether row memoisation actually skips** (count renders) |
| 3. Browser | `tests/ExGrid.Browser` | Playwright | The Scrollbar Gutter; and to come: capture-phase keys, clipboard, popovers, multiple-instance independence |

**Rules:**

- **Layer 3 does not run automatically, and layers 1 and 2 cannot cover it.** `npm ci &&
  npx playwright test` in `tests/ExGrid.Browser`, or `nix develop .#browser -c npx playwright
  test`. It needs Chrome installed and starts the DemoHost itself; there is no CI, so it is run
  by hand — and **deliberately on Windows or Linux**, where the platform's own scrollbars occupy
  layout and the assertions are not tautologies. `tests/ExGrid.Browser/README.md` says what it
  asserts and what it deliberately does not.

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

## Working in parallel

Independent tasks may run as background agents, each in its own worktree
(`.claude/worktrees/`, git-ignored; the flake rule above still applies inside it). The
rules that make this safe:

- **Decisions are serial. Only implementation is parallel.** A background agent never changes
  an ADR, `CONTEXT.md` or `docs/definition-of-done.md`. When its task turns out to need one,
  it stops, returns the proposal and the reason as its report, and leaves its worktree
  building. The orchestrator collects the proposals from every running agent and puts them
  **together** in front of the user, who decides; the orchestrator writes the ADR and sends
  the decision back. Merging is detecting the collision and presenting it, not resolving it.
  Two proposals that touch the same concept are the case to watch — one implementation may
  have to be redone, and that is the user's call.
- **Split by files and by decisions.** Parallelise when the file sets are disjoint and no
  task is expected to need an ADR; then the merge is textual and the orchestrator can do it.
  If a task cannot be described without a decision, make the decision first, here, and fan
  out afterwards — the ADR is the contract that makes parallel work possible.
- **Only one agent at a time runs layer 3.** The DemoHost sits on a fixed port and an
  already-running host is reused, so a second runner would be testing the *other*
  worktree's code and passing. The suite is also single-worker because it changes the
  page zoom.
- **Remove a worktree when its agent is done.** `git worktree list` shows the leftovers; a
  stale one starts the next agent from an old tip.

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
- **Do not weaken a requirement to make an implementation easier.** If a criterion cannot be met,
  leave it failing and say so. A criterion quietly relaxed to fit the code is how a specification
  stops meaning anything — and changing what is required is an ADR change, made deliberately and
  in writing.
- **Do not stop at an intermediate task while unblocked work remains.** Finishing a step is not
  finishing the task. If something is genuinely blocked, name what blocks it and carry on with
  everything that is not.
