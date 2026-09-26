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
- **What a beta promises.** Every layer passed in CI on the commit it was built from. That
  commit is on `main`. Its release notes name what the Definition of Done still owes:
  `docs/implementation-status.md` at that commit is the list. It promises nothing about the
  criteria that CI cannot run (ADR-0041's "what CI does not replace").
- **`ExGrid` and `ExGrid.MudBlazor` ship together, at one version.** The Wrapper depends on
  exactly the core version it was built with. The range is `[x]`, not `>= x`. So a Consumer who
  upgrades one without the other is told so at restore — NU1608 for a direct reference, NU1107
  where two dependencies disagree — and is never left, unwarned, with a Wrapper running against
  a core it never met (the first principle: say so rather than be quietly wrong). `ExGrid.Fluxor` and
  `ExSheet` do not exist yet and ship nothing.
- **MIT.** The repository was public with no licence, which legally meant "all rights reserved".
  Nobody could have used a package. MIT is what the design systems a Wrapper sits on use, and
  asks only that the notice is kept. Each package states it as `PackageLicenseExpression`, and
  the text is `LICENSE` at the root.

### How a version is published

- **A tag is the only place a version is written.** `v0.1.0-beta.1` names `0.1.0-beta.1`. The
  projects carry `0.0.0-dev`, so a package packed by hand says plainly that it is not one.
- **Pushing the tag runs `.github/workflows/release.yml`:**
  1. It refuses a tag whose commit is not on `main`, and a version without a prerelease suffix.
     The second refusal is the Definition of Done's gate, enforced structurally. It comes out
     only in the change that rewrites this ADR at sign-off.
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
  3. It publishes **the very files that job tested**, not a rebuild, to nuget.org, along with the
     symbol packages.
  4. It creates a GitHub prerelease for the tag, with the packages attached. If the tag came
     with a release of its own, which is what GitHub's release page makes, it completes that
     one instead. It marks it prerelease, attaches the packages, and puts this note above what
     the release already says. *(Added 2026-09-25: the first tag was made that way. The
     workflow refused the second release after the packages were already on nuget.org, and
     the release it left behind carried neither the packages nor the note.)*
- **Trusted Publishing, not a stored key.** The publish job exchanges GitHub's OIDC token for a
  nuget.org key that lives an hour (`NuGet/login`). The only standing configuration:
  - a policy on nuget.org naming this repository and `release.yml`;
  - the nuget.org profile name, as the secret `NUGET_USER`.

  There is no long-lived key to leak or rotate. The job runs in a GitHub environment,
  `nuget`, so a required reviewer can be added in the repository's settings without touching
  the workflow.

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

- **A reviewed public surface.** Nothing has decided which C# types are the Consumer's contract.
  The core has 98 public types, and some of them, such as `ViewportGeometry` and `EdgeBand`, are
  public only so the test projects can reach them. A `0.x` beta may ship that. `1.0` may not.
  First an ADR states what is public. Then the rest becomes internal, reachable by the test
  projects and never by the Wrapper (ADR-0030: a Wrapper has only the seams every Consumer has).
  From then on a baseline file makes every change to the surface show up in review.
- **Removing the prerelease-only refusal from `release.yml`**, in the same change that rewrites
  this ADR.

## Rejected

- **No package until Step 7.** The runs it waits on — Windows, the IME, the filed record — do
  not change what a package contains. Meanwhile, nobody outside the repository could find what
  only a real application finds.
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
- **An API key in a repository secret.** It works, but it is a standing credential that has to
  be rotated. Trusted Publishing needs one policy and nothing to rotate.
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.

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
