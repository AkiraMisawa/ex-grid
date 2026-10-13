# The release mechanics: every ADR, Definition of Done, glossary and project-instruction edit

Branch `claude/release-workflow-checks`, 2026-10-12, with `claude/release-notes-stable-blocks` merged into it and
`origin/main` at `2265cda6` (#82 to #89), so that both land as one pull request. The branch builds the release
workflow's checks (`tools/release/ExGrid.Release`, tested by `tests/ExGrid.Release.Tests` in layer 1, run by
`release.yml` and `docs.yml`) and the Docs Site's notes parser for a release's blocks
(`samples/ExGrid.Docs.Generator/ReleaseNotes.cs`, tested by `tests/ExGrid.Docs.Generator.Tests`). A background
agent edits no ADR, not the Definition of Done, not `CONTEXT.md` and not the project instructions, so every edit they
need is here.

This list replaces `release-workflow-checks-apply.md` and `release-notes-stable-blocks-apply.md`, and the notes
branch's `release-notes-stable-blocks.md`, which are deleted in the commit that adds it: their edits are folded in
below, their questions were decided by the user on 2026-10-12, and their readings are written as decided.
`by-hand-rows-table.md` stays, because the release check reads the table there until the Definition of Done holds
it: **delete it in the change that applies edit 27.**

Each edit is an exact old text and its new text, in whole lines with the target's own indentation, generated and
checked by script against this branch's tip: every old text occurs once in its file at the point it is applied, and
the list applies in order. The list was applied to the working tree and the whole of `tests/ExGrid.Release.Tests`
run against the result, the table then read from the Definition of Done: it passes. Then the working tree was put
back.

## What the user decided on 2026-10-12, and where each is written

1. **A release candidate's notes carry the stable blocks, and its Signed off block takes a pending form of its own**
   that says the sign-off is being made against this candidate: refused on a version without a suffix, and the only
   form a candidate may use. The workflow reads a candidate's blocks by form only, through the Docs Site's build, and
   reads a sign-off in full only on a stable tag, whose commit is the candidate's commit X plus only records and
   notes. A candidate's GitHub release and its section on the site open with a note of their own. ADR-0205's "the
   same checks a stable tag runs" gains a dated note naming the two checks only a stable tag runs; ADR-0202's
   "eighteenth run on `0.1.0-rc.1`" stays. Edits 1, 2, 4, 7, 15 to 20, 25, 28 to 31.
2. **The ADR-0042 rewrite and the new `release.yml` land on `main` before `release/0.1` is cut.** The two
   conditional edits of the workflow branch's list apply: they are edits 22 and 28. Edits 3 and 10 say it too.
3. **Every reading the implementations settled is confirmed and written as decided.** X in full (edits 4, 15); a
   stable version a stable tag is above publishes no site (edits 8, 14, 21); only an `oracleSkip` saying why
   `oracle.ps1` cannot ask a case excuses it (edits 4, 23); `-rc.N` spelt exactly (edits 1, 4, 17); FN-12e as a
   Windows row and a macOS row (edit 27); a record's commit must stay fetchable or the tag is refused (edits 4, 24,
   27); the notes' block order and forms (edits 15, 20); only the build that publishes a version reads its SHOULD
   IDs and §21 items against the Definition of Done (edit 17). The readings the earlier list numbered besides — no
   leading zero, `results.md` naming X by seven digits or more, a candidate judged at its tagged commit, KB-3 on
   macOS a layer-3 run, a record naming its commit — are written the same way (edits 4, 24, 27).
4. **Each run tags the commit it verified**, and the session that made the run pushes the tag; a by-hand record
   names its tag; the release check fetches the record's commit through it. The tag's form is decision (6)'s, which
   replaced the Windows-only form this decision first gave and this branch's widening of it to every platform.
   Edits 4, 24 and 27; the procedure is `docs/specs/exsheet/verify-on-windows-17.md`'s new section "The verified
   commit's tag", on this branch.
5. **WR-10's MudBlazor floor record is indexed in `verification/by-hand.json`** with its paths,
   `:(glob)src/*.MudBlazor/**` and `samples/ExGrid.DemoPages/`, so the release workflow refuses a tag whose Wrappers
   or demo pages changed since the floor run. Edits 4, 24 and 27.
6. **A verified commit's tag is `verified/` followed by the name of the record's directory under `verification/`,
   and nothing else**: one tag for each record directory, neither the platform nor the run's purpose part of the
   rule, so `verification/2026-10-01-windows-browser-11/keys.md` is tagged `verified/2026-10-01-windows-browser-11`.
   The release check refuses a record whose tag is not exactly that. It replaces decision (4)'s form everywhere:
   edits 4, 24 and 27, the procedure's section, the check (`ByHand`) and the index, where SH-42's record is now
   tagged `verified/2026-10-01-windows-browser-11`.
7. **A MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that version
   only**; the floor run, layer 3 at 9.6.0, holds while its paths are unchanged. Edit 24 and edit 27's WR-10 row
   say it in a dated note. WR-10's records name in the index the versions their layer-2 runs covered
   (`versions`). How the release check holds them is this branch's, said in notes of the check's own, not
   credited to the user (edits 4, 24 and 27): it reads each version's `published` date from nuget.org's
   registration index, takes X's time to be X's commit time, and refuses the tag naming each stable version within
   the range the Wrappers declare, listed and published before X's time, that no record names.
8. **The tags of the runs already made are pushed by the orchestrator once this list's pull request has merged**,
   not before and not by a background agent: "For whoever applies it" gives the commands. The Windows session then
   tags the area and readings passes of 2026-10-12 by the same rule.
9. **A layer-2 run at a 9.x published after the floor record is a run of its own.** Its record goes in its own
   `verification/<date>-<name>/` directory, and the run tags the commit it verified `verified/` followed by that
   directory's name, as decisions (4) and (6) say for every run. WR-10's entry in `verification/by-hand.json` names
   that record (commit, tag, path, verdict, the version it covered) besides the floor record. The index names it
   in `laterRecords`, this branch's name for the field.
   The release check reads every record WR-10's entry names: the floor record's paths hold since its commit, and
   each later record's tag names its commit. Edits 24 and 27 say it in dated notes; the check (`ByHand`) and the
   index's `about` hold it.
10. **An unlisted version is not asked for.** The release check reports it as out of the check because it is
    unlisted; a version that is listed but given no date still refuses the tag. Edits 24 and 27 say it in a dated
    note beside decision (7)'s; the check (`WrapperRange`) and the index's `about` hold it.

11. **VZ-18, the check on the remote-isolation (Citrix) PC, is a Known limitation of `0.1.0`.** It is not run,
    and the 2026-10-08 observation, which names no commit, is not taken as its record. Edit 27's VZ-18 row says
    it in a dated note. How the index marks such a row and how the release check reads it are this branch's,
    said in notes of the check's own, not credited to the user (edit 27; the index's `about`; `ByHand`): the
    row's `knownLimitation` names the version, the day and the decision, the row keeps no record, the table's
    row says "Known limitation of" that version, and for that version's candidates and the version the check
    names the row in a notice instead of refusing it. The Definition of Done's own VZ-18 row, §21.11's
    reservation and its "VZ-18 is checked as written", ADR-0202 and the release notes' line are
    `claude/dod-gaps-restatements`' list's, and this list does not edit them. Step 4's ADR-0208 note of
    2026-10-11 still names VZ-18 among the rows checked by hand; the table this list's edit 27 adds right after
    it says the row is a Known limitation of `0.1.0`.

Besides, as the rule every record checked by hand already lives under (ADR-0208, and decision (3) for the floor
record), not a new decision: **a later record holds only while the Wrappers' and the demo pages' paths are
unchanged between its commit and X**, exactly as the floor record is held. Edits 4, 24 and 27 say it in notes of
the check's own, not credited to the user.

Besides: `CONTEXT.md` gains **Release Candidate** (edit 30), and FN-12e's rows name
`src/ExGrid.MudBlazor/MudExGridPresentation.cs` and `src/ExGrid.MudBlazor/RobotoGlyphTable.cs` (edit 27).

## What the verifiers found in the two lists, and what this one does instead

- **An undated insertion into ADR-0205's text of 2026-10-11** ("after the products and in this order (ADR-0190)"):
  the sentence stands as written, and the rule is a dated note after it (edit 20).
- **ADR-0190's "What refuses a mistake" gained refusals undated**, and its list's last line was rewritten: the line
  stands, each refusal of 2026-10-12 is a dated line after it, and what only the publishing build reads is a dated
  note on its bullet (edit 17).
- **ADR-0190's Signed off text** now says what holds for a stable version (five labelled lines, X a candidate's
  commit in full, one record and one commit, never the pending form) and what a candidate's pending form is (one
  `**Pending:**` line naming the candidate by its tag, and no record and no commit), in a dated note under the
  two-block bullet, which stands as written; the blocks of 2026-10-11 are a note of their own date (edit 15).
- **ADR-0205's note on the two stable-only checks** is edit 18.
- **Every note is dated and says it was decided with the user**, the workflow list's edit 9 among them, which had
  carried no date (edit 10). The ADR-0042 rewrite's own note quotes the note it replaces (edit 3).
- **The by-hand table** carries FN-12e's Wrapper paths and WR-10's row, and the whole section is a dated addition
  (edit 27).
- **The two lists' Step 7 edits**, which replaced the same lines, are one edit with two dated notes (edit 28).
- **The procedure's revision note named an eighteenth run's procedure that does not exist.** It now says that a
  later run's procedure, when one is written, carries the section.
- **This list did not say how ADR-0208 is divided with `claude/mudblazor-range-floor`'s list**, and said the
  release workflow "reads it too". The section below says what each list says.

## Where this list meets other branches' lists

- **`claude/mudblazor-range-floor`.** Its edits touch ADR-0022, ADR-0205's "The Wrappers' MudBlazor range" and the
  Consequences bullet after edit 22's, ADR-0208's "Every automated layer still runs in full" (its edit 8), and in the
  Definition of Done PRE-3, Step 4's Discharges paragraph (after edit 27's table), §23 and PV-1: none of this list's
  old texts, and none of its old texts is changed here, so the two apply in either order, to the same result.
  **Edit 27's WR-10 row presumes that list's edit 6**, which adds WR-10 to §23; apply both.
  - **ADR-0208 is divided between the two.** That list's edit 8, under "Every automated layer still runs in full",
    keeps only the pointer to ADR-0205's exception, under which the floor run, with the layer-2 runs recorded
    beside it, is carried forward by its record. It does not state the release workflow's check. The check is this
    list's edit 24 alone, under "The release workflow checks it": WR-10 indexed beside the rows, its paths
    compared, every record its entry names read (decision (9)), and the MudBlazor versions those records name held
    to the versions nuget.org's registration index dates before X (decision (7), as this branch's check reads it).
  - **The Definition of Done likewise.** That list's Step 4 floor paragraph (its edit 5) says how the floor run
    and the layer-2 runs are made, recorded, indexed and tagged, and how a person reads with `git diff --quiet`
    whether their record holds; its
    §23 row (its edit 6) is the criterion. This list's edit 27 says what the release workflow refuses: the row's
    paths changed since the floor record, a record the entry names that does not hold, and a version published
    before X that no record names.
- **ADR-0208.** A list from the run-17 or area branches that edits "Questions are asked when the feature is built"
  or "The release workflow checks it" would meet edits 23 and 24's anchors.

## For whoever applies it

- **Push the tags of the runs already made once this list's pull request has merged** (decision (8)): not before,
  and not from a background agent's session. Each is `verified/` followed by the name of its record's directory
  (decision (6)). The record index names the first three, and the release check refuses a candidate until they can
  be fetched; the seventeenth run's records name the fourth once they are indexed.

  ```sh
  git tag -a verified/2026-10-01-windows-15 16552992f8fa6838114b7934c81190b788b3f32c -m "The commit verification/2026-10-01-windows-15/ verified"
  git tag -a verified/2026-10-02-windows-16 bf13de1950975a5ec5f1ce9db675358db9e97aa9 -m "The commit verification/2026-10-02-windows-16/ verified"
  git tag -a verified/2026-10-01-windows-browser-11 76d3866c3233fc76b79956f157c2e3f7ee18a14a -m "The commit verification/2026-10-01-windows-browser-11/ verified"
  git tag -a verified/2026-10-11-windows-17 8436e45e73083944e413e92340f73dbba0b521c4 -m "The commit verification/2026-10-11-windows-17/ verified"
  git push origin verified/2026-10-01-windows-15 verified/2026-10-02-windows-16 verified/2026-10-01-windows-browser-11 verified/2026-10-11-windows-17
  ```

  The Windows session then tags the area and readings passes of 2026-10-12 by the same rule, each at the commit
  its report names: `verified/2026-10-12-windows-area`, `verified/2026-10-12-windows-area-2` and
  `verified/2026-10-12-windows-readings`.
- **WR-10's row has no record.** `verification/2026-10-12-linux-mudblazor-floor/` (on `claude/mudblazor-range-floor`)
  is a bisection on Playwright's bundled Chromium, and says itself that it does not discharge the floor run. Until
  the floor run is made and indexed, the release check refuses a candidate on WR-10, as it does on every row never
  recorded. When it is indexed, its record names besides the MudBlazor versions its layer-2 runs covered,
  `"versions": ["9.6.0", …]`, which the release check holds to the versions published before X (decision (7)).
- **A later layer-2 run's record** (decision (9)). It is a run of its own, in a directory of its own, tagged by
  it, and WR-10's entry names it in `laterRecords`, beside `record`, the floor record:
  `{"commit": …, "tag": "verified/<its directory>", "path": …, "verdict": "pass", "versions": ["9.12.0"]}`. The
  check reads each as it reads the floor record, its paths compared from its own commit, and refuses two records
  the entry names in one directory. `claude/mudblazor-range-floor`'s edit 5 says how the run is made.
- **A later record's commit is held to the Wrappers at X** by ADR-0208's rule, as the floor record is: its paths
  unchanged between its commit and X. A later run made at a commit whose Wrappers differ, as on a branch never
  merged, is refused naming the files, and its versions are not counted.
- **Live, against nuget.org, 2026-10-12.** In a scratch repository whose Wrappers declare `[9.6.0,10.0.0)` and
  whose floor record, made 2026-07-01 and naming 9.6.0 alone, is indexed: at an X committed 2026-09-01 the check
  named 9.7.0 (2026-07-09), 9.8.0 (2026-08-05) and 9.9.0 (2026-08-24) as published after the floor record and before
  X, and said 9.10.0 and 9.11.0 were published after X and not asked for; at an X committed that day it named 9.7.0
  to 9.11.0; with a floor record naming 9.6.0 to 9.11.0 it let WR-10 hold.
- **Noticed, not changed:** ADR-0042's "What a package must prove" names `_content/ExGrid/ex-grid.css`, `ex-grid.js`
  and `mud-ex-grid.css`, and packs "both packages", while `check.sh` packs ten and checks their minified builds
  (ADR-0123). That is stale on `main` and not this list's.

## The edits, in the order they apply

1. **ADR-0042, Decision: the release candidate, what it runs, and the notes it carries.** Decision (1), and decision (3)'s reading that `-rc.N` is spelt exactly. The note of 2026-10-11 is ADR-0205's; the note of 2026-10-12 names the two checks a candidate does not run, its pending Signed off form and its note.
2. **ADR-0042, Decision: what a release candidate promises.** ADR-0205's and ADR-0208's promises of 2026-10-11, and, dated 2026-10-12, decision (1): a candidate is not signed off, and its Signed off block says the sign-off is being made against it.
3. **ADR-0042, "How a version is published": the note that announced the rewrite becomes the rewrite's own.** Decision (2): the rewrite lands on `main` before `release/0.1` is cut. The announcing note is quoted, since it said where the rewrite would be made and that is what changed.
4. **ADR-0042, "How a version is published", step 1: what each kind of version must carry.** The rewrite of step 1, with the readings decision (3) confirmed (X in full; `-rc.N` spelt exactly; no leading zero; only an `oracleSkip` saying why `oracle.ps1` cannot ask a case excuses it; a record's commit fetchable or the tag refused; a record naming its commit), decision (1) (a candidate's blocks read by form only; a sign-off read in full only on a stable tag; X a candidate's commit), decision (4) (the commit fetched through its `verified/*` tag), decision (5) (WR-10 indexed beside the rows), decision (6) (the tag named for the record's directory, or refused) and the check of decisions (7) and (9) (every record WR-10's entry names read, and the versions they name held to those nuget.org's registration index dates before X). The old step is quoted at its end.
5. **ADR-0042, "How a version is published", step 2: the long run.** ADR-0205's long run, decided 2026-10-11.
6. **ADR-0042, "How a version is published", step 4: a stable version's release is a release.** ADR-0205, decided 2026-10-11.
7. **ADR-0042, "The release's text and the Docs Site": the candidate's note.** Decision (1): a candidate's GitHub release opens with a note of its own, not the plain prerelease note.
8. **ADR-0042, "The release's text and the Docs Site": which releases publish the site.** ADR-0205's rule of 2026-10-11, and decision (3)'s reading that a stable version a stable tag is above publishes no site, a stable version existing once the repository has a tag without a suffix.
9. **ADR-0042, "The family ships together": a stable version still waits, on its sign-off.** ADR-0200 and ADR-0205, decided 2026-10-11.
10. **ADR-0042, "What a stable version needs besides Step 7": the refusal, replaced.** Decision (2) dates it: the rewrite that replaces the refusal lands on `main` on 2026-10-12.
11. **ADR-0042, Rejected: an unsuffixed `0.1.0`, before and after the sign-off.** ADR-0205, decided 2026-10-11.
12. **ADR-0042, Rejected: two options the rewrite turned down.** ADR-0205's, decided 2026-10-11.
13. **ADR-0042, Consequences: the tag is X plus its records, and the checks are code.** ADR-0205's structural check (2026-10-11), and the checks' home and tests, confirmed with decision (3).
14. **ADR-0110, Publishing: which releases publish the site once a stable version exists.** ADR-0205's Consequences ask for it ("ADR-0110: a prerelease publishes no site once a stable version exists"), with decision (3)'s reading that a stable version a stable tag is above publishes none.
15. **ADR-0190, "What a version says": the candidate's note, the candidate's own Signed off form, and every block's place and form.** Decision (1) (the candidate's note; the pending form; a candidate's blocks read by form), and decision (3)'s reading of the blocks' order and forms. The two-block bullet stands as written: the blocks ADR-0202 and ADR-0205 added on 2026-10-11, never written here, are a dated note under it, and what was decided on 2026-10-12 is a dated note after that, which says what holds for a stable version and what a candidate's pending form is.
16. **ADR-0190, "Where they are published": the release's text of a candidate.** Decision (1): the candidate's note opens a candidate's GitHub release, as it opens its section on the site.
17. **ADR-0190, "What refuses a mistake": what the build refuses besides, named in dated notes.** The refusals the build gained on 2026-10-12, each a dated line after the list's own, and what only the publishing build reads, a dated note on its bullet: decision (1) and the last reading of decision (3). The list's last line stands as written.
18. **ADR-0205, "The release branch": the two checks only a stable tag runs.** Decision (1): "the same checks a stable tag runs" gains a dated note naming the two checks only a stable tag runs, with the candidate's pending Signed off form and its note.
19. **ADR-0205, "The release branch": X is the candidate's commit.** Decision (1): the stable tag's commit is the candidate's commit X plus only records and notes.
20. **ADR-0205, "What a stable version's notes carry": where the blocks stand, and a candidate's.** The finding: "after the products and in this order (ADR-0190)" was an undated insertion into the text of 2026-10-11. It moves into a dated note, with decision (1)'s pending form and decision (3)'s reading of the order.
21. **ADR-0205, "After 0.1.0": no site from a stable version a stable tag is above.** Decision (3)'s reading that a stable version a stable tag is above publishes no site.
22. **ADR-0205, Consequences: the rewrite went in on `main`.** Decision (2), with which the conditional edit C1 of the workflow branch's list applies.
23. **ADR-0208, "Questions are asked when the feature is built": which `oracleSkip` excuses a case.** Decision (3)'s reading that only an `oracleSkip` saying why `oracle.ps1` cannot ask a case excuses it. ADR-0208 says an `oracleSkip` excuses a case; without this note it and the check would quietly differ.
24. **ADR-0208, "Checks by hand are repeated only when the code they cover changes": how the check reads the records.** Decisions (3) to (7), (9) and (10): the index, a record's commit fetched through its `verified/*` tag, named for the record's directory, or the tag refused, a candidate judged at its tagged commit, WR-10's floor record indexed beside the rows, a MudBlazor 9.x published after it and before X asking for the layer-2 runs at that version only, that run a run of its own whose record WR-10's entry names besides the floor record, and an unlisted version not asked for. How the check holds the versions, by nuget.org's published dates and X's commit time, is a note of the check's own.
25. **Definition of Done, §1: a release candidate is no release either.** The notes branch's edit 4, with decision (1)'s pending form and the candidate's note.
26. **Definition of Done, §1 Evidence: one `.trx` per test project, not twelve.** Not a decision, a count that went stale: the solution holds fourteen test projects with `tests/ExGrid.Release.Tests`. Step 1's loop copies every project's `results.trx` without naming them, so it needs no change.
27. **Definition of Done, §22 Step 4: the table of rows checked by hand, and the code each covers.** The table ADR-0208 asks for, word for word as `docs/agents/proposals/by-hand-rows-table.md` holds it: decisions (3) to (7) — FN-12e as a Windows row and a macOS row, both with the MudBlazor Chrome's Roboto metrics (`MudExGridPresentation.cs`, `RobotoGlyphTable.cs`), a record's commit kept fetchable by its `verified/*` tag, named for the record's directory, and WR-10's row, whose dated notes say that a MudBlazor 9.x published after its record and before X asks for the layer-2 runs at that version only, and that such a run is a run of its own whose record WR-10's entry names besides the floor record (decision (9)), and that an unlisted version is not asked for (decision (10)); and VZ-18's row, whose dated note says it is a Known limitation of `0.1.0` (decision (11)). The whole section is a dated addition of 2026-10-12; what the release workflow reads for WR-10, and how it reads a row marked a Known limitation, are notes of the check's own. Delete the proposal file in the same change.
28. **Definition of Done, §22 Step 7: the sign-off is made against a candidate, and the rewrite went in on `main`.** The notes branch's edit 5 and the workflow branch's conditional edit C2 replaced the same lines; they are one edit here, two dated notes, with decision (1) (the candidate's pending form; X the candidate's commit) and decision (2).
29. **CONTEXT.md, Release Notes: what a stable version's and a candidate's notes say.** The notes branch's edit 6, with decision (1)'s pending form and the candidate's note.
30. **CONTEXT.md, Release Candidate: the new term.** AGENTS.md: a new term goes into the glossary. "Release candidate" is the one kind of version whose name holds "release" and that is a prerelease, which Release Notes' _Avoid_ line warns about.
31. **AGENTS.md (CLAUDE.md is a link to it), Toolchain: what a tag does.** Not an ADR or the Definition of Done, but a background agent does not edit the project instructions either. Once the rewrite lands, the sentence it replaces is false.
32. **Definition of Done, §22 Step 2: the release tool reads the run.** Not a decision and no rule changed: the DoD audit of 2026-10-12 found that Step 2 had no mechanical reader of the skips. The release tool's `skips` subcommand reads Step 1's `.trx` files against the tests Step 2 names; the note says how, and that the release workflow, which does not run Step 1, does not call it. A note of the tool's own, not credited to the user.

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
  branch. A candidate is a prerelease, and ships as one.)*
  *(2026-10-12, decided with the user, ADR-0205 and
  [ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md):
  a candidate runs every check a stable tag runs but the two that read a sign-off ("How a version is
  published", step 1). Its notes carry the blocks a version without a suffix must, its Signed off block
  in a pending form of its own that says the sign-off is being made against this candidate, and its
  GitHub release opens with a note of its own, not the prerelease note. A candidate is written exactly
  `-rc.N`, N from 1: any other suffix that begins with `rc` is refused.)*
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
  *(2026-10-11, decided with the user, ADR-0205 and
  [ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md):
  a release candidate promises more. Its commit is on `main` or a release branch. CI's long run passed
  on it. No question to Excel is open in ExSheet's corpus at it. Every row the Definition of Done checks
  by hand holds there on a record whose code is unchanged.)*
  *(2026-10-12, decided with the user: a candidate is still not signed off. The sign-off is being made
  against it, as its notes' Signed off block says in its pending form, and the version without a suffix
  that follows is that candidate's commit plus the sign-off's records and its notes.)*
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
````

### 5. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
````

New:

````text
  2. It runs the whole of `ci.yml` on that commit: layers 1–3, and the `package` job below.
     *(2026-10-11, decided with the user, ADR-0205: for a version without a suffix and for a release
     candidate, with the long run, `long: true`: the soak on both browsers and both hosts, and ST-1 at
     10⁶ rows. If it fails, nothing is published. The records stay the Definition of Done's: a watched
     Step 4 and a local Step 5 (ADR-0041). The long run is the tag's own proof, at the commit it
     publishes.)*
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
     *(2026-10-11, decided with the user, ADR-0205: for a version without a suffix, the GitHub release
     is a release, not a prerelease, and carries no prerelease note. A release GitHub's page made is
     marked a release.)*
````

### 7. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
  - **Step 4's text is the Release Notes**, passed with `--notes-file`, after the prerelease note for a
    prerelease. The prerelease note is held once, in `docs/releases/prerelease-note.md`. GitHub's
    generated list is no longer asked for. A release GitHub's page already made is given that text,
    replacing what it said, rather than having the note put above it.
````

New:

````text
  - **Step 4's text is the Release Notes**, passed with `--notes-file`, after the prerelease note for a
    prerelease. The prerelease note is held once, in `docs/releases/prerelease-note.md`. GitHub's
    generated list is no longer asked for. A release GitHub's page already made is given that text,
    replacing what it said, rather than having the note put above it.
    *(2026-10-12, decided with the user: a release candidate's text opens with a note of its own, the
    candidate's note, held once in `docs/releases/candidate-note.md`: what a candidate promises, and that
    the sign-off is being made against it. The prerelease note opens an alpha's or a beta's, and a
    version without a suffix opens with neither.)*
````

### 8. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

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
````

### 9. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

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
  *(2026-10-11, decided with the user: the first stable version is `0.1.0`, and it carries all ten
  packages ([ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)), so
  §27, §29 and §30 gate it beside ExGrid's sections. "The refusal of an unsuffixed version stays" now
  reads: a stable version still waits, and the workflow refuses it until its notes name its sign-off
  ("How a version is published", step 1; ADR-0205).)*
````

### 10. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **Removing the prerelease-only refusal from `release.yml`**, in the same change that rewrites
  this ADR.
````

New:

````text
- **Removing the prerelease-only refusal from `release.yml`**, in the same change that rewrites
  this ADR.
  *(2026-10-12, decided with the user: done in the rewrite of "How a version is published", which lands
  on `main` before `release/0.1` is cut, and not as a removal: the refusal is replaced by the check of
  the sign-off in its step 1 (ADR-0205).)*
````

### 11. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
````

New:

````text
- **An unsuffixed `0.1.0`.** Under SemVer that is a release, and under the Definition of Done a
  release waits for Step 7. The suffix is what keeps a beta from quietly redefining the word.
  *(2026-10-11, decided with the user, ADR-0205: still rejected before the sign-off. An unsuffixed
  `0.1.0` is now tagged once its notes name a sign-off the workflow can check, and on nothing else.)*
````

### 12. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.
````

New:

````text
- **Rebuilding in the publish job.** That would ship a package no test ever loaded.
- *(Added 2026-10-11, decided with the user, ADR-0205.)* **Removing the refusal of an unsuffixed version
  at the sign-off**, as this ADR first said. The change that removed it could not be the commit that was
  verified.
- *(Added 2026-10-11, decided with the user, ADR-0205.)* **Trusting the notes' Signed off block without
  reading the repository.** A block naming a record that was never filed, or a commit the tag has moved
  on from, would publish a stable version nobody signed off. The workflow reads both.
````

### 13. docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md

Old:

````text
- **The first publish is a person's act.** Someone creates the nuget.org policy and the
  `NUGET_USER` secret once, then pushes the tag.
````

New:

````text
- **The first publish is a person's act.** Someone creates the nuget.org policy and the
  `NUGET_USER` secret once, then pushes the tag.
- *(Added 2026-10-11, decided with the user, ADR-0205.)* **A stable version's tag is the sign-off's
  commit plus its records and notes**, and the release workflow checks it. The Definition of Done's
  Step 7 clause on the refusal reads the same.
- *(Added 2026-10-12, decided with the user.)* **The release workflow's checks are code with tests.**
  They are `tools/release/ExGrid.Release`, and their tests, `tests/ExGrid.Release.Tests`, run in layer 1
  on every push. A tag is therefore not the first time they run.
````

### 14. docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md

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
    and 2026-10-12, decided with the user, for a stable version that a stable tag is above.)* **Once a
    stable version exists, a prerelease's release publishes no site, and neither does a stable version
    that a stable tag is above.**
    - The site describes the latest stable version. A prerelease's notes are on its GitHub release,
      and reach the site with the next stable publish.
    - A stable version exists once the repository has a tag without a suffix.
    - A patch to an older line, such as `0.1.1` from `release/0.1` after `0.2.0`, publishes no site:
      versions are compared by SemVer precedence, and its pages would replace a later version's.
    - `release.yml`'s first job decides it, and says so. `docs.yml` refuses a call that comes anyway,
      and refuses too when it cannot read the repository's tags.
    - The on-demand publish of `main` or a `lab/*` branch stays, under its banner.
````

### 15. docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md

Old:

````text
- **A prerelease's notes begin with the prerelease note.** It is held once, in
  `docs/releases/prerelease-note.md`, with a `{ref}` where the commit goes, and says what ADR-0042
  promises of a beta: every layer passed in CI on this commit, the Definition of Done is not signed
  off, and what it still owes is `docs/implementation-status.md` at that commit. The release workflow
  and the Docs Site both read that file, so the two cannot say different things.
- *(2026-10-12, decided with the user.)* **A release candidate rehearses a stable version's notes.** An
  `-rc.N` version's notes carry the blocks a version without a suffix must carry, and the build checks
  them as it checks a stable version's, so ADR-0205's rehearsal covers the notes too. An `-alpha` or
  `-beta` version's notes are refused if they carry any of them.
- **A stable version's notes carry two more blocks**, which the build requires of a version without a
  suffix (Definition of Done §1 and Step 7):
  - **Signed off** — the `verification/<date>/results.md` the sign-off was made against;
  - **SHOULD criteria accepted failing** — each criterion's ID and the written reason, or "None."
````

New:

````text
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
    changes, Known limitations, ExGrid, ExSheet, ExPivot, Data, Signed off, SHOULD criteria accepted
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
````

### 16. docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md

Old:

````text
- **On GitHub, as the release's text.** The release workflow renders the prerelease note (for a
  prerelease) followed by the version's file, with the site's routes made absolute, and passes it with
  `--notes-file`. It no longer asks for `--generate-notes`.
````

New:

````text
- **On GitHub, as the release's text.** The release workflow renders the prerelease note (for a
  prerelease) followed by the version's file, with the site's routes made absolute, and passes it with
  `--notes-file`. It no longer asks for `--generate-notes`.
  *(2026-10-12, decided with the user: for a release candidate it renders the candidate's note instead,
  and the site's section of a candidate opens with that note too.)*
````

### 17. docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md

Old:

````text
  - `Published: pending` on any file but the newest;
  - a version without a suffix whose notes lack the Signed off and SHOULD blocks.
- **When the build is given the version it publishes** (`-p:DocsVersion=` on a tag), it also refuses a
  missing file for that version, and any Change Note still under `docs/releases/unreleased/`.
- **The release workflow's first job refuses the tag** before CI's twenty minutes when the tagged commit
  has no `docs/releases/<version>.md`, still holds a Change Note, or has an older file still `pending`.
````

New:

````text
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
````

### 18. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

````text
  *(2026-10-12, decided with the user: the rehearsal includes the stable version's release notes. A
  release candidate's notes carry the blocks a stable version's must (ADR-0190's note of this date),
  checked as a stable version's are, so the text the GitHub release will show is seen before the tag.)*
````

New:

````text
  *(2026-10-12, decided with the user: the rehearsal includes the stable version's release notes. A
  release candidate's notes carry the blocks a stable version's must (ADR-0190's note of this date),
  checked as a stable version's are, so the text the GitHub release will show is seen before the tag.)*
  *(2026-10-12, decided with the user: **two checks are a stable tag's only**, the two that read a
  sign-off, since a candidate has none yet. They are the Signed off block read in full — the
  `results.md` at the tag, X in full, the record naming X, X the commit a candidate of the version was
  tagged on — and the structural check, that the tag is X plus only `verification/` and
  `docs/releases/`. A candidate's Signed off block takes a pending form of its own, saying that the
  sign-off is being made against this candidate, and the workflow reads a candidate's blocks by form
  only, through the Docs Site's build. A candidate's GitHub release and its section on the site open
  with a note of its own, not the prerelease note. ADR-0202's "the eighteenth run … on `0.1.0-rc.1`
  itself" stays: that run is made at the candidate's commit, which the sign-off is made against.)*
````

### 19. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

````text
- **The sign-off is made against a commit of the branch, X.** Every step of the Definition of Done's §22
  runs at X; `results.md` names X.
````

New:

````text
- **The sign-off is made against a commit of the branch, X.** Every step of the Definition of Done's §22
  runs at X; `results.md` names X.
  *(2026-10-12, decided with the user: X is the commit a release candidate was tagged on. The sign-off
  is made against that candidate, whose notes say so, and the release workflow refuses a stable tag
  whose X no `-rc.N` of the same version was tagged on.)*
````

### 20. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

````text
The Docs Site's notes parser requires each of them of an unsuffixed version.
````

New:

````text
The Docs Site's notes parser requires each of them of an unsuffixed version.
*(2026-10-12, decided with the user: they stand after the products, in the order above, each in the
form ADR-0190's note of this date gives, Known limitations standing after Behaviour changes. The parser
requires them of a release candidate too, whose notes rehearse them, its Signed off block in a pending
form of its own that says the sign-off is being made against this candidate, and refuses them on an
`-alpha` or a `-beta`.)*
````

### 21. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

````text
- **Once a stable version exists, a prerelease does not publish the Docs Site.** The site describes the
  latest stable version; a prerelease's notes are on its GitHub release, and reach the site with the next
  stable publish. The on-demand publish of `main` or a `lab/*` branch stays, under its banner (ADR-0110).
````

New:

````text
- **Once a stable version exists, a prerelease does not publish the Docs Site.** The site describes the
  latest stable version; a prerelease's notes are on its GitHub release, and reach the site with the next
  stable publish. The on-demand publish of `main` or a `lab/*` branch stays, under its banner (ADR-0110).
  *(2026-10-12, decided with the user: nor does a version without a suffix that a stable tag is above by
  SemVer precedence, a patch to an older line, so `0.1.1` from `release/0.1` after `0.2.0` leaves
  `0.2.0`'s site in place. A stable version exists once the repository has a tag without a suffix.)*
````

### 22. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

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
  *(2026-10-12, decided with the user: the rewrite and this `release.yml` go in on `main` before
  `release/0.1` is cut, so the branch carries them from its cut, as "The release branch" asks.)*
````

### 23. docs/adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md

Old:

````text
  - The remedy is a run of `oracle.ps1 -Area` over the files that hold such cases, which takes minutes.
    The whole corpus is never put to Excel again.
````

New:

````text
  - The remedy is a run of `oracle.ps1 -Area` over the files that hold such cases, which takes minutes.
    The whole corpus is never put to Excel again.
  - *(2026-10-12, decided with the user, with the release workflow's check,
    [ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)'s "How a
    version is published".)* **Only an `oracleSkip` that says why `oracle.ps1` cannot ask a case excuses
    it.** One that says "ask by keys" names a case `oracle.ps1 -Keys` asks, and `engineDiffersByDecision`
    names a case `oracle.ps1` asks and compares; both stay open until a run has answered them and a
    person has marked the case `observed`. A release candidate is refused on the same terms as a version
    without a suffix.
````

### 24. docs/adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md

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
  *(2026-10-12, decided with the user, with the check, ADR-0042:
  - **Each row's last record is indexed** in `verification/by-hand.json` with the row's paths, which
    the Definition of Done's Step 4 table names too; a tag where the two differ is refused. A record
    names the commit it verified, in full in the index, and the record's own file names it too.
  - **Each run tags the commit it verified** `verified/` followed by the name of its record's directory
    under `verification/`, and nothing else, one tag for each record directory, whatever the platform or
    the run's purpose: `verification/2026-10-01-windows-browser-11/keys.md` is tagged
    `verified/2026-10-01-windows-browser-11`. The session that made the run pushes the tag. The record
    names its tag, a record whose tag is not exactly that is refused, and the release workflow fetches the
    record's commit through it. A record's commit must stay fetchable: a tag the workflow cannot fetch,
    or one that names another commit, refuses the tag as unchecked, and never lets the row hold.
  - **A release candidate**, which has no sign-off, is judged at its tagged commit.
  - **WR-10's floor run is indexed beside the rows**, with its paths, `:(glob)src/*.MudBlazor/**` and
    `samples/ExGrid.DemoPages/`, so that the release workflow refuses a tag whose Wrappers or demo pages
    changed since the floor run
    ([ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s notes of this
    date).
  - **A MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that
    version only.** The floor run, layer 3 at 9.6.0, holds while its paths are unchanged.)*
  *(2026-10-12, decided with the user: **an unlisted version is not asked for.** The release check reports it
  as out of the check because it is unlisted.)*
  *(2026-10-12, decided with the user: **a layer-2 run at a 9.x published after the floor record is a run of
  its own.** Its record goes in its own `verification/<date>-<name>/` directory, and the run tags the commit
  it verified `verified/` followed by that directory's name, as every run does. WR-10's entry in
  `verification/by-hand.json` names that record (commit, tag, path, verdict, the version it covered) besides
  the floor record. The release check reads every record WR-10's entry names: the floor record's paths hold
  since its commit, and each later record's tag names its commit.)*
  *(2026-10-12, how the release workflow checks the notes above: each record WR-10's entry names is a pass,
  names its commit, and is fetched through its tag, which names that commit; each holds while the row's
  paths are unchanged between its own commit and X, as every record checked by hand is held; and two
  records in one directory are refused. Each record names the MudBlazor versions its layer-2 runs covered,
  and the check holds the union of them to
  what nuget.org has published: it reads the range each Wrapper's project declares on its reference to
  MudBlazor, reads each version's `published` date from nuget.org's registration index, takes X's time to be
  X's commit time (for a release candidate, the commit it is tagged on), and refuses the tag naming each
  stable version inside the range, published before X's time, that no record names. A version published
  after X's time is not asked for, nor is an unlisted one, which a notice names. One listed and given no
  date is asked for, since whether it came before X cannot be said. One published before the floor record
  is asked of the floor run's layer-2 runs, which cover every published version of the range. An index it
  cannot read refuses the tag as unchecked.)*
````

### 25. docs/definition-of-done.md

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
a release candidate, `0.1.0-rc.N`, is a prerelease too, and no release either: the sign-off is made
against it. Its notes rehearse the release's. They carry the blocks a version without a suffix must —
its known limitations, the SHOULD criteria accepted failing with their reasons, what is out of its scope
and what it supports — and a Signed off block in a pending form of its own, saying that the sign-off is
being made against this candidate. They open with the candidate's note, not the prerelease note. An
alpha's or a beta's notes carry none of the blocks.)*
````

### 26. docs/definition-of-done.md

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

### 27. docs/definition-of-done.md

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

*(Added 2026-10-12, decided with the user: the table
[ADR-0208](adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md)
of 2026-10-11 asks for, written with the release workflow's check, and what that check reads, confirmed the
same day: FN-12e as two rows, a record's commit kept fetchable by a tag named for the record's directory, and
WR-10's floor run indexed beside the rows.)* Each row a person checks, where it is checked, and the paths whose
change could change its result.

- **A row's latest record holds while its paths are unchanged.** That means unchanged between the
  commit the record was made at and the commit being signed off, or, for a release candidate, the
  commit it is tagged on.
- **The release workflow compares them** (`git diff --quiet`). It refuses a version without a suffix,
  and a release candidate, when:
  - a path changed;
  - the row has no record, and is not marked a Known limitation of the version being released *(2026-10-12,
    how the release workflow reads a row put out of a version by decision: such a row is named in a notice and
    never holds; see the index below)*;
  - its latest record is not a pass;
  - its record does not name the commit the index gives it;
  - the record's tag is not `verified/` followed by the name of the record's directory;
  - that commit cannot be fetched through the record's tag, or the tag names another commit, so whether
    its paths changed cannot be checked;
  - a path names nothing;
  - for WR-10, a record its entry names besides the floor record is refused as the floor record would be,
    its paths compared from its own commit; or a stable MudBlazor version within the range the Wrappers
    declare, listed on nuget.org and published before X's commit time by its registration index or given no
    date there, is not among the versions its records' layer-2 runs covered; or that index cannot be read
    *(2026-10-12, how the release workflow checks the WR-10 row's notes below)*.
- **The records are indexed in `verification/by-hand.json`.** For each row it names the same paths as
  this table, the row's latest record, the commit that record was made at, and the record's tag. *(2026-10-12,
  how the release workflow checks the WR-10 row: for WR-10 each record names besides the MudBlazor versions
  its layer-2 runs covered, and the entry names the records of later layer-2 runs, by the row's notes,
  besides the floor record.)* *(2026-10-12, how the release workflow reads a row put out of a version by
  decision: the index marks it `knownLimitation`, naming the version without a suffix, the day of the decision
  and its words, and gives it no record, and this table's row says "Known limitation of" that version. The
  mark reaches that version's release candidates and the version, and no other; the workflow refuses it on a
  row that also has a record, without its date or its words, or where this table does not say the same.)* A
  layer-1 test (`tests/ExGrid.Release.Tests`) fails when the index and the table differ, and the release
  workflow refuses a tag where they do.

**The paths are drawn wide** (ADR-0208). A row names whole files, and a directory where its behaviour
crosses several:

- the grid's script and stylesheet sources, and their minified builds, which are what a browser runs;
- the keyboard, editor and toolbar files;
- the measuring code;
- the spec a row runs;
- the DemoHost's or the Docs Site's page, only where the row reads that page.

When in doubt, the path goes in. The list is reviewed with the row, and in any change that moves the
code it names. A path is a file, a directory ending in `/`, or a pattern git reads as a glob, written
`:(glob)<pattern>`, where `*` stays within a directory and `**` crosses them: `:(glob)src/*.MudBlazor/**` is
every file below each Wrapper.

**A run that records a row** writes its record under `verification/`, in a directory named for the run, as
`verification/2026-10-11-windows-17/`, naming the commit it verified in full or by seven digits or more. It
tags that commit `verified/` followed by the name of that directory *(2026-10-12, decided with the user: the
tag is `verified/` followed by the name of the record's directory under `verification/`, and nothing else, one
tag for each record directory, whatever the platform or the run's purpose; a record whose tag is not exactly
that is refused)*: `verification/2026-10-01-windows-browser-11/keys.md` is tagged
`verified/2026-10-01-windows-browser-11`, and a later `verification/2026-11-02-macos-kb3/`,
`verified/2026-11-02-macos-kb3`. The session that made the run pushes the tag, which is never moved, so a squash
merge or a deleted branch cannot take the commit away. In the same commit as the record, it replaces the row's
record in `verification/by-hand.json`, naming the commit, the tag, the file and the verdict. The release
workflow fetches the commit through the tag.

**FN-12e is two rows.** On Windows it keeps the measurement as well as the painting while
`tests/GlyphWidths` holds no `system-ui.windows.json`, so that its MUST, the widest glyph measured on each
platform, never passes on a painting alone; once that record is committed and held by layer 1, the Windows
row narrows to the painting in the same change. On macOS, whose widths are recorded and held by layer 1, it
checks the painting.

**WR-10's floor run is indexed beside the rows**
([ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s
notes of 2026-10-12). It is automated, but a person makes it once, not CI, and its record carries it
forward while the Wrappers and the demo pages are unchanged, so the release workflow reads its paths as a
row's and refuses a tag whose Wrappers or demo pages changed since the run.

| Row | Where | Checked by hand | The code it covers |
|---|---|---|---|
| **VZ-14** | Windows | `scrollbar.spec.mjs`'s VZ-14 block at a fractional display scale, headed, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `tests/ExGrid.Browser/fixtures.mjs`, `tests/ExGrid.Browser/scrollbar.spec.mjs` |
| **VZ-16** | Windows | A real browser zoom told to the grid as it changes, on a page that is only displayed. A thin line and a gridline in device pixels at a real 2.25: the display at 150%, the browser's zoom at 150%, on `/sheet?case=11` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Components/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetAppearance.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **FN-12e** | Windows | The widest glyph of each `CellTextMetrics` class, measured in Chrome and Edge, while the Windows widths are not recorded (no `system-ui.windows.json` in `tests/GlyphWidths`). On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExGrid.MudBlazor/MudExGridPresentation.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **FN-12e** | macOS | On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell. The macOS widths are measured already, `tests/GlyphWidths/*.macos.json` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExGrid.MudBlazor/MudExGridPresentation.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **ED-30** | Windows | A real IME on `/sheet` and `/sheet?chrome=mud`, on Chrome and Edge, on both hosts and behind 150 ms: the sixteenth run's cases k1 to k16, and DC-47's | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormulaAids.cs`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-42** | Windows | Ctrl+1 to Ctrl+5 reach `/sheet` before the browser's tab switching, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-51** | Windows | Ctrl+F1 reaches `/sheet`, and the toolbar hides and shows | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-52** | Windows | Alt released alone, and F10, show the KeyTips on `/sheet` under both Chromes, and the browser's menu does not open. Each letter is read beside Excel's Home tab. Nothing starts while an edit is open | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **KB-3** | macOS | Layer 3 on a Mac, where `ControlOrMeta` presses Command: Cmd+Enter fills, on every key path, the editor's included | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `src/ExSheet/SheetStructureKeys.cs`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `tests/ExGrid.Browser/` |
| **VZ-18** | the Citrix PC | Ctrl+↓, Ctrl+↑ and PageDown on the Docs Site's blotter paint the rows they reveal, without a further scroll. *(2026-10-12, decided with the user: VZ-18, the check on the remote-isolation (Citrix) PC, is a Known limitation of `0.1.0`. It is not run, and the 2026-10-08 observation, which names no commit, is not taken as its record.)* | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `src/ExGrid.MudBlazor/`, `samples/ExGrid.Docs/Examples/Grid/Blotter/`, `samples/ExGrid.Docs/Showcases/Blotter.razor` |
| **§21.11** | Windows | Where DOM focus is after ↓, → and ↓ ↓ on `/sheet`, with and without a Focus. Read under NVDA, under Narrator, and with neither, on Chrome and Edge | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **WR-10** | Windows or Linux | Step 4's floor run: the layer-3 tests that run under a Wrapper, against a DemoHost built with the MudBlazor every Wrapper declares as its floor, on both hosts, in Chrome and in Edge; and beside it each Wrapper's layer-2 suite at every published version of its declared range. *(2026-10-12, decided with the user: a MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that version only; the floor run, layer 3 at 9.6.0, holds while its paths are unchanged.)* *(2026-10-12, decided with the user: an unlisted version is not asked for; the release check reports it as out of the check because it is unlisted.)* *(2026-10-12, decided with the user: a layer-2 run at such a version is a run of its own; its record goes in its own `verification/<date>-<name>/` directory, the run tags the commit it verified `verified/` followed by that directory's name, and WR-10's entry in `verification/by-hand.json` names that record besides the floor record. The release check reads every record the entry names: the floor record's paths hold since its commit, and each later record's tag names its commit.)* | `:(glob)src/*.MudBlazor/**`, `samples/ExGrid.DemoPages/` |
````

### 28. docs/definition-of-done.md

Old:

````text
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)*
````

New:

````text
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)* *(2026-10-12, decided with the user: that change goes in on `main`
  before `release/0.1` is cut, and the branch carries it from its cut.)* *(2026-10-12, decided with the
  user, ADR-0190 and ADR-0205: the sign-off is made against a release candidate, and the commit it was
  made against is that candidate's. The candidate's notes carry a Signed off block in a pending form of
  its own, saying that the sign-off is being made against it, with the blocks of the SHOULD criteria
  accepted failing and of what is out of scope, which the Docs Site's build checks, so that what the
  release's notes will say is read before its tag. The release workflow reads this `results.md` and that
  commit only on the stable tag.)*
````

### 29. CONTEXT.md

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
every SHOULD accepted failing and what was left out of its scope, and say what it supports. A Release
Candidate's carry the same blocks, to rehearse them, but say that its sign-off is being made, and open
with a note of their own
([ADR-0190](./docs/adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md),
[ADR-0202](./docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md),
[ADR-0205](./docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)).
````

### 30. CONTEXT.md

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
A prerelease, `-rc.N`, tagged to rehearse the version without a suffix that follows it: it publishes
through the same workflow and runs that version's checks but the two that read a sign-off, and its
Release Notes carry the blocks that version's will. The Definition of Done is signed off against a
Release Candidate, and the version that follows is its commit with only the sign-off's records and its
notes added
([ADR-0205](./docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)).
_Avoid_: beta (a beta runs none of the stable path's checks); "release" alone for it
````

### 31. AGENTS.md

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
  suffix unless its Release Notes name a sign-off, made against a release candidate, that the
  workflow can check (ADR-0042, ADR-0205). It refuses that version, and a release candidate
  (`-rc.N`), while a question to Excel is open or a row checked by hand does not hold on its record
  (ADR-0208). Every public member of a shipped project needs an XML doc comment — a missing one
  fails the build
````

### 32. docs/definition-of-done.md

Old:

````text
that date); its measurements are explicit tests as PV-21's are, and the variant with a whole-column
`SUM` on every row runs as `spikes/exsheet-sh19-whole-column`, outside the suite.)*
````

New:

````text
that date); its measurements are explicit tests as PV-21's are, and the variant with a whole-column
`SUM` on every row runs as `spikes/exsheet-sh19-whole-column`, outside the suite.)*

*(2026-10-12, the release tool's reader of this step; the rule above is unchanged.)* The run is read from
the `.trx` files Step 1 copied into `verification/<date>/`:

```sh
nix develop -c dotnet run --project tools/release/ExGrid.Release -- skips --trx verification/<date>
```

It refuses each test that failed, and each that did not run and is not one of those above, by its project
and its name. It refuses an allowed test left out otherwise than this step says: an explicit measurement
is written `NotRunnable` in its `.trx`, and a skip `NotExecuted` with the reason its test gives, which must
be that reason. It refuses a test project of `ExGrid.slnx` with no `.trx`, a `.trx` named for no test
project, and a `.trx` it cannot read whole. The list it holds the run to is written in its code as this
step names the tests, and a layer-1 test holds the two together and to the tests in the source. It names
`CsvClockTests`' skip with its reason; the `.trx` does not say the machine, so whoever signs off confirms
from the run that it was one where `TZ` cannot move the zone. Nor does it read CON-7's count of unobserved
exceptions, which is read from `layers-1-2.log`. The release workflow does not run Step 1, so it does not read
the run: the sign-off does.
````
