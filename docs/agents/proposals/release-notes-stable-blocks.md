# Proposal: a release's blocks in the Release Notes

From the background agent on `claude/release-notes-stable-blocks`, 2026-10-11. It asks for text in
ADR-0190 and in `CONTEXT.md`, which a background agent does not write, and for the user's word on the
points below that no decision states and the implementation had to settle.

## Why

ADR-0190 lists two blocks a version without a suffix carries, Signed off and SHOULD criteria accepted
failing, and its "What refuses a mistake" names only those two. ADR-0202 added Known limitations, after
Behaviour changes. ADR-0205 added Out of scope for this version and Supported, and said what Signed off
holds. Both say that they amend ADR-0190, and ADR-0190's text has not been amended.

The Docs Site's notes parser (`samples/ExGrid.Docs.Generator/ReleaseNotes.cs`) now requires the five
blocks of a version without a suffix, in the order below, and refuses each of them on a prerelease. The
text below is what ADR-0190 would say to match it.

## Text for ADR-0190

In "What a version says", replace the bullet that begins "A stable version's notes carry two more
blocks" with:

> - **A stable version's notes carry five more blocks**, which the build requires of a version without a
>   suffix and refuses on a prerelease. A release's headings are, in this order: Highlights, Breaking
>   changes, Behaviour changes, **Known limitations**, ExGrid, ExSheet, ExPivot, Data, **Signed off**,
>   **SHOULD criteria accepted failing**, **Out of scope for this version** and **Supported**.
>   - **Known limitations**
>     ([ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)):
>     what the version does not do, or "None.". Each line begins with the products it reaches in bold, as
>     a Breaking line does, names the limitation in a sentence, and links the page of the site that says
>     it in full.
>   - **Signed off** (the Definition of Done's Step 7,
>     [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)):
>     five lines, each beginning with its label in bold, in this order:
>     - `Record:` the `verification/<date>/results.md` the sign-off was made against, linked at the
>       version's tag (`…/blob/v<version>/verification/<date>/results.md`). The record is committed after
>       X, so it exists at the tag and not at X;
>     - `Commit:` the commit X, its 40 characters in a code span. The release workflow reads it;
>     - `Toolchain:`, `OS:` and `Browsers:`, the versions the sign-off ran with.
>
>     The block names one record and one commit, so that the release workflow reads exactly those.
>   - **SHOULD criteria accepted failing** (the Definition of Done's §1): each line begins with the
>     criterion's ID in bold, or several separated by commas, and gives the written reason it was accepted;
>     or "None.". The ID is a SHOULD of the Definition of Done: a MUST that fails stops the release, and an
>     OBSERVATIONAL number never fails.
>   - **Out of scope for this version** (Step 7, ADR-0205): each line names the Definition of Done's §21
>     item accepted in writing as out of the version's scope, as §21.11, and links where it was accepted;
>     or "None.".
>   - **Supported** (ADR-0205): four lines, each beginning with its label in bold, in this order:
>     `Hosts:`, `Browsers:`, `Target framework:` and `Packages:`. The target framework and the packages
>     are those its `Target:` and `Packages:` header lines name.
>
>   *(Amended 2026-10-11, decided with the user in ADR-0202 and ADR-0205: the decision first listed two
>   blocks, Signed off and SHOULD criteria accepted failing. ADR-0202 added Known limitations, and ADR-0205
>   added the last two and said what Signed off holds.)*

In "What refuses a mistake", replace "a version without a suffix whose notes lack the Signed off and
SHOULD blocks." with:

> - a version without a suffix whose notes lack one of a release's five blocks, or hold one out of its form;
>   a prerelease whose notes carry one;
> - in the build that publishes a release, a SHOULD criterion accepted failing that is not a SHOULD of the
>   Definition of Done, and a §21 item out of scope that §21 does not have.

## Text for `CONTEXT.md`

In **Release Notes**, replace "a stable version's name its sign-off and every SHOULD accepted failing" with:

> a stable version's also say what it does not do (its Known limitations), name its sign-off, every SHOULD
> accepted failing and what was left out of its scope, and say what it supports

and add ADR-0202 and ADR-0205 beside ADR-0190.

## Points the implementation settled that no decision states

Each is built as described. The user may want another answer.

1. **Where the four blocks of ADR-0205 stand.** ADR-0202 places Known limitations after Behaviour
   changes. ADR-0205 lists Signed off, SHOULD criteria accepted failing, Out of scope and Supported in
   that order, but not where. They are placed after the products, where the parser already put the
   first two.
2. **A prerelease refuses all five, Known limitations included.** ADR-0205 says only that the parser
   requires them of a version without a suffix. The refusal follows from ADR-0190's fixed headings: a
   prerelease's list does not have them, and a prerelease is not signed off. **It has a consequence for
   the release candidates:** an `-rc.N` is a prerelease, so it carries none of the five, and the GitHub
   release text of these blocks is first published by the stable tag. The candidates rehearse everything
   else on the stable path. If the user wants the candidates to rehearse these blocks too, the
   alternative is to let a release candidate carry Known limitations and Supported, but never Signed off,
   SHOULD criteria accepted failing or Out of scope, which only a sign-off can write.
3. **The Definition of Done is read only by the build that publishes the release**, the one given
   `-p:DocsVersion=<version>`, which `ci.yml` runs on the tag before anything is pushed. Every build reads
   the blocks' form. A version already published stays true of its day, however the Definition of Done is
   restated after it. Without this, `release/0.1` merging back into `main` (ADR-0205) would fail
   `main`'s build if `main` had restated a criterion the notes name during the days of the sign-off. The
   cost is that the author of the cut sees a wrong ID only by building with
   `-p:DocsVersion=<version>`. The cut's steps in ADR-0190 could say so.
4. **The Signed off lines have labels**, so that the release workflow reads the commit and the record
   from fixed places. The sibling branch's reader (`claude/release-workflow-checks`,
   `tools/release/ExGrid.Release/SignOff.cs`) takes the one `verification/…/results.md` and the one
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

- `release.yml`'s render step ("The release's text") needs no change. It passes the version's file through,
  with no prerelease note for a version without a suffix. It was run over a release's notes: every
  block comes out as GitHub renders it, site routes made whole and ADRs linked. Marking the GitHub release
  a release rather than a prerelease is ADR-0205's workflow change, which this branch does not touch.
- No `docs/releases/<version>.md` without a suffix is added.
