# Report APIs may change, and remote reports recover their baseline

*(Decided with the user, 2026-10-06, continuation Q5 to Q7 on `claude/live-data-next`.
This settles the API-compatibility, remote Order Key and recovery questions left by
[ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md).
Built on `claude/live-data-next` and taken on `claude/live-data-best` on 2026-10-08, with the
sections of that day below: Details by Source Version, a delta checked against a digest, and a
throwing Order Key refused by name.)*

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
- A version-specific operation captures its report identity and target. Copy and the Selection
  Summary keep their existing full-range and version-correct semantics. An operation that cannot be
  answered under its captured version is refused; it is never silently rebound to the latest report.
- **Details are asked by the Source Version the cell was shown under** *(settled while merging,
  2026-10-08)*. They are the Source Records behind a cell — its row Items, column Items and Hidden
  Items — and a layout gesture does not change them. Asked by Report Version, as first built, an open
  Details tab stopped paging after two layout-only gestures (two Report Versions are kept) and said
  that the data had changed, which was untrue. `PivotReportMetadata.DetailsQuery` builds the engine's
  own `PivotDetailsQuery` from a detached row, and `PivotReportSource.DetailsAsync` answers it while the
  source holds that Source Version, as on `main`; the only refusal is `SourceVersionNotHeld`.

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

## A delta is checked before it is shown *(decided with the user, 2026-10-08)*

A delta is the server's word for which rows of the Window changed. A Consumer that builds deltas
itself — a server not running `LocalPivotReportSource`, a relay that coalesces or drops deltas, a client
delegate that turns push messages into deltas — can leave out a row whose shown values changed, most
easily a subtotal or a grand total. The row then keeps its old values beside new ones, and nothing says
so: a quiet wrongness of the kind principle 1 forbids.

**So every delta carries the digest of the Window it produces, and the client checks it before the result
becomes current.**
- `PivotReportUpdate.WindowDigest`: FNV-1a 64 over a canonical encoding of the Report Version, the Window's
  start, its row count and the report's, then every row of the Window in order — its role, Value Field,
  key Items, Row Path, labels and each value cell's shown text. Not the rows the delta carries: a digest
  over those would agree with the mistake. Nothing per process enters it (`GetHashCode` never does), so
  a server on CoreCLR and a browser on WebAssembly compute the same value.
- `LocalPivotReportSource` computes it over the whole Window after the delta, and on every complete
  Window.
- `PivotReportClient` applies the delta to the Window it holds, computes the digest of the result and
  compares. A missing or mismatching digest is recovered as a missing baseline is: a complete current
  Window is asked for, and nothing of the unverified delta is painted. If that fails, the last complete
  report stays as a Stale Report with Retry.
- A Consumer that builds deltas computes the digest over its whole resulting Window. The XML docs of
  `PivotReportUpdate`, `PivotReportRowChange` and `PivotReportSource.Fetch` say so.

Considered and not taken: **writing the obligation down and trusting it** — chosen first, then reversed
by the user the same day — and **sending complete Windows only**, which gives up the delta's saving for a
check that costs a hash of the painted rows.

## A registered Order Key that throws is refused by name *(2026-10-08)*

A server-registered Order Key that throws is refused across the wire as
`PivotReportRefusalKind.OrderKeyFailed`, naming its field, for a Window and for Items; before, the
exception was lost to the transport, and ADR-0060's named refusal with it.

## Verification

Section 32 judges this ADR with LV-26 to LV-28 (LV-23 to LV-25 on the Codex track). Tests include equal
Source Versions with different report settings, obsolete replies, missing baselines, failed recovery, an
empty visible delta whose off-screen selection values changed, Details paged across a collapse and a sort,
a delta missing a subtotal's change, a delta with no digest, the digest across a JSON round trip, and a
throwing Order Key over JSON. Local/server reference equality and complete-batch publication are LV-24;
ADR-0153 settled the row ownership, the incremental design and the full-refresh capability.
