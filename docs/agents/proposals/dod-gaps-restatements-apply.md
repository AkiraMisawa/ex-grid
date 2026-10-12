# The Definition of Done's gaps the audit of 2026-10-12 found: every ADR and Definition of Done edit

Branch `claude/dod-gaps-restatements`, 2026-10-12, from `origin/main` at `2265cda6`. The audit is
`docs/agents/dod-audit-2026-10-12.md` on `claude/dod-audit-2026-10-12`. A background agent edits no ADR and not
the Definition of Done, so every edit they need is here.

Each edit is an exact old text and its new text, in whole lines with the target's own indentation, generated
and checked by script against this branch's tip: every old text occurs once in its file, and the list applies
in order.

## What the user decided on 2026-10-12, and where each is written

1. **(a) PF-1 is restated to fit ADR-0204.** §22 Step 3's grep and the row allow exactly ADR-0204's
   page-follow line, `element.scrollIntoView({ block: 'nearest', inline: 'nearest' })` in `bringIntoWindow`,
   `src/ExGrid/Assets/ex-grid.js`, and nothing else. Any other `scrollIntoView`, `getBoundingClientRect`,
   `clientWidth` or `offsetWidth` still fails. Edits 1, 2 and 3.
2. **(b) WR-2 is restated to ticket 96**: a number operand is a `MudTextField` read in the current culture.
   Edit 5.
3. **(c) UX-1**: the three bare pixel values in `ex-grid.css` come from Grid Metrics, emitted inline on the
   instance root, and lie on Device Pixels. Code and tests on this branch; any ADR-0027/0028 edit is below.
4. **(g) VZ-18's check on the remote-isolation (Citrix) PC is a Known limitation of `0.1.0`.** It is not run,
   and the 2026-10-08 observation, which names no commit, is not taken as its record. Edits 6, 7, 8 and 9, and
   the Release Notes line below. The release check's side (`verification/by-hand.json` and the table of rows
   checked by hand) is `claude/release-workflow-checks`'.

Not decisions, the audit's:

- **(d) SRV-2**: three layer-3 tests skipped on the Server host without naming an exception; they run there
  now (tests only, on this branch).
- **(e) "twelve test projects"**: §22 Step 1's restatement note said twelve; edit 4 notes the count, in a note
  not credited to the user. §1's Evidence ("twelve in all") is `claude/release-workflow-checks`' list's edit 26,
  whose old text no edit here touches.

## The Release Notes' Known limitations line

`0.1.0`'s notes are cut at its release (ADR-0190, ADR-0205); no file holds them yet. Their Known limitations
carry this line, in the form ADR-0202 gives:

```md
- **ExGrid:** the repaint after Ctrl+↓, Ctrl+↑ or PageDown is not checked on a PC under Citrix with the browser's hardware acceleration off, or behind a remote browser isolation service: its browser test gates, the check on such a PC was not run for this version ([Getting started](getting-started)) (VZ-18, ADR-0202).
```

## Where this list meets other branches' lists

- **`claude/release-workflow-checks`** edits §1's Evidence (its edit 26), §22 Step 4's table (its edit 27, after
  the ADR-0208 note this list does not touch), ADR-0202 nowhere. **`claude/mudblazor-range-floor`** edits
  §22 Step 4's Discharges paragraph, §23 and PV-1. None of their old texts is touched here.

## The edits, in the order they apply

1. **PF-1: ADR-0204's page-follow line allowed.** Decision (a), whole row.
2. **§22 Step 3: PF-1's grep, and the check that the line is the one ADR-0204 placed.** Decision (a).
3. **§22 Step 3's restatement notes: a dated note.** Decision (a).
4. **§22 Step 1: the test projects' count.** The audit's (e), uncredited.
5. **WR-2: a number is a `MudTextField` read in the current culture.** Decision (b), whole row.
6. **VZ-18: the check on the Citrix PC is a Known limitation.** Decision (g), whole row.
7. **§21.11: VZ-18's reservation.** Decision (g): the run on that PC is not made at `0.1.0`'s sign-off.
8. **§21.11: "VZ-18 is checked as written".** Decision (g).
9. **ADR-0202: `0.1.0`'s Known limitations.** Decision (g).

### 1. docs/definition-of-done.md

Old:

````text
| **PF-1** | MUST | No per-cell JS interop, and no layout read on the path to a paint (ADR-0021 P4) | grep the sources under `src/` — the scripts' and stylesheets' sources in `Assets/`, the C# and the Razor; not `wwwroot`'s minified builds and source maps, nor `bin` or `obj` — for `getBoundingClientRect`, `clientWidth`, `offsetWidth`, `scrollIntoView`, comment lines left out (the command is in §22 Step 3); count interop calls per frame in Layer 3 via CDP | zero matches in the component — the command prints nothing; interop calls per scroll frame ≤ 1. *(Restated 2026-10-11, decided with the user: the grep read all of `src/`, which on a correct tree matches the source map of `ex-grid.min.js` (it carries the script's comments), three doc-comment lines in `ViewportBox.cs` and `ColumnGeometry.cs`, and a comment in `ex-grid.js`, each saying why the grid does not read layout. A use is a line of code, which the narrowed grep still finds; a comment after code on the same line is still printed, and read.)* |
````

New:

````text
| **PF-1** | MUST | No per-cell JS interop, and no layout read on the path to a paint (ADR-0021 P4) | grep the sources under `src/` — the scripts' and stylesheets' sources in `Assets/`, the C# and the Razor; not `wwwroot`'s minified builds and source maps, nor `bin` or `obj` — for `getBoundingClientRect`, `clientWidth`, `offsetWidth`, `scrollIntoView`, comment lines left out, and ADR-0204's one page-follow line allowed (the commands are in §22 Step 3); count interop calls per frame in Layer 3 via CDP | zero matches in the component but ADR-0204's page-follow line, `element.scrollIntoView({ block: 'nearest', inline: 'nearest' })` in `bringIntoWindow` in `src/ExGrid/Assets/ex-grid.js`, which is that file's only `scrollIntoView` — both commands print nothing; interop calls per scroll frame ≤ 1. *(Restated 2026-10-11, decided with the user: the grep read all of `src/`, which on a correct tree matches the source map of `ex-grid.min.js` (it carries the script's comments), three doc-comment lines in `ViewportBox.cs` and `ColumnGeometry.cs`, and a comment in `ex-grid.js`, each saying why the grid does not read layout. A use is a line of code, which the narrowed grep still finds; a comment after code on the same line is still printed, and read.)* *(2026-10-12, decided with the user: the grep allows exactly ADR-0204's page-follow line and nothing else. `scrollIntoView` there is no read: the browser places the element at the nearest edge and the grid measures nothing ([ADR-0204](adr/0204-the-page-follows-what-a-move-reveals-and-a-scripted-focus-asks-whether-the-keyboard-is-still-here.md); ADR-0021's "the grid never measures"). The grep printed that line on `main` from #86 on, so the row failed on a correct tree. Any other `scrollIntoView`, and any `getBoundingClientRect`, `clientWidth` or `offsetWidth`, still fails.)* |
````

### 2. docs/definition-of-done.md

Old:

````text
grep -rnE "getBoundingClientRect|clientWidth|offsetWidth|scrollIntoView" src/ \
  --exclude-dir=wwwroot --exclude-dir=bin --exclude-dir=obj \
  | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/\*|\*|@\*)'                             # PF-1: prints nothing
````

New:

````text
grep -rnE "getBoundingClientRect|clientWidth|offsetWidth|scrollIntoView" src/ \
  --exclude-dir=wwwroot --exclude-dir=bin --exclude-dir=obj \
  | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/\*|\*|@\*)' \
  | grep -vxE "src/ExGrid/Assets/ex-grid\.js:[0-9]+:[[:space:]]*element\.scrollIntoView\(\{ block: 'nearest', inline: 'nearest' \}\);"   # PF-1: prints nothing
awk '/^[[:space:]]*(\/\/|\/\*|\*)/{next} /^    const bringIntoWindow = /{f=1} /scrollIntoView/{all++; if(f)n++} f&&/^    };$/{f=0}
  END{if(n!=1||all!=1)print "PF-1: scrollIntoView is not ADR-0204'\''s one page-follow line in bringIntoWindow"}' \
  src/ExGrid/Assets/ex-grid.js                                                     # PF-1: prints nothing
````

### 3. docs/definition-of-done.md

Old:

````text
grep read `wwwroot`, and stopped at a digit, so `--ex-reference-1-pointed` came out cut; it reads the
source, digits included. KB-16's grep is new here, with its row. PF-2 read a "five-entry table";
ADR-0021's table has grown since, and the check names no count.)*
````

New:

````text
grep read `wwwroot`, and stopped at a digit, so `--ex-reference-1-pointed` came out cut; it reads the
source, digits included. KB-16's grep is new here, with its row. PF-2 read a "five-entry table";
ADR-0021's table has grown since, and the check names no count.)*
*(2026-10-12, decided with the user: PF-1's grep allows exactly ADR-0204's page-follow line,
`element.scrollIntoView({ block: 'nearest', inline: 'nearest' })` in `bringIntoWindow` in
`src/ExGrid/Assets/ex-grid.js`, and nothing else; the `awk` beside it prints unless that line is the
file's only `scrollIntoView` and sits in `bringIntoWindow`. Any other `scrollIntoView`,
`getBoundingClientRect`, `clientWidth` or `offsetWidth` still prints.)*
````

### 4. docs/definition-of-done.md

Old:

````text
`layer2.log`. `0.1.0` carries all ten packages (ADR-0200), so the run is the whole solution's — its
twelve test projects, every product's layers 1 and 2 and the demo API's tests — once, with each
project's results kept apart.)*
````

New:

````text
`layer2.log`. `0.1.0` carries all ten packages (ADR-0200), so the run is the whole solution's — its
twelve test projects, every product's layers 1 and 2 and the demo API's tests — once, with each
project's results kept apart.)* *(2026-10-12: "twelve" is every test project in `ExGrid.slnx`, thirteen
on `main` that day with `tests/ExGrid.Docs.Generator.Tests`, and fourteen with
`tests/ExGrid.Release.Tests`; the run takes them all, whatever their count.)*
````

### 5. docs/definition-of-done.md

Old:

````text
| **WR-2** | MUST | A condition's operator is a `MudSelect` offering exactly `Allowed`, in the core's order; its value is a `MudTextField` for text, a `MudNumericField` for a number, an editable `MudDatePicker` for a date — a `MudTextField` for a `DateTimeOffset` column *(amended 2026-10-02, ticket 98: a calendar's day has no offset, which such a column refuses, ADR-0023)* — and a `MudSelect` of true / false **with nothing chosen at first** for a boolean; Apply is unavailable until the operator's value is given (ADR-0009/0039) | Layer 2 per column type | the controls and the gating as stated |
````

New:

````text
| **WR-2** | MUST | A condition's operator is a `MudSelect` offering exactly `Allowed`, in the core's order; its value is a `MudTextField` for text, a `MudTextField` for a number, read in the current culture, an editable `MudDatePicker` for a date — a `MudTextField` for a `DateTimeOffset` column *(amended 2026-10-02, ticket 98: a calendar's day has no offset, which such a column refuses, ADR-0023)* *(2026-10-12, decided with the user: a number's control is a `MudTextField` read in the current culture, as ticket 96 made it. The row said a `MudNumericField`, which reopened `1234.5` as `1234,5` under de-DE and read it back as 12345; ticket 96 reads the text through `FilterPanelChoices.ReadOperand` in the culture the form shows numbers in, refusing by name a text that reads two ways, and both Chromes read it so. `MudFilterPanelTests` pins the `MudTextField`.)* — and a `MudSelect` of true / false **with nothing chosen at first** for a boolean; Apply is unavailable until the operator's value is given (ADR-0009/0039) | Layer 2 per column type | the controls and the gating as stated |
````

### 6. docs/definition-of-done.md

Old:

````text
| **VZ-18** | MUST | A reveal is repainted: in the frame after it the offset moves one pixel away from the edge it stands at, and in the frame after that it moves back; the core paints no slice for either move and reads neither; a scroll the user makes between them stands, and the grid paints where it stands. Under Citrix with the browser's hardware acceleration off, Ctrl+↓, Ctrl+↑ and PageDown paint the rows they reveal without a further scroll (ADR-0012, 2026-10-08; ADR-0021) | Layer 3 `virtualisation.spec.mjs` for the mechanism; by hand on the Citrix PC where the white Viewport was seen, from the Docs Site's blotter, since no layer-3 run reaches it (that PC also shows the site as a remote browser isolation mirror, so the check covers both layers until ADR-0012's run of 2026-10-09 separates them) | the scroll events after PageDown read T, T−1, T and the painted slice changes once; a scroll made at the second frame is where the scroller and the painted rows end; on the Citrix PC the last rows show at once |
````

New:

````text
| **VZ-18** | MUST | A reveal is repainted: in the frame after it the offset moves one pixel away from the edge it stands at, and in the frame after that it moves back; the core paints no slice for either move and reads neither; a scroll the user makes between them stands, and the grid paints where it stands. Under Citrix with the browser's hardware acceleration off, Ctrl+↓, Ctrl+↑ and PageDown paint the rows they reveal without a further scroll (ADR-0012, 2026-10-08; ADR-0021) | Layer 3 `virtualisation.spec.mjs` for the mechanism; by hand on the Citrix PC where the white Viewport was seen, from the Docs Site's blotter, since no layer-3 run reaches it (that PC also shows the site as a remote browser isolation mirror, so the check covers both layers until ADR-0012's run of 2026-10-09 separates them) | the scroll events after PageDown read T, T−1, T and the painted slice changes once; a scroll made at the second frame is where the scroller and the painted rows end; on the Citrix PC the last rows show at once *(2026-10-12, decided with the user: **the check on the remote-isolation (Citrix) PC is a Known limitation of `0.1.0`.** It is not run for `0.1.0`, and the observation of 2026-10-08, which names no commit, is not taken as its record. The layer-3 half, `virtualisation.spec.mjs`, still gates. ADR-0202's Known limitations and `0.1.0`'s Release Notes say so.)* |
````

### 7. docs/definition-of-done.md

Old:

````text
| **Which layer VZ-18's repaint answers** — the white Viewport was seen on a PC that, it turned out on 2026-10-09, showed the site through a remote browser isolation mirror, so whether Citrix's painting or the mirror needed the repaint is not known. The repaint stays, harmless elsewhere, and VZ-18 is checked as written, across both layers | **the localhost run on that PC** that `verification/2026-10-09-windows-remote-isolation/README.md` describes: the Docs Site served from the PC itself, the repaint off, Ctrl+↓ pressed ([ADR-0012](adr/0012-anchor-focus-and-keyboard-navigation.md), 2026-10-09). Made at the sign-off's visit to that PC if the PC can serve it (reserved 2026-10-11, decided with the user) |
````

New:

````text
| **Which layer VZ-18's repaint answers** — the white Viewport was seen on a PC that, it turned out on 2026-10-09, showed the site through a remote browser isolation mirror, so whether Citrix's painting or the mirror needed the repaint is not known. The repaint stays, harmless elsewhere, and VZ-18 is checked as written, across both layers | **the localhost run on that PC** that `verification/2026-10-09-windows-remote-isolation/README.md` describes: the Docs Site served from the PC itself, the repaint off, Ctrl+↓ pressed ([ADR-0012](adr/0012-anchor-focus-and-keyboard-navigation.md), 2026-10-09). Made at the sign-off's visit to that PC if the PC can serve it (reserved 2026-10-11, decided with the user) *(2026-10-12, decided with the user: VZ-18's check on that PC is a Known limitation of `0.1.0` and is not run for it, so neither is this run at `0.1.0`'s sign-off; the reservation stands for after it.)* |
````

### 8. docs/definition-of-done.md

Old:

````text
bundled undo stack, a richer announced contract for the Cell Editor, the year ▸ month ▸ day tree, a range of
columns through a Pointing Scope (SH-37 pins today's refusal and passes), ExGrid reading a Snapshot,
which layer VZ-18's repaint answers (VZ-18 is checked as written), Find All or Replace, dragging a
Reference Outline, or a column band.
````

New:

````text
bundled undo stack, a richer announced contract for the Cell Editor, the year ▸ month ▸ day tree, a range of
columns through a Pointing Scope (SH-37 pins today's refusal and passes), ExGrid reading a Snapshot,
which layer VZ-18's repaint answers (VZ-18 is checked as written), Find All or Replace, dragging a
Reference Outline, or a column band.
*(2026-10-12, decided with the user: VZ-18's check on the remote-isolation PC is a Known limitation of
`0.1.0`, not run for it; the row's layer-3 half still gates.)*
````

### 9. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

Old:

````text
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
````

New:

````text
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12, decided with the user: also ExGrid's VZ-18 on the remote-isolation (Citrix) PC. The
  repaint after a reveal is in, and its layer-3 test gates; the check on that PC is not run for `0.1.0`,
  and the observation of 2026-10-08, which names no commit, is not taken as its record.)*
````
