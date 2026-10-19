# Release Notes are written with each change and published from the repository

*(Decided with the user on 2026-10-11, while restructuring the Docs Site after mudblazor.com and
ag-grid.com. It amends [ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)'s
"How a version is published" and [ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)'s
publishing.)*

Two versions have been published, `0.1.0-beta.1` and `0.1.0-beta.2`, and neither says well what it
holds.

- **Their text is GitHub's.** `release.yml` asks for `--generate-notes`, which lists the merged pull
  requests by title. beta.2's list mixes CI-only changes (#20, #26, #27, #55, #56) with the products.
  It never says that beta.2 moved the target from `net8.0` to `net10.0`
  ([ADR-0022](./0022-packages-target-net10-and-run-on-everything-newer.md)), which breaks a Consumer
  still on .NET 8.
- **beta.1's release carries neither its packages nor the ADR-0042 note.** The workflow refused to make
  a second release over the one GitHub's page had made (ADR-0042, 2026-09-25).
- **The text is not in the repository,** so the Docs Site cannot show it. ADR-0110 lets the site call no
  server.
- **The Definition of Done expects release notes that do not exist yet.** §1 asks a stable version's
  notes to name every SHOULD that fails, with its reason. ADR-0042 asks a beta's notes to name what the
  Definition of Done still owes.

ag-grid publishes a changelog, a "What's New" per version and an upgrade page per version that says
"There are no breaking changes" when there are none. MudBlazor lists its GitHub releases, fetched when
the page loads. Neither fits as it is: ag-grid's changelog is generated from a ticket system this
repository does not have, and MudBlazor's fetch is a server call.

## The decision

### What a version says

- **Each published version has its Release Notes, one file: `docs/releases/<version>.md`.** Its first
  line is `# <version>`, the file's name. Then header lines in the tickets' `Status:` style:
  - `Published: 2026-10-04`, the UTC day the release workflow pushed the packages to nuget.org, or
    `Published: pending` (below);
  - `Packages:` the package IDs it published;
  - `Target:` the target framework, `net10.0` today.
- **Then fixed headings, in this order, every one present.** A heading with nothing under it says
  "None.", as ag-grid's upgrade pages do, so that a reader can tell "nothing" from "not written".
  1. **Highlights** — at most five lines, each linking the Docs Site page that shows it.
  2. **Breaking changes** — what a Consumer must change. A `0.x` version may break the API between
     numbers (ADR-0042), so this comes first after the Highlights.
  3. **Behaviour changes** — the same call giving a different result: a value painted, refused or
     rounded differently. This family displays money, so a different result for the same code is
     written down even when no signature changed.
  4. **ExGrid**, **ExSheet**, **ExPivot**, **ExChart**, **Data** — the Added and Fixed lines per
     product, each with its pull request, ADR and criterion where it has them. A product with nothing
     new says "No change. Its packages are republished at this version." **ExChart** joined the family
     after versions were published (ADR-0280), so its heading stands only in a version whose
     `Packages:` line names one of its packages: "No change" under it, in a version that never
     published it, would be false of that version. *(Added 2026-10-19, when ExChart was registered.)*
- **A prerelease's notes begin with the prerelease note.** It is held once, in
  `docs/releases/prerelease-note.md`, with a `{ref}` where the commit goes, and says what ADR-0042
  promises of a beta: every layer passed in CI on this commit, the Definition of Done is not signed
  off, and what it still owes is `docs/implementation-status.md` at that commit. The release workflow
  and the Docs Site both read that file, so the two cannot say different things.
  *(2026-10-12, decided with the user: a release candidate's notes begin instead with a note of its
  own, the candidate's note, held once in `docs/releases/candidate-note.md` with a `{ref}` where the
  commit goes. It says what ADR-0042 and ADR-0205 promise of a candidate, and that the Definition of
  Done is being signed off against it. The release workflow and the Docs Site both read that file.)*
- *(2026-10-12, decided with the user.)* **A release candidate rehearses a stable version's notes.** An
  `-rc.N` version's notes carry the blocks a version without a suffix must carry, and the build checks
  them as it checks a stable version's, so ADR-0205's rehearsal covers the notes too. An `-alpha` or
  `-beta` version's notes are refused if they carry any of them.
  *(2026-10-12, decided with the user: one block is a candidate's own. Its sign-off is still being
  made, so its Signed off block takes a pending form, below, and the build reads a candidate's blocks
  by form. The release workflow reads a sign-off in full only on a stable tag.)*
- **A stable version's notes carry two more blocks**, which the build requires of a version without a
  suffix (Definition of Done §1 and Step 7):
  - **Signed off** — the `verification/<date>/results.md` the sign-off was made against;
  - **SHOULD criteria accepted failing** — each criterion's ID and the written reason, or "None."

  *(2026-10-11, decided with the user in
  [ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md) and
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md):
  a stable version's notes carry three blocks besides, Known limitations, Out of scope for this version
  and Supported, and its Signed off block names, besides the record, the commit X the sign-off was made
  against and the toolchain, OS and browser versions it ran with.)*

  *(2026-10-12, decided with the user.)* **Where the blocks stand, and the form of each**, which the
  build checks:
  - **Their order.** Known limitations stands after Behaviour changes. The other four stand after the
    products, in this order: Signed off, SHOULD criteria accepted failing, Out of scope for this
    version, Supported. A release's headings are therefore Highlights, Breaking changes, Behaviour
    changes, Known limitations, ExGrid, ExSheet, ExPivot, ExChart (as above), Data, Signed off, SHOULD criteria accepted
    failing, Out of scope for this version and Supported.
  - **Known limitations**: what the version does not do, or "None.". Each line begins with the products
    it reaches in bold, as a Breaking line does, names the limitation in a sentence, and links the page
    of the site that says it in full.
  - **Signed off, for a version without a suffix**: five lines, each beginning with its label in bold,
    in this order:
    - `Record:` the `verification/<date>/results.md` the sign-off was made against, linked at the
      version's own tag, since the record is committed after X and so is at the tag and not at X;
    - `Commit:` the commit X, its forty hex digits in a code span: the commit a release candidate of
      the version was tagged on, which the release workflow reads;
    - `Toolchain:`, `OS:` and `Browsers:`, the versions the sign-off ran with.

    The block names one record and one commit, a link's address included, so that the release workflow
    reads exactly those. It never takes the pending form.
  - **Signed off, for a release candidate: the pending form.** One line, `**Pending:**`, saying that the
    Definition of Done is being signed off against this candidate, and naming it by its tag in a code
    span, as `v0.1.0-rc.1`. It names no record and no commit: those are the release's, named once the
    sign-off is made. It is the only form a candidate's block takes.
  - **SHOULD criteria accepted failing**: each line begins with the criterion's ID in bold, or several
    separated by commas, and gives the written reason it was accepted; or "None.". The ID is a SHOULD of
    the Definition of Done: a MUST that fails stops the release, and an OBSERVATIONAL number never fails.
  - **Out of scope for this version**: each line names the Definition of Done's §21 item accepted in
    writing as out of the version's scope, as §21.11, and links where it was accepted; or "None.".
  - **Supported**: four lines, each beginning with its label in bold, in this order: `Hosts:`,
    `Browsers:`, `Target framework:` and `Packages:`. The target framework and the packages are those its
    `Target:` and `Packages:` header lines name.

### How they are written

- **A change a Consumer can see adds a Change Note**, in the same branch:
  `docs/releases/unreleased/<slug>.md`. Header lines:
  - `Product:` ExGrid, ExSheet, ExPivot, ExChart or Data;
  - `Kind:` Added, Fixed, Behaviour or Breaking;
  - `Docs:` the site's route and anchor that shows it, when one does;
  - `ADR:` the decision's number, when there is one.

  Then a blank line and one or two sentences. A Breaking or Behaviour note says what a Consumer
  changes, or will see.
  - **One file per change**, so two branches never edit the same line (AGENTS.md, "Working in
    parallel").
  - **Written when the change is made**, because that is when its author knows whether it breaks
    anything.
- **The cut.** Before a tag, one change named "Release notes for `<version>`":
  1. gathers the Change Notes into `docs/releases/<version>.md`, under the headings above;
  2. writes the Highlights;
  3. deletes the Change Notes;
  4. writes `Published: pending`;
  5. writes the previous version's date into its file, if it still says `pending`;
  6. moves every line of each `src/*/PublicAPI.Unshipped.txt` but its `#nullable enable` into that
     project's `PublicAPI.Shipped.txt` (ADR-0201, 2026-10-12). For a stable version this step is made
     before the commit the sign-off is made against, since the tag may differ from it only in the records
     and the notes (ADR-0205).

  The tag goes on the commit that merges it. A tag publishes all of `main` (ADR-0042), so its notes
  must hold all of `main`.

### The date

- **A version's date is the UTC day the release workflow pushed it to nuget.org.**
  - Its file says `pending` at the tag, since nobody can know the day before the push.
  - The workflow hands the day to the site it publishes, which therefore shows the date at once.
  - The next cut writes it into the file.
- **The two betas are dated from nuget.org**: `0.1.0-beta.1` on 2026-09-25 (pushed 00:07 UTC) and
  `0.1.0-beta.2` on 2026-10-04 (23:53 UTC). The tag commits (2026-09-24, 2026-10-03) and GitHub's
  timestamp for beta.1 (2026-09-24 23:53) give other days, and are not what a Consumer could install.

### Where they are published

- **On the Docs Site, at `/release-notes`**: every version, newest first, one anchor each
  (`#0-1-0-beta-2`). The page is generated at build time from the files, so nothing is fetched.
  - A version with a suffix carries a Prerelease chip; a stable one a Release chip.
  - Filter chips select a product, or only the breaking and behaviour changes.
  - A site not published from a tag also shows an **Unreleased** section, built from the Change Notes,
    under a warning that names the commit or branch it came from.
  - A Docs Site section that the newest version's notes link shows a chip, read from those links, so
    there is no second record of what is new:
    - **"New in `<version>`"** when a Highlight or an Added line links it;
    - **"Changed in `<version>`"** when only a Breaking, Behaviour or Fixed line does. "New in" wins when
      both link the same section.
  - On a site not published from a tag, a section a Change Note links shows **"Not on nuget.org yet"**,
    linking the Unreleased section. A reader of a `main` or `lab/*` site then knows that the section
    describes code no published version holds.

  *(Amended with the user on 2026-10-11, while reviewing the Docs Site's change. The decision first
  named only the "New in" chip. The site was built with the two others, because a section that the
  newest notes link for a Behaviour or Breaking line is not new, and calling it new would be the site
  quietly wrong about the product. The user chose to keep them and record them here rather than
  cut them.)*
- **On GitHub, as the release's text.** The release workflow renders the prerelease note (for a
  prerelease) followed by the version's file, with the site's routes made absolute, and passes it with
  `--notes-file`. It no longer asks for `--generate-notes`.
  *(2026-10-12, decided with the user: for a release candidate it renders the candidate's note instead,
  and the site's section of a candidate opens with that note too.)*
  - A release that GitHub's page already made is given that text, replacing what it said.
  - **beta.1 and beta.2 are written once, after the fact,** from the git log, the pull requests and
    ADR-0042. They name beta.2's change of target as breaking. The user reviews them before they merge.
    Rewriting the two GitHub releases from them is one operation on the public repository, made with
    the user's approval.

### What refuses a mistake

- **The Docs Site's build refuses**, as an error naming the file, the line and the fix:
  - a construct the notes' small Markdown subset does not have (headings, bullets with one nested level,
    paragraphs, bold, code spans and links);
  - a heading missing or out of order, or empty without "None.";
  - a `Product` or `Kind` outside its list;
  - a first line that differs from the file's name;
  - an `ADR-NNNN` with no file, or a `Docs:` link to a route or anchor the site does not have;
  - `Published: pending` on any file but the newest;
  - a version without a suffix whose notes lack the Signed off and SHOULD blocks;
  - *(2026-10-12, decided with the user.)* a version without a suffix, or a release candidate, whose
    notes lack one of the five blocks a release carries, or hold one out of its form (above): a
    release's Signed off block in the pending form, and a candidate's in a release's, among them;
  - *(2026-10-12, decided with the user.)* an `-alpha`'s, a `-beta`'s or any other prerelease's notes
    that carry one of the five;
  - *(2026-10-12, decided with the user.)* a suffix that begins with `rc` and is not exactly `-rc.N`
    with N from 1, as `-rc1` or `-RC.1`, whose notes would be read as a beta's, without the blocks a
    candidate rehearses;
  - *(2026-10-12, decided with the user.)* a release candidate's notes with no candidate's note to
    begin them, or a candidate's note without its `{ref}`.
- **When the build is given the version it publishes** (`-p:DocsVersion=` on a tag), it also refuses a
  missing file for that version, and any Change Note still under `docs/releases/unreleased/`.
  *(2026-10-12, decided with the user: in a release's or a release candidate's notes it also reads the
  SHOULD criteria accepted failing and the §21 items out of scope against the Definition of Done, and
  refuses an ID that is not a SHOULD there and an item §21 does not have. Only the build that publishes
  a version reads them against it, so a version already published stays true of its own day however the
  Definition of Done is restated after it.)*
- **The release workflow's first job refuses the tag** before CI's twenty minutes when the tagged commit
  has no `docs/releases/<version>.md`, still holds a Change Note, or has an older file still `pending`.
  *(2026-10-12, decided with the user: what it refuses besides, by the kind of version the tag names, is
  ADR-0042's "How a version is published", step 1.)*

## Considered options

- **GitHub's generated notes, as today.** They are not in the repository, so the site cannot show them,
  and pull request titles are not what a Consumer needs to know: they mixed CI work with the products,
  and missed the one breaking change.
- **Fetching the GitHub releases when the page loads, as MudBlazor's site does.** A server call
  (ADR-0110), and an empty page when it fails.
- **One `CHANGELOG.md` at the root.** Every branch would edit the same lines, and the release workflow
  would need a second parser to cut one version out of it.
- **One shared "next" file that every change edits.** The same conflict, every time two branches run at
  once.
- **Notes written only at the cut.** Whoever cuts would have to rediscover, from the diff, whether each
  change broke anything. The author knew when they made it.
- **A "since" marker typed on each Docs Site section.** Typed by hand, it can be wrong, and the build
  can check only that the version it names exists. The "New in" chip is read from the notes instead.

## Consequences

- ADR-0042's "How a version is published" is rewritten to match: the first job's refusals, the release's
  text, and the Docs Site published by the release after the push (ADR-0110).
- `CONTEXT.md` gains **Release Notes** and **Change Note**.
- AGENTS.md's "The Docs Site moves with the change" gains the Change Note, and the pull request template a
  "Release notes" section beside "Docs Site". `/code-review`'s Standards axis checks both.
- The Docs Site's generator gains the notes' parser and its checks, pinned by a test project of its own,
  malformed inputs included.
- A behaviour change merged without a Change Note cannot be caught mechanically. The rule, the template
  and the review are what catch it.
