# Proposal: the Wrappers' MudBlazor floor — keep 9.0.0 and change the Wrappers, or raise it to 9.6.0

Written by the background agent on `claude/mudblazor-range-floor`, which implemented ADR-0205's "The
Wrappers' MudBlazor range" and ran its floor. A background agent does not edit an ADR or
`docs/definition-of-done.md`, so this is a question for the user and text for the orchestrator. The
declared range is unchanged on the branch: each Wrapper still takes `[9.0.0,10.0.0)`.

> **Decided with the user, 2026-10-12: (B), in full.** Each Wrapper now takes `[9.6.0,10.0.0)`, and the
> branch carries what (B) lists. ADR-0022's rule covers the Wrappers' floor, with a note that a `0.x`
> prerelease line may raise it (§3, the first reading). The floor run goes into the Definition of Done
> as §6(a) and §6(b) with F = 9.6.0, a record holding while `src/*.MudBlazor/` and
> `dotnet/samples/ExGrid.DemoPages/` are unchanged up to X (§6, the second reading), and §6(c)'s layer 2
> across the range joins it. §7's layer-2 guard is added, with a test for §2.4. The ADR and Definition
> of Done edits are in `docs/agents/proposals/mudblazor-range-floor-apply.md`. What follows is the
> proposal as the user read it.

The evidence is the record `verification/2026-10-12-linux-mudblazor-floor/results.md`.

## 1. The question

ADR-0205 gives each Wrapper MudBlazor `[9.0.0,10.0.0)` and asks for its layer-3 specs to be run once
against a DemoHost pinned to the floor. **That run fails at 9.0.0, on both hosts.** Bisected over every
9.x from 9.0.0 to 9.9.0, **the lowest at which it passes is 9.6.0** (9.0.0 to 9.5.0 fail; 9.6.0 passes on
both hosts, but for two failures that are not MudBlazor's, §2.3). 9.10.0 (2026-09-13) and 9.11.0
(2026-09-27), published inside the range, were not run in layer 3. The bisection ran on Playwright's
bundled Chromium 141 as the one `chrome` project: it says where the floor run should pass, and is not
that run (§6).

Which is it?

- **(A) Keep `[9.0.0,10.0.0)`**, and change the Wrappers until the floor run passes at 9.0.0; or
- **(B) Raise the floor to 9.6.0**: each Wrapper takes `[9.6.0,10.0.0)`, and the floor run is then made
  at 9.6.0, in installed Chrome and Edge on both hosts (§6). This record's runs at 9.6.0 say it should
  pass; they do not stand in for it.

**Recommended: (B).** The reasons are in §4.

(Both 9.6.0 runs fail the Copied Range test, which cannot pass on this container's Chromium at any
version, and the WebAssembly run failed ED-30 once, which fails at 9.5.0 and 9.8.0 as well. Neither
follows a MudBlazor version (§2.3), and both stand between this record and a floor run that counts
(§6).)

Since the review, the Wrappers' layer-2 suites were also run with MudBlazor pinned to each published
9.x, 9.10.0 and 9.11.0 included. They found a third MudBlazor behaviour, at 9.3.0 to 9.5.0 only: a
refused filter operand loses its words (§2.4). It lies inside `[9.0.0,10.0.0)` and above its floor, so
no floor run at 9.0.0 could see it.

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

### 2.4 A refused operand loses its words at 9.3.0 to 9.5.0 (`ExGrid.MudBlazor`, found in layer 2)

`ExGrid.MudBlazor`'s layer-2 suite passes at 9.0.0 to 9.2.0 and at 9.6.0 to 9.11.0. At 9.3.0, 9.4.0 and
9.5.0 it fails seven tests: a filter operand that does not read one way (a number in another culture's
separators, a `DateTimeOffset` column's date with no offset) is refused, and Apply stays unavailable,
but the refusal's words are not on the page. The field has no error class, `aria-invalid="false"`, and
no helper text. Those seven are WR-2's and ADR-0023's "refused by name" tests
(`MudNumberOperandTests`, `MudDeclaredDateOperandTests`). The record's §6 has the runs and the markup.

- The operand is a `MudTextField` given `Value`, `ValueChanged`, `Error` and `ErrorText` by the Wrapper.
- **9.3.0**, [MudBlazor#12892](https://github.com/MudBlazor/MudBlazor/pull/12892) "Inputs: Fix validation
  on programmatic value change": once the field is touched, a `Value` parameter that changes re-validates
  it, and that validation, which has nothing of its own to refuse, writes `Error` back to false over the
  Wrapper's.
- **9.6.0**, [MudBlazor#13312](https://github.com/MudBlazor/MudBlazor/pull/13312) "Inputs: Don't
  re-validate on the `@bind-Value` echo": a `Value` that is the field's own `ValueChanged` coming back no
  longer re-validates.

Each is the change in MudBlazor's log at its boundary that re-validates, or stops re-validating, on a
value from the parent: matched by reading the two diffs, not by building MudBlazor at each commit. Seen
in bUnit only (Apply's state read at 9.5.0 by a probe): no layer-3 test types a refused operand under the
mud Chrome, so neither the floor run nor its bisection could see it. To the user it is a filter that
will not apply and does not say why: the first principle's "say it cannot be done", lost.

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
- **`ExGrid.MudBlazor`, the operand** (§2.4): the refusal held at 9.3.0 to 9.5.0 as well, with the
  Wrapper's own words under the field instead of MudBlazor's `ErrorText`, or with no `Value` handed back
  to a touched field. The floor run cannot show this; layer 2 run at those versions can.
- Layer-2 tests for the attributes, the floor run at 9.0.0 again on both hosts, and layer 2 at every 9.x
  of the range (§6).

What it costs:

- **The Wrappers would carry a copy of MudBlazor's own ARIA wiring**, written to match what 9.4.0 and
  9.6.0 do, and kept in step by hand: the pairing this repository refuses elsewhere ("must move
  together").
- **It is still incomplete at 9.0.0–9.3.0**: no `aria-controls` and no `aria-activedescendant`, because the
  list's id is MudBlazor's and is not rendered before 9.4.0.
- **Every later Wrapper change has to hold at 9.0.0 as well as at 9.9.0.** Between 9.0.0 and 9.9.0
  MudBlazor made 604 commits, 84 of them titled for the components the Wrappers use (MudSelect, MudMenu,
  MudDialog, MudPopover, the inputs and pickers, MudList, MudCheckBox, MudRadio). Layer 2 already runs at
  the floor on every push (§7), so what a Wrapper change does to markup and to a component's state — a
  role, a name, an error's words — is seen at 9.0.0 at once, wherever a layer-2 test asserts it. What
  only a browser shows — where focus lands, what a timing changes — is seen only at the next floor run.
- **Neither end of the range sees what breaks between them** (§2.4). The wider the range, the more
  versions a Consumer may resolve that no run of ours has met.
- The change itself is small (a handful of selects, and their layer-2 tests); the run at 9.0.0 is about
  an hour of layer 3 per host here. The obligation is what lasts, for as long as the range starts at
  9.0.0.

### (B) Raise the floor to 9.6.0

What changes, all small and on this branch if the user decides it:

- The three Wrapper projects: `[9.6.0,10.0.0)`. `dotnet/tests/ExGrid.PackageSmoke/check.sh` asserts
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
- **Two comments that name the floor** say 9.6.0: `dotnet/samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj`
  ("The latest MudBlazor, above the Wrapper's 9.0.0 floor on purpose") and
  `dotnet/tests/ExSheet.MudBlazor.Tests/ExSheet.MudBlazor.Tests.csproj` ("It runs ExSheet.MudBlazor on
  MudBlazor's floor, 9.0.0, as the Wrapper's tests do (ADR-0022)").
- **ADR-0022, a question for the user.** The Wrappers take their floor by ADR-0022's rule:
  `ExGrid.MudBlazor.csproj` calls its range "ADR-0022's rule applied to the one dependency this package
  adds", and `ExSheet.MudBlazor.csproj`, `ExPivot.MudBlazor.csproj` and the ExSheet Wrapper's tests cite
  ADR-0022 for it too. ADR-0022's Consequences say: "Raising the floor is a breaking change. It happens
  only as a deliberate decision — a major version bump and a rewrite of this ADR". **Does that cover the
  Wrappers' MudBlazor floor?**
  - If it does, (B) is that deliberate decision, made without a major version bump, and ADR-0022 gains a
    dated note beside ADR-0205's rewrite saying why a 0.x line may raise it: no version without a suffix
    has been published, so the raise reaches only `0.1.0-beta.N` Consumers, whom ADR-0042 already tells
    that "a beta may break it between numbers"; SemVer promises nothing across 0.x; and ADR-0022's own
    correction of 2026-09-25 raised its runtime floor under a published prerelease. The Change Notes,
    as *breaks*, tell the Consumer.
  - If it does not — ADR-0022 sets the runtime floor, and the Wrappers borrow only its shape, the lowest
    version resolved upward — the three Wrapper projects' comments and the ExSheet Wrapper's tests'
    cite ADR-0205 for the floor instead of ADR-0022.

What it costs: a Consumer on MudBlazor 9.0–9.5 takes a minor upgrade within 9 to use the Wrappers.
MudBlazor 9.6.0 was published on 2026-06-27 (UTC, nuget.org); the demo pages, the Docs Site and every
CI run are on 9.9.0, and no Wrapper had been tested on anything earlier until this record.

## 4. Recommendation: (B), and why

1. **Rather than be quietly wrong, say it cannot be done.** All three failures are the kind that look
   right on screen: a control with no role or name, a keyboard lost only when a focus request comes
   early, and a filter that will not apply and no longer says why. A floor at 9.6.0 makes restore say so
   (NU1605) instead of running a Wrapper that is wrong without showing it.
2. **An outcome never depends on timing.** Below 9.2.0 Field Settings keeps or loses the keyboard by when
   the focus request arrives. (A) can remove that too, by moving the Wrappers off MudBlazor's presenter;
   (B) removes it by refusing the versions that have it, and leaves no code of ours to keep it away.
3. **(A) copies MudBlazor's internals into the Wrappers**, stays partial at 9.0.0–9.3.0, and binds every
   later Wrapper change to two MudBlazor versions far apart and to every version between them (§2.4).
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

**(a) Step 4 gains a paragraph**, after the Server-host commands. Its commands are the suite's own
config and every project in it, like the rest of Step 4; nothing in it points at a record:

> **The Wrappers' MudBlazor floor** (ADR-0205). Once before the sign-off, the layer-3 tests that run
> under a Wrapper run against a DemoHost built with the MudBlazor every Wrapper declares as its floor,
> `F`, on both hosts, in Chrome and in Edge, and at 150 % where `chrome-150` runs some of them. Every other
> run stays on the version the demo pages pin. The demo pages are pinned for the run only, both hosts are
> built from clean, and the version each will load is read back before the run:
>
> ```sh
> sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\1F"|' dotnet/samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
> rm -rf dotnet/samples/ExGrid.DemoHost/bin dotnet/samples/ExGrid.DemoHost/obj/Debug \
>        dotnet/samples/ExGrid.DemoHost.Server/bin dotnet/samples/ExGrid.DemoHost.Server/obj/Debug
> nix develop -c dotnet build dotnet/samples/ExGrid.DemoHost
> nix develop -c dotnet build dotnet/samples/ExGrid.DemoHost.Server
> nix develop -c dotnet build dotnet/samples/ExGrid.DemoApi
> # both name F
> strings -el dotnet/samples/ExGrid.DemoHost/bin/Debug/net10.0/wwwroot/_framework/MudBlazor.*.wasm | grep -A1 '^FileVersion'
> strings -el dotnet/samples/ExGrid.DemoHost.Server/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'
>
> cd tests/browser
> G="[Mm]ud|is not the grid's \(KB-35|apply the condition \(KB-31, SRV-5\)|Escape, Escape straight after an Inner Popup|a menu taking the keyboard a round trip late|with and without a Wrapper|SH-46/DC-59: the Selection's outline|the palette reaches the pane|RI-3: every page opens|inspectors\.spec\.mjs|WR-7a|DC-58: a bold number that fits"
> # every project: chrome, msedge, and chrome-150's Wrapper tests
> npx playwright test --grep "$G" 2>&1 | tee ../../verification/<date>/mudblazor-floor.log
> EXGRID_HOSTING=server npx playwright test --grep "$G" 2>&1 \
>   | tee ../../verification/<date>/mudblazor-floor-server.log
> cd ../..
> git checkout -- dotnet/samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
> ```
>
> `G` is the tests that run under a Wrapper: those whose file, `describe` or title names MudBlazor or
> `mud` (the `?chrome=mud` variants among them), and, by name, those that open a page under a Wrapper
> without saying so. A test of the second kind joins `G` when it is written. On a machine without GNU
> `strings`, each `MudBlazor` file's version is read back another way; it is never assumed.

At this branch's tip, `G` lists 293 tests on `chrome`, the same 293 on `msedge`, and six on
`chrome-150` (`sheet-borders.spec.mjs:260`'s Selection outline, and five of `part-c.spec.mjs` and
`sheet-paper.spec.mjs`, all under the mud Chrome). The record's selection at `0c0ab368` was 282: `main`
has since added twelve tests under a Wrapper and renamed one, and `G`'s first term takes in all of them.

The clean build matters: on this branch, two WebAssembly runs made straight after a re-pin (to 9.4.0,
then to 9.9.0) failed tests that pass at that version, and passed once both hosts were rebuilt from
clean.

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
  `dotnet/samples/ExGrid.DemoPages/`. It would need ADR-0205 noted, since it narrows "once".

**This record does not discharge the step under either reading, at 9.6.0 or at any version.** It is a
bisection on Playwright's bundled Chromium 141, run as the one `chrome` project through a config that is
not in the repository:

- Step 4 asks for Chrome and Edge, and the suite's config says a bundled build "would be answering for
  the wrong one". `msedge` never ran.
- `chrome-150` never ran, so the Wrapper tests it runs at 1.5 scale were never run at any pin.
- The Copied Range test "with and without a Wrapper" cannot pass on Chromium 141 (§2.3). It has never
  passed at 9.6.0, so it is unverified at the floor, and a criterion that could not be verified is not
  passed.
- The 9.6.0 WebAssembly run failed ED-30 (`key-field.spec.mjs:341`, under the mud Chrome), which did not
  fail in 20 runs at 9.9.0, the demo pages' version. Under WR-10 as drafted, that run fails.

So the step still owes a run at the decided floor in installed Chrome and Edge, `chrome-150`'s Wrapper
tests included, on both hosts, by the commands of (a). And ED-30's failure is settled first, by its own
ticket (§7), or shown to fail at the demo pages' version so that WR-10's rule names it as that test's.
Until then no floor run counts.

**(c) A question the evidence adds: layer 2 across the range.** §2.4 fails at 9.3.0 to 9.5.0 and at
neither end. A floor run at one version, whichever, cannot see a failure of that shape, and CI's layer 2
runs at the floor only (§7). The record's §6 shows a cheaper check that can: each Wrapper's layer-2
suite with MudBlazor pinned in its test project to each published version of the declared range. Here
that took about five minutes a version for the three suites, an hour for the twelve 9.x. It sees
markup and component state, not focus or timing, so it adds to the floor run and does not replace it.
Proposed: Step 4's floor paragraph also runs that, and records it beside the floor run. It widens
ADR-0205's "the floor is run once", so it is the user's to decide, and ADR-0205 would say so.

## 7. Found while doing it, for the orchestrator

- **The Wrappers' layer-2 suites already run at the floor.** `ExGrid.MudBlazor.Tests`,
  `ExSheet.MudBlazor.Tests` and `ExPivot.MudBlazor.Tests` reference the Wrapper projects and nothing
  else that brings MudBlazor, so NuGet resolves the lowest version the range allows, as it did for the
  old `9.0.0` reference. The same holds for `dotnet/tests/ExGrid.PackageSmoke`. Only the DemoPages (and the
  Docs Site) pin a later 9.x.
- **bUnit cannot see §2.2's focus loss, but it can see §2.1's missing role and name, and it saw §2.4.**
  *(Corrected after the review: this said bUnit could see neither failure of §2.)* Which element holds
  focus, and when a focus request lands, are the browser's; the Operator's `role` and `aria-label` are
  markup on MudInput's presenter `<div tabindex="0">`, which layer 2 already renders at the floor
  (`MudFilterPanelTests` and `MudPopoverFocusTests` find `.mud-ex-grid-filter-operator`). A probe that read
  that `<div>` and the hidden `<input>` in bUnit, with MudBlazor pinned in the test project to each 9.x,
  gave the browser diagnostic's readings at every version: nothing at 9.0.0 to 9.3.0, the hidden input
  only at 9.4.0 and 9.5.0, the `<div>` from 9.6.0 to 9.11.0 (record §6). No layer-2 test asserts it, so
  the suite passes at 9.0.0 on every push. **Proposed**: a layer-2 test named for WR-2 / A11Y-19 and
  ADR-0205 that pins `role="combobox"` and the field's label as `aria-label` on the focusable element of
  every `MudSelect` a Wrapper puts in front of the keyboard: the filter panel's two operators and its
  Boolean value, Field Settings' and Value Field Settings' selects, Format Cells' two. Only the Operator
  was probed. The test fails at 9.0.0 today, so it goes in with the decision: under (B) with the floor's
  raise, where it holds the floor on every CI run; under (A) with the Wrappers' change.
- **The Copied Range test "with and without a Wrapper" cannot pass on Playwright's Chromium 141**, which
  this container and other agents' local runs use: no `clipboardchange` before Chrome 143 (ADR-0170).
  Every local run here reports it failing. A `test.skip` on a browser without `ClipboardChangeEvent`
  would say so instead, but the spec is not this branch's and the decision is not an agent's.
- **ED-30's third-composition test (`key-field.spec.mjs:341`, mud Chrome) is intermittent on
  WebAssembly**, at `0c0ab368`: 8 failures in 126 runs across six MudBlazor versions, all with the same
  reading (the editor's waiting request granted between one composition's end and the next one's start).
  A ticket for whoever owns ADR-0080's Keyboard Field: whether the race is the product's or the test's
  (its two DevTools commands sent at once), and the fix that makes the outcome deterministic. `main` has
  changed the grid's script and ExSheet since `0c0ab368` (#81, #86's "a scripted focus asks first"),
  and after this branch merged it the test still failed once in 30 runs at 9.8.0.
- **`dotnet/src/ExGrid.Data.Arrow/ExGrid.Data.Arrow.csproj` says the same thing NU1608 does not do.** Its
  comment says a Consumer who moves Apache.Arrow to 24 "is told so at restore (NU1608) instead of meeting
  a break at run time". NU1608 is a warning: restore succeeds, and the package runs on 24 unless the
  application treats the warning as an error. This branch corrected the same claim in its Change Notes
  and on the Installation page; the Arrow comment is `main`'s (ADR-0065), so it is named here and left.
- **The root `README.md` still says "Beta"** and `0.1.0-beta.N` with `--prerelease` in its Install and
  Status sections. ADR-0205 asks the package readmes to be worded for any version, and this branch
  did that; the root readme is not packed, and describes the repository as it is today. It will be
  false at the `0.1.0` tag (Y may change only `verification/` and `docs/releases/`), so it has to be
  reworded on the release branch before X, or on `main` before the cut.
- **The Docs Site's Installation and Packages pages** say "Every version so far is a prerelease,
  `0.1.0-beta.N`" and show `--prerelease` in `Examples/Setup/install.sh`. True today; ADR-0205's "the
  site describes the latest stable version" makes them false once `0.1.0` is published, so they
  belong to the same rewording.
