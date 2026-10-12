# The public surface is what Consumers, Chromes and the sibling products use

*(Decided with the user on 2026-10-11. It settles
[ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)'s "a
reviewed public surface" for the first stable version,
[ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)'s `0.1.0`.)*

ADR-0042 asked, before any stable version, for an ADR stating which C# types are the Consumer's
contract, the rest made internal, and a baseline file from then on. Nothing has done it yet.

- **The ten packages export 524 public top-level types**: ExGrid 193, ExPivot.Engine 101, ExSheet.Engine
  54, ExSheet 54, ExGrid.Data 46, ExPivot 42, ExPivot.MudBlazor 14, ExGrid.MudBlazor 13,
  ExSheet.MudBlazor 5, ExGrid.Data.Arrow 2. ADR-0042's "98" counted the core before ExSheet and ExPivot
  asked for their declarations.
- **By what uses them, ExGrid's 193 split** into 119 that a sibling product uses, 25 that only the
  samples and the Docs Site use, 42 that only tests use, and 7 that nothing uses. About 30 of the last
  two groups are machinery: `EdgeBand`, `CycleOrder`, `SortCycle`, `ViewportBox`, `ClipboardRules`,
  `CopyDecision`, `PasteDecision`, `OverflowRules`, `GridKeys`, `RowMarkRules`. The rest cannot leave the
  surface, because a public signature names them (`IRowMarks<TRow>` is the type of `Marks`).
- **ExGrid, ExGrid.MudBlazor, ExSheet.Engine and ExPivot.MudBlazor declare no `InternalsVisibleTo`**, so
  every type their tests reach is public. The other six declare it for their own test projects only.
- **Some of the surface cannot be made internal.** Razor generates every component public, so
  `ExGridRow`, `SummaryFiguresMenu`, ExPivot's views and frames and ExPivot.MudBlazor's parts are public,
  and so are the types of their parameters. The grid's 26 `[JSInvokable]` methods are public because
  JavaScript calls them, and so are their types (`ClipboardPayload`).

## The decision

### What is public

**A type is public when a Consumer, a Chrome or a sibling product uses it, or when a public signature
names it.** In the packages' terms:

1. **The Consumer's contract**: the components and their parameters, events and methods; the types those
   name; the sources, queries, filters, sorts and the data a Consumer passes or receives.
2. **The Chrome's seams**: `IGridChrome` and the contexts its signatures carry, and the helpers that let a
   substituted Chrome decide as the core does (`FilterPanelChoices`, `MenuKeys`).
3. **What the sibling products use, as Consumers.** ExSheet and ExPivot are ExGrid's Consumers
   ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md),
   [ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)), and a Wrapper has
   only the seams every Consumer has
   ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)). So what they use
   from a sibling is in that sibling's contract — the declarations ExSheet and ExPivot asked for, the
   values public methods return, the helpers a Consumer answering `OnCopy` needs. Where a sibling uses a
   type for one value that a contract member already answers, the sibling moves to that member instead:
   `ExSheet` reads `ViewportGeometry.MaxScrollHeightPx` to refuse a row height, and
   `GridMetrics.LargestRowHeightFor` already returns that answer "so as not to derive the bound again".
4. **Every type a public signature of the above names.**

**Everything else becomes internal.** A member of a public type whose signature names an internal type
becomes internal with it (`ColumnWidthSpec.ResolveWidthPx(AutoWidth)`).

- **Tests reach internals through `InternalsVisibleTo`, granted to test projects only**, including
  another product's test project where it uses the type. **Never to a package**: a sibling or a Wrapper
  that needed an internal would be doing what a third party's Wrapper could not (ADR-0030, DoD FL-1).

### What is public and not the contract

**Razor components a Consumer does not place, `[JSInvokable]` methods and the types only they name stay
public, marked `[EditorBrowsable(EditorBrowsableState.Never)]`, and are not the contract.**

- They are listed in the baseline and on the Docs Site's API reference as "not the contract: may change
  in any version", so that hiding them is stated, not silent.
- A member only tests call (`OnPasteAsync`) is not one of these: it becomes internal.
- Rejected for `0.1.0`:
  - moving the 26 interop methods to an internal receiver object, which would take every key,
    clipboard, pointer, IME and scroll path through layer 3 again on both hosts;
  - rewriting the part components as internal C# classes.

  Either can be reconsidered at the review before `1.0`.

### How deep, for `0.1.0`

- **Types are reviewed; members are not reshaped.** `0.1.0` promises the Definition of Done's sign-off,
  not a frozen API (ADR-0200). A `0.x` minor version may change a member, and its Release Notes say so
  under "Breaking changes"
  ([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
- **Before `1.0`, every member is reviewed.** That is a later decision's work.
- **Making a type internal that `0.1.0-beta.2` exported is a breaking change**, and gets its Change Note.
  A package validation run once against `0.1.0-beta.2` lists them for `0.1.0`'s Release Notes.

### The baseline

- **`Microsoft.CodeAnalysis.PublicApiAnalyzers`, in every shipped project**, with `PublicAPI.Shipped.txt`
  and `PublicAPI.Unshipped.txt` beside it.
  - A public member not declared in them (RS0016) or declared and gone (RS0017) fails the build, as an
    undocumented public member already does (CS1591 under warnings as errors).
  - The analyzer is a private asset, so no package depends on it.
- **The cut before a tag moves `Unshipped` into `Shipped`** (ADR-0190).
- **If the analyzer cannot see the members Razor generates** (a component's `[Parameter]`s written in
  `@code`), the first build of the change that adds it shows it. A layer-1 snapshot test of each shipped
  assembly's surface then replaces it, for the same files' purpose.

### What the lists raised *(2026-10-12, decided with the user)*

The per-package lists, made by this rule and each read again by a second reader, raised three questions
the rule did not settle:

- **"A member only tests call becomes internal" reaches test hooks, machinery, and entry points a
  contract member already answers**: `OnPasteAsync`; `Entry.FromFormula`, which `Entry.Parse` answers;
  `Sheet.SetEntry` (`SetEntries`); `Sheet.Rename` (`Do(SheetEdit.Rename)`); `ClipboardParse.ParseBlock`
  (`Parse`); ExPivot's `ItemListCap`.
  - **A member that is data on a contract type stays public**, even where only tests read it today: data
    a Consumer passes or receives (`CsvEncoding.ShiftJis`, `Snapshot.IsBlank`, `GridFindResult.Row`),
    ExPivot's `IsLoading` and `RefreshAsync`, and the MudBlazor Wrapper's font seam.
  - **So does a member an ADR or a criterion gives the Consumer**: `ExGrid.CanSummarize` (ADR-0130),
    `GetFocusedValue` (FN-20), `GridRuleset.Row` (ADR-0034).
  - Read literally, the clause would have hidden about sixty such members, some of which the Docs Site
    tells a Consumer to write.
- **ExPivot.Engine's whole-report path becomes internal.** That is `PivotCube`, `PivotReport`,
  `PivotReportRow`, `PivotReportColumn` and `PivotValue`, with the `PivotEngine` members that name them.
  - Nothing outside tests uses it.
  - Since ADR-0151 a server computes through `PivotReportSource` and the display types, which are
    contract. ADR-0060's "a server computes the same report the screen shows" now holds through that
    contract, and the Engine's README shows it that way.
- **`RowMarkRules.LineUp` is contract.** `RowMarkIntent.Positions` sends a Consumer answering
  `OnMarkIntentAsync` to it, and ADR-0043 keeps a gesture's meaning in the core.
  - Its `HeaderState` and `HeaderPress`, which only the core uses, become internal.
  - The Context's list of machinery named `RowMarkRules` whole, counted by what used it. That is
    corrected here.

## Considered options

- **Review every type and member, and reshape the poor ones, before `0.1.0`.** The strongest reading, but
  each contested member is a decision, and `0.x` exists to let members move.
- **Mark the non-contract types `[EditorBrowsable(Never)]` and leave them public.** Cheap, but a Consumer
  could still depend on `EdgeBand`, and only a document would say not to. Internal makes the compiler say
  it.
- **Ship the surface as it is, and review it at `1.0`.** It relaxes ADR-0042 and the Definition of Done to
  fit the code, and test-only types would reach a stable API reference, where removing them later breaks
  someone.
- **Keep every type a sibling touches public, whole.** No code change, but `ViewportGeometry`'s slicing
  would become contract for one constant's sake.
- **`InternalsVisibleTo` to the sibling packages.** It rewrites the premise that ExSheet, ExPivot and the
  Wrappers are built as Consumers, and FL-1 could no longer be shown by a Wrapper.
- **NuGet package validation alone as the baseline.** It reports breaking changes only; an addition would
  not show up in review, and ADR-0042 asks for every change.

## Consequences

- ADR-0042's bullet is corrected: the count, and "A `0.x` beta may ship that. `1.0` may not.", which was
  written when stable meant `1.0`.
- The Definition of Done's Step 7 clause on the public surface is unchanged in wording, and is met at the
  depth stated here.
- The work, in tickets, after the branches that add public members have merged: the per-package lists by
  this rule (a type whose category is unclear is put to the user), the internalisation and the
  `InternalsVisibleTo` grants, ExSheet's move to `LargestRowHeightFor`, the `[EditorBrowsable(Never)]`
  marks, the baseline files, and a Change Note per type `0.1.0-beta.2` exported that becomes internal.
- The Docs Site's API reference, which waited for this decision, can be built: it shows the contract, and
  lists what is public and not the contract separately.
