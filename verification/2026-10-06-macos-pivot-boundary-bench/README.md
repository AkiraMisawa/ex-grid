# Pivot computation boundary: restricted incremental A/B experiment

Measured 2026-10-06 on an Apple M4 Pro, 24 GiB RAM, macOS arm64; Chrome
154.0.8037.98, .NET SDK 10.0.203 / net10.0, Node 24.14.1. Release, stock non-AOT WASM
runtime (the publish log records that the optional `wasm-tools` optimization was absent).
All **672 measured actions** completed; 180 independent fresh-fold/sequence checks and the
shipped-engine oracle passed. No unexpected browser console or runtime message occurred in
the successful smoke or measurement run. No production decision is made here.

## Results

**Initial load, incremental updates, expand, and sort have different outcomes.** At 400,000
leaves with 150 ms added RTT, A → B was **2,960 → 217 ms initially**, **177 → 175 ms for the
1,000-record update affecting the Window**, **28 → 178 ms for expand**, and **296 → 211 ms for
sort**. These are seven-sample medians of data/action request to the second animation frame,
not predictions for completed ExPivot. In particular, an already-incremental A does not
rebuild all leaves on each ordinary update.

The complete min/median/max summaries are in [raw/summary.json](raw/summary.json), the
96-row overview in [tables.md](tables.md), and every measured phase in
[raw/measurements.json](raw/measurements.json). All times below are actual measurements;
150 ms is added by the proxy, not added to a zero-latency number afterward. Zero means no
added latency, not a physically zero-cost loopback connection.

### Initial data request

All cases have **1,000,000 Source Records** already generated/folded on the server before
the timed request. The generated/folded source took medians of 31.7, 28.2, and 25.8 ms for
the three shapes (ranges 24.4–41.2, 23.5–38.8, and 24.3–35.1 ms); this common preparation is
excluded from both initial request timings.

| Leaves / report rows | A, no added RTT | B, no added RTT | A, 150 ms RTT | B, 150 ms RTT | A gzip body | B gzip body |
|---:|---:|---:|---:|---:|---:|---:|
| 2,976 / 3,001 | 33.3 ms | 12.6 ms | 207.0 ms | 176.5 ms | 41,966 B | 744 B |
| 100,000 / 100,101 | 728.5 ms | 16.2 ms | 898.1 ms | 196.6 ms | 1,226,290 B | 635 B |
| 400,000 / 400,101 | 2,814.3 ms | 36.3 ms | 2,960.3 ms | 217.3 ms | 4,853,763 B | 637 B |

For 400,000 leaves without added latency, A's median includes **1,667.2 ms parsing UTF-8
JSON**, **70.3 ms building the shared report**, and **1,059.2 ms requesting/materializing the
body**, which itself includes server time and browser decompression. B parses its 40 rows in
0.6 ms. Its same report construction runs server-side in a least-of-seven **6.14 ms**.
The complete server request consumes a least-of-seven 90.32 ms process CPU for A versus
24.77 ms for B: B avoids serializing/gzipping the entire Leaf set, even though it computes
the report. A's initial raw body is 24,648,989 bytes; B's is 2,535 bytes.

**Encoding matters.** These row-wise benchmark payloads are not the shipped columnar
`PivotJson` contract. An untimed equivalent parallel-array encoding cut raw bytes from
24,648,989 to 12,249,026 for 400,000 leaves. Using the **same Python zlib 1.3.2 level 1 encoder**
for both forms, gzip changed from 3,218,981 to 2,719,782 bytes. This comparison must not use
4,853,763 as its row-wise baseline: that is the actual .NET HTTP compressor's different
output. At 2,976 leaves the same-encoder pair is 31,840 → 26,924 bytes, and at 100,000 it is
790,944 → 658,572. Values were round-tripped exactly. No columnar parse time was measured,
so the A timing above is not a lower bound or an intrinsic cost of the Leaf boundary. See
[raw/encoding-sizes.json](raw/encoding-sizes.json).

### Ordinary value updates

Representative 400,000-leaf shape; first Window contains the subtotal and 39 leaves of
group 0. It does **not** contain the Grand Total. `Visible 1000` affects exactly one leaf in
this Window and 999 outside its group. The Window therefore changes in exactly two numeric
cells (subtotal + leaf); off-screen cases change zero displayed cells.

| Update | A / B median, no added RTT | A / B median, 150 ms RTT | A raw / gzip bytes | B raw / gzip bytes | B rows/cells sent |
|---|---:|---:|---:|---:|---:|
| Visible 1 | 30.6 / 30.9 ms | 162.7 / 178.7 ms | 124 / 125 | 263 / 218 | 2 / 2 |
| Off-screen 1 | 30.9 / 30.4 ms | 163.7 / 175.4 ms | 127 / 129 | 101 / 98 | 0 / 0 |
| Visible 1000 | 30.7 / 30.6 ms | 176.9 / 174.7 ms | 27,097 / 7,969 | 263 / 217 | 2 / 2 |
| Off-screen 1000 | 30.6 / 30.1 ms | 180.3 / 177.8 ms | 27,100 / 7,965 | 101 / 98 | 0 / 0 |

B reduces large-batch payloads here, but **visible 1 is larger under B**: two report rows
cost more than one leaf replacement. Zero rows still has a version/envelope payload.
For visible 1000 at no added latency, A spends a median 3.2 ms parsing and 0.4 ms in shared
incremental computation. B spends a least-of-seven 0.032 ms on report update + Window diff,
and a median 0.1 ms parsing in the browser. Complete server process CPU minima are 0.179 ms
(A) and 0.111 ms (B). Both completed .NET renders have 0.4 ms medians. Browser stopwatch
values around 0.0–0.1 ms are resolution-limited; 0 does not mean no work.

At 150 ms RTT, the minimum/maximum frame times for visible 1000 overlap: **161.6–191.3 ms
(A), 161.4–177.3 ms (B)**. The 2.2 ms median difference is not evidence of a universal
update-latency win. The practical measured distinction is reduced client parsing and bytes
for broad off-screen batches, while both request/response paths pay an RTT. A real push
transport was not measured.

### Layout interactions

| Leaves | Expand A / B, 150 ms RTT | Sort A / B, no added RTT | Sort A / B, 150 ms RTT |
|---:|---:|---:|---:|
| 2,976 | 30.8 / 179.4 ms | 31.5 / 31.1 ms | 30.7 / 179.8 ms |
| 100,000 | 27.6 / 178.7 ms | 64.2 / 30.6 ms | 80.5 / 179.6 ms |
| 400,000 | 27.5 / 177.8 ms | 297.0 / 30.5 ms | 295.5 / 211.2 ms |

A expands from its local report. B asks the server and pays the round trip. For the large
sort, CoreCLR's faster execution outweighs that round trip in this restricted model; it
does not for the smaller shapes. At 400,000 leaves/no added RTT the shared sort has a
median 283.6 ms WASM compute time versus a server least-of-seven 20.22 ms. This is the same
`Array.Sort`-based algorithm, not an optimized server algorithm compared to a full client
rebuild. Group order is fixed; only each group's child Items are value-sorted.

### Client memory

Reported `GC.GetTotalMemory(false)` after explicit full collections, medians over 14
end-of-sequence readings per mode/cardinality (seven at each RTT), including the runtime:

| Leaves | A WASM managed memory | B WASM managed memory |
|---:|---:|---:|
| 2,976 | 4.93 MiB | 4.70 MiB |
| 100,000 | 13.47 MiB | 4.69 MiB |
| 400,000 | 39.79 MiB | 4.69 MiB |

A holds the complete specialized report; B holds its 40 rows. These are **not peak memory,
RSS, WebAssembly linear-memory capacity, or the complete browser memory**. Server GC
readings and CDP heap readings remain in raw data, but server readings include request/
serialization pools with variable retention and are not isolated model footprints. They
must not be presented as the per-session cost of B. B still holds the full source and report
on the server. The raw `managedAllocatedBytes` counter often reported zero in operations
known to allocate; it is retained as diagnostic output and is **not a usable allocation
measurement**.

These narrow current-state readings do not cover ExPivot's real paint/history/cache roots,
Highlights, Details, or long-running source retention. The separately investigated product
OOM cannot be concluded from this prototype. The existing product's source-record and
high-Leaf cases must remain separate from this boundary comparison.

## Question

For an otherwise identical incremental decimal Sum model, what changes when the complete
Leaf Aggregates and report model live in WebAssembly (A), versus when the same model lives on
the source server and the browser receives only its requested 40-row Window (B)? This is a
boundary experiment. Neither proposed production implementation exists. Comparing today's
full ExPivot rebuild with a new incremental server would not answer this question.

## Restricted common semantics

The original, disposable `Shared/Model.cs` runs unchanged in CoreCLR and WebAssembly. Two text
row fields, one exact `decimal` Sum, Compact-style group subtotals at the top, and a Grand Total
at the end. A value update replaces only its leaf Row, affected group Rows and Grand Total.
Unchanged Rows keep their instances. Stable Items; no insert/delete. A sort orders the inner
Items by Sum descending, with Item ID as the tie breaker. Expand/collapse affects the first
group only. The private numeric/index arrays are mutable; this is not the product's ownership
or immutability design.

One million Source Records are generated and folded before the timed initial request. The
three separate cardinalities are 2,976 leaves / 24 groups (3,001 report rows), 100,000 / 100
(100,101 rows), and 400,000 / 100 (400,101 rows). Record count and leaf cardinality are different
axes; this is not a claim that a million records normally creates 400,000 leaves. The raw
records stay on the HTTP server in both modes. This experiment does not measure a client
holding a million raw Source Records.

The first Window contains group 0's subtotal and 39 of its leaves, and **no Grand Total**.
For `visible 1`, leaf 0 changes, so two displayed numeric cells change. For `visible 1000`,
leaf 0 plus 999 different leaves outside group 0 change. `off-screen` changes only different
leaves outside group 0. Thus off-screen changes update the server's Grand Total but do not
change any cell in this Window. Both batch sizes change one Source Record per touched leaf.

A sends the whole Leaf set initially, then replacement values for touched leaves; the client
updates the report and extracts 40 rows. B builds the same report on the server and sends
40 rows initially; ordinary updates send only changed Window rows (2 or 0 in these cases).
B's expand/sort sends a new 40-row Window; A performs the same action locally. Both render the
same keyed Blazor Row component, memoized by immutable Row reference, one label and one value
per row. This is not ExGrid's complete renderer: selection, highlights, Summary, copying,
column measurement, gestures, Details, and Chrome are deliberately absent.

## Measurement and validation

`run.sh` publishes Release WebAssembly and CoreCLR, disables CoreCLR tiered compilation, and
uses an actual HTTP proxy with symmetrical 75 ms one-way delay for the 150 ms RTT case. It
runs headless installed Chrome and seven repetitions. Application/framework boot happens at
RTT 0 before measurements. A small complete workload warms both modes first. Generation time
is recorded independently. Every action includes client request through two animation frames
after Blazor's completed render; this is a frame opportunity, not a compositor paint timestamp.

Server Source/Report/serialization/gzip phases are synchronous wall-clock elapsed time;
`serverProcessCpuMs` is a separate process CPU delta, with its native clock's resolution. Use
the least of seven for isolated CoreCLR steps, plus median/min/range for browser timings.
Client parse, shared-model compute, 40-row application/extraction, and completed Blazor render
are separate. `networkToBodyMs` includes request/response, server time, decompression, and
HttpClient's UTF-8 body materialization. Phases are not assumed to sum to frame time.

`rawBytes` is UTF-8 JSON body size; `gzipBytes` is the actual `CompressionLevel.Fastest` gzip
body served over HTTP, including its gzip wrapper and excluding HTTP headers/TCP overhead.
It is a compressed payload count, not total packet bytes. The custom row-wise JSON is not
current `PivotJson`'s columnar Leaf Aggregate transport; future encodings can change both
absolute payload sizes and parsing allocations. Local A actions have no HTTP body.
`sentRows` and `sentLeaves` describe payload objects, and `changedPaintedRows` counts actual
displayed value/label changes. For ordinary B updates the number of changed numeric cells is
the number of sent rows. Structural replacements also include their labels.

Every action's Window checksum must match across A/B and RTTs. Each operation in repetition 0
also checks against a fresh fold of all raw records. Every full sequence checks the complete
incremental report against a fresh fold (A in the browser; B on the server). The untimed
`Oracle` program checks all eight phases against the shipped `PivotEngine.Compute` for 5,952
records / 2,976 leaves, comparing every row label and exact decimal value. Correctness checks
and explicit GC happen outside timed actions.

`clientMemory.managedLiveBytes` is WebAssembly managed memory after forced GC;
`serverMemory.managedLiveBytes` is CoreCLR managed memory after forced GC. They are separate
heaps and are not RSS or peaks. `runtimeMemory` is CDP's JavaScript heap reporting; it is not
the complete WebAssembly linear memory, renderer RSS, or total browser process memory. None
of these alone proves bounded memory under the product's history/cache rules.

## Limits

This tests a WebAssembly client calling an HTTP source. With Blazor Server, report computation
already runs on CoreCLR and the browser receives circuit render diffs; these numbers do not
apply to that hosting mode unchanged.

This answers boundary costs for one favorable incremental Sum shape, not the speed or memory
safety of finished ExPivot. It excludes aggregation parts for other functions, decimal
overflow, floating-point accumulation order, percentages whose dependency fan-out can be
broad, changes to Items/labels/fields, structural deltas, schema negotiation, lost-baseline
recovery, cancellation, transport push, Source Version-aware gestures, and shared server
sessions. Updates use HTTP request/response in both modes, so an RTT is paid by both; this is
not a push-latency measurement. A local expand/sort versus B's remote expand/sort is intentional.
B shifts full report memory/CPU to the server; it does not remove that work or solve a large
server's memory budget. General retention/OOM work is a separate measurement.

No Enterprise source was copied. No `src/`, ADR, glossary, completion criterion, or public API
is changed. No Docs Site update applies because this is only a disposable experiment.

## Reproduction

Reserve the exclusive browser/build/timing slot, then run from the repository root:

```sh
bash verification/2026-10-06-macos-pivot-boundary-bench/run.sh
```

Requires nix, installed Chrome, npm registry access, and free ports 5894/5895/5896. The harness
has its own published hosts and is not in `ExGrid.slnx`. `BOUNDARY_LEAVES`, `BOUNDARY_RTTS`,
`BOUNDARY_REPEATS`, and `BOUNDARY_OUTPUT` narrow a diagnostic run. No full suite is invoked.
Do not archive generated `.boundary-publish`, `bin`, `obj`, or `node_modules`.
After timings, `encoding_sizes.py` can use the running host to reshape the identical initial
A payload into parallel JSON arrays and report raw/gzip sizes only. This optional reference
is neither a browser timing nor the shipped `PivotJson` format.
