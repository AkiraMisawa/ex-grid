# 07: Bespoke Proxies — the records and the list

Status: ready-for-agent

**What to build:** the Bespoke Proxy store and the list on the Bespoke tab (spec, "Bespoke
Proxies").

- **The record:** ticker, reference entity name, ccy, seniority, doc clause, recovery and one Entry
  per tenor, stored as written in row 3 under the two heading rows. It also holds the template and
  parameters it was stamped from, and its versions.
- **Identity:** `ticker.ccy.seniority.doc_clause`, unique, and only `A–Z a–z 0–9 . _ -`. Anything
  else is refused by name.
- **The Linked Tables `Cds` and `Rec`:** shaped as the spec says, rebuilt from each snapshot after
  Auto-quote and Overrides, and pushed whole.
- **Evaluation on the server:** one headless `Sheet` per proxy, per snapshot.
- **The list:** an ExGrid with the Formula Bar shown read-only, showing a Point's Formula. Errors
  are Cell State Error, changes carry the Change Highlight, and a Point is faint unless every cell
  of its Read Set is an observed `Cds` row at its own tenor.
- **Seed proxies** to start from: `max(UST, average of named AAA sovereigns)`, `a × reference + b`,
  and a proxy reading `CDX.NA.IG.OTR`.

**Blocked by:** 01, 02, 03

- [ ] Layer 1: the faint rule on observed, Auto-quoted and other-tenor reads; an identity refused;
  a proxy's Values match an ExSheet showing the same Entries
- [ ] Layer 3: the Formula Bar shows the Formula of the selected Point, on both hosts
