# Report APIs may change, and remote reports recover their baseline

*(Decided with the user, 2026-10-06, continuation Q5 to Q7 on `claude/live-data-next`.
This settles the API-compatibility, remote Order Key and recovery questions left by
[ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md).
Implementation is pending.)*

The new server boundary returns report Windows rather than the Leaf Aggregates expected by the
old `PivotSource.Fetch` contract. The component also exposes a synchronous whole `PivotReport`,
which cannot describe a remote Window without downloading the entire report or pretending
unloaded rows are available. **The API may change to express the new boundary correctly.**
The user explicitly permits retiring the old API and using more appropriate names. Preserving
an old name, compatibility overload or parallel legacy path is not a requirement. This is
permission for the redesign, not a requirement to rename every unaffected member.

- Choose public names and types for the new responsibilities; do not silently change a Leaf
  Aggregate method to return report rows under the same documented contract.
- Separate the component's current report metadata and Window from direct engine access to a
  complete report. A synchronous whole-report facade is not required for the remote path.
- Update the repository's Consumers, examples, tests and Docs Site with the API. Document the
  replacement for retired entry points and the new source's obligations.
- Leaf Aggregates remain meaningful inside report computation and at a database-provider
  boundary. Removing the old component-facing transport does not require loading a SQL
  database's Source Records into a browser Snapshot.

## Remote Order Keys and explicit display settings

**A custom Order Key for a remote report is registered on the server and selected by an
identifier.** A C# delegate is not serialized across the connection. Local computation still
accepts the local function. The same function may be shared by application code on both hosts;
the library does not invent a second definition of its order.

Culture, number/date formats and the words the report paints are explicit report settings.
Culture never chooses the display language implicitly. The server and browser must refer to
the same settings when interpreting the report, a selection or a cell. An unrecognized or
unsupported ordering policy is refused by name, not replaced with label order. A function
that throws retains ADR-0060's named refusal; a null key retains its existing meaning.

This changes ADR-0060's earlier statement that an Order Key only runs on the component's side
and a server never sees it. Its ordering semantics are unchanged: the key orders Items, does
not merge them, and has the same tie and Blank/error rules.

## A delta names the report it advances

**Source Version alone is insufficient.** Two reports can use the same data under different
Layouts, collapse states, sorts or display settings. A **Report Version** identifies the
settled report state, including which Source Version and report settings it used. It is
distinct from the Row Sequence Version, which changes only when row keys or their order change.
The wire representation of that identity is an implementation choice.

- A delta identifies its baseline and resulting Report Version and belongs to the requested
  report/Window. It is applied atomically only to that baseline. An obsolete response never
  replaces the current request's result, even if its Source Version happens to match.
- A Source Version change with no changed displayed value still advances the report's version
  information. It must not leave Details or Selection Summary reading an older state merely
  because the Window's visible delta was empty.
- A version-specific operation captures its report identity and target. Copy, Selection Summary
  and Details keep their existing full-range and version-correct semantics. An operation that
  cannot be answered under its captured version is refused; it is never silently rebound to
  the latest report.

## Missing baselines recover automatically

**When a gap, reconnection or discarded server state makes a delta inapplicable, request a
complete current Window for the active report automatically.** The client does not guess how
to apply the delta or mix rows from different versions. The complete replacement, its extent
and its metadata become current together. Superseding the request still discards its answer.

If recovery fails or is refused, keep the last complete report identified as a Stale Report and
offer Retry, as ADR-0067 requires. Automatic display recovery is not permission to retry a
version-specific operation against different data. No requirement to keep every historical
report or to maintain a permanent connection is introduced. The Consumer still owns transport,
authentication and transport-level retry policy.

The alternative was to stop at Stale Report/Retry whenever a baseline was lost. The user chose
automatic Window recovery first, while preserving explicit failure when a complete answer
cannot be obtained.

## Verification and remaining design

Section 32 gains LV-23 to LV-25 for remote policies/settings, baseline recovery and
version-specific operations outside the Window. Tests include equal Source Versions with
different report settings, obsolete replies, missing baselines, failed recovery, and an empty
visible delta whose off-screen selection values changed. Local/server reference equality and
complete-batch publication remain LV-20.

The row/history ownership and incremental dependency design are still to be completed. Whether
an external provider unable to supply changes may opt into full refresh is a separate scope
decision; this ADR does not quietly make it an exception to incremental computation.
