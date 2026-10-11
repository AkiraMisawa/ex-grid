# The Roadmap is read from the tickets and the ADRs, and carries no dates

*(Decided with the user on 2026-10-11, while restructuring the Docs Site after mudblazor.com and
ag-grid.com. It adds a page to
[ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md).)*

A Consumer deciding whether to put the family in front of people who read money off it asks two
questions the Docs Site cannot answer today: what is coming, and what will never come. The answers
exist, but in places a Consumer does not read:

- **the tickets** under `docs/specs/*/issues/`, by their `Status:` line;
- **the scope tables** of [ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)
  (ExSheet) and [ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)
  (ExPivot), whose columns are "First version", "Later" and "Why later";
- **the refusals** recorded in ADRs: right-to-left layout (ADR-0031), touch and phones (ADR-0045),
  Safari and Firefox (ADR-0017), the grid executing sort, filter or grouping (ADR-0001);
- **the Definition of Done's §21.11**, which lists what is reserved and the trigger that settles each.

ag-grid's Roadmap groups items by quarter and marks each planned or shipped, with the caveat that
"timing may shift"; its Pipeline lists tickets scheduled for a named version. MudBlazor fetches a
`ROADMAP.md` when the page loads. Both make promises of time, and here **a date on a web page would be
a decision that no ADR made** — and, if missed, the site quietly wrong about the product.

## The decision

- **The Docs Site has a Roadmap at `/roadmap`, generated at build time** from the repository it was
  built from. Nothing is fetched (ADR-0110).
- **It has no dates, quarters or target versions.** Work is ordered by the state of its decision, never
  by a calendar. The page's lead says so, and that a listed item is not a promise: a later decision can
  drop it.
- **It says what it is as of**: "As of 0.1.0-beta.2" on a site published from a tag, or "As of main at
  `<commit>`" / "As of `<branch>` at `<commit>`" otherwise, with a link to the tickets on `main`, which may
  be newer.
- **Six sections, in this order, each read from one source:**
  1. **On main, not in a version yet** — the Change Notes ([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
     On a site published from a tag it is empty by construction, and says that the version holds all
     of `main`.
  2. **Decided, ready to build** — tickets whose `Status:` is `ready-for-agent` or `ready-for-human`.
  3. **Waiting on a decision** — tickets whose `Status:` is `needs-triage` or `needs-info`.
  4. **Later** — the "Later" rows of ADR-0046's and ADR-0059's scope tables, each with its "Why later".
  5. **Toward the next stable version** — written by hand, every row citing the ADR or the Definition of
     Done section that requires it. The Definition of Done's §21.11 and `docs/implementation-status.md`
     are linked at the ref, not copied, so that an item §21.11 records with no trigger (Find All and
     Replace, the column band) never reads as planned.
  6. **Not planned** — the refused rows of the same scope tables, and a hand-written list in which every
     line cites the ADR that refused it. It answers an evaluator's fastest question: "will it ever do
     X?"
- **Every ticket that is not `done` or `wontfix` carries a Roadmap line** beside its `Status:`:
  `Roadmap: <Products> — <one sentence a Consumer understands>`, or `Roadmap: internal` for a ticket a
  Consumer would not care about (a verification run, a process change). Internal tickets are left off
  the page, and so are CI, test and tooling work.
- **`done` joins the ticket roles** in `docs/agents/issue-tracker.md`. 154 tickets already use it.
- **A ticket that is done leaves the Roadmap.** It appears again only in the Release Notes of the
  version that shipped it.
- **Product filter chips** select ExGrid, ExSheet, ExPivot or Data. Each row links its ticket and its
  ADR at the commit the site was built from.

### What refuses a mistake

The Docs Site's build refuses, as an error naming the file, the line and the fix:

- an open ticket with no Roadmap line;
- a product outside the four;
- a cited ADR with no file;
- a scope-table row without exactly one mark;
- a Roadmap sentence, or a hand-written row, that contains a date, a quarter or a version number.

## Considered options

- **ag-grid's Roadmap by quarter, and its Pipeline by version.** Each date is a promise the ADRs have
  not made. A missed one leaves the site wrong about the product, which is the first thing this family
  refuses to be.
- **MudBlazor's `ROADMAP.md`, fetched when the page loads.** A server call (ADR-0110), and a page that
  goes empty when the fetch fails.
- **A Roadmap written by hand, in one file, checked against the tickets.** The build could check that
  each row cites something real, but not that a ticket is missing. Generated from the tickets, a
  missing row cannot happen.
- **No Roadmap; link the Definition of Done and the status document.** Both are written for whoever
  builds the family, and neither says "will not do".

## Consequences

- ADR-0110 gains the page, in its Project group.
- `CONTEXT.md` gains **Roadmap**.
- The open tickets gain their Roadmap lines in the change that builds the page, and
  `docs/agents/issue-tracker.md` gains the line and the `done` role.
- Records the page reads must be true first: ADR-0046's table still listed spilled arrays as Later,
  although [ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md) shipped
  them in `0.1.0-beta.2`. It is amended in the same change.
- The Roadmap goes stale between publishes. Its "as of" stamp keeps it honest, not current.
