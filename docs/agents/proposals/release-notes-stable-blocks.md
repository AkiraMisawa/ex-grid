# Proposal: a release's blocks in the Release Notes

From the background agents on `claude/release-notes-stable-blocks`, 2026-10-11 and 2026-10-12. The text
ADR-0190, ADR-0205, the Definition of Done and `CONTEXT.md` need is an apply list,
`docs/agents/proposals/release-notes-stable-blocks-apply.md`. This file holds what the user may want to
answer: two questions the decision of 2026-10-12 leaves open, and the points the implementation settled
that no decision states.

## Why

ADR-0190 lists two blocks a version without a suffix carries, Signed off and SHOULD criteria accepted
failing, and its "What refuses a mistake" names only those two. ADR-0202 added Known limitations, after
Behaviour changes. ADR-0205 added Out of scope for this version and Supported, and said what Signed off
holds. On 2026-10-12 the user decided that a release candidate's notes carry the same blocks, checked as a
stable version's are, and that an alpha's or a beta's are refused if they carry one (ADR-0190's and
ADR-0205's notes of that date).

The Docs Site's notes parser (`samples/ExGrid.Docs.Generator/ReleaseNotes.cs`) now does exactly that:

- a version without a suffix and an `-rc.N` carry all five blocks, in ADR-0190's order with Known
  limitations after Behaviour changes and the release's own four after the products;
- a candidate's blocks are read as a release's: the Record linked at the candidate's own tag, the full
  commit, one record and one commit in the block, Supported agreeing with the header lines, and, in the
  build given `-p:DocsVersion=` for it, the SHOULD criteria and §21 items read against the Definition of
  Done;
- any other suffix, an alpha's, a beta's or another, refuses each of the five at its heading.

`tests/ExGrid.Docs.Generator.Tests/ReleaseNotesTests.cs` holds a test per kind: a candidate with every block
builds, a candidate missing one is refused, a candidate's block out of form is refused as a release's is, an
alpha or a beta carrying one is refused, and a release missing one is refused.

## Questions the decision of 2026-10-12 leaves open

1. **Does the release workflow read a candidate's Signed off block as it reads a stable version's?** The
   reader is on `claude/release-workflow-checks` (`tools/release/ExGrid.Release/SignOff.cs`, called from
   `Gate.cs`), not on this branch. That branch, written the day before, runs every check of a stable tag on
   a candidate "except the two that read a sign-off": the Signed off block and the structural check (the
   tag is X plus only `verification/` and `docs/releases/`). Its ADR-0042 rewrite says the same, and that a
   candidate "is still not signed off". Read literally, "checked as a stable version's are" runs both on a
   candidate too. Then:
   - each candidate is tagged on its X plus records and notes, with a `results.md` that names X at the tag:
     the sign-off record is begun at X before the candidate is tagged, and a fix is a new X and a new
     candidate, as ADR-0205 already says;
   - ADR-0202's note that the eighteenth run is made "on `0.1.0-rc.1` itself" reads as a run at X, which
     `rc.1` publishes.

   A patch for that branch, which makes its reader do this, was prepared and tested apart from it (the
   report names its file); it is not on any branch. The alternative is the branch as it stands: the build
   checks a candidate's blocks (this branch), and the workflow reads a sign-off only on a stable tag. Either
   way, the sibling's ADR-0042 rewrite needs a word for the candidate's notes.
2. **The prerelease note above a candidate's Signed off block.** A candidate is a prerelease, so its GitHub
   release and its section on the site begin with the prerelease note (ADR-0190), which says "the
   Definition of Done has not been signed off". The candidate's notes now carry a block headed "Signed off"
   naming a record and a commit. Both are true of a candidate whose sign-off is under way, but a reader sees
   them one above the other. Options: leave it; give a candidate a note of its own, held beside
   `prerelease-note.md`; or word the one note so that it fits both ("…has not been signed off; a release
   candidate's Signed off block names the sign-off it rehearses").

## Points the implementation settled that no decision states

Each is built as described. The user may want another answer.

1. **Where the four blocks of ADR-0205 stand.** ADR-0202 places Known limitations after Behaviour
   changes. ADR-0205 lists Signed off, SHOULD criteria accepted failing, Out of scope and Supported in
   that order, but not where. They are placed after the products, where the parser already put the
   first two.
2. **What a release candidate is, by its name.** `-rc.N`, with N from 1 and no leading zero, as the release
   workflow on `claude/release-workflow-checks` reads a tag. A suffix that begins `rc` in any case and is not
   that (`-rc1`, `-RC.1`, `-rc.0`, `-rc`, `-rc.1.2`) is refused at the file's first line: its notes would be
   read as a beta's, which carry none of the blocks a candidate rehearses. Any other suffix, `-alpha`,
   `-beta` or `-preview.1`, is a prerelease whose notes refuse the blocks.
3. **The Definition of Done is read only by the build that publishes the version**, the one given
   `-p:DocsVersion=<version>`, which `ci.yml` runs on the tag before anything is pushed: a candidate's tag as
   a release's. Every build reads the blocks' form. A version already published stays true of its day,
   however the Definition of Done is restated after it. Without this, `release/0.1` merging back into
   `main` (ADR-0205) would fail `main`'s build if `main` had restated a criterion the notes name during the
   days of the sign-off. The cost is that the author of the cut sees a wrong ID only by building with
   `-p:DocsVersion=<version>`. The cut's steps in ADR-0190 could say so.
4. **The Signed off lines have labels**, so that the release workflow reads the commit and the record
   from fixed places. The sibling branch's reader takes the one `verification/…/results.md` and the one
   40-character commit anywhere in the block. The two agree: the parser also refuses a second commit or
   a second record anywhere in the block, a link's address included, so notes that build are notes the
   workflow can read. It reads the block as written, as the workflow does, on the Record line as well:
   a second record in a link's address there is refused, and so is the record's own path written with an
   escape or an entity (`verification/2026\-11\-02/results.md`), which renders as the record but which
   the workflow would read as a second one.
5. **A known limitation names its products in bold, and links a page of the site**, not only an address
   outside it. ADR-0202 says it links "the page that says it in full", and its "Where 0.1.0 says what it
   does not do" names the product's Docs Site page. The products let the page's product filter show the
   limitations of a product, as it shows the breaking changes.
6. **The chips follow ADR-0190 as written.** "New in" comes from a Highlight or an Added line, and "Changed
   in" from a Breaking, Behaviour or Fixed line. The site had given "Changed in" to every section any line
   of the newest notes linked. With a release, that would have marked every page a known limitation links
   as changed. Lines under the five blocks, and a product's paragraphs, now give no chip. No published
   version's notes change what they show. Beta.1's and beta.2's linked lines are all Highlights or Added,
   Fixed, Breaking or Behaviour lines.

## What it changes nowhere else

- `release.yml` on this branch needs no change. Its render step ("The release's text") passes the version's
  file through after the prerelease note for any version with a suffix, a candidate's blocks included, as
  GitHub renders them, with site routes made whole and ADRs linked. Its admission is still `main`'s, which
  refuses a version without a suffix; ADR-0205's workflow is `claude/release-workflow-checks`.
- No `docs/releases/<version>.md` for a candidate or a release is added.
