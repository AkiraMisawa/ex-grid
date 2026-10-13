# ADR-0205's MudBlazor floor, run and bisected *(2026-10-12, the Wrappers at `0c0ab368`)*

ADR-0205 gives each MudBlazor Wrapper — `ExGrid.MudBlazor`, `ExSheet.MudBlazor`, `ExPivot.MudBlazor` —
MudBlazor `[9.0.0,10.0.0)`, and asks for the Wrappers' layer-3 specs to be run once against a DemoHost
pinned to the floor, 9.0.0. This is that run, and the bisection that followed it.

**The run fails at 9.0.0 on both hosts. Bisected over every 9.x from 9.0.0 to 9.9.0, the lowest version
at which the selection passes is 9.6.0, on both hosts.** Every failure below 9.6.0 is one of two
MudBlazor behaviours, each ended by a MudBlazor change named below:

- **The filter panel's Operator has no role and no name** from 9.0.0 to 9.5.0: 14 tests on each host.
  Ended by MudBlazor [#13080](https://github.com/MudBlazor/MudBlazor/pull/13080) (9.6.0), after
  [#13077](https://github.com/MudBlazor/MudBlazor/pull/13077) (9.4.0) had put them on an element nobody
  focuses.
- **ExPivot's Field Settings… opens with the keyboard on `<body>`** at 9.0.0 and 9.1.0, whenever its focus
  request arrives before MudSelect's second render: on WebAssembly, and on Server when the circuit is
  quick. 2 tests on WebAssembly; the suite runs them at 150 ms on Server, where they pass. Ended by
  MudBlazor [#12837](https://github.com/MudBlazor/MudBlazor/pull/12837) (9.2.0).

Two other tests failed and are not the floor's: the Copied Range test "with and without a Wrapper" fails
at every version in this container, 9.9.0 included, because its Chromium has no `clipboardchange` (§3);
and ED-30's third-composition test under the mud Chrome fails now and then on WebAssembly, at 9.5.0,
9.6.0 and 9.8.0 alike, and follows no MudBlazor version (§4).

This record is a bisection on Playwright's bundled Chromium, run as the one `chrome` project: it says
where the floor run should pass, and is not that run. `msedge` and `chrome-150` never ran, and the Copied
Range test could not (the proposal's §6 says what the floor run is, and why this is not it).

The declared range is unchanged by this record. Whether the floor moves to 9.6.0, or the Wrappers change
to hold at 9.0.0, is the user's question: `docs/agents/proposals/mudblazor-range.md`.

*(Added after the review, the same day: the Wrappers' layer-2 suites, run with MudBlazor pinned to each
published 9.x, find a third MudBlazor behaviour that this layer-3 selection could not: from 9.3.0 to
9.5.0 a refused filter operand loses its words (§6).)*

*(Added after the decision, the same day: the user raised the floor to 9.6.0, and each Wrapper now takes
`[9.6.0,10.0.0)`. The layer-2 tests written for it, named for WR-10, fail below the floor and pass at every
published 9.x from it (§7). This record still does not discharge the floor run of the Definition of
Done's Step 4.)*

## The machine

| | |
|---|---|
| CPU | Intel(R) Xeon(R) Processor @ 2.10GHz, 4 vCPUs, a KVM guest |
| Memory | 15.7 GiB |
| OS | Ubuntu 24.04.5 LTS, Linux 6.18.44 |
| .NET | SDK 10.0.203 (nix-managed); Debug builds, as `dotnet run` makes them |
| Browser | Playwright's Chromium 141.0.7390.37 (build 1194) as the one `chrome` project, headed under `xvfb-run`; Playwright 1.62.1, Node 24.14.1 |

No Chrome or Edge is installed here, so every run used the untracked
`tests/ExGrid.Browser/playwright.container.local.mjs`, which runs the suite's own config with that
Chromium as `--project=chrome`. Other agents ran layer 3 on this machine between this record's runs,
never during one: every run held the layer-3 lock.

## How

**The code** is `0c0ab368` on `claude/mudblazor-range-floor`, whose three Wrapper projects reference
MudBlazor `[9.0.0,10.0.0)`. Nothing under `src/` or `tests/` was changed for any run.

**How MudBlazor was pinned.** `samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj` references MudBlazor
directly (9.9.0 is committed), and both DemoHosts reach MudBlazor through it and through the Wrappers.
The direct reference is the nearest, and NuGet takes it over the Wrappers' range, so the version written
there is the one both hosts load. For each version:

```sh
sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\1<version>"|' samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
rm -rf samples/ExGrid.DemoHost/bin samples/ExGrid.DemoHost/obj/Debug \
       samples/ExGrid.DemoHost.Server/bin samples/ExGrid.DemoHost.Server/obj/Debug
nix develop -c dotnet build samples/ExGrid.DemoHost
nix develop -c dotnet build samples/ExGrid.DemoHost.Server
nix develop -c dotnet build samples/ExGrid.DemoApi
# the version each host will load, read back from what was built
strings -el samples/ExGrid.DemoHost/bin/Debug/net10.0/wwwroot/_framework/MudBlazor.*.wasm | grep -A1 '^FileVersion'
strings -el samples/ExGrid.DemoHost.Server/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'
```

Both hosts' `bin` and `obj/Debug` go first because, on this branch, two WebAssembly runs made straight
after a re-pin without them (to 9.4.0, then to 9.9.0) failed tests that pass at that version, and passed
once both hosts were rebuilt from clean. Every pin but the first was built this way: the first, 9.0.0 for
the full runs, was the builder's, made before this step, and has no readback (below). `builds.log` holds
the readback of every other pin; each names the pinned version for both hosts. After the last run the
demo pages were put back with `git checkout`, at 9.9.0.

**The selection** is the layer-3 tests that run under a Wrapper: those whose file, `describe` or title
names MudBlazor or `mud` (the `?chrome=mud` variants among them), and those that open a page under a
Wrapper without saying so in their title. `selection.txt` holds the `--grep` and Playwright's `--list` of
it at this commit: 282 tests in 31 files.

**The commands**, one host at a time, holding the layer-3 lock, from `tests/ExGrid.Browser`:

```sh
G="$(sed -n 2p ../../verification/2026-10-12-linux-mudblazor-floor/selection.txt | sed 's/^# --grep //')"
# Server: the browser at the latency proxy on 5490, the host behind it on 6490, the demo API on 8490
EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5490 \
  flock /tmp/claude-0/exgrid-layer3.lock nix develop .#browser -c xvfb-run -a \
  npx playwright test -c playwright.container.local.mjs --project=chrome --grep "$G"
# WebAssembly: the host on 5491, the demo API on 8491
EXGRID_BASE_URL=http://localhost:5491 \
  flock /tmp/claude-0/exgrid-layer3.lock nix develop .#browser -c xvfb-run -a \
  npx playwright test -c playwright.container.local.mjs --project=chrome --grep "$G"
```

A targeted run adds spec locations after the `--grep` (`popovers.spec.mjs:399 mud-app.spec.mjs:355
pivot.spec.mjs:603 pivot.spec.mjs:787`); a repeat adds `--repeat-each=<n>`. The suite's config has
`retries: 0`, so every failure counted here is a first try.

**The diagnostic**, `zz-floor-focus.spec.mjs` (kept here), was copied into `tests/ExGrid.Browser` for each
targeted run and removed after it. It opens Field Settings… under the mud Chrome with every `focusin`,
`focusout` and change of the sort select's input `type` logged, and reads, at the filter panel's Operator,
which element has focus and which carries a `role` and an `aria-label`. Its first run, at 9.4.0 on
Server, imported Playwright's plain `test`, which does not wait for the circuit, and failed for that; it
used the suite's fixtures from then on.

**The bisection.** 9.0.0 failed on both hosts. The builder of this branch had run the failing tests at
9.5.0, 9.6.0 and 9.9.0 before it stopped, which put the boundary at 9.6.0; the full selection was then
run at 9.6.0 (it passes) and at 9.5.0 (it fails), on both hosts. Below a failing version, 9.1.0 to 9.4.0
were targeted runs only — one or two tests of each kind of failure, and the diagnostic — which shows that
each fails and where each kind starts and stops. 9.1.0 and 9.2.0 ran on WebAssembly only, where both kinds
show. 9.7.0 to 9.9.0 were not run in full here: 9.9.0 is what the demo pages pin, and CI runs the whole
suite at it on every push. Every 9.x from 9.0.0 to 9.9.0 restored; none was skipped. 9.10.0 (published
2026-09-13) and 9.11.0 (2026-09-27) are inside `[9.0.0,10.0.0)` and were not run in layer 3: they are
untested there. Layer 2 ran at both (§6).

## Results

### The full selection

| MudBlazor | Host | Passed | Failed | Skipped | The failures |
|---|---|---:|---:|---:|---|
| 9.0.0 | Server | 266 | 15 | 1 | 14 × Operator (§1), Copied Range (§3) |
| 9.0.0 | WebAssembly | 258 | 17 | 7 | 14 × Operator (§1), 2 × Field Settings (§2), Copied Range (§3) |
| 9.5.0 | Server | 266 | 15 | 1 | 14 × Operator (§1), Copied Range (§3) |
| 9.5.0 | WebAssembly | 260 | 15 | 7 | 14 × Operator (§1), Copied Range (§3) |
| **9.6.0** | **Server** | **280** | **1** | **1** | Copied Range (§3) |
| **9.6.0** | **WebAssembly** | **273** | **2** | **7** | Copied Range (§3); ED-30, intermittent (§4) |

The 14 Operator failures are the same 14 tests at 9.0.0 and 9.5.0, on both hosts. The skipped tests are
each host's own: on Server, DC-47's WebAssembly-only test; on WebAssembly, the six that need a round trip
(`declarations.spec.mjs:1031`, `edit-stands.spec.mjs:444/469/681/705/726`) and ticket 78's 150 ms
circuit test.

The 9.0.0 runs are the builder's, at the same commit, before the clean-build step: its pin was the first
from the committed 9.9.0, and its failures cannot be 9.9.0's (the Operator passes there). The clean
targeted run at 9.0.0 below fails the same tests.

### One test of each kind, with the diagnostic

`popovers.spec.mjs:399` (KB-31) and `mud-app.spec.mjs:355` (WR-7) stand for the Operator,
`pivot.spec.mjs:603` (PV-11) and `:787` (the cramped Field List) for Field Settings.

| MudBlazor | Host | Operator: 2 tests | Field Settings: 2 tests | The Operator's focused `<div>` | Its hidden `<input>` | Field Settings' focus, at 0 ms |
|---|---|---|---|---|---|---|
| 9.0.0 | Server | 2 failed | 2 passed | no role, no name | no role, no name | `<body>` |
| 9.0.0 | WebAssembly | 2 failed | 2 failed | no role, no name | no role, no name | `<body>` |
| 9.1.0 | WebAssembly | 2 failed | 2 failed | no role, no name | no role, no name | `<body>` |
| 9.2.0 | WebAssembly | 2 failed | 2 passed | no role, no name | no role, no name | `input[type=text]` |
| 9.3.0 | Server | 2 failed | 2 passed | no role, no name | no role, no name | `input[type=text]` |
| 9.3.0 | WebAssembly | 2 failed | 2 passed | no role, no name | no role, no name | `input[type=text]` |
| 9.4.0 | Server | 2 failed | 2 passed | — (the diagnostic's first run, above) | — | — |
| 9.4.0 | WebAssembly | 2 failed | 2 passed | no role, no name | `combobox`, "Operator" | `input[type=text][role=combobox]` |
| 9.9.0 | Server | 1 passed | 1 passed | `combobox`, "Operator" | `combobox`, "Operator" | `input[type=text][role=combobox]` |
| 9.9.0 | WebAssembly | 1 passed | 1 passed | `combobox`, "Operator" | `combobox`, "Operator" | `input[type=text][role=combobox]` |

At 9.9.0 the targeted run was `copied-range.spec.mjs:159 popovers.spec.mjs:399 pivot.spec.mjs:603`; the
Copied Range test failed on both hosts (§3).

## 1. The Operator has no role and no name until 9.6.0

**The tests.** KB-31, KB-32, KB-35, FN-21, SRV-5 and WR-7 under the mud Chrome reach the filter panel's
Operator by Tab and look for the focused element by `role="combobox"` and the name "Operator", or by
`getByRole('combobox', { name: 'Operator' })`. From 9.0.0 to 9.5.0 there is none, and each waits 60 s and
fails.

**What is on the page.** The Operator's `MudSelectItem`s give their text as child content
(`MudExGridFilterPanel.razor`), so MudSelect shows the chosen operator through MudInput's hidden-input
presenter: an `<input type="hidden">` and, beside it, a `<div tabindex="0">` holding the item's content,
which is what Tab and `FocusAsync` focus. The diagnostic reads both:

- **9.0.0 to 9.3.0**: neither has a role or a name. MudSelect sets no `role` at all.
- **9.4.0 and 9.5.0**: the hidden input has `role="combobox"` and `aria-label="Operator"`; the focused
  `<div>` has neither. [MudBlazor#13077](https://github.com/MudBlazor/MudBlazor/pull/13077), "MudSelect:
  add combobox/listbox ARIA wiring and correct option selection state" (in 9.4.0), added
  `GetInputUserAttributes`, which puts `role`, `aria-label` (from `Label`), `aria-expanded`,
  `aria-controls` and `aria-haspopup` on the MudInput's `<input>`. On this path that input is the hidden
  one, which nothing focuses and no screen reader reads.
- **9.6.0 on**: the focused `<div>` has `role="combobox"` and `aria-label="Operator"`.
  [MudBlazor#13080](https://github.com/MudBlazor/MudBlazor/pull/13080), "MudSelect: Mirror combobox
  semantics onto the hidden-input presenter" (in 9.6.0), added `GetDisplayUserAttributes` to MudInput,
  which copies `role` and every `aria-*` attribute onto the presenter `<div>`.

Nothing else differs. With the tests pointed at the Operator by its class instead of its role, every
Operator test passes at 9.0.0 on WebAssembly (the builder's diagnostic runs, 2026-10-12T02:08Z and
02:22Z, not kept): Tab, Escape, Enter, the Inner Popup's report and the dialog geometry hold. What fails
is the semantics. On screen nothing is wrong; to a screen reader the field is a focusable "=" with no
role and no name.

## 2. Field Settings opens with the keyboard on `<body>` until 9.2.0

**The tests.** PV-11's caption test and the cramped Field List test open ExPivot's Field Settings… under
the mud Chrome and expect DOM focus inside the panel. At 9.0.0 and 9.1.0 on WebAssembly it is on
`<body>`, and both fail. The suite runs them on Server at a 150 ms round trip, and there they pass at
every version.

**Why.** The panel's sort select (`MudPivotFieldSettings.razor`) has a `ToStringFunc` and child content
in its items, and takes DOM focus through `MudSelect.FocusAsync()` when the panel's `FocusRequest` comes.

- Before 9.2.0 an item's child content outranks `ToStringFunc`: `CanRenderValue` is true whenever the
  chosen item has child content, and the select uses the hidden-input presenter of §1.
- MudSelect cannot show a value from child content before its items have registered. Its first render
  shows a text `<input>`; its `OnAfterRenderAsync(firstRender)` awaits its key interceptor's subscription,
  a JS call, and then asks for a second render, which switches to the presenter. `MudInput.FocusAsync()`
  focuses whichever element the current render shows.
- When the focus request comes between the two renders, the text input takes focus, the second render
  turns it into `type="hidden"`, and the browser drops focus to `<body>`. The diagnostic's trace at 9.0.0
  on WebAssembly (`targeted-9.0.0-wasm.log`; 9.1.0 is the same):

  ```
  288ms focusin INPUT[type=text].mud-input-slot.mud-input-root
  390ms input type text -> hidden (focused: true)
  405ms focusout INPUT[type=hidden].mud-input-slot.mud-input-root
  ```

  and on Server with no round trip the same three lines, within 30 ms (`targeted-9.0.0-server.log`). At
  150 ms the request reaches the browser after the second render, the `<div>` takes it, and PV-11 passes
  on Server: an outcome that depends on timing.
- [MudBlazor#12837](https://github.com/MudBlazor/MudBlazor/pull/12837), "MudSelect: Give ToStringFunc
  Higher Precedence" (in 9.2.0), makes `CanRenderValue` false whenever `ToStringFunc` gives a non-empty
  text. The select keeps its text input through both renders, and focus stays on it: from 9.2.0 the
  diagnostic finds `input[type=text]` focused, with `role="combobox"` from 9.4.0.

## 3. The Copied Range test is this container's, not the floor's

`copied-range.spec.mjs:159`, "on a Sheet the dashes are its outline's colour, with and without a
Wrapper", fails at every version run here, 9.9.0 included: no `.ex-copied-range` appears after Ctrl+C.
Playwright's Chromium 141 has no `ClipboardChangeEvent` (`typeof ClipboardChangeEvent` is `"undefined"`
on a page served from localhost, a secure context); Chrome shipped `clipboardchange` in 143, and ADR-0170
draws no Copied Range in a browser without it. CI's Chrome and Edge have it. The test is judged by CI's
run at the demo pages' version.

## 4. ED-30's third composition, intermittent

`key-field.spec.mjs:341` under the mud Chrome, "ED-30 / ADR-0080: with the editor's request already
waiting, the key that ends the second composition and starts a third leaves DOM focus in the Keyboard
Field until the third ends …", failed once in the 9.6.0 WebAssembly run: where the Keyboard Field should
hold `かな感じｄ`, drawn and focused, it was empty, not drawn and not focused — the editor's waiting
request had been granted between the second composition's end and the third's start. It passed in every
other full run. Repeated alone on WebAssembly, under the mud Chrome:

| MudBlazor | Runs | Failed |
|---|---:|---:|
| 9.0.0 | 15 | 0 |
| 9.5.0 | 15 | 2 |
| 9.6.0 | 5, then 8, then 20 | 1, then 0, then 0 |
| 9.7.0 | 20 | 0 |
| 9.8.0 | 20 | 4 |
| 9.9.0 | 20 | 0 |
| 9.8.0, after the merge of `main` (`4f66422c`) | 30 | 1 |

Every failure has the same reading, at line 360. The last row is the branch after it merged `main`,
which had changed the grid's script and ExSheet since `0c0ab368` (#81; #86, "a scripted focus asks
first"): the test still fails now and then there. Under the built-in Chrome the test passed 8 of 8 at
9.6.0 (`ed30-9.6.0-both-chromes-x8-wasm.log`, where the mud Chrome also passed 8 of 8). It fails at
9.5.0, 9.6.0 and 9.8.0 and was not seen to fail in 15 or 20 runs at 9.0.0, 9.7.0 and 9.9.0, so it follows
no MudBlazor version: a floor of 9.6.0 neither brings it nor removes it, and it is not one of the floor's
failures. Under the mud Chrome the Cell Editor is a bare `<input>` (`MudCellEditor.razor`), no MudBlazor
control. The test sends the end of one composition and the start of the next as two DevTools commands at
once, and the page's answer to the first — on WebAssembly, the Cell Editor's whole opening and its
request for the keyboard — races the second. This record does not find the cause. An outcome that
depends on timing is a defect wherever it is, in the product or in its test, and it is reported to the
orchestrator.

## 5. Could the Wrappers hold at 9.0.0 by a change of their own?

Yes, in both cases, at a cost. Not made here.

- **The Operator** (and the second condition's operator, and the Boolean value select): render the items
  as text through `ToStringFunc`, with no child content, so MudSelect takes its text-`<input>` path at
  every 9.x, and pass `role="combobox"`, the field's `aria-label`, `aria-haspopup` and `aria-expanded` as
  attributes, which MudSelect forwards to that input (MudInput splats them onto it at every 9.x). Below
  9.4.0 nothing else would name it; from 9.4.0 MudBlazor adds the same, and the Wrapper's take precedence.
  It stays partial below 9.4.0: no `aria-controls` or `aria-activedescendant`, because the list's id is
  MudBlazor's own and is not rendered before 9.4.0.
- **Field Settings** (and Value Field Settings' two selects): drop the items' child content; they already
  have a `ToStringFunc`, so the text path is taken before 9.2.0 too, and the second render no longer
  changes the focused element.

The cost — a copy of MudBlazor's ARIA wiring kept in step by hand, and every later Wrapper change held at
9.0.0 as well as at 9.9.0 — is set against raising the floor in the proposal.

## 6. Layer 2 at every 9.x *(added after the review, the same day)*

The Wrappers' layer-2 suites take the lowest version the range allows, 9.0.0, so CI runs them there and
nowhere else. Here each was run with MudBlazor pinned in its test project to every published 9.x, 9.0.0 to
9.11.0, at the branch's tip after its merge of `main` (`be20e74e`). A `<PackageReference
Include="MudBlazor" Version="<v>" />` added to the test project is the nearest reference, as the demo
pages' is for the hosts. The version each `ExGrid.MudBlazor.Tests` run loaded was read back by its probe,
whose output is named for it; for the other two, the last run's `MudBlazor.dll` was read back as
9.11.0.0. Each project was put back after its last run. `layer2-sweep.log` holds every summary and every
failure; `zz-layer2-probes.cs` holds the probes, which assert nothing and were removed after.

| MudBlazor | `ExGrid.MudBlazor.Tests` (235) | `ExSheet.MudBlazor.Tests` (62) | `ExPivot.MudBlazor.Tests` (76) | The Operator's focusable `<div>` | Its hidden `<input>` |
|---|---|---|---|---|---|
| 9.0.0, 9.1.0, 9.2.0 | pass | pass | pass | no role, no name | no role, no name |
| 9.3.0 | **7 fail** | pass | pass | no role, no name | no role, no name |
| 9.4.0, 9.5.0 | **7 fail** | pass | pass | no role, no name | `combobox`, "Operator" |
| 9.6.0 to 9.11.0 | pass | pass | pass | `combobox`, "Operator" | `combobox`, "Operator" |

**The Operator.** The probe reads, in bUnit, what the browser diagnostic of §1 read at each version both
ran, and 9.10.0 and 9.11.0 read as 9.6.0 does. Layer 2 renders the element; no layer-2 test asserts its
role or name, which is why the suite passes at 9.0.0. Field Settings' focus (§2) is the browser's, and
`ExPivot.MudBlazor.Tests` passes at every version.

**A refused operand loses its words, 9.3.0 to 9.5.0.** The seven failures are WR-2's and ADR-0023's
"refused by name" tests: the five cases of `MudNumberOperandTests.A_text_that_does_not_read_one_way_is_refused_by_name`,
its `The_chromes_own_word_for_a_refusal_stands`, and `MudDeclaredDateOperandTests`' `DateTimeOffset` case.
In each, the operand's text is its label twice ("ValueValue", the legend's and the label's) where the
refusal should follow. The operand probe's markup after "1.234" in de-DE: at 9.5.0 the field has no
`mud-input-error` class, `aria-invalid="false"`, and no helper text; at 9.6.0 it has all three, with the
refusal. The Apply probe at 9.5.0: Apply is disabled and nothing was applied. So the filter refuses, and
does not say why.

The operand is a `MudTextField` to which the Wrapper gives `Value`, `ValueChanged`, `Error` and
`ErrorText`. [MudBlazor#12892](https://github.com/MudBlazor/MudBlazor/pull/12892), "Inputs: Fix
validation on programmatic value change" (in 9.3.0), makes a touched input re-validate when its `Value`
parameter changes, and that validation, with nothing of its own to refuse, sets `Error` back to false.
[MudBlazor#13312](https://github.com/MudBlazor/MudBlazor/pull/13312), "Inputs: Don't re-validate on the
`@bind-Value` echo" (in 9.6.0), skips it when the new `Value` is the input's own `ValueChanged` coming
back. Matched by reading the two diffs in MudBlazor's history, not by building MudBlazor at each commit.

Not seen in a browser: no layer-3 test types a refused operand under the mud Chrome, so neither the layer-3
selection above nor its bisection could see it. It lies inside `[9.0.0,10.0.0)` and above its floor: a floor
run at 9.0.0 would not see it either.

## 7. After the decision: the floor at 9.6.0, held in layer 2 *(added the same day, at `6a868089`)*

The user decided the proposal's option (B): each Wrapper declares `[9.6.0,10.0.0)`, so the Wrappers' layer-2
suites, which resolve the lowest version a range allows, now run at 9.6.0 on every push. Two kinds of test
were added for it, named for WR-10 and ADR-0205:

- **Every MudSelect a Wrapper draws names the element that takes its keyboard**: `role="combobox"` and the
  field's own label as `aria-label` on the one element of the field that Tab and `FocusAsync` reach. The
  filter panel's Operator (a text and a number column), its second condition and its Boolean value
  (`ExGrid.MudBlazor.Tests`); Field Settings' sort and Value Field Settings' two selects
  (`ExPivot.MudBlazor.Tests`); Format Cells' Horizontal and Underline (`ExSheet.MudBlazor.Tests`).
- **A refused operand keeps its words** (`ExGrid.MudBlazor.Tests`): typed in de-DE, `1.234`, then `1234`,
  then `1234.5`, a submit and a render; while refused the field carries `mud-input-error`,
  `aria-invalid="true"` and the refusal in its helper text, and Apply stays unavailable.

**Below the floor** (`layer2-guards-below-floor.log`). The three Wrappers were put back to `[9.0.0,10.0.0)`
for the run, so that each compiled against the pinned version, and MudBlazor was pinned in each test
project; only the new tests ran. Every touched project file was restored after.

| MudBlazor | Filter panel: the two operators | Filter panel: Boolean value | ExPivot's three selects | Format Cells' two | The refused operand |
|---|---|---|---|---|---|
| 9.0.0, 9.1.0, 9.2.0 | fail | fail | fail | fail | pass |
| 9.3.0 | fail | fail | fail | fail | **fail** |
| 9.4.0, 9.5.0 | fail | pass | pass | pass | **fail** |

The selects that give their text through `ToStringFunc`, or show no value yet, keep MudSelect's text
`<input>`, which MudBlazor names from 9.4.0 (#13077); the operators, whose items give their text as
child content, focus the hidden-input presenter's `<div>`, named from 9.6.0 (#13080). The operand test
fails where §6's seven tests did. The failing tests' markup is in the log, cut; the operand test's
assertion message was not kept.

**From the floor** (`layer2-range.log`). Step 4's layer-2 commands, as the apply list
(`docs/agents/proposals/mudblazor-range-floor-apply.md`, edit 5) writes them, run as written but for
`nix develop` given the worktree's flake. nuget.org listed 9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0 and 9.11.0.
All three suites passed at each, whole — `ExGrid.MudBlazor.Tests` 240, `ExSheet.MudBlazor.Tests` 64,
`ExPivot.MudBlazor.Tests` 79 — and each run read back the version it was pinned to. This is a check of
the commands and of the new tests, at a commit that is not X: it is not the record Step 4 asks for.

## Files

| File | What it holds |
|---|---|
| `selection.txt` | The `--grep` and Playwright's `--list` of the selection at `0c0ab368` |
| `layer3-<version>-<host>.log` | The full selection at 9.0.0, 9.5.0 and 9.6.0, trimmed to the header, the failures with their errors, and the summary |
| `targeted-<version>-<host>.log` | The diagnostic's output and the targeted tests at 9.0.0 to 9.4.0 and 9.9.0, trimmed |
| `ed30-<version>-x<n>-wasm.log` | ED-30 under the mud Chrome repeated on WebAssembly (`both-chromes`: under each Chrome; `merged`: after the merge of `main`) |
| `builds.log` | Each pin's build, with the MudBlazor version each host loads |
| `zz-floor-focus.spec.mjs` | The diagnostic, as run (copied into `tests/ExGrid.Browser`, removed after each run) |
| `layer2-sweep.log` | §6: the three Wrappers' layer-2 suites at every published 9.x, and the probes' readings |
| `zz-layer2-probes.cs` | §6's probes, as run (appended to two test classes of `tests/ExGrid.MudBlazor.Tests`, removed after) |
| `layer2-guards-below-floor.log` | §7: the WR-10 layer-2 tests at 9.0.0 to 9.5.0, with the Wrappers put back to `[9.0.0,10.0.0)` |
| `layer2-range.log` | §7: the three Wrappers' layer-2 suites at 9.6.0 to 9.11.0, by Step 4's commands |

The full logs, with every passing test's line, and the failing tests' traces were not kept; a trimmed
log keeps every failure's error and every count.
