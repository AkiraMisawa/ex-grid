# 14: Users and the change log

Status: ready-for-agent

**What to build:** the name picker's user on every request, and the record of every change (spec,
"What is recorded").

- **Every change is recorded** with the user, the time, and the state before and after: proxies'
  versions, Overrides, the Marked set, the tenor range, the schedule, lock take-overs and Markings.
- **A Change log view** on `/cds`, filterable by curve and by user.

**Blocked by:** 03

- [ ] Layer 1: each kind of change leaves one record with its before and after
- [ ] Layer 3: a change made on one host's page shows in the log on the other's
