# Proposals from the release workflow's checks

From `claude/release-workflow-checks`, besides `adr-0042-rewrite.md` and `by-hand-rows-table.md`. A
background agent changes no ADR and not the Definition of Done. Each item below is text for the
orchestrator to commit, or a question it has to put to the user.

## 1. ADR-0110: a prerelease publishes no site once a stable version exists

ADR-0205's Consequences say: "ADR-0110: a prerelease publishes no site once a stable version exists".
ADR-0110 does not say it yet. The workflow now does it, so its "Publishing" section needs a dated
bullet after "So the release publishes the site, after the push":

> - *(2026-10-11, decided with the user,
>   [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md).)*
>   **Once a stable version exists, a prerelease's release publishes no site.**
>   - The site describes the latest stable version. A prerelease's notes are on its GitHub release,
>     and reach the site with the next stable publish.
>   - A stable version exists once the repository has a tag without a suffix.
>   - `release.yml`'s first job decides it, and says so. `docs.yml` refuses a prerelease's call that
>     comes anyway.
>   - The on-demand publish of `main` or a `lab/*` branch stays, under its banner.

## 2. The Definition of Done counts the test projects

§1's Evidence names "one `<test project>.trx` per test project … twelve in all".

- The solution already holds thirteen: `ExGrid.Docs.Generator.Tests` came with #77.
- This branch adds a fourteenth, `tests/ExGrid.Release.Tests`. It holds the release checks' tests.
  The release workflow's checks are code, so their tests run in layer 1 on every push, not first on
  a tag.
- Step 1's loop copies every project's `results.trx` without naming them, so it needs no change.

Proposed: "… and so on, one for each test project in `ExGrid.slnx`". A count goes stale with the next
project.

## 3. The "Signed off" block, as the release check reads it

The Docs Site's notes parser (`samples/ExGrid.Docs.Generator/ReleaseNotes.cs`) is the other reader of
this block. It is being extended on `claude/release-notes-stable-blocks`, for ADR-0205's other blocks.
The two readers must agree on what the block holds.

The release check (`tools/release/ExGrid.Release/SignOff.cs`) reads the text under `## Signed off`, up
to the next `## ` heading. It refuses the tag unless that text holds:

- **exactly one** `verification/<dir>/results.md` (the parser's own pattern);
- **exactly one** commit written in full, forty lowercase hex digits.

Layout is free: the path and the commit may sit in one paragraph or in separate bullets.

Two things for the parser's branch:

- **The parser's current check rejects a second bullet.** It requires *every* item under Signed off to
  match the `results.md` pattern. A block carrying ADR-0205's toolchain, OS and browser lines as bullets
  of their own would therefore fail it. The rule should move from "every item" to "the block as a
  whole".
- **The parser should also require the commit in full**, so a notes file the site accepts is one the
  release accepts. Suggested shape:

  ```
  ## Signed off

  Against `verification/2026-11-02-windows/results.md`, made at `0123456789abcdef0123456789abcdef01234567`.

  - Toolchain: .NET SDK 10.0.100, Node 22.
  - Windows 11 25H2; macOS 26.6; Ubuntu 24.04.
  - Chrome 154.0.8037.93; Edge 154.0.4258.48.
  ```

## 4. A record's commit must stay reachable

The by-hand check compares each row's paths between its record's commit and the commit signed off. It
can only compare a commit it has.

- **How the workflow finds the commit.** The release workflow's checkout fetches every branch and tag.
  A commit none of them holds is asked of the remote by its full name. GitHub serves it while any ref
  reaches it, its pull request's `refs/pull/N/head` among them.
- **Where the commits are today.** A Windows run's verified commit usually sits on a branch that is
  squash-merged, so `main` never holds it. The three seeded records' commits are held today by:
  - `claude/exsheet-windows-verify-11`;
  - `claude/exsheet-windows-verify-15`;
  - `claude/exsheet-windows-verify-16`;
  - and their feature branches.
- **What happens if they go.** If those branches are deleted, the check refuses with "cannot be
  checked", never passes.
- **For the user, if wanted:** a run could also tag its verified commit (for example
  `verified/2026-10-12-windows-17`), so that the record cannot lose it.

## 5. The eighteenth run and `rc.1`

A release candidate runs ADR-0208's by-hand check against its own commit, as the brief for this branch
asked ("every check a stable tag runs except the Signed off block and the structural check"). So:

- `rc.1` is refused until every row holds at its commit.
- An eighteenth run, if one is needed, is made at the commit `rc.1` will be tagged on, and filed before
  the tag.

ADR-0202's note of 2026-10-11 had the eighteenth run made "on `0.1.0-rc.1` itself". ADR-0208's
correction reads either way.

If the user wants a candidate published first and its rows run on the published candidate, the by-hand
half of a candidate's check has to report rather than refuse. That is a one-line change in
`Gate.Run`, and the user's call.

## 6. Where the rewrite lands

ADR-0205's Consequences and the Definition of Done's Step 7 say the check is added "in the change on the
release branch". This branch is cut from `main`. `adr-0042-rewrite.md` sets out the choice:

- land it on `main` before the cut, which needs a word in Step 7 and in ADR-0205;
- or hold it for `release/0.1`.
