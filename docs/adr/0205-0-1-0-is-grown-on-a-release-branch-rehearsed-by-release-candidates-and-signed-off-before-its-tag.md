# 0.1.0 is grown on a release branch, rehearsed by release candidates, and signed off before its tag

*(Decided with the user on 2026-10-11. It rewrites
[ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)'s "How a
version is published" for stable versions, and amends
[ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)
(where a tag goes, what a stable version's notes carry) and
[ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)
(what a prerelease publishes).)*

A tag runs the `release.yml` of the commit it names. Today's refuses a version without a suffix, and
ADR-0042 lets that refusal come out only in the change that rewrites it at sign-off. So the commit that
was verified cannot be the one that is tagged: something must be added after the sign-off, and whatever
is added was not verified.

Signing off takes days of runs by hand — Windows, a real IME, the Citrix PC, macOS, Excel beside the
grid — while five or more branches are being built at once. Holding `main` still for those days would
stop all of them; letting `main` move would leave the sign-off describing a commit nobody tags.

The user asked whether a branch that is cut, grown with the fixes the sign-off needs, and then
released, is a poor process. It is the ordinary one — a release branch, with release candidates.

## The decision

### The release branch

- **When everything `0.1.0` needs has merged, `release/0.1` is cut from `main`.** From then on the branch
  takes fixes only. `main` goes on.
- **A fix is made on `main` first and taken to the branch**, with its Change Note, so that nothing the
  branch ships is missing from `main`. A fix that only the branch needs (its records, its notes) is made
  there.
- **The branch carries the stable path from the start**: this ADR's `release.yml`, ADR-0042 rewritten,
  and package readmes worded for any version (no "beta" in them, and no `--prerelease` they would have
  to drop).
- **Release candidates** — `v0.1.0-rc.1`, `rc.2`, … — are tagged on the branch. Each publishes to
  nuget.org as a prerelease, through the same workflow and the same checks a stable tag runs, so the
  stable path is rehearsed before it is used. A fix found during the sign-off makes the next candidate.
  *(2026-10-12, decided with the user: the rehearsal includes the stable version's release notes. A
  release candidate's notes carry the blocks a stable version's must (ADR-0190's note of this date),
  checked as a stable version's are, so the text the GitHub release will show is seen before the tag.)*
- **The sign-off is made against a commit of the branch, X.** Every step of the Definition of Done's §22
  runs at X; `results.md` names X.
- **The stable tag goes on Y, which is X plus only the records and the notes**: the
  `verification/<date>/` directory and `docs/releases/0.1.0.md`. The release workflow reads X from the
  notes' "Signed off" block and **refuses the tag if X..Y touches anything else**. A fix after X is a new
  X and a new sign-off of what it touches.
- **Afterwards the branch is merged into `main`** (one merge commit), so the records, the notes and any
  branch-only fix are on `main`. The Change Notes the cut gathered are deleted on `main` by that merge.

### What the release workflow does

- **It accepts a tag on `main` or on a `release/*` branch.** ADR-0042's "on `main`" becomes "on `main` or
  a release branch".
- **The refusal of an unsuffixed version is replaced by a structural check, not removed.** An unsuffixed
  tag is refused unless its notes have a "Signed off" block naming a `verification/<date>/results.md`
  that exists at the tag and the commit X it was made against, and X..tag touches only `verification/`
  and `docs/releases/`. The Definition of Done's gate stays enforced by the workflow; it now reads the
  sign-off instead of refusing until someone deletes a line.
- **An unsuffixed tag, and a release candidate, run CI's long run** (`long: true`): the soak on both
  browsers and both hosts and ST-1 at 10⁶ rows. If it fails, nothing is published. The records are still
  the Definition of Done's — a watched Step 4 and a local Step 5 (ADR-0041); the long run is the tag's
  own proof at the commit it publishes.
- **The GitHub release of an unsuffixed tag is a release, not a prerelease**, and carries no prerelease
  note.

### What a stable version's notes carry

Besides ADR-0190's headings and [ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)'s
"Known limitations", a version without a suffix carries:

- **Signed off**: the `results.md`, the commit X, and the toolchain, OS and browser versions used;
- **SHOULD criteria accepted failing**: each ID and its reason, or "None.";
- **Out of scope for this version**: the Definition of Done's §21 items accepted in writing as out of
  scope, each linked;
- **Supported**: the hosts (WebAssembly and Blazor Server), the browsers (Chrome and Edge, ADR-0017), the
  target framework and the packages.

The Docs Site's notes parser requires each of them of an unsuffixed version.

### The Wrappers' MudBlazor range

- **Each Wrapper declares MudBlazor `[9.0.0,10.0.0)`.** A Consumer on MudBlazor 10 is told at restore
  (NU1608), as `Apache.Arrow`'s `[23,24)` already tells (ADR-0065).
- **The floor is run once before the sign-off**: the Wrappers' layer-3 specs against a DemoHost pinned to
  MudBlazor 9.0.0, recorded under `verification/`. Every other run stays on the version the DemoPages
  pin.

### After `0.1.0`

- **Every version without a suffix passes all of §22, a patch included.** The Definition of Done's "a
  criterion that could not be verified is not passed" is not narrowed for patches.
  *(2026-10-11, decided with the user, [ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md):
  every automated step of §22 still runs on every such version. The rows a person checks by hand are run
  again only when the code each names has changed since its last record, and a question to Excel is
  asked when its feature is built, never at a release.)*
- **An urgent fix ships first as a release candidate** from `release/0.1` (`0.1.1-rc.1`), the same day,
  and the stable patch follows its sign-off. Patches come from the release branch; new features go on
  `main` toward the next minor version, whose prereleases are `0.2.0-beta.N`.
- **Once a stable version exists, a prerelease does not publish the Docs Site.** The site describes the
  latest stable version; a prerelease's notes are on its GitHub release, and reach the site with the next
  stable publish. The on-demand publish of `main` or a `lab/*` branch stays, under its banner (ADR-0110).

## Considered options

- **Rewrite first, then hold `main` still from the sign-off to the tag.** One commit line and no merge
  commit, but every other branch waits for days of runs by hand.
- **A release branch without candidates.** The stable path would run for the first time on the real tag.
- **Sign off, then one commit that rewrites ADR-0042 and `release.yml`, tagged.** The least machinery,
  but the stable path is first run in earnest, and only a reader checks that the last commit changed
  nothing else.
- **CI's long-run artifacts as the record**, saving the watched soak. It would change ADR-0041's rule
  that a record is written from a run someone watched.
- **Patches verified only where their diff reaches.** Faster, but it narrows "not verified is not
  passed" for exactly the versions most often installed without reading. *(2026-10-11: still rejected
  for the automated layers. For the rows checked by hand it is taken, with each row naming its code,
  ADR-0208.)*
- **A prerelease republishing the site, or a second site under `/next/`.** The first documents
  parameters a stable Consumer cannot install, which ADR-0110 refused for `main`; the second doubles the
  publishing for a need nobody has yet.

## Consequences

- ADR-0042 is rewritten on the release branch, in the change that adds this `release.yml`, with what it
  said kept: its refusal is replaced as above, not quietly dropped. The Definition of Done's Step 7
  clause on the refusal says the same.
- ADR-0190 is amended: a stable version's tag goes on its release branch's Y, and its notes carry the
  blocks above. ADR-0110: a prerelease publishes no site once a stable version exists.
- The package readmes are reworded for any version before `rc.1`. `check.sh`, PV-1 and the three Wrapper
  readmes state `[9.0.0,10.0.0)`.
- `docs/agents/numbering.md` gains nothing: a release branch takes no numbers.
