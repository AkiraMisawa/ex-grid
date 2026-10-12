# A release's blocks in the Release Notes: every ADR, Definition of Done and glossary edit

Branch `claude/release-notes-stable-blocks`, 2026-10-12. The Docs Site's notes parser
(`samples/ExGrid.Docs.Generator/ReleaseNotes.cs`) requires a release's five blocks of a version without a
suffix and of a release candidate (`-rc.N`), checks a candidate's as it checks a release's, and refuses them
on an alpha, a beta or any other prerelease: the decision of 2026-10-12 in ADR-0190's and ADR-0205's notes.
The edits below make the text say what the build does. Each is an exact old text and its new text, checked
against this branch's tip: every old text occurs once, and the list applies in order.

What the edits do not settle, and the points the implementation settled that no decision states, are in
`docs/agents/proposals/release-notes-stable-blocks.md`.

### 1. docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md

Old:

````text
- **A stable version's notes carry two more blocks**, which the build requires of a version without a
  suffix (Definition of Done §1 and Step 7):
  - **Signed off** — the `verification/<date>/results.md` the sign-off was made against;
  - **SHOULD criteria accepted failing** — each criterion's ID and the written reason, or "None."
````

New:

````text
- **A stable version's notes carry five more blocks**, which the build requires of a version without a
  suffix and of a release candidate (the note above), and refuses on an `-alpha`, a `-beta` or any other
  prerelease. A release's headings are, in this order: Highlights, Breaking changes, Behaviour changes,
  **Known limitations**, ExGrid, ExSheet, ExPivot, Data, **Signed off**, **SHOULD criteria accepted
  failing**, **Out of scope for this version** and **Supported**.
  - **Known limitations**
    ([ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)):
    what the version does not do, or "None.". Each line begins with the products it reaches in bold, as
    a Breaking line does, names the limitation in a sentence, and links the page of the site that says
    it in full.
  - **Signed off** (the Definition of Done's Step 7,
    [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)):
    five lines, each beginning with its label in bold, in this order:
    - `Record:` the `verification/<date>/results.md` the sign-off was made against, linked at the
      version's own tag (`…/blob/v<version>/verification/<date>/results.md`). The record is committed
      after X, so it exists at the tag and not at X;
    - `Commit:` the commit X, its 40 characters in a code span. The release workflow reads it;
    - `Toolchain:`, `OS:` and `Browsers:`, the versions the sign-off ran with.

    The block names one record and one commit, a link's address included, so that the release workflow
    reads exactly those.
  - **SHOULD criteria accepted failing** (the Definition of Done's §1): each line begins with the
    criterion's ID in bold, or several separated by commas, and gives the written reason it was accepted;
    or "None.". The ID is a SHOULD of the Definition of Done: a MUST that fails stops the release, and an
    OBSERVATIONAL number never fails.
  - **Out of scope for this version** (Step 7, ADR-0205): each line names the Definition of Done's §21
    item accepted in writing as out of the version's scope, as §21.11, and links where it was accepted;
    or "None.".
  - **Supported** (ADR-0205): four lines, each beginning with its label in bold, in this order:
    `Hosts:`, `Browsers:`, `Target framework:` and `Packages:`. The target framework and the packages
    are those its `Target:` and `Packages:` header lines name.

  *(Amended 2026-10-11 and 2026-10-12, decided with the user in ADR-0202, ADR-0205 and the note above: the
  decision first listed two blocks, Signed off and SHOULD criteria accepted failing, required of a version
  without a suffix. ADR-0202 added Known limitations, ADR-0205 added the last two and said what Signed off
  holds, and the note of 2026-10-12 gave a release candidate's notes the same five.)*
````

### 2. docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md

Old:

````text
  - `Published: pending` on any file but the newest;
  - a version without a suffix whose notes lack the Signed off and SHOULD blocks.
- **When the build is given the version it publishes** (`-p:DocsVersion=` on a tag), it also refuses a
  missing file for that version, and any Change Note still under `docs/releases/unreleased/`.
````

New:

````text
  - `Published: pending` on any file but the newest;
  - a version without a suffix, or a release candidate, whose notes lack one of a release's five blocks or
    hold one out of its form; an `-alpha`'s, a `-beta`'s or any other prerelease's notes that carry one;
  - a suffix that begins `rc` and is not `-rc.N` with N from 1, as `-rc1` or `-RC.1`, whose notes would be
    read as a beta's, without the blocks a candidate rehearses.

  *(Amended 2026-10-12: the last of these read "a version without a suffix whose notes lack the Signed off
  and SHOULD blocks". ADR-0202 and ADR-0205 added three blocks, and the note of 2026-10-12 gave a release
  candidate's notes all five.)*
- **When the build is given the version it publishes** (`-p:DocsVersion=` on a tag), it also refuses a
  missing file for that version, and any Change Note still under `docs/releases/unreleased/`. In a
  release's or a release candidate's notes it also reads the SHOULD criteria accepted failing and the
  §21 items out of scope against the Definition of Done, and refuses an ID that is not a SHOULD there and
  an item §21 does not have. No other build reads them against it, so a version already published stays
  true of its own day however the Definition of Done is restated after it.
````

### 3. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

````text
The Docs Site's notes parser requires each of them of an unsuffixed version.
````

New:

````text
The Docs Site's notes parser requires each of them of an unsuffixed version, after the products and in
this order (ADR-0190). *(2026-10-12, decided with the user: it requires them of a release candidate too,
whose notes rehearse them, checks them as it checks a release's, and refuses them on an `-alpha` or a
`-beta`, as the note under "The release branch" says.)*
````

### 4. docs/definition-of-done.md

Old:

````text
It is not a release in this document's sense, and nothing here is relaxed for it. *(Added
2026-09-24.)*
````

New:

````text
It is not a release in this document's sense, and nothing here is relaxed for it. *(Added
2026-09-24.)* *(2026-10-12, decided with the user,
[ADR-0190](adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md) and
[ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md):
a release candidate, `0.1.0-rc.N`, is a prerelease too, and no release either. Its notes rehearse the
release's: they carry the blocks a version without a suffix must — its known limitations, its sign-off, the
SHOULD criteria accepted failing with their reasons, what is out of its scope and what it supports — checked
as a release's are, so that their text is read before the release is tagged. An alpha's or a beta's notes
carry none of them.)*
````

### 5. docs/definition-of-done.md

Old:

````text
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)*
````

New:

````text
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)* *(2026-10-12, decided with the user, ADR-0190 and ADR-0205: each release
  candidate's notes carry the same Signed off block, and the blocks of the SHOULD criteria accepted failing
  and of what is out of scope, checked by the Docs Site's build as a release's are, so that what the
  release's notes will say of this sign-off is read before its tag.)*
````

### 6. CONTEXT.md

Old:

````text
release and the Docs Site's Release notes page. A prerelease's notes say what the Definition of Done
still owes; a stable version's name its sign-off and every SHOULD accepted failing
([ADR-0190](./docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
````

New:

````text
release and the Docs Site's Release notes page. A prerelease's notes say what the Definition of Done
still owes; a stable version's also say what it does not do (its Known limitations), name its sign-off,
every SHOULD accepted failing and what was left out of its scope, and say what it supports, and a release
candidate's carry the same, to rehearse them
([ADR-0190](./docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md),
[ADR-0202](./docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md),
[ADR-0205](./docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)).
````
