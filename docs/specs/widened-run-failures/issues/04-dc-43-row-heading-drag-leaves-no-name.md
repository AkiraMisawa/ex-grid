# 04: DC-43's Row Heading drag leaves the Name Box empty

Status: needs-triage
Roadmap: ExGrid, ExSheet — A Row Heading drag at the bottom edge always ends with its selection named.

**Seen:** `headings.spec.mjs:240`, "DC-43: a Row Heading drag at the bottom edge scrolls down, and never
sideways", Server host, Chrome shard 2/4 on `main` and Chrome and Edge shard 2/4 on the branch: the
Name Box expected "A3", received "" (line 257).

- [ ] Find whether the drag's selection is lost or read before it is answered.
- [ ] Fix it in the product, or where the test reads.
- [ ] The test passes 20 runs under the widened conditions.
