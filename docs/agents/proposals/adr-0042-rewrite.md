# Proposal: ADR-0042 rewritten for the stable path

From `claude/release-workflow-checks`. ADR-0205 says ADR-0042 "is rewritten on the release branch, in
the change that adds this `release.yml`, with what it said kept". The branch adds that `release.yml`, so
this is the rewrite. A background agent does not edit an ADR, so the text is here for the orchestrator
to commit.

## How to apply it

Replace the body of `docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md`
with everything under "The rewritten ADR" below. The title and the file name stay: a stable version
still waits for its sign-off, and only how the workflow knows it has one changed.

- **What is new** is marked by dated notes.
- **What it replaces** is quoted beside each change, as the "rationale was replaced once" model
  (ADR-0005) asks.
- **The paragraphs nothing changed** are left word for word.

**Where it lands.** ADR-0205 and the Definition of Done's Step 7 say the check is added "on the release
branch". This branch is cut from `main`, so the orchestrator decides between two courses:
- Land the rewrite and this `release.yml` on `main` before `release/0.1` is cut, so that the cut carries
  them. The Step 7 clause and ADR-0205's Consequences then need a word saying it went in on `main`
  first.
- Hold both for the release branch.

A prerelease from `main` behaves as before, except for one change: once a version without a suffix is
tagged, its release publishes no Docs Site. Nor does a stable version some stable tag is above (reading
3).

## Readings this rewrite writes down, for the orchestrator to confirm

These are details the check had to decide that ADR-0205 and ADR-0208 leave open. Each is written into
the text below, so it becomes the decision once the orchestrator commits it.

1. **X is written in full, forty hex digits**, in the "Signed off" block. An abbreviation can stop being
   unique as the repository grows, and a word made of the letters a to f can read as one.
2. **`results.md` names X**, by its full name or a prefix of seven digits or more, as the Windows runs'
   reports write "Verified commit: `1655299`". ADR-0205 says it does; the check holds it, so a typo in
   the notes cannot point the structural check at another commit.
3. **A stable version exists once the repository has a tag without a suffix.** A stable tag that the
   workflow refused, and that was never deleted, therefore keeps prereleases from publishing the site.
   The admit job says so, so this is not quiet. The alternative is to read published GitHub releases,
   which puts a network answer into the decision.
   - **A stable version publishes the site only when no stable tag is above it** by SemVer precedence.
     ADR-0205 has patches come from the release branch while `main` moves to the next minor, so
     `v0.1.1` from `release/0.1` may come after `v0.2.0`. Published, its site would replace `0.2.0`'s
     and describe a version without `0.2.0`'s features, while nuget.org's latest is `0.2.0`. The admit
     job decides it and says so; `docs.yml` refuses such a call that comes anyway.
4. **A suffix that begins with `rc` is a release candidate only as `-rc.N`**, with N from 1. Any other
   spelling is refused, not published as a plain prerelease without the checks a candidate rehearses.
5. **No leading zero in the three numbers**: NuGet would publish `v01.2.0` as `1.2.0`, a version the
   tag does not name.
6. **Only `oracleSkip` is a written reason a case cannot be asked**, as ADR-0208 says, and not every
   `oracleSkip`.
   - **`engineDiffersByDecision` is not one.** The brief for this branch asked for it to count, and the
     first version of the check counted it. The case data shows the reading does not hold: `oracle.ps1`
     asks such a case, comparing Excel's answer with its `excelExpect`, or recording it when there is
     none. On `main` after #83 and #84, SORT-012 to 014 and UNIQUE-010 to 011 are `uncertain` with an
     `engineDiffersByDecision` that reads "refused with `#VALUE!` until Excel is asked";
     INDIRECT-036 to 038 with one that reads "R1C1 text in a form no run has observed"; INDIRECT-043
     with "`#VALUE!` until a Windows run observes it". Counted as a reason, these nine would have let
     `rc.1` and `0.1.0` through with those questions open, against ADR-0208 and ADR-0202's SH-7. Such
     a case is closed by a run of `oracle.ps1` and a person who settles what it recorded, then marks it
     `observed`.
   - **An `oracleSkip` that says "ask by keys" is not one either.** `oracle.ps1` blocks such a case
     through COM and asks it under `-Keys` (column-widths CW-030 to 032 on `main`), so it is inside
     ADR-0208's remedy. The check counts it open, matching the words as `oracle.ps1` does, ignoring
     case, and the refusal names `oracle.ps1 -Area <files> -Keys -Update` for it.
   - **If the user wants `engineDiffersByDecision` to count later**, it should still leave open a case
     with an `excelExpect` while its source is not `observed` (that `excelExpect` states Excel's answer
     without anyone having asked), and a case whose text says Excel is still to be asked.
7. **A release candidate's rows checked by hand are judged at the tagged commit.** It has no sign-off,
   so it has no X.
   - This refuses `rc.1` until every row holds there, so an eighteenth run, if one is needed, is made
     at the commit `rc.1` will be tagged on, before the tag.
   - ADR-0202's note of 2026-10-11 had the eighteenth run made "on `0.1.0-rc.1` itself". ADR-0208's
     correction reads either way.
   - If a candidate is to be published first and its rows run on it after, this half of the candidate's
     check has to report rather than refuse. That is the user's call.
8. **A record's commit must be in the repository the workflow checks out.**
   - The checkout fetches every branch and tag. A commit held by none of them is asked of the remote
     by its full name.
   - A record made on a branch that is later deleted, after a squash merge, is otherwise refused as
     "cannot be checked".
   - The seeded records' commits are held today by the verify branches
     `claude/exsheet-windows-verify-11`, `-15` and `-16`.
   - Whether a run should also tag its verified commit (for example `verified/<date>`) is open.

---

## The rewritten ADR

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
  branch. A candidate is a prerelease, and ships as one. It also runs every check a stable tag runs,
  except the two that read a sign-off (below).)*
- **What a beta promises.** Every layer passed in CI on the commit it was built from. That
  commit is on `main`. Its release notes name what the Definition of Done still owes:
  `docs/implementation-status.md` at that commit is the list. It promises nothing about the
  criteria that CI cannot run (ADR-0041's "what CI does not replace").
  *(2026-10-11: a release candidate promises more. Its commit is on a release branch. CI's long run
  passed on it. No question to Excel is open in ExSheet's corpus at it. Every row the Definition of
  Done checks by hand holds there on a record whose code is unchanged
  ([ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md)).
  It is still not signed off.)*
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

*(Rewritten 2026-10-11, decided with the user, [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)
and [ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md).)*

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

     A suffix that begins with `rc` is a release candidate, written `-rc.N` with N from 1. Any other
     spelling of one is refused, rather than published as a plain prerelease without the checks a
     candidate rehearses.

     **A release candidate is refused besides** while either of ADR-0208's checks fails:
     - **The corpus.** Any of ExSheet's Excel cases
       (`tests/ExSheet.Engine.Tests/ExcelCases`) is `uncertain` or `documented` without a written
       reason it cannot be asked, in `oracleSkip`. An `oracleSkip` that says "ask by keys" is no such
       reason, since `oracle.ps1 -Keys` asks the case; nor is `engineDiffersByDecision`, since
       `oracle.ps1` asks that case too.
     - **The rows checked by hand.** For any row the Definition of Done checks by hand, at the tagged
       commit:
       - it has no record;
       - its latest record is not a pass;
       - a path it covers changed since the commit that record was made at;
       - a path it names no longer exists.

       The rows and their paths are Step 4's table, "Rows checked by hand, and the code each covers".
       The records are indexed in `verification/by-hand.json`, which names the same paths. The workflow
       refuses a tag where the two differ.

     **A version without a suffix is refused besides** unless all of these hold:
     - its notes' "Signed off" block names a `verification/<date>/results.md` that is at the tag;
     - the block names the commit X the sign-off was made against, written in full, forty hex digits;
     - that `results.md` names X;
     - the tag is X, or a descendant of it that differs from X only under `verification/` and
       `docs/releases/`;
     - both of a candidate's checks pass, with the rows checked by hand judged at X, not at the tag.

     A fix after X is a new X, and a new sign-off of what it touches.

     *(Until 2026-10-11 this step read: "It refuses a tag whose commit is not on `main`, and a version
     without a prerelease suffix. The second refusal is the Definition of Done's gate, enforced
     structurally. It comes out only in the change that rewrites this ADR at sign-off.")*
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
     *(2026-10-11, ADR-0205: for a version without a suffix and for a release candidate, with the long
     run, `long: true`: the soak on both browsers and both hosts, and ST-1 at 10⁶ rows. If it fails,
     nothing is published. The records stay the Definition of Done's: a watched Step 4 and a local
     Step 5 (ADR-0041). The long run is the tag's own proof, at the commit it publishes.)*
  3. It publishes **the very files that job tested**, not a rebuild, to nuget.org, along with the
     symbol packages.
  4. It creates a GitHub prerelease for the tag, with the packages attached. If the tag came
     with a release of its own, which is what GitHub's release page makes, it completes that
     one instead. It marks it prerelease, attaches the packages, and puts this note above what
     the release already says. *(Added 2026-09-25: the first tag was made that way. The
     workflow refused the second release after the packages were already on nuget.org, and
     the release it left behind carried neither the packages nor the note.)*
     *(2026-10-11, ADR-0205: for a version without a suffix, the GitHub release is a release, not a
     prerelease, and carries no prerelease note. A release GitHub's page made is marked a release.)*
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
  - **Then the release publishes the Docs Site** ([ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)),
    with the version, the tag and the UTC day the packages were pushed. The site states a version only
    once nuget.org has it.
    *(2026-10-11, ADR-0205: **once a stable version exists, a prerelease's release publishes no Docs
    Site, and neither does a stable version that a stable tag is above.**
    - A stable version exists once the repository has a tag without a suffix.
    - The site describes the latest stable version. A prerelease's notes are on its GitHub release, and
      reach the site with the next stable publish.
    - A patch to an older line, such as `0.1.1` from `release/0.1` after `0.2.0`, publishes none:
      versions are compared by SemVer precedence, so its site would replace a later version's.
    - The release's first job decides it, and says so. `docs.yml` refuses a call that comes anyway,
      and refuses too when it cannot read the repository's tags.)*
- **Trusted Publishing, not a stored key.** The publish job exchanges GitHub's OIDC token for a
  nuget.org key that lives an hour (`NuGet/login`). The only standing configuration:
  - a policy on nuget.org naming this repository and `release.yml`;
  - the nuget.org profile name, as the secret `NUGET_USER`.

  There is no long-lived key to leak or rotate. The job runs in a GitHub environment,
  `nuget`, so a required reviewer can be added in the repository's settings without touching
  the workflow.

### The family ships together *(added 2026-10-03)*

A tag publishes ten packages, all at its one version:

| Product | Packages |
|---|---|
| ExGrid | `ExGrid`, `ExGrid.MudBlazor` |
| ExSheet | `ExSheet.Engine`, `ExSheet`, `ExSheet.MudBlazor` |
| The Snapshot | `ExGrid.Data`, `ExGrid.Data.Arrow` |
| ExPivot | `ExPivot.Engine`, `ExPivot`, `ExPivot.MudBlazor` |

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
  *(2026-10-11: the first stable version is `0.1.0`, and it carries all ten packages
  ([ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)), so §27, §29
  and §30 gate it beside ExGrid's sections. "The refusal of an unsuffixed version stays" now reads:
  a stable version still waits, and the workflow refuses it until its notes name its sign-off
  ("How a version is published", step 1).)*
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
  *(Done 2026-10-11, in this rewrite, and not as a removal: the refusal is replaced by the check of the
  sign-off in "How a version is published", step 1 (ADR-0205).)*

## Rejected

- **No package until Step 7.** The runs it waits on — Windows, the IME, the filed record — do
  not change what a package contains. Meanwhile, nobody outside the repository could find what
  only a real application finds.
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
  *(2026-10-11: still rejected before the sign-off. An unsuffixed `0.1.0` is now tagged once its notes
  name a sign-off the workflow can check, and on nothing else.)*
- **An API key in a repository secret.** It works, but it is a standing credential that has to
  be rotated. Trusted Publishing needs one policy and nothing to rotate.
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.
- *(Added 2026-10-11.)* **Removing the refusal of an unsuffixed version at the sign-off**, as this ADR
  first said. The change that removed it could not be the commit that was verified (ADR-0205).
- *(Added 2026-10-11.)* **Trusting the notes' Signed off block without reading the repository.** A
  block naming a record that was never filed, or a commit the tag has moved on from, would publish a
  stable version nobody signed off. The workflow reads both.

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
- *(Added 2026-10-11.)* **A stable version's tag is the sign-off's commit plus its records and notes**,
  and the release workflow checks it. The Definition of Done's Step 7 clause on the refusal reads the
  same (ADR-0205).
- *(Added 2026-10-11.)* **The release workflow's checks are code with tests.** They are
  `tools/release/ExGrid.Release`, and their tests, `tests/ExGrid.Release.Tests`, run in layer 1 on
  every push. A tag is therefore not the first time they run.
