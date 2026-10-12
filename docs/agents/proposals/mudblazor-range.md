# Proposal: the Wrappers' MudBlazor floor — keep 9.0.0 and change the Wrappers, or raise it to 9.6.0

Written by the background agent on `claude/mudblazor-range-floor`, which implemented ADR-0205's "The
Wrappers' MudBlazor range" and ran its floor. A background agent does not edit an ADR or
`docs/definition-of-done.md`, so this is a question for the user and text for the orchestrator. The
declared range is unchanged on the branch: each Wrapper still takes `[9.0.0,10.0.0)`.

The evidence is the record `verification/2026-10-12-linux-mudblazor-floor/results.md`.

## 1. The question

ADR-0205 gives each Wrapper MudBlazor `[9.0.0,10.0.0)` and asks for its layer-3 specs to be run once
against a DemoHost pinned to the floor. **That run fails at 9.0.0, on both hosts.** Bisected over the
published 9.x releases, **the lowest at which it passes is 9.6.0** (9.0.0 to 9.5.0 fail; 9.6.0 passes on
both hosts, but for two failures that are not MudBlazor's, §2.3).

Which is it?

- **(A) Keep `[9.0.0,10.0.0)`**, and change the Wrappers until the floor run passes at 9.0.0; or
- **(B) Raise the floor to 9.6.0**: each Wrapper takes `[9.6.0,10.0.0)`, and the floor run is the one at
  9.6.0 that this record already holds.

**Recommended: (B).** The reasons are in §4.

(Both 9.6.0 runs fail the Copied Range test, which fails at 9.9.0 too in this container, and the
WebAssembly run failed ED-30 once, which fails at 9.8.0 as well: neither is a MudBlazor floor's, §2.3.)

## 2. What fails below 9.6.0, and why

### 2.1 The filter panel's Operator has no role and no name until 9.6.0 (`ExGrid.MudBlazor`)

From 9.0.0 to 9.5.0, the element that takes the keyboard in the Operator select of the MudBlazor
filter panel has no `role` and no accessible name. Fourteen tests per host fail on it (popovers FN-21,
KB-31, KB-32; circuit KB-35, KB-31, SRV-5; mud-app WR-7), each where it looks for the focused combobox
named "Operator". On screen nothing is wrong: the field shows its operator, takes the keyboard, opens its
list. To a screen reader it is a focusable "=" with no role and no name.

- The Operator's items show their text as `MudSelectItem` child content. MudSelect then renders its
  value through MudInput's *hidden-input presenter*: an `<input type="hidden">` and a `<div tabindex="0">`
  beside it, which takes focus.
- **9.4.0**, [MudBlazor#13077](https://github.com/MudBlazor/MudBlazor/pull/13077) "add combobox/listbox
  ARIA wiring": MudSelect gives its input `role="combobox"`, `aria-label` (its `Label`),
  `aria-expanded`, `aria-controls` and `aria-haspopup` — on the `<input>`, which on this path is the
  hidden one.
- **9.6.0**, [MudBlazor#13080](https://github.com/MudBlazor/MudBlazor/pull/13080) "Mirror combobox
  semantics onto the hidden-input presenter": MudInput copies the `role` and `aria-*` attributes onto the
  focusable `<div>` (`GetDisplayUserAttributes`).

The diagnostic in the record reads the focused element and the hidden input at each version: no role
anywhere at 9.0.0–9.3.0, the role and name on the hidden input only at 9.4.0 and 9.5.0, on the focused
`<div>` from 9.6.0. With the tests pointed at the Operator by its class instead, every one of them passes
at 9.0.0 on WebAssembly (the builder's diagnostic run): the keyboard behaviour holds, the semantics do
not.

### 2.2 Field Settings opens with the keyboard nowhere until 9.2.0 (`ExPivot.MudBlazor`)

At 9.0.0 and 9.1.0, ExPivot's Field Settings… panel under the MudBlazor Chrome opens with DOM focus on
`<body>` on WebAssembly; PV-11 and the cramped Field List test fail on it. The suite runs both on Server
at a 150 ms round trip, where they pass; the diagnostic, at no round trip, loses the keyboard on Server
too. 9.2.0 and later keep it on both.

- The panel's sort select has `ToStringFunc` *and* child content in its items. Before 9.2.0 the child
  content wins, so the select uses the hidden-input presenter of §2.1.
- MudSelect cannot render a value from child content before its items have registered, so its first
  render shows a text `<input>`, and its `OnAfterRenderAsync(firstRender)` awaits a JS call and then asks
  for a second render. `MudInput.FocusAsync` focuses whichever element the *current* render shows. When
  the panel's focus request comes between the two renders — on WebAssembly, and on Server with no round
  trip — it focuses the text input; the second render then turns that input into `type="hidden"`, and the
  browser drops focus to `<body>`. At 150 ms on Server the request arrives after the second render, and
  the `<div>` takes it. The record's focus trace shows the sequence.
- **9.2.0**, [MudBlazor#12837](https://github.com/MudBlazor/MudBlazor/pull/12837) "Give ToStringFunc
  Higher Precedence": a non-empty `ToStringFunc` result makes `CanRenderValue` false, so the select keeps
  its text input and the focus stays.

This one is an outcome that depends on timing: the same Wrapper on the same MudBlazor keeps or loses the
keyboard by when the focus request arrives.

### 2.3 Two failures that are not MudBlazor's

- **The Copied Range test "with and without a Wrapper"** fails at every version, 9.9.0 included, in this
  container: Playwright's Chromium 141 has no `ClipboardChangeEvent` (Chrome shipped it in 143), and
  ADR-0170 draws no Copied Range without it. CI's Chrome passes it. It is the environment's, and is
  judged by CI.
- **ED-30's third-composition test under the mud Chrome** fails now and then on WebAssembly: 2 of 16
  runs at 9.5.0, 2 of 34 at 9.6.0, 4 of 20 at 9.8.0, and none of 16, 20 and 20 at 9.0.0, 9.7.0 and 9.9.0
  (full runs included). It
  follows no MudBlazor version, so no floor brings or removes it. It is an outcome that depends on
  timing — in the product's Keyboard Field hand-off (ADR-0080) or in the test, which sends a
  composition's end and the next one's start as two DevTools commands at once — and wants its own
  ticket (§7).

## 3. The two options

### (A) Keep `[9.0.0,10.0.0)` and change the Wrappers

The Wrappers can be made to hold at 9.0.0. Not made on this branch; what it would take:

- **`ExGrid.MudBlazor`**: the Operator, the second condition's operator and the Boolean value select
  render their items as text through `ToStringFunc`, with no child content, so MudSelect takes its text
  `<input>` path on every 9.x; and the Wrapper passes `role="combobox"`, `aria-label` (the field's own
  label), `aria-haspopup="listbox"` and `aria-expanded` (kept from `OpenChanged`) as attributes, which
  MudSelect forwards to that input. From 9.4.0 MudBlazor adds the same attributes, and the Wrapper's take
  precedence.
- **`ExPivot.MudBlazor`**: the three selects of Field Settings and Value Field Settings already have a
  `ToStringFunc`; their items drop their child content, so the text path is taken before 9.2.0 too.
- **`ExSheet.MudBlazor`**: Format Cells' two selects, checked the same way.
- Layer-2 tests for the attributes, and the floor run at 9.0.0 again on both hosts.

What it costs:

- **The Wrappers would carry a copy of MudBlazor's own ARIA wiring**, written to match what 9.4.0 and
  9.6.0 do, and kept in step by hand: the pairing this repository refuses elsewhere ("must move
  together").
- **It is still incomplete at 9.0.0–9.3.0**: no `aria-controls` and no `aria-activedescendant`, because the
  list's id is MudBlazor's and is not rendered before 9.4.0.
- **Every later Wrapper change has to hold at 9.0.0 as well as at 9.9.0**, and the floor run checks that
  once before a sign-off. Between 9.0.0 and 9.9.0 MudBlazor made 604 commits, 84 of them titled for
  the components the Wrappers use (MudSelect, MudMenu, MudDialog, MudPopover, the inputs and pickers,
  MudList, MudCheckBox, MudRadio); a Wrapper change that leans on one of them is seen only at the next
  floor run.
- The change itself is small (a handful of selects, and their layer-2 tests); the run at 9.0.0 is about
  an hour of layer 3 per host here. The obligation is what lasts, for as long as the range starts at
  9.0.0.

### (B) Raise the floor to 9.6.0

What changes, all small and on this branch if the user decides it:

- The three Wrapper projects: `[9.6.0,10.0.0)`. `tests/ExGrid.PackageSmoke/check.sh` asserts
  `[9.6.0, 10.0.0)` in each `.nuspec`.
- The Wrappers' layer-2 suites then run at 9.6.0, since NuGet resolves the lowest version a range allows
  (§7): the floor is exercised by every layer-2 run, not only by the floor run.
- **The three Wrapper readmes** say: "This brings in … and MudBlazor 9: the package takes
  `[9.6.0,10.0.0)`, any 9.x from 9.6.0. An application on an earlier 9.x is told so at restore (NU1605,
  an error by default) and moves up within 9; one on MudBlazor 10 is told too (NU1608)."
- **The Docs Site's Installation page**: "Each MudBlazor Wrapper takes `[9.6.0,10.0.0)`: 9.6.0 or any later
  9.x. …"
- **The three Change Notes** become *breaks*: an application on MudBlazor 9.0–9.5 that takes the new
  Wrapper must move MudBlazor to 9.6.0 or later.
- **PV-1** says "`ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor
  `[9.6.0,10.0.0)` (ADR-0205)", and adds 0205 to the row's ADRs. **PRE-3**'s closing sentence says
  `[9.6.0,10.0.0)`, and its list drops "from 9.0.0" for `ExPivot.MudBlazor` (§5).
- **ADR-0205** (the orchestrator's): its two bullets become "Each Wrapper declares MudBlazor
  `[9.6.0,10.0.0)`" and "the floor is run … pinned to the floor every Wrapper declares", with a note of
  what changed and why: the floor run of 2026-10-12 failed at 9.0.0, the lowest passing 9.x was 9.6.0,
  and the record names the MudBlazor changes.

What it costs: a Consumer on MudBlazor 9.0–9.5 takes a minor upgrade within 9 to use the Wrappers.
MudBlazor 9.6.0 was published on 2026-06-26; the demo pages, the Docs Site and every CI run are on
9.9.0, and no Wrapper has been tested on anything earlier until this run.

## 4. Recommendation: (B), and why

1. **Rather than be quietly wrong, say it cannot be done.** Both failures are the kind that look right on
   screen: a control with no role or name, and a keyboard lost only when a focus request comes early. A
   floor at 9.6.0 makes restore say so (NU1605) instead of running a Wrapper that is wrong without
   showing it.
2. **An outcome never depends on timing.** Below 9.2.0 Field Settings keeps or loses the keyboard by when
   the focus request arrives. (A) can remove that too, by moving the Wrappers off MudBlazor's presenter;
   (B) removes it by refusing the versions that have it, and leaves no code of ours to keep it away.
3. **(A) copies MudBlazor's internals into the Wrappers**, stays partial at 9.0.0–9.3.0, and binds every
   later Wrapper change to two MudBlazor versions far apart, checked once per release.
4. **(B) costs a Consumer one minor upgrade** within the major version the range already names.

If the user prefers (A) — for a Consumer known to be held below 9.6.0 — the change in §3 (A) is the
work, and the floor run is repeated at 9.0.0 once it is made.

## 5. Wording either way: PV-1 and PRE-3

PRE-3 already states the range (restated 2026-10-11 with ADR-0205), but its list still says "from
9.0.0" for `ExPivot.MudBlazor`, and PV-1 says it outright. Both read as a floor with no ceiling, which
is what ADR-0205 replaced. With `F` the floor the user decides (9.6.0 under (B), 9.0.0 under (A)):

**PV-1**, the statement — replace

> `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor from 9.0.0.

with

> `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor
> `[F,10.0.0)` (ADR-0205).

and add `0205` to the row's closing ADR list: "(ADR-0059/0062/0064/0205)".

**PRE-3**, the statement — in the list, replace

> `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor from 9.0.0 (PV-1).

with

> `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor (PV-1).

and its sentence "Each Wrapper takes MudBlazor in the one range ADR-0205 sets, `[9.0.0,10.0.0)`" names
`[F,10.0.0)`.

No other row names a MudBlazor version. SH-47 names "MudBlazor" with no version, and §23 none.

## 6. A decision, not wording: where the floor run lives in §22

ADR-0205 says "The floor is run once before the sign-off: the Wrappers' layer-3 specs against a
DemoHost pinned to MudBlazor 9.0.0, recorded under `verification/`." The Definition of Done has no
row and no step for it, so Step 7 would not ask for it, and nothing says which record counts or how
the DemoHost is pinned. Proposed, for the user to decide:

**(a) Step 4 gains a paragraph**, after the Server-host commands:

> **The Wrappers' MudBlazor floor** (ADR-0205). Once before the sign-off, the layer-3 tests that run
> under a Wrapper run against a DemoHost built with the MudBlazor every Wrapper declares as its floor, on
> both hosts. Every other run stays on the version the demo pages pin. The demo pages are pinned for the
> run only, both hosts are built from clean, and the version each loads is read back before the run:
>
> ```sh
> sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\1F"|' samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
> # the commands of verification/2026-10-12-linux-mudblazor-floor/results.md, on each host
> git checkout -- samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
> ```
>
> The tests are the record's selection: those whose file, `describe` or title names MudBlazor or `mud`
> (the `?chrome=mud` variants among them), and those that open a page under a Wrapper without saying so
> in their title, which the record lists by name.

The clean build matters: on this branch, two WebAssembly runs made straight after a re-pin (to 9.4.0,
then to 9.9.0) failed tests that pass at that version, and passed once both hosts were rebuilt from
clean. The record's commands remove both hosts' `bin` and `obj/Debug` and print the version each host
will load.

**(b) §23 gains a row** (it is the Wrapper section; ExSheet's and ExPivot's Wrappers have no section
of their own, and their floor is the same run):

> | **WR-10** | MUST | Each MudBlazor Wrapper works at the floor it declares: the layer-3 tests run
> under `ExGrid.MudBlazor`, `ExSheet.MudBlazor` and `ExPivot.MudBlazor` pass against a DemoHost
> built with that MudBlazor, on both hosts (ADR-0205) | Step 4's floor run, recorded under
> `verification/` | zero failures; a failure that also fails at the demo pages' version is that
> test's, and is judged by the run at that version |

The open point for the user: **which commit the floor run must be made at.** ADR-0205 says "once
before the sign-off", not "at X". Read strictly, a run on an earlier commit satisfies it, however much
the Wrappers change after it. Two readings:

- **Once, at any commit before X** — what ADR-0205's words say. Cheap, and it catches a Wrapper
  written against an API MudBlazor added after the floor, which is what a floor run is for.
- **At X, or on a commit whose Wrappers and demo pages are unchanged by X** — the ADR-0208 shape
  (a record holds while its code is unchanged), with the paths `src/*.MudBlazor/` and
  `samples/ExGrid.DemoPages/`. It would need ADR-0205 noted, since it narrows "once".

This record's runs are at `0c0ab368`; under the first reading, and with (B), it already discharges the
step; under the second it is a dry run.

## 7. Found while doing it, for the orchestrator

- **The Wrappers' layer-2 suites already run at the floor.** `ExGrid.MudBlazor.Tests`,
  `ExSheet.MudBlazor.Tests` and `ExPivot.MudBlazor.Tests` reference the Wrapper projects and nothing
  else that brings MudBlazor, so NuGet resolves the lowest version the range allows, as it did for the
  old `9.0.0` reference. The same holds for `tests/ExGrid.PackageSmoke`. Only the DemoPages (and the
  Docs Site) pin a later 9.x. bUnit cannot see either failure of §2 (a focused element, an attribute on
  the element focus lands on), which is why they pass at 9.0.0.
- **The Copied Range test "with and without a Wrapper" cannot pass on Playwright's Chromium 141**, which
  this container and other agents' local runs use: no `clipboardchange` before Chrome 143 (ADR-0170).
  Every local run here reports it failing. A `test.skip` on a browser without `ClipboardChangeEvent`
  would say so instead, but the spec is not this branch's and the decision is not an agent's.
- **ED-30's third-composition test (`key-field.spec.mjs:341`, mud Chrome) is intermittent on
  WebAssembly**, at `0c0ab368`: 8 failures in 126 runs across six MudBlazor versions, all with the same
  reading (the editor's waiting request granted between one composition's end and the next one's start).
  A ticket for whoever owns ADR-0080's Keyboard Field: whether the race is the product's or the test's
  (its two DevTools commands sent at once), and the fix that makes the outcome deterministic. `main` has
  changed the grid's script and ExSheet since `0c0ab368` (#81, #86's "a scripted focus asks first"); the
  repeat after this branch's merge of `main` is in the record.
- **The root `README.md` still says "Beta"** and `0.1.0-beta.N` with `--prerelease` in its Install and
  Status sections. ADR-0205 asks the package readmes to be worded for any version, and this branch
  did that; the root readme is not packed, and describes the repository as it is today. It will be
  false at the `0.1.0` tag (Y may change only `verification/` and `docs/releases/`), so it has to be
  reworded on the release branch before X, or on `main` before the cut.
- **The Docs Site's Installation and Packages pages** say "Every version so far is a prerelease,
  `0.1.0-beta.N`" and show `--prerelease` in `Examples/Setup/install.sh`. True today; ADR-0205's "the
  site describes the latest stable version" makes them false once `0.1.0` is published, so they
  belong to the same rewording.
