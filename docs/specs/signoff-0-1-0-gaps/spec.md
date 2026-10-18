# The gaps the 0.1.0 sign-off found

Status: ready-for-agent
Roadmap: internal

**Why:** while the evidence for each of the Definition of Done's criteria was read for the 0.1.0 sign-off
([`verification/2026-10-18-linux-signoff/results.md`](../../../verification/2026-10-18-linux-signoff/results.md)), some things were found that no criterion fails as written, but that a later
version should not carry. The user decided on 2026-10-18 to sign off 0.1.0 with them as they stood and to
make each a ticket after it. They are the tickets below. None of them changes an ADR by itself; a ticket
that turns out to need one stops and records it first (`docs/agents/issue-tracker.md`).

- [01](issues/01-tests-for-the-unasserted-clauses.md): a test for each of the 37 clauses no test asserts.
- [02](issues/02-the-core-refuses-a-command-it-disabled.md): the core runs a command a Chrome invokes
  without reading its `Enabled`.
- [03](issues/03-adr-0021-names-page-follow.md): ADR-0021 still refuses `scrollIntoView`, which ADR-0204
  allows, and `ex-grid.js` still says six permitted uses.
- [04](issues/04-pv-50-is-written-twice.md): PV-50 is written twice in the Definition of Done.
- [05](issues/05-the-popover-hold-mirrors-menukeys.md): the script's popover hold mirrors
  `MenuKeys.ResolveInColumnMenu`.
- [06](issues/06-layer-3-presses-what-a-mac-presses.md): two layer-3 gaps a Mac shows.
- [07](issues/07-escape-three-times-then-alt-h-h-loses-the-alt.md): a machine-speed KeyTips race CI found on this branch.
