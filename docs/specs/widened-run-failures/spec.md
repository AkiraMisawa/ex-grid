# What the widened run fails on `main`

Status: needs-triage
Roadmap: internal

**Why:** ticket 02 of the Input Order (`docs/specs/input-order/issues/02-the-core-takes-input-one-at-a-time-by-number.md`)
passed by being no worse than `main` under the Hunt and the widened run (`hunt.yml`: the Server host with a
150 ms round-trip floor and a CPU throttle of 4). The user decided on 2026-10-19 that what fails on `main` as
well is not that ticket's to fix, and that each such failure gets a ticket of its own. These are those
tickets. Under ADR-0220, a red that passes on a re-run is a defect, so each is one.

The runs compared are `main` at `7740be3` (run 39097590704) and the Input Order branch at `6e20a96` (run
39298709434). A failure counts as shared when it failed in both. Every message quoted below was read from
a job log, and the logs keep only their last 4,999 lines.

- [01](issues/01-pivot-csv-accepts-no-file.md): ExPivot's CSV page accepts no file under the widened run.
- [02](issues/02-number-format-box-after-keys-typed-at-once.md): the Number Format box reads `General`
  after Format Cells or the KeyTips are driven by keys typed at once.
- [03](issues/03-ri-9-floating-inspector-does-not-move.md): RI-9's floating inspector is not moved by its
  title bar.
- [04](issues/04-dc-43-row-heading-drag-leaves-no-name.md): DC-43's Row Heading drag leaves the Name Box
  empty.
- [05](issues/05-the-10mb-paste-stalls.md): the ~10 MB paste never completes on the Server host.
- [06](issues/06-a-paste-stream-that-fails-is-a-refused-paste.md): a paste stream that fails to read
  escapes as an unhandled circuit exception.
- [07](issues/07-sh-45-border-tab-medium-does-not-check.md): SH-45's Border tab does not check Medium on
  Edge.
- [08](issues/08-two-instances-share-a-root-id.md): two instances on one page render the same root id.

Failed on `main` only, and passed on the branch's shards that ran them: the column-width group
(format-cells-mud:377, sheet-toolbar:189/227/265, format-cells:356, format-keys:176, declarations:2533,
sheet-vs-excel:788), DC-44, SR-2e, PF-7, sizing:167/232/273, sheet-vs-excel:251/451, gestures:22/44/75/129,
UX-21 (on CON-6, see 06) and circuit:497. They get no ticket now. One that fails again on `main` after the
Input Order merged gets one then.
