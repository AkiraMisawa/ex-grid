# The first stable version is 0.1.0, and it carries the whole family

*(Decided with the user on 2026-10-11, the first of the decisions the first stable version needs.
It amends [ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md),
which left the version and the products a stable version carries to ExGrid's sign-off, and the
Definition of Done's §2.)*

Two prereleases are published, `0.1.0-beta.1` and `0.1.0-beta.2`. ADR-0042 says that a version
without a suffix waits for the Definition of Done's Step 7, and that what such a version carries is
decided at sign-off. The user asked what is left before the first release, and decided its shape
first, so that the work toward it has one target.

## The decision

- **The first stable version is `0.1.0`.** No suffix, so it is a release in the Definition of Done's
  sense and waits for Step 7.
  - It stays `0.x`. Under SemVer a `0.x` minor version may break the API, and the Release Notes'
    "Breaking changes" says where it did
    ([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
    What `0.1.0` promises is that the Definition of Done was signed off on it, not that its API is
    frozen. ADR-0042's "`0.x` says the API is still moving" still holds.
  - Rejected: **`1.0.0`**, with or without release candidates first. It would also promise an API that
    does not break within `1.x`, which is a separate step the user chose not to take now.
- **It carries all ten packages, stable, at that one version**: `ExGrid`, `ExGrid.MudBlazor`,
  `ExSheet.Engine`, `ExSheet`, `ExSheet.MudBlazor`, `ExGrid.Data`, `ExGrid.Data.Arrow`,
  `ExPivot.Engine`, `ExPivot` and `ExPivot.MudBlazor`.
  - One tag still publishes the family at one version, and every reference inside it stays exact
    (ADR-0042, "The family ships together"). `release.yml` and the package check need no change of
    shape.
  - **So every product's own criteria now gate the tag.** The tag cannot publish a stable `ExSheet`
    that §27 has not judged, so §27 (ExSheet), §29 (ExPivot), §30 (the Snapshot's packages) and §23
    (the MudBlazor Wrapper) gate `0.1.0` beside ExGrid's sections. Each section said it would become
    its product's gate "when [the product] is proposed for a release of its own"; that is now.
  - Rejected: **ExGrid, its Wrapper and `ExGrid.Data` stable, the rest staying prereleases.** It would
    have kept the gate to ExGrid's sections, and needed a tag that publishes only some packages.
    Rejected too: **ExGrid and `ExGrid.Data` only**, which would leave every MudBlazor Consumer on a
    prerelease Wrapper.
- **`0.1.0` claims that ExGrid runs on Blazor Server.**
  - The claim is made by the ADR the Definition of Done's §24 asks for, written once every §24 MUST
    passes and before the tag. That ADR also rewrites
    [ADR-0017](./0017-target-chromium-browsers-only.md)'s premise that the grid runs on WebAssembly.
  - §24 gates `0.1.0`. Its SRV-2 lists the layer-3 tests that may skip on the Server host; the tests
    that skip there today without being on its list are settled before the claim, each run on the
    Server host or named on the list with its reason.
  - Rejected: **not claiming it**, which would have meant withdrawing the claims already made outside
    the Definition of Done — the repository's description, and `ExPivot`'s readme in the
    `0.1.0-beta.2` package.
- **§32's criteria on ExPivot's report Windows judge ExPivot, not ExGrid.** LV-24 to LV-31 and LV-34
  describe code in `ExPivot.Engine` and `ExPivot`. §2 had placed all of §32 in ExGrid's gate because
  "it is ExGrid code", which those rows are not. Since `0.1.0` carries ExPivot, they still gate the
  tag, through ExPivot's judgement.

## Consequences

- The Definition of Done's §2 says what gates `0.1.0`, and the preambles of §23, §24, §27, §29, §30
  and §32 point here. Each product section gains the preconditions §2 has for ExGrid, applied to its
  own packages, at sign-off.
- ADR-0042 carries a dated note pointing here. Its prerelease-only refusal comes out only in the change
  that rewrites it at sign-off, as it says.
- The work toward `0.1.0` now includes what §27, §29 and §30 still owe: ExSheet's open function tickets,
  ExPivot's run beside Excel on Windows, and every by-hand row in those sections. Which of ExSheet's
  functions must be in `0.1.0`, and how what is not yet supported is said, are decided next.
- What `0.1.0` needs of the public C# surface — ADR-0042's "a reviewed public surface" — is decided in
  an ADR of its own.
