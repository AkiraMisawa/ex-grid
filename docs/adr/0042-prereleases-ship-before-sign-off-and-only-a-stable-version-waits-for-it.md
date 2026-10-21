# Prereleases ship before sign-off; only a stable version waits for it

The Definition of Done says **"MUST — release is not possible while this fails"**, and its
Step 7 is the sign-off that makes a release possible. By 2026-09-24 every layer ran in CI
([ADR-0041](./0041-ci-runs-every-layer-and-layer-three-gates-on-linux.md)), and all three passed
on real Chrome and Edge. What still stood between the code and Step 7 were runs only a person
can make: Windows at fractional scaling, a real IME, and the filed `verification/` record. It is
also the moment somebody outside the repository can first try the grid: in their own
application, from a package, not from a clone.

The Definition of Done never said whether a published prerelease is a "release". Left unsaid,
either the first package waits for a sign-off that does not depend on it, or the first package
quietly redefines the word. Neither is acceptable, so this records the answer.

## Decision

**A version with a prerelease suffix may ship before sign-off. A version without one is a
release in the Definition of Done's sense, and waits for Step 7.**

- **The line is `0.1.0-beta.N`.** SemVer 2. `0.x` says the API is still moving. The public
  surface has not been reviewed yet (below), and a beta may break it between numbers. NuGet hides a
  suffixed version unless the Consumer asks for prereleases, so nobody gets one by accident.
  *(2026-10-11, decided with the user,
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md):
  a stable version is first rehearsed by **release candidates**, `0.1.0-rc.N`, tagged on its release
  branch. A candidate is a prerelease, and ships as one.)*
  *(2026-10-12, decided with the user, ADR-0205 and
  [ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md):
  a candidate runs every check a stable tag runs but the two that read a sign-off ("How a version is
  published", step 1). Its notes carry the blocks a version without a suffix must, its Signed off block
  in a pending form of its own that says the sign-off is being made against this candidate, and its
  GitHub release opens with a note of its own, not the prerelease note. A candidate is written exactly
  `-rc.N`, N from 1: any other suffix that begins with `rc` is refused.)*
- **What a beta promises.** Every layer passed in CI on the commit it was built from. That
  commit is on `main`. Its release notes name what the Definition of Done still owes:
  `docs/implementation-status.md` at that commit is the list. It promises nothing about the
  criteria that CI cannot run (ADR-0041's "what CI does not replace").
  *(2026-10-11, decided with the user, ADR-0205 and
  [ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md):
  a release candidate promises more. Its commit is on `main` or a release branch. CI's long run passed
  on it. No question to Excel is open in ExSheet's corpus at it. Every row the Definition of Done checks
  by hand holds there on a record whose code is unchanged.)*
  *(2026-10-12, decided with the user: a candidate is still not signed off. The sign-off is being made
  against it, as its notes' Signed off block says in its pending form, and the version without a suffix
  that follows is that candidate's commit plus the sign-off's records and its notes.)*
- **`ExGrid` and `ExGrid.MudBlazor` ship together, at one version.** The Wrapper depends on
  exactly the core version it was built with. The range is `[x]`, not `>= x`. So a Consumer who
  upgrades one without the other is told so at restore — NU1608 for a direct reference, NU1107
  where two dependencies disagree — and is never left, unwarned, with a Wrapper running against
  a core it never met (the first principle: say so rather than be quietly wrong). `ExGrid.Fluxor` and
  `ExSheet` do not exist yet and ship nothing.
  *(Widened 2026-10-03, decided with the user: **the whole family ships at that one version**, from
  the same tag. ExSheet, ExPivot and the data packages had been built, packed and checked beside
  ExGrid since 2026-09-27 and 2026-09-30, but kept out of the release (Definition of Done §2). The
  user asked for them to ship. See "The family ships together" below.)*
- **MIT.** The repository was public with no licence, which legally meant "all rights reserved".
  Nobody could have used a package. MIT is what the design systems a Wrapper sits on use, and
  asks only that the notice is kept. Each package states it as `PackageLicenseExpression`, and
  the text is `LICENSE` at the root.

### How a version is published

*(Rewritten 2026-10-12, decided with the user: the rewrite
[ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md) and
[ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md) asked for on
2026-10-11, with what the release workflow's check settled and the user confirmed on 2026-10-12. It
lands on `main` before `release/0.1` is cut, so that the release branch carries the stable path from its
cut. Before the rewrite, this note read: "ADR-0205 rewrites this for stable versions. `0.1.0` is grown on
`release/0.1` and rehearsed by release candidates; a tag may be on `main` or a release branch; the
refusal of an unsuffixed version is replaced by a check that the notes name a sign-off made at a commit
the tag differs from only by its records and notes. This section is rewritten on the release branch,
with what it said kept.")*

**What changed.** A tag may be on a release branch. A release candidate rehearses the stable path. Step 1
refused every version without a suffix until the sign-off; that refusal is replaced by a check of the
sign-off itself.

**Why.** A tag runs the `release.yml` of the commit it names. Step 1 said its refusal "comes out only in
the change that rewrites this ADR at sign-off". That made the verified commit and the tagged commit
necessarily different: something had to be added after the sign-off, and whatever was added was not
verified (ADR-0205). The refusal is therefore replaced, not dropped. The Definition of Done's gate stays
enforced by the workflow: it reads the sign-off, instead of refusing until somebody deletes a line.

What this section said before is quoted beside each change.

- **A tag is the only place a version is written.** `v0.1.0-beta.1` names `0.1.0-beta.1`. The
  projects carry `0.0.0-dev`, so a package packed by hand says plainly that it is not one.
- **Pushing the tag runs `.github/workflows/release.yml`.** What the version is decides what its release
  must carry. The checks are `tools/release/ExGrid.Release`; their tests, `tests/ExGrid.Release.Tests`,
  are part of layer 1. They run before CI's twenty minutes, and every refusal is said, not only the
  first.
  1. **Every tag is refused unless:**
     - its version is written as SemVer writes it, with no leading zero in its three numbers (NuGet
       would publish `v01.2.0` as `1.2.0`, which the tag does not name);
     - its commit is on `main` or on a `release/*` branch;
     - its Release Notes are cut (below).

     A suffix that begins with `rc` is a release candidate, written exactly `-rc.N` with N from 1. Any
     other spelling of one, as `-rc1`, `-RC.1` or `-rc.0`, is refused, rather than published as a plain
     prerelease without the checks a candidate rehearses.

     **A release candidate is refused besides** while either of ADR-0208's checks fails:
     - **The corpus.** Any of ExSheet's Excel cases (`tests/ExSheet.Engine.Tests/ExcelCases`) is
       `uncertain` or `documented` without an `oracleSkip` that says why `oracle.ps1` cannot ask it. An
       `oracleSkip` that says "ask by keys" is no such reason, since `oracle.ps1 -Keys` asks the case;
       nor is `engineDiffersByDecision`, since `oracle.ps1` asks that case too.
     - **The rows checked by hand.** For any row the Definition of Done checks by hand, at the tagged
       commit:
       - it has no record;
       - its latest record is not a pass, or does not name the commit the index says it was made at;
       - a path it covers changed since that commit;
       - the tag the record names is not `verified/` followed by the name of the record's directory under
         `verification/`;
       - that commit cannot be fetched through that tag, or the tag names another commit, so whether its
         paths changed cannot be checked;
       - a path it names no longer exists;
       - for WR-10, a record its entry names besides the floor record is refused as the floor record would
         be, its paths compared from its own commit; or a stable MudBlazor version within the range the
         Wrappers declare, listed on nuget.org and published before X's commit time by its registration index
         or given no date there, is not among the versions its records' layer-2 runs covered; or that index
         cannot be read *(2026-10-12, how the release workflow checks ADR-0208's notes of that date on
         WR-10's layer-2 runs)*.

       The rows and their paths are Step 4's table, "Rows checked by hand, and the code each covers",
       which indexes the Wrappers' MudBlazor floor run, WR-10, beside them. The records are indexed in
       `verification/by-hand.json`, which names the same paths. The workflow refuses a tag where the two
       differ.

     A candidate's notes carry a stable version's blocks, its Signed off block in its pending form, and
     step 2's build reads them by form (ADR-0190's notes of 2026-10-12). The workflow reads no sign-off
     on a candidate: it does not read the Signed off block, and it judges the candidate's rows checked by
     hand at its tagged commit.

     **A version without a suffix is refused besides** unless all of these hold:
     - its notes' Signed off block names one `verification/<date>/results.md`, and that file is at the
       tag;
     - the block names one commit, X, the one the sign-off was made against, written in full, forty hex
       digits, since an abbreviation can stop being unique;
     - X is the commit a release candidate of the same version was tagged on: the sign-off is made
       against that candidate;
     - that `results.md` names X, in full or by seven digits or more;
     - the tag is X, or a descendant of it that differs from X only under `verification/` and
       `docs/releases/`;
     - both of a candidate's checks pass, with the rows checked by hand judged at X, not at the tag.

     A fix after X is a new candidate, a new X, and a new sign-off of what it touches.

     *(Before this rewrite, this step read: "It refuses a tag whose commit is not on `main`, and a
     version without a prerelease suffix. The second refusal is the Definition of Done's gate, enforced
     structurally. It comes out only in the change that rewrites this ADR at sign-off.")*
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
     *(2026-10-11, decided with the user, ADR-0205: for a version without a suffix and for a release
     candidate, with the long run, `long: true`: the soak on both browsers and both hosts, and ST-1 at
     10⁶ rows. If it fails, nothing is published. The records stay the Definition of Done's: a watched
     Step 4 and a local Step 5 (ADR-0041). The long run is the tag's own proof, at the commit it
     publishes.)*
  3. It publishes **the very files that job tested**, not a rebuild, to nuget.org, along with the
     symbol packages.
  4. It creates a GitHub prerelease for the tag, with the packages attached. If the tag came
     with a release of its own, which is what GitHub's release page makes, it completes that
     one instead. It marks it prerelease, attaches the packages, and puts this note above what
     the release already says. *(Added 2026-09-25: the first tag was made that way. The
     workflow refused the second release after the packages were already on nuget.org, and
     the release it left behind carried neither the packages nor the note.)*
     *(2026-10-11, decided with the user, ADR-0205: for a version without a suffix, the GitHub release
     is a release, not a prerelease, and carries no prerelease note. A release GitHub's page made is
     marked a release.)*
- *(Rewritten 2026-10-11, decided with the user with
  [ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md).)*
  **The release's text and the Docs Site.** Steps 1 and 4 above change, and a step follows them:
  - **Step 1 refuses more, before CI's twenty minutes**: a tagged commit with no
    `docs/releases/<version>.md`, or whose first line is not the version; one that still holds a Change
    Note under `docs/releases/unreleased/`; and one where an older version's file still reads
    `Published: pending`.
  - **Step 2 builds the Docs Site with the version it is about to publish**, so the site's own checks of
    the Release Notes run on the tag before anything is pushed.
  - **Step 4's text is the Release Notes**, passed with `--notes-file`, after the prerelease note for a
    prerelease. The prerelease note is held once, in `docs/releases/prerelease-note.md`. GitHub's
    generated list is no longer asked for. A release GitHub's page already made is given that text,
    replacing what it said, rather than having the note put above it.
    *(2026-10-12, decided with the user: a release candidate's text opens with a note of its own, the
    candidate's note, held once in `docs/releases/candidate-note.md`: what a candidate promises, and that
    the sign-off is being made against it. The prerelease note opens an alpha's or a beta's, and a
    version without a suffix opens with neither.)*
    *(2026-10-18, decided with the user: a GitHub release holds 125,000 characters of text, and
    `0.1.0-rc.1`'s Release Notes did not fit. Its packages reached nuget.org, and then GitHub refused the
    release, so the Docs Site was not published either. Notes that do not fit are shortened for the
    release only. They keep, in order, what opens them, the Highlights, Known limitations and a stable
    version's four blocks ([ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)),
    and as many of the other sections as fit. Each section left out keeps its heading and links the whole
    notes at the tagged commit. The notes in the repository and on the Docs Site stay whole.)*
  - **Then the release publishes the Docs Site** ([ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)),
    with the version, the tag and the UTC day the packages were pushed. The site states a version only
    once nuget.org has it.
    *(2026-10-11, decided with the user, ADR-0205; and 2026-10-12, decided with the user, for a stable
    version that a stable tag is above.)* **Once a stable version exists, a prerelease's release
    publishes no Docs Site, and neither does a stable version that a stable tag is above.**
    - A stable version exists once the repository has a tag without a suffix. A refused stable tag that
      is never deleted therefore counts, and the release's first job says so.
    - The site describes the latest stable version. A prerelease's notes are on its GitHub release, and
      reach the site with the next stable publish.
    - A patch to an older line, such as `0.1.1` from `release/0.1` after `0.2.0`, publishes none:
      versions are compared by SemVer precedence, so its site would replace a later version's.
    - The release's first job decides it, and says so. `docs.yml` refuses a call that comes anyway,
      and refuses too when it cannot read the repository's tags.
- **Trusted Publishing, not a stored key.** The publish job exchanges GitHub's OIDC token for a
  nuget.org key that lives an hour (`NuGet/login`). The only standing configuration:
  - a policy on nuget.org naming this repository and `release.yml`;
  - the nuget.org profile name, as the secret `NUGET_USER`.

  There is no long-lived key to leak or rotate. The job runs in a GitHub environment,
  `nuget`, so a required reviewer can be added in the repository's settings without touching
  the workflow.

### The family ships together *(added 2026-10-03)*

A tag publishes fourteen packages, all at its one version *(ten until 2026-10-19, when ExChart's three
joined the train: ADR-0283; thirteen until 2026-10-21, when ExBoard joined it: ADR-0340)*:

| Product | Packages |
|---|---|
| ExGrid | `ExGrid`, `ExGrid.MudBlazor` |
| ExSheet | `ExSheet.Engine`, `ExSheet`, `ExSheet.MudBlazor` |
| The Snapshot | `ExGrid.Data`, `ExGrid.Data.Arrow` |
| ExPivot | `ExPivot.Engine`, `ExPivot`, `ExPivot.MudBlazor` |
| ExChart | `ExChart.Engine`, `ExChart`, `ExChart.MudBlazor` |
| ExBoard | `ExBoard` |

- **One version, not one per product.** Every reference inside the family is exact (`[x]`), as the
  Wrapper's on the core is above. ExSheet and ExPivot draw with ExGrid and depend on exactly the
  ExGrid they were built and tested with. A version per product would let a Consumer combine an
  ExSheet with an ExGrid it never met, which is what the exact range exists to refuse.
- **What a beta promises does not change.** Every layer passed in CI on the commit, which runs every
  product's tests. The release note names `docs/implementation-status.md`, which lists what each
  product still owes.
- **A stable version still waits.** The refusal of an unsuffixed version stays. When ExGrid is
  signed off (Step 7), which of the other products a stable version carries is decided in the change
  that rewrites this ADR, against §27, §29 and §30. Those sections judge their own products, and
  still never gate ExGrid.
  *(2026-10-11, decided with the user: the first stable version is `0.1.0`, and it carries all ten
  packages ([ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)), so
  §27, §29 and §30 gate it beside ExGrid's sections. "The refusal of an unsuffixed version stays" now
  reads: a stable version still waits, and the workflow refuses it until its notes name its sign-off
  ("How a version is published", step 1; ADR-0205).)*
- **The package check packs all ten into one feed**, `.feed`, and the release publishes it as it is.
  Until 2026-10-03, ExSheet's and ExPivot's packages went into feeds of their own so that `.feed`
  held only ExGrid's two. The check now fails if `.feed` holds anything but the ten and their
  symbol packages.
- **Trusted Publishing covers the new package IDs.** The nuget.org policy names this repository and
  `release.yml`, not a package. The first push of each new ID creates it under the policy owner's
  account.

### What a package must prove before it ships

The DemoHost references the projects, so layer 3 never loads anything from a package. Static web
assets, the dependency ranges and the package metadata are exactly what a project reference
skips. So **`ci.yml` gains a `package` job, on every push and pull request:**

- It packs both packages: XML documentation, a symbol package with Source Link, and a
  deterministic build.
- It checks each `.nuspec`: the licence, the readme, the repository and commit, and the
  dependencies — `Microsoft.AspNetCore.Components.Web` at 8.0.0 for the core
  ([ADR-0022](./0022-packages-target-net10-and-run-on-everything-newer.md); 10.0.0 since ADR-0022 moved the target to `net10.0`, after
  `0.1.0-beta.1`), and the core at
  exactly this version for the Wrapper.
- A Consumer project that sits outside the solution, `tests/ExGrid.PackageSmoke`, restores both
  from the packed files alone. It builds with warnings as errors, against a page that uses the
  grid and the Wrapper's paper. Then it publishes, and the published output must hold
  `_content/ExGrid/ex-grid.css`, `_content/ExGrid/ex-grid.js` (the module path the component
  imports) and `_content/ExGrid.MudBlazor/mud-ex-grid.css`.

Behaviour is still layer 3's, on the DemoHost, which builds the same sources. The smoke test
proves only what packaging changes.

## What a stable version needs besides Step 7

*(2026-10-11, decided with the user: the first stable version is `0.1.0`, it carries all ten packages,
and it claims Blazor Server —
[ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md). What it needs of
the public surface below is decided in an ADR of its own.)*

- **A reviewed public surface.** Nothing has decided which C# types are the Consumer's contract.
  The core has 98 public types, and some of them, such as `ViewportGeometry` and `EdgeBand`, are
  public only so the test projects can reach them. A `0.x` beta may ship that. `1.0` may not.
  First an ADR states what is public. Then the rest becomes internal, reachable by the test
  projects and never by the Wrapper (ADR-0030: a Wrapper has only the seams every Consumer has).
  From then on a baseline file makes every change to the surface show up in review.
  *(Settled 2026-10-11 by
  [ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md),
  decided with the user. The count was stale: the core exports 193 public types and the ten packages
  524. "A `0.x` beta may ship that. `1.0` may not." was written when a stable version meant `1.0`; the
  first stable is `0.1.0` (ADR-0200), and it ships a surface reviewed type by type, with members
  reviewed before `1.0`.)*
- **Removing the prerelease-only refusal from `release.yml`**, in the same change that rewrites
  this ADR.
  *(2026-10-12, decided with the user: done in the rewrite of "How a version is published", which lands
  on `main` before `release/0.1` is cut, and not as a removal: the refusal is replaced by the check of
  the sign-off in its step 1 (ADR-0205).)*

## Rejected

- **No package until Step 7.** The runs it waits on — Windows, the IME, the filed record — do
  not change what a package contains. Meanwhile, nobody outside the repository could find what
  only a real application finds.
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
  *(2026-10-11, decided with the user, ADR-0205: still rejected before the sign-off. An unsuffixed
  `0.1.0` is now tagged once its notes name a sign-off the workflow can check, and on nothing else.)*
- **An API key in a repository secret.** It works, but it is a standing credential that has to
  be rotated. Trusted Publishing needs one policy and nothing to rotate.
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.
- *(Added 2026-10-11, decided with the user, ADR-0205.)* **Removing the refusal of an unsuffixed version
  at the sign-off**, as this ADR first said. The change that removed it could not be the commit that was
  verified.
- *(Added 2026-10-11, decided with the user, ADR-0205.)* **Trusting the notes' Signed off block without
  reading the repository.** A block naming a record that was never filed, or a commit the tag has moved
  on from, would publish a stable version nobody signed off. The workflow reads both.

## Consequences

- **The Definition of Done's "release" now has a scope.** It means a version without a
  prerelease suffix. §1 and Step 7 say so and point here.
- **Every public member carries an XML doc comment.** `GenerateDocumentationFile` is on for the
  shipped projects, and a warning is an error there, so an undocumented public member fails the
  build.
- **Each package carries a readme**, `src/<package>/README.md`: what it is, how to reference it,
  the stylesheet link, and a minimal page. The repository's `README.md` stays about the
  repository.
- **The first publish is a person's act.** Someone creates the nuget.org policy and the
  `NUGET_USER` secret once, then pushes the tag.
- *(Added 2026-10-11, decided with the user, ADR-0205.)* **A stable version's tag is the sign-off's
  commit plus its records and notes**, and the release workflow checks it. The Definition of Done's
  Step 7 clause on the refusal reads the same.
- *(Added 2026-10-12, decided with the user.)* **The release workflow's checks are code with tests.**
  They are `tools/release/ExGrid.Release`, and their tests, `tests/ExGrid.Release.Tests`, run in layer 1
  on every push. A tag is therefore not the first time they run.

*(2026-10-19, [ADR-0283](./0283-exchart-ships-on-the-familys-train-measured-and-checked-by-machine.md),
decided with the user: ExChart's `ExChart.Engine`, `ExChart` and `ExChart.MudBlazor` join this train
once they are in `src/`, making thirteen packages at the tag's one version. Until then the list above
stands.)*

*(2026-10-21, [ADR-0340](./0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md) "Shipping":
`ExBoard` joins the train, with exact references to the ExGrid, ExPivot and ExChart it was built with
and no Wrapper, making fourteen packages.)*
