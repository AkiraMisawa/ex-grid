# 04: The Record Key and the Change Batch

Status: ready-for-agent

**What to build:**

- **The Record Key**: one declared column, Text or Integer. Two records under one key are refused,
  naming the key.
- **The Change Batch**: the records added, the records changed (by key), and the keys removed. It
  is applied whole into the next Snapshot, or refused whole.
- **The next Snapshot** shares every segment the batch did not touch.
  - A changed record keeps its place in the order, and an added one goes at the end.
  - A dictionary only grows, so a code means the same text in every version.
- **A Snapshot without a key** takes only batches that add.
- **What a reader can fold in:** it is handed what a batch removed and what it added, so that it can
  update what it computed rather than start again (ADR-0066).

**Blocked by:** 01

- [ ] DA-11: every clause a named test
- [ ] DA-12: applying 1,000 changes to 1,000,000 records allocates in proportion to the changes
- [ ] DA-2: the Snapshot before a batch reads exactly as before

## Comments
