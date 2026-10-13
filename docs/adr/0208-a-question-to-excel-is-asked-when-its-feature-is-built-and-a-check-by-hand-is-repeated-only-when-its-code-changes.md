# A question to Excel is asked when its feature is built, and a check by hand is repeated only when its code changes

*(Decided with the user on 2026-10-11, while the seventeenth Windows run was handed over. It amends
[ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s
"Every version without a suffix passes all of §22" and its rejection of "patches verified only where
their diff reaches", for the rows checked by hand. It also amends
[ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)'s
note of the same day, which split the sign-off's Windows run.)*

The seventeenth Windows run takes about half a day of a person at a Windows desktop. ADR-0205 asks every
stable version to pass all of §22, a patch included. Read with the by-hand rows, that is half a day at
the desktop before every release, an urgent patch's too. A release would wait for someone's free half
day, and releases that wait are put off.

The user's view: half a day is acceptable this once. The run collects development's requirements
accurately, and that has to be done. But no release should require it.

What the run does by hand is two kinds of work, and they age differently:

- **Questions**, asked of Excel or of the platform: what Excel's `ERROR.TYPE` gives for `#SPILL!`, which
  letters the Home tab's KeyTips use, how wide Segoe UI's widest digit is. An answer does not change
  when our code does. Once recorded, it becomes a test: a corpus case `observed`, a constant and the test
  that holds it.
- **Checks**, of the build on a real desktop: VZ-14's Focus at a fractional scale, ED-30's real IME,
  SH-51's Ctrl+F1 reaching the page. A check's record is true of the code it ran on. It stays true for as
  long as that code is unchanged.

## The decision

### Questions are asked when the feature is built

- **A question for Excel or the platform belongs to the feature that needs the answer.** The ticket
  that builds the feature asks it, or the Windows run that follows that ticket does.
  - The answer is recorded under `verification/`, and pinned by a test.
  - A ticket does not close on "the next Windows run asks".
- **The release does not ask questions. It checks, mechanically, that none is open.** The release
  workflow refuses a version without a suffix while any corpus case is `uncertain` or `documented`,
  unless the case carries a written reason it cannot be asked (`oracleSkip`, as `undo` cannot go
  through COM).
  - The remedy is a run of `oracle.ps1 -Area` over the files that hold such cases, which takes minutes.
    The whole corpus is never put to Excel again.
- **The seventeenth run is the one exception**: the questions that piled up before this rule, asked
  together before `0.1.0`.

### Checks by hand are repeated only when the code they cover changes

- **Every row the Definition of Done verifies by hand names the code it covers**: the paths whose change
  could change its result. The rows are VZ-14, VZ-16's real zoom and real 225%, FN-12e's measurements on
  each platform, ED-30's real IME, SH-42's and SH-51 and SH-52's browser keys, KB-3 on macOS, VZ-18 on
  the remote-isolation PC, and §21.11's screen-reader check.
- **A row's record holds until a commit changes one of its paths.**
  - A release needs a new record only for the rows whose paths changed between the commit of their last
    record and the commit being signed off.
  - Only those rows are run again.
- **The release workflow checks it.** For each row it reads the last record's commit from
  `verification/` and compares the paths with the signed-off commit (`git diff --quiet`). It refuses
  the tag if any row's paths changed and no newer record exists.
- **The paths are drawn wide.** A list too narrow lets a change through unchecked, which is the
  failure this family refuses. Each row names whole files, and, where its behaviour crosses several,
  their directory:
  - the grid's script and stylesheet sources;
  - the keyboard, editor or toolbar files;
  - the measuring code;
  - the DemoHost's own page only where the row reads that page.

  When in doubt, the path goes in. The list is reviewed with the row, and in any change that moves
  the code it names.
- **Every automated layer still runs in full on every candidate and every stable tag**: layers 1 to 3 on
  both hosts and both browsers, the soak, the package check. Only the rows a person runs are judged by
  their code. "A criterion that could not be verified is not passed" holds: a row whose code is
  unchanged since its record was verified on that code.
  *(2026-10-12, decided with the user: **one automated run is an exception**: the Wrappers' MudBlazor
  floor run, with the layer-2 runs recorded beside it, is carried forward by its record, as
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s
  notes of that date under "The Wrappers' MudBlazor range" say.)*
  *(2026-10-12, decided with the user: a layer-2 run at a 9.x published after the floor record is a run
  of its own, and its record is carried forward too.)*

### For `0.1.0`

- **The seventeenth run's Part G is the record of ExGrid's by-hand rows**, made at its verified commit.
  It is not a dry run.
- **The eighteenth run is made only for the rows whose paths change between that commit and
  `0.1.0-rc.1`**, and runs only those rows. Its likely causes, among the work still to land:
  - ADR-0204's page that follows the Focus, and its scripted focus (ED-30, the screen-reader check);
  - ADR-0206's tints and pinned shade (VZ-16's lines).
- **If none of them changes, there is no eighteenth run.**
- *(2026-10-12, decided with the user.)* **The questions the seventeenth run's corrections raised are
  asked in one pass before the tag.** Correcting the engine by area added `uncertain` cases to features
  whose tickets were done (tickets 01, 02, 04, 07, 16, 18 and the arrays tickets). They are asked by one
  `oracle.ps1 -Area` pass over every corpus file the corrections touched, together with `PMT` at small
  rates and `NPV` (ADR-0202's note of this date), before `0.1.0-rc.1`. The tickets stay done: this rule's
  "a ticket does not close on 'the next run asks'" is kept by that pass being part of the release, which
  the release check enforces by refusing any `uncertain` case. Tickets 07 and 16, which the corrections
  reopened, close when the pass has answered them.

## Considered options

- **Every stable version runs every by-hand row, as ADR-0205 says.** It is the most certain, and it
  costs half a day of a person before every release, an urgent patch's included. Releases would wait.
- **A Windows runner on CI for the rows that can be automated.** The hosted runner has Chrome, Edge and
  Segoe UI, which would cover FN-12e and the browser keys. Whether it can set a display scale, and run a
  real IME, would need a measurement. Not taken now, decided with the user; it remains open for later.
- **Dropping the by-hand rows from the release gate.** A change to the IME path would then ship without
  a real IME having typed into it. That is the quiet failure this family refuses.
- **Patches verified only where their diff reaches, for every layer.** ADR-0205 rejected this, and the
  rejection stands for the automated layers. They are cheap, so narrowing them saves nothing. The rows
  a person runs are the only place where the saving is real.

## Consequences

- ADR-0205 carries a dated note: all of §22's automated steps run on every version without a suffix,
  and the by-hand rows follow this decision.
- ADR-0202's note of 2026-10-11 is corrected: the seventeenth run's Part G is a record, and the
  eighteenth is conditional.
- The Definition of Done's Step 4 gains a table, "Rows checked by hand, and the code each covers". It is
  written with the release workflow's check (ADR-0205's release mechanics). Step 7 accepts a by-hand row
  as passed on its latest record when its paths are unchanged since.
- `docs/specs/exsheet/verify-on-windows-17.md`'s Part G is reworded to match.
- Tickets that leave a question "for the next Windows run" are answered by the seventeenth run. From
  then on, such a question is part of building its ticket.
