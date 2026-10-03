# 11: Marking

Status: ready-for-agent

**What to build:** the Marking job in DemoApi, and its controls on `/cds` (spec, "Marking").

- **Triggers:** the schedule, set on the page in its stated time zone, and **Mark now** with a
  reason.
- **One Marking at a time:**
  - a manual request carries the state it reviewed, and is refused if that state has moved;
  - it is also refused if the same state is already marked;
  - each refusal names who and when.
- **The input** is the latest snapshot held at the time.
- **Any error stops it.** A scheduled run that meets an error writes nothing, records why and raises
  the banner. It is not retried. A missed time is recorded at the next start.
- **The output:**
  - the three CSV files, with the header, order, `n/a`, bp and decimals the spec gives;
  - the manifest;
  - each file written under a temporary name and renamed, and `latest` replaced last.
- **The output root** is configured, and the page shows it.

**Blocked by:** 10

- [ ] Layer 1: the same snapshot and versions give the same bytes; an error writes nothing; a moved
  state and a repeated state are refused; `latest` names only a complete folder
- [ ] Layer 3: Mark now with a reason writes the folder and then `latest`, on both hosts
