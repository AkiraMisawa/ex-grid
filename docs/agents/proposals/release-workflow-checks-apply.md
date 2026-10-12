# The release workflow's checks: every ADR, Definition of Done and project-instruction edit

Branch `claude/release-workflow-checks`, 2026-10-12, merged with `origin/main` at `2265cda6` (#82 to #89). The
branch builds ADR-0205's and ADR-0208's release checks (`tools/release/ExGrid.Release`, tested by
`tests/ExGrid.Release.Tests` in layer 1, and run by `release.yml` and `docs.yml`). A background agent edits no ADR,
not the Definition of Done, not `CONTEXT.md` and not the project instructions, so every edit they need is here.

Each edit is an exact old text and its new text, in whole lines with the target's own indentation, generated and
checked by script against this branch's tip: every old text occurs once in its file at the point it is applied,
and the list applies in order. The Definition of Done that results from it was put in the working tree and the
release check's layer-1 tests run against it (the table in it, not the proposal's, is then the one they read):
they pass. The conditional edits, after the list, apply after it.

This list replaces `adr-0042-rewrite.md` and `release-workflow-checks.md`, which are deleted in the commit that adds
it: their texts are edits here, and their readings and questions are below. `by-hand-rows-table.md` stays, because
the release check reads the table there until the Definition of Done holds it; **delete it in the change that
applies edit 17**.

## Questions for the user

None of these is settled here. Where the branch had to take a side to be built, it took the first option, and its
texts state it; the edits that state it are named, so another answer knows what to change.

1. **Does the release workflow read a release candidate's Signed off block, and judge its rows at the commit the
   block names?** On 2026-10-12 the user decided that a candidate's notes carry a stable version's blocks,
   "checked as a stable version's are" (ADR-0205's note), and "the build checks them" (ADR-0190's note). The two
   readers are the Docs Site's build (on `claude/release-notes-stable-blocks`) and this branch's workflow.
   - **(a) As built.** The build checks the blocks' form, a candidate's included. The workflow reads a sign-off only
     on a version without a suffix, and judges a candidate's rows checked by hand at its tagged commit. So `rc.1`
     is refused until every row holds at the commit it is tagged on, and an eighteenth run, if one is needed, is
     made at that commit and filed before the tag (this branch's reading 7). Stated by edits 1, 4, 15 and 18, and by
     the comments in `Gate.cs`, `TagVersion.cs` and `release.yml`.
   - **(b) Read literally.** The workflow reads a candidate's block as a stable version's, structural check
     included: each candidate is tagged on its X plus records and notes, with a `results.md` naming X at the tag,
     and its rows are judged at X. `claude/release-notes-stable-blocks` prepared a patch for this branch that does
     it, kept off every branch (its report names the file). In `Gate.Run`, the candidate's branch calls
     `SignOff.Check` as the stable one does; the four edits and the comments change their sentence on it.
   - **(c) This branch's own alternative** (its old proposal, item 5): publish a candidate first and run its rows on
     the published candidate. The by-hand half of a candidate's check then reports rather than refuses, a one-line
     change in `Gate.Run`.
   - **Recommendation:** neither proposal makes one. The brief this branch was built to said "every check a stable
     tag runs except the Signed off block and the structural check", which is (a).
2. **Where the rewrite lands.** ADR-0205's Consequences and the Definition of Done's Step 7 say ADR-0042 is
   rewritten "on the release branch, in the change that adds this `release.yml`". This branch is cut from `main`.
   - **(a) On `main`, before `release/0.1` is cut**, so the cut carries the stable path from its start, as ADR-0205's
     "The release branch" asks. Apply the conditional edits C1 and C2 too. A beta from `main` then behaves as
     before, except that once a version without a suffix is tagged it publishes no Docs Site.
   - **(b) Held for `release/0.1`**: this branch merges into the release branch when it is cut, the list is applied
     there, and the conditional edits are not.
   - **Recommendation:** the proposal set out both and made none.
3. **The readings the check and its table had to make**, which ADR-0205 and ADR-0208 leave open. Each is built
   and written into the edits named, so applying them makes it the decision; the proposals' recommendation is each
   as built.
   1. **X is written in full, forty hex digits**, in the Signed off block: an abbreviation can stop being unique,
      and a word of the letters a to f can read as one. The notes parser's `Commit:` line requires the same.
      Edit 4.
   2. **`results.md` names X**, in full or by a prefix of seven digits or more, as the Windows runs' reports write
      "Verified commit: `1655299`", so a slip in the notes cannot point the structural check at another commit.
      Edit 4.
   3. **A stable version exists once the repository has a tag without a suffix**, not once GitHub shows a release:
      a refused stable tag never deleted keeps prereleases from publishing the site, and the admit job says so.
      Reading published releases would put a network answer into the decision. **A stable version publishes the
      site only when no stable tag is above it** by SemVer precedence, so `v0.1.1` from `release/0.1` after
      `v0.2.0` leaves `0.2.0`'s site in place; `docs.yml` refuses such a call that comes anyway. Edits 7 and 13,
      and this branch's Docs Site edit (the Release notes page's lead).
   4. **A suffix that begins with `rc` is a release candidate only as `-rc.N`**, N from 1; any other spelling is
      refused, not published as a plain prerelease without the checks a candidate rehearses. The notes parser reads
      a name the same way. Edit 4.
   5. **No leading zero in the three numbers**: NuGet would publish `v01.2.0` as `1.2.0`. Edit 4.
   6. **Only an `oracleSkip` that says why `oracle.ps1` cannot ask a case excuses it.** `engineDiffersByDecision`
      does not: `oracle.ps1` asks such a case, comparing Excel's answer with `excelExpect`, or recording it when there
      is none; counted, SORT-012 to 014, UNIQUE-010 and 011, INDIRECT-036 to 038 and INDIRECT-043 would have let
      `rc.1` through with their questions open. An `oracleSkip` that says "ask by keys" does not either:
      `oracle.ps1 -Keys` asks it (CW-030 to 032), and the refusal names `oracle.ps1 -Area <files> -Keys -Update`.
      If the user wants `engineDiffersByDecision` to count, it should still leave open a case with an `excelExpect`
      while its source is not `observed`, and one whose text says Excel is still to be asked. Edits 4 and 14.
   7. **A release candidate's rows are judged at its tagged commit.** See question 1. Edits 4 and 15.
   8. **A record's commit must be where the workflow can fetch it.** The checkout fetches every branch and tag, and
      a commit none holds is asked of the remote by its full name (GitHub serves it while any ref reaches it, a pull
      request's `refs/pull/N/head` among them). Otherwise the tag is refused as "cannot be checked", never passed.
      The seeded records' commits are held today by `claude/exsheet-windows-verify-11`, `-15` and `-16`. Edit 15.
   9. **FN-12e is two rows, Windows and macOS.** The row reads "by hand on each platform; Layer 3 on Linux", and
      Linux is layer 3's, in CI. **macOS checks the painting only**: its widths are recorded and held by layer 1
      (`tests/GlyphWidths/*.macos.json`, `GlyphWidthCorpusTests`), so a person checks a bold amount painted without
      an ellipsis and `####` filling its cell; the seventeenth run's Part G covers Windows only, so this row needs a
      run on a Mac, which KB-3 needs anyway. **Windows keeps the measurement as well**, while `tests/GlyphWidths`
      holds no `system-ui.windows.json` (its README leaves Segoe UI out of the tables until a Windows record is
      added): FN-12e's MUST, "the widest glyph measured for its class on Windows, macOS and Linux", is never let
      pass on a painting check alone. Once that record is committed and held by layer 1, the Windows row narrows to
      the painting, in the same change. Edit 17.
   10. **KB-3 on macOS is a layer-3 run on a Mac**, the suite's `ControlOrMeta` presses being Command there, so its
       paths include `tests/ExGrid.Browser/`, the suite that run is. Edit 17.
   11. **The seeded records.** VZ-14's is the fifteenth run's Part A, a pass. SH-42's is the eleventh run's Part B,
       on a standalone `keys.html`, which is the run the row names; its code has changed since, so the check asks
       for the seventeenth run's Part D. ED-30's is the sixteenth run, seeded as `fail`: that run gave no verdict by
       row, and its differences 1 and 3 read against ED-30's statement. Every other row has never been recorded.
       These are `verification/by-hand.json`'s, which no edit restates.
   12. **A record names the commit it verified**, in full or by seven digits or more, and the check holds the
       index to it, so a slip in the index's commit cannot move a row's baseline unseen. Edits 4 and 17.
4. **Should a Windows run also tag the commit it verified** (for example `verified/2026-10-12-windows-17`), so that a
   squash merge and a deleted branch cannot take it from the record? Open; the proposal makes no recommendation.
5. **WR-10's floor record is not checked by the release workflow.** `claude/mudblazor-range-floor` has the floor
   run's record hold "while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from the commit it was
   made at up to X (the shape of ADR-0208)", read by a person with `git diff`. This branch's check reads only the
   rows of Step 4's by-hand table, and WR-10's run is automated, so it is not one of them. Indexing it beside them
   (a second index, or a kind of row) would make the workflow refuse a tag whose Wrappers changed since the floor
   run. Raised here because the two decisions meet; no proposal recommends either.

## What changed since the proposals

- **Re-checked against `main` after #82 to #89.** ADR-0042 is as the proposal found it, so its rewrite is cut into
  twelve edits that each touch only what changes; the paragraphs nothing changes are left in place rather than
  restated. ADR-0110, ADR-0208 and the Definition of Done moved, and the anchors are taken at the merged tip.
- **A release candidate rehearses the stable notes blocks** (decided 2026-10-12, ADR-0190, ADR-0205): edits 1, 2 and
  4 say so, and that the workflow does not read a candidate's Signed off block (question 1).
- **The Wrappers' MudBlazor floor is 9.6.0** (decided 2026-10-12, implemented on `claude/mudblazor-range-floor`): no
  text of this branch, and no edit here, states a MudBlazor version or range, so none needed to change. ADR-0042's
  package paragraph, which this list does not touch, names none either.
- **The Signed off block, as `claude/release-notes-stable-blocks`'s parser requires it** (labelled `Record:` and
  `Commit:` lines, the record linked at the tag): the old proposal's item 3 asked that branch for exactly this.
  The release check reads that form, which a new test pins
  (`SignOffTests.The_labelled_block_the_notes_parser_requires_is_read`).
- **FN-12e's two rows name more code**: `src/ExGrid/Cells/` (`PaintedTextOf`, how a cell is asked for its fitted
  text), `src/ExGrid/GridPresentationDefaults.cs` (the defaults FN-12e is about), `src/ExSheet/SheetColumns.cs` and
  `SheetRow.cs` (the widths in characters and the fitted General), and `src/ExSheet.Engine/CellDisplay.cs` and
  `NumberText.cs` (where `####` is decided). Neither row has a record, so nothing held is undone. The index and the
  table carry the same paths.
- **The table's text**: its heading is a bold line, so the rest of Step 4 is not filed under a `####` heading, and
  its list of refusals names a record whose commit cannot be fetched, as the check refuses it.
- **Edits 14, 15, 18 and 19 are new.** ADR-0208's rule and the check would otherwise differ without a word (14, 15);
  the glossary entry is offered (18); and the project instructions' sentence on what a tag does becomes false once
  the rewrite lands (19).
- **Noticed, not changed:** ADR-0042's "What a package must prove" names `_content/ExGrid/ex-grid.css`,
  `ex-grid.js` and `mud-ex-grid.css`, and packs "both packages", while `check.sh` packs ten and checks their minified
  builds (ADR-0123) and that the application links none itself (#82). That is stale on `main` and not this list's.

## Where this list meets other branches' lists

- **`claude/release-notes-stable-blocks`.** Its ADR-0190 edit 1 says what the Signed off block holds; this branch's
  reader takes that form (above). Its Definition of Done edit 5 replaces the same two Step 7 lines as C2 (see C2).
  Its `CONTEXT.md` edit 6 rewrites the Release Notes entry; edit 18 here adds an entry after Change Note's, so the
  two do not overlap, and both say a candidate's notes carry a release's blocks. Its question 2, the prerelease
  note above a candidate's Signed off block, is its own; edit 2's last sentence reads true whichever answer.
- **`claude/mudblazor-range-floor`.** Its Definition of Done edit 5 rewrites Step 4's Discharges paragraph and
  inserts the floor paragraph after it, later in Step 4 than edit 17's table, and its edit 3 touches another bullet
  of ADR-0205's Consequences than C1: disjoint. Its PRE-3 and PV-1 edits carry the floor; this list has none for
  them. Question 5 is where the two meet.
- **ADR-0208.** A list from the run-17 or area branches that edits "Questions are asked when the feature is built"
  or "The release workflow checks it" would meet edits 14 and 15's anchors.

## The edits, in the order they apply

1. **ADR-0042, Decision: the release candidate, and the notes it rehearses.** The proposal's note, and the decision
   of 2026-10-12 that a candidate rehearses a stable version's notes. "Except the two that read a sign-off" is
   question 1's option (a), as built.
2. **ADR-0042, Decision: what a release candidate promises.** The proposal said "Its commit is on a release
   branch"; the workflow admits a candidate on `main` too, so the promise says what is checked. The last sentence
   follows the decision of 2026-10-12.
3. **ADR-0042, "How a version is published": the note that announced the rewrite becomes the rewrite's own.** As
   proposed. The note it replaces only announced this rewrite; it names no place, so it reads true whichever course
   question 2 takes.
4. **ADR-0042, "How a version is published", step 1: what each kind of version must carry.** The proposal's step 1,
   with what the check refuses since (a record that does not name its index's commit, a commit that cannot be
   fetched), one record and one commit, as `claude/release-notes-stable-blocks`'s ADR-0190 edit has the Signed off
   block hold them, and a paragraph on a candidate's notes for the decision of 2026-10-12. States readings 1, 2, 4
   to 8 and question 1's option (a).
5. **ADR-0042, "How a version is published", step 2: the long run.** As proposed.
6. **ADR-0042, "How a version is published", step 4: a stable version's release is a release.** As proposed.
7. **ADR-0042, "The release's text and the Docs Site": which releases publish the site.** The proposal's note, out
   of the italics, which do not span a list. States reading 3.
8. **ADR-0042, "The family ships together": a stable version still waits, on its sign-off.** As proposed.
9. **ADR-0042, "What a stable version needs besides Step 7": the refusal, replaced.** The proposal dated this "Done
   2026-10-11"; it is done when this list is applied, so it carries no date.
10. **ADR-0042, Rejected: an unsuffixed `0.1.0`, before and after the sign-off.** As proposed.
11. **ADR-0042, Rejected: two options the rewrite turned down.** As proposed.
12. **ADR-0042, Consequences: the tag is X plus its records, and the checks are code.** As proposed.
13. **ADR-0110, Publishing: once a stable version exists, which releases publish the site.** ADR-0205's
    Consequences ask it ("ADR-0110: a prerelease publishes no site once a stable version exists"). The proposal's
    bullet, set one level in, beside the bullet it follows. States reading 3.
14. **ADR-0208, "Questions are asked when the feature is built": which `oracleSkip` excuses a case.** Not in the
    proposals: ADR-0208 says the release excuses a case with an `oracleSkip`, and reading 6 narrows which. Without
    a note here, ADR-0208 and the check would quietly differ.
15. **ADR-0208, "Checks by hand are repeated only when the code they cover changes": how the check reads the
    records.** Not in the proposals: readings 7 and 8, and the index, written beside the rule they read. Without
    it, ADR-0208 names only the signed-off commit, which a candidate does not have.
16. **Definition of Done, §1 Evidence: one `.trx` per test project, not twelve.** The solution holds thirteen test
    projects on `main` (`ExGrid.Docs.Generator.Tests` came with #77) and fourteen with this branch's
    `tests/ExGrid.Release.Tests`. Step 1's loop copies every project's `results.trx` without naming them, so it
    needs no change.
17. **Definition of Done, §22 Step 4: the table of rows checked by hand, and the code each covers.** The table the
    note above it promises ("The table of rows and paths is written here with that check"), word for word as
    `docs/agents/proposals/by-hand-rows-table.md` holds it. Its heading is a bold line rather than a `####`
    heading, so the rest of Step 4 is not filed under it; the check reads either. Delete that proposal file in the
    same change.
18. **CONTEXT.md: Release Candidate (offered; no proposal carried it).** Offered, not proposed before: "release
    candidate" is the one kind of version whose name holds "release" and that is a prerelease, which Release Notes'
    _Avoid_ line warns about. Leave it out if the glossary is not to carry release terms beyond Release Notes and
    Change Note. "But the two that read a sign-off" follows question 1's option (a).
19. **AGENTS.md (CLAUDE.md is a link to it), Toolchain: what a tag does.** Not ADR or Definition of Done text, but
    a background agent does not edit the project instructions either. Once the rewrite lands, the sentence it
    replaces is false.

### 1. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **The line is `0.1.0-beta.N`.** SemVer 2. `0.x` says the API is still moving. The public
  surface has not been reviewed yet (below), and a beta may break it between numbers. NuGet hides a
  suffixed version unless the Consumer asks for prereleases, so nobody gets one by accident.
````

New:

````text
- **The line is `0.1.0-beta.N`.** SemVer 2. `0.x` says the API is still moving. The public
  surface has not been reviewed yet (below), and a beta may break it between numbers. NuGet hides a
  suffixed version unless the Consumer asks for prereleases, so nobody gets one by accident.
  *(2026-10-11, decided with the user,
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md):
  a stable version is first rehearsed by **release candidates**, `0.1.0-rc.N`, tagged on its release
  branch. A candidate is a prerelease, and ships as one. It also runs every check a stable tag runs,
  except the two that read a sign-off (below).)*
  *(2026-10-12, decided with the user,
  [ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md) and
  ADR-0205: the rehearsal covers the notes. A candidate's notes carry the blocks a version without a
  suffix must, its Signed off block among them, and the Docs Site's build checks them as it checks a
  stable version's, so the text a stable release will show is read before its tag.)*
````

### 2. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **What a beta promises.** Every layer passed in CI on the commit it was built from. That
  commit is on `main`. Its release notes name what the Definition of Done still owes:
  `docs/implementation-status.md` at that commit is the list. It promises nothing about the
  criteria that CI cannot run (ADR-0041's "what CI does not replace").
````

New:

````text
- **What a beta promises.** Every layer passed in CI on the commit it was built from. That
  commit is on `main`. Its release notes name what the Definition of Done still owes:
  `docs/implementation-status.md` at that commit is the list. It promises nothing about the
  criteria that CI cannot run (ADR-0041's "what CI does not replace").
  *(2026-10-11: a release candidate promises more. Its commit is on `main` or a release branch. CI's
  long run passed on it. No question to Excel is open in ExSheet's corpus at it. Every row the
  Definition of Done checks by hand holds there on a record whose code is unchanged
  ([ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md)).
  It is still not signed off: the Signed off block its notes carry rehearses the stable version's
  (2026-10-12).)*
````

### 3. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
*(2026-10-11, decided with the user:
[ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)
rewrites this for stable versions. `0.1.0` is grown on `release/0.1` and rehearsed by release
candidates; a tag may be on `main` or a release branch; the refusal of an unsuffixed version is
replaced by a check that the notes name a sign-off made at a commit the tag differs from only by its
records and notes. This section is rewritten on the release branch, with what it said kept.)*
````

New:

````text
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
````

### 4. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **Pushing the tag runs `.github/workflows/release.yml`:**
  1. It refuses a tag whose commit is not on `main`, and a version without a prerelease suffix.
     The second refusal is the Definition of Done's gate, enforced structurally. It comes out
     only in the change that rewrites this ADR at sign-off.
````

New:

````text
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
       - its latest record is not a pass, or does not name the commit the index says it was made at;
       - a path it covers changed since that commit, or that commit cannot be fetched;
       - a path it names no longer exists.

       The rows and their paths are Step 4's table, "Rows checked by hand, and the code each covers".
       The records are indexed in `verification/by-hand.json`, which names the same paths. The workflow
       refuses a tag where the two differ.

     A candidate's notes carry a stable version's blocks, and step 2's build checks them as it checks a
     stable version's (ADR-0190's note of 2026-10-12). The workflow reads a sign-off only on a version
     without a suffix: it does not read a candidate's Signed off block, and it judges a candidate's rows
     checked by hand at the tagged commit.

     **A version without a suffix is refused besides** unless all of these hold:
     - its notes' "Signed off" block names one `verification/<date>/results.md`, and that file is at
       the tag;
     - the block names one commit, X, the one the sign-off was made against, written in full, forty hex
       digits;
     - that `results.md` names X;
     - the tag is X, or a descendant of it that differs from X only under `verification/` and
       `docs/releases/`;
     - both of a candidate's checks pass, with the rows checked by hand judged at X, not at the tag.

     A fix after X is a new X, and a new sign-off of what it touches.

     *(Before this rewrite, this step read: "It refuses a tag whose commit is not on `main`, and a
     version without a prerelease suffix. The second refusal is the Definition of Done's gate, enforced
     structurally. It comes out only in the change that rewrites this ADR at sign-off.")*
````

### 5. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
````

New:

````text
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
     *(2026-10-11, ADR-0205: for a version without a suffix and for a release candidate, with the long
     run, `long: true`: the soak on both browsers and both hosts, and ST-1 at 10⁶ rows. If it fails,
     nothing is published. The records stay the Definition of Done's: a watched Step 4 and a local
     Step 5 (ADR-0041). The long run is the tag's own proof, at the commit it publishes.)*
````

### 6. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
     workflow refused the second release after the packages were already on nuget.org, and
     the release it left behind carried neither the packages nor the note.)*
````

New:

````text
     workflow refused the second release after the packages were already on nuget.org, and
     the release it left behind carried neither the packages nor the note.)*
     *(2026-10-11, ADR-0205: for a version without a suffix, the GitHub release is a release, not a
     prerelease, and carries no prerelease note. A release GitHub's page made is marked a release.)*
````

### 7. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
  - **Then the release publishes the Docs Site** ([ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)),
    with the version, the tag and the UTC day the packages were pushed. The site states a version only
    once nuget.org has it.
````

New:

````text
  - **Then the release publishes the Docs Site** ([ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)),
    with the version, the tag and the UTC day the packages were pushed. The site states a version only
    once nuget.org has it.
    *(2026-10-11, ADR-0205; 2026-10-12 for a stable version below another.)* **Once a stable version
    exists, a prerelease's release publishes no Docs Site, and neither does a stable version that a
    stable tag is above.**
    - A stable version exists once the repository has a tag without a suffix.
    - The site describes the latest stable version. A prerelease's notes are on its GitHub release, and
      reach the site with the next stable publish.
    - A patch to an older line, such as `0.1.1` from `release/0.1` after `0.2.0`, publishes none:
      versions are compared by SemVer precedence, so its site would replace a later version's.
    - The release's first job decides it, and says so. `docs.yml` refuses a call that comes anyway,
      and refuses too when it cannot read the repository's tags.
````

### 8. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **A stable version still waits.** The refusal of an unsuffixed version stays. When ExGrid is
  signed off (Step 7), which of the other products a stable version carries is decided in the change
  that rewrites this ADR, against §27, §29 and §30. Those sections judge their own products, and
  still never gate ExGrid.
````

New:

````text
- **A stable version still waits.** The refusal of an unsuffixed version stays. When ExGrid is
  signed off (Step 7), which of the other products a stable version carries is decided in the change
  that rewrites this ADR, against §27, §29 and §30. Those sections judge their own products, and
  still never gate ExGrid.
  *(2026-10-11: the first stable version is `0.1.0`, and it carries all ten packages
  ([ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)), so §27, §29
  and §30 gate it beside ExGrid's sections. "The refusal of an unsuffixed version stays" now reads:
  a stable version still waits, and the workflow refuses it until its notes name its sign-off
  ("How a version is published", step 1).)*
````

### 9. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **Removing the prerelease-only refusal from `release.yml`**, in the same change that rewrites
  this ADR.
````

New:

````text
- **Removing the prerelease-only refusal from `release.yml`**, in the same change that rewrites
  this ADR.
  *(Done in the rewrite of "How a version is published", and not as a removal: the refusal is replaced
  by the check of the sign-off in its step 1 (ADR-0205).)*
````

### 10. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
````

New:

````text
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
  *(2026-10-11: still rejected before the sign-off. An unsuffixed `0.1.0` is now tagged once its notes
  name a sign-off the workflow can check, and on nothing else.)*
````

### 11. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.
````

New:

````text
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.
- *(Added 2026-10-11.)* **Removing the refusal of an unsuffixed version at the sign-off**, as this ADR
  first said. The change that removed it could not be the commit that was verified (ADR-0205).
- *(Added 2026-10-11.)* **Trusting the notes' Signed off block without reading the repository.** A
  block naming a record that was never filed, or a commit the tag has moved on from, would publish a
  stable version nobody signed off. The workflow reads both.
````

### 12. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **The first publish is a person's act.** Someone creates the nuget.org policy and the
  `NUGET_USER` secret once, then pushes the tag.
````

New:

````text
- **The first publish is a person's act.** Someone creates the nuget.org policy and the
  `NUGET_USER` secret once, then pushes the tag.
- *(Added 2026-10-11.)* **A stable version's tag is the sign-off's commit plus its records and notes**,
  and the release workflow checks it. The Definition of Done's Step 7 clause on the refusal reads the
  same (ADR-0205).
- *(Added 2026-10-11.)* **The release workflow's checks are code with tests.** They are
  `tools/release/ExGrid.Release`, and their tests, `tests/ExGrid.Release.Tests`, run in layer 1 on
  every push. A tag is therefore not the first time they run.
````

### 13. docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md

Old:

````text
  - **So the release publishes the site, after the push.** `release.yml` gains a last job that calls
    `docs.yml` with the version, the tag and the UTC day of the push (ADR-0190). The site therefore
    states a version only once nuget.org has it. `docs.yml` is no longer started by a tag.
````

New:

````text
  - **So the release publishes the site, after the push.** `release.yml` gains a last job that calls
    `docs.yml` with the version, the tag and the UTC day of the push (ADR-0190). The site therefore
    states a version only once nuget.org has it. `docs.yml` is no longer started by a tag.
  - *(2026-10-11, decided with the user,
    [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md);
    2026-10-12 for a stable version below another, with the release workflow's check.)* **Once a stable
    version exists, a prerelease's release publishes no site, and neither does a stable version that a
    stable tag is above.**
    - The site describes the latest stable version. A prerelease's notes are on its GitHub release,
      and reach the site with the next stable publish.
    - A stable version exists once the repository has a tag without a suffix.
    - A patch to an older line, such as `0.1.1` from `release/0.1` after `0.2.0`, publishes no site:
      versions are compared by SemVer precedence, and its pages would replace a later version's.
    - `release.yml`'s first job decides it, and says so. `docs.yml` refuses a call that comes anyway,
      and refuses too when it cannot read the repository's tags.
    - The on-demand publish of `main` or a `lab/*` branch stays, under its banner.
````

### 14. docs/adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md

Old:

````text
  - The remedy is a run of `oracle.ps1 -Area` over the files that hold such cases, which takes minutes.
    The whole corpus is never put to Excel again.
````

New:

````text
  - The remedy is a run of `oracle.ps1 -Area` over the files that hold such cases, which takes minutes.
    The whole corpus is never put to Excel again.
  - *(2026-10-12, decided with the user with the release workflow's check,
    [ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)'s "How a
    version is published".)* **An `oracleSkip` excuses a case only when it says why `oracle.ps1`
    cannot ask it.** One that says "ask by keys" names a case `oracle.ps1 -Keys` asks, and
    `engineDiffersByDecision` names a case `oracle.ps1` asks and compares; both stay open until a run
    has answered them and a person has marked the case `observed`. A release candidate is refused on
    the same terms as a version without a suffix.
````

### 15. docs/adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md

Old:

````text
- **The release workflow checks it.** For each row it reads the last record's commit from
  `verification/` and compares the paths with the signed-off commit (`git diff --quiet`). It refuses
  the tag if any row's paths changed and no newer record exists.
````

New:

````text
- **The release workflow checks it.** For each row it reads the last record's commit from
  `verification/` and compares the paths with the signed-off commit (`git diff --quiet`). It refuses
  the tag if any row's paths changed and no newer record exists.
  *(2026-10-12, decided with the user with the check, ADR-0042: each row's last record is indexed in
  `verification/by-hand.json`, with the row's paths, which the Definition of Done's Step 4 table names
  too; a tag where the two differ is refused. A record names the commit it verified, and that commit
  must stay where the release workflow can fetch it: one it cannot fetch refuses the tag as unchecked,
  never lets it pass. A release candidate, which has no sign-off, is judged at its tagged commit.)*
````

### 16. docs/definition-of-done.md

Old:

````text
- `layers-1-2.log`, the console output of section 22's Step 1, and one `<test project>.trx` per test
  project beside it — `ExGrid.Tests.trx`, `ExGrid.Components.trx`, `ExSheet.Engine.Tests.trx` and so
  on, twelve in all — each holding every test of that project with its outcome, and every skip
````

New:

````text
- `layers-1-2.log`, the console output of section 22's Step 1, and one `<test project>.trx` per test
  project beside it — `ExGrid.Tests.trx`, `ExGrid.Components.trx`, `ExSheet.Engine.Tests.trx` and so
  on, one for each test project in `ExGrid.slnx` *(it said "twelve in all" until 2026-10-12, when there
  were fourteen)* — each holding every test of that project with its outcome, and every skip
````

### 17. docs/definition-of-done.md

Old:

````text
when its feature is built, never at a release, and the corpus may hold no `uncertain` or `documented`
case without a written reason.)*
````

New:

````text
when its feature is built, never at a release, and the corpus may hold no `uncertain` or `documented`
case without a written reason.)*

**Rows checked by hand, and the code each covers**

*(Added 2026-10-11, decided with the user,
[ADR-0208](adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md).)*
Each row a person checks, where it is checked, and the paths whose change could change its result.

- **A row's latest record holds while its paths are unchanged.** That means unchanged between the
  commit the record was made at and the commit being signed off, or, for a release candidate, the
  commit it is tagged on.
- **The release workflow compares them** (`git diff --quiet`). It refuses a version without a suffix,
  and a release candidate, when:
  - a path changed;
  - the row has no record;
  - its latest record is not a pass;
  - its record does not name the commit the index gives it;
  - that commit cannot be fetched, so whether its paths changed cannot be checked;
  - a path names nothing.
- **The records are indexed in `verification/by-hand.json`.** For each row it names the same paths as
  this table, the row's latest record, and the commit that record was made at. A layer-1 test
  (`tests/ExGrid.Release.Tests`) fails when the two differ, and the release workflow refuses a tag
  where they do.

**The paths are drawn wide** (ADR-0208). A row names whole files, and a directory where its behaviour
crosses several:

- the grid's script and stylesheet sources, and their minified builds, which are what a browser runs;
- the keyboard, editor and toolbar files;
- the measuring code;
- the spec a row runs;
- the DemoHost's or the Docs Site's page, only where the row reads that page.

When in doubt, the path goes in. The list is reviewed with the row, and in any change that moves the
code it names.

**A run that records a row** writes its record under `verification/<date>/`, naming the commit it
verified. In the same commit, it replaces the row's record in `verification/by-hand.json`.

| Row | Where | Checked by hand | The code it covers |
|---|---|---|---|
| **VZ-14** | Windows | `scrollbar.spec.mjs`'s VZ-14 block at a fractional display scale, headed, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `tests/ExGrid.Browser/fixtures.mjs`, `tests/ExGrid.Browser/scrollbar.spec.mjs` |
| **VZ-16** | Windows | A real browser zoom told to the grid as it changes, on a page that is only displayed. A thin line and a gridline in device pixels at a real 2.25: the display at 150%, the browser's zoom at 150%, on `/sheet?case=11` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Components/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetAppearance.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **FN-12e** | Windows | The widest glyph of each `CellTextMetrics` class, measured in Chrome and Edge, while the Windows widths are not recorded (no `system-ui.windows.json` in `tests/GlyphWidths`). On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **FN-12e** | macOS | On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell. The macOS widths are measured already, `tests/GlyphWidths/*.macos.json` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **ED-30** | Windows | A real IME on `/sheet` and `/sheet?chrome=mud`, on Chrome and Edge, on both hosts and behind 150 ms: the sixteenth run's cases k1 to k16, and DC-47's | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormulaAids.cs`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-42** | Windows | Ctrl+1 to Ctrl+5 reach `/sheet` before the browser's tab switching, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-51** | Windows | Ctrl+F1 reaches `/sheet`, and the toolbar hides and shows | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-52** | Windows | Alt released alone, and F10, show the KeyTips on `/sheet` under both Chromes, and the browser's menu does not open. Each letter is read beside Excel's Home tab. Nothing starts while an edit is open | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **KB-3** | macOS | Layer 3 on a Mac, where `ControlOrMeta` presses Command: Cmd+Enter fills, on every key path, the editor's included | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `src/ExSheet/SheetStructureKeys.cs`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `tests/ExGrid.Browser/` |
| **VZ-18** | the Citrix PC | Ctrl+↓, Ctrl+↑ and PageDown on the Docs Site's blotter paint the rows they reveal, without a further scroll | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `src/ExGrid.MudBlazor/`, `samples/ExGrid.Docs/Examples/Grid/Blotter/`, `samples/ExGrid.Docs/Showcases/Blotter.razor` |
| **§21.11** | Windows | Where DOM focus is after ↓, → and ↓ ↓ on `/sheet`, with and without a Focus. Read under NVDA, under Narrator, and with neither, on Chrome and Edge | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
````

### 18. CONTEXT.md

Old:

````text
([ADR-0190](./docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
_Avoid_: changeset, fragment
````

New:

````text
([ADR-0190](./docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
_Avoid_: changeset, fragment

**Release Candidate**:
A prerelease, `-rc.N`, tagged before a version without a suffix to rehearse it: it publishes through the
same workflow and runs the stable path's checks but the two that read a sign-off, and its Release Notes
carry the blocks the stable version's will. It is not a release: the Definition of Done is signed off for
the version without a suffix that follows it
([ADR-0205](./docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)).
_Avoid_: beta (a beta runs none of the stable path's checks); "release" alone for it
````

### 19. AGENTS.md

Old:

````text
- **Only `src/` packs, and only a tag writes a version.** The projects say `0.0.0-dev`; pushing
  a `v*` tag runs `release.yml`, which publishes a prerelease and refuses a version without a
  suffix until the Definition of Done is signed off (ADR-0042). Every public member of a shipped
  project needs an XML doc comment — a missing one fails the build
````

New:

````text
- **Only `src/` packs, and only a tag writes a version.** The projects say `0.0.0-dev`; pushing
  a `v*` tag runs `release.yml`, which publishes a prerelease, and refuses a version without a
  suffix unless its Release Notes name a sign-off the workflow can check (ADR-0042, ADR-0205). It
  refuses that version, and a release candidate (`-rc.N`), while a question to Excel is open or a row
  checked by hand does not hold on its record (ADR-0208). Every public member of a shipped
  project needs an XML doc comment — a missing one fails the build
````

## Conditional edits, after the list above: only if question 2 is answered "on `main`, before the cut"

C1. **ADR-0205, Consequences: the rewrite went in on `main` (only if question 2 is answered "on `main` before the
    cut").** The words the proposal said this course needs.
C2. **Definition of Done, §22 Step 7: the same (only if question 2 is answered "on `main` before the cut").**
    `claude/release-notes-stable-blocks`'s edit 5 replaces the same two lines. If it is applied first, put this
    note after its note instead: the two notes do not overlap in meaning.

### C1. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

````text
- ADR-0042 is rewritten on the release branch, in the change that adds this `release.yml`, with what it
  said kept: its refusal is replaced as above, not quietly dropped. The Definition of Done's Step 7
  clause on the refusal says the same.
````

New:

````text
- ADR-0042 is rewritten on the release branch, in the change that adds this `release.yml`, with what it
  said kept: its refusal is replaced as above, not quietly dropped. The Definition of Done's Step 7
  clause on the refusal says the same.
  *(2026-10-12, decided with the user: the rewrite and this `release.yml` went in on `main` before
  `release/0.1` was cut, so the branch carries them from its cut, as "The release branch" asks.)*
````

### C2. docs/definition-of-done.md

Old:

````text
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)*
````

New:

````text
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)* *(2026-10-12, decided with the user: that change went in on `main`
  before `release/0.1` was cut, and the branch carries it from its cut.)*
````
