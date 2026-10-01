# 07: A faster CSV read

Status: ready-for-agent

**What to build:** reading a CSV under a Schema faster, keeping every rule of ADR-0063. Nothing is
guessed, and a value that cannot be read is refused by its row and column. PV-21 asks for a million
rows in 4 s in a published WebAssembly build. It measured 14.6 s, with 955 ms on CoreCLR against
about 0.4 s (`verification/2026-10-01-linux-measure`). The time goes on reading, not on getting the
file in. The user asked for the reader to be optimised (2026-10-01).

- **Profile first, on CoreCLR and in the browser.** Find where the time goes: finding separators
  and line ends, decoding, parsing numbers and dates, the dictionary, the per-row checkpoint.
- **Change what the profile shows, one thing at a time**, each with its before and after measured.
  No change to the API or to what is read, refused or reported.
- **Every existing test stays green.** A change that alters a refusal's text or its row and column
  is a regression.

**Blocked by:** None

- [x] The profile, and each change with its before and after, recorded in this ticket
- [x] DA-17's CSV row measured again on CoreCLR and in a published WebAssembly build, and recorded
  beside the first measurement

## Comments

2026-10-01: done, measured on a machine other than the first record's (a 4-vCPU Firecracker guest,
Xeon at 2.1 GHz), which was shared with two other agents for most of the work. So every before was
measured again here, alternating with its after under the same conditions, as medians. The numbers
behind everything below are in `verification/2026-10-01-linux-measure-csv/metrics.json`.

**The result.** A million rows of /pivot-csv's trade export (89.7 MiB, 12 columns, keyed by Id):

| | Before (this machine) | After |
|---|---|---|
| /pivot-csv in a published WebAssembly build, through `InputFile` | 12.9 s | **4.0 s** (3.9 to 4.9) |
| CoreCLR, from memory (`MeasureTests`) | 692 ms | **491 ms** |
| CoreCLR, from a file in the page cache | 779 ms | **456 ms** |

PV-21's 4 s is met at the median on this machine, not with a margin and not by every read. The
first record's machine read the base 13% slower in the browser, so there it would likely sit a
little over 4 s. CoreCLR's tenth, about 0.4 s, is not met.

**The profile.** Taken with a scratch profiler and a scratch Blazor WebAssembly page outside the
repository, published as the DemoHost is: the read whole, the tokenizer alone, one declared column at
a time; `dotnet-trace`'s sampled thread time on CoreCLR; micro-benchmarks of the shapes a reader
could take; the yields; and the file through `InputFile` as /pivot-csv takes it. At the base:

- **CoreCLR, 692-755 ms.** Finding the fields 150 ms (the sampled split: `CsvTokenizer.Cut` 12%).
  The Id column, a million distinct texts, about 200 ms: each decoded, looked up and added to a
  growing `Dictionary<string, int>` (its lookup and insert 13% of the samples, collections 7%). Every
  other column 10-40 ms (`NumberText.Parse` 5%, and the field's way through `FieldReader.Read`,
  `CsvRead.Field` and `Commit` 10%). The Record Key up to about 30 ms.
- **The browser, 9.4-9.8 s of work, 11.1-12.9 s with the page's yields.** In the interpreter a call
  costs 16-24 ns (1-2 ns on CoreCLR), and the reader made about ten for every field, so each column
  cost 0.3-0.8 s. Finding the fields 1.2 s (a byte at a time costs 3.5 ns; sixteen at a time 1.9 ns
  a byte); the Id column 1.3 s; the Record Key 0.5 s; the yields 1.3-3.5 s.
- **Through `InputFile`**, each read is a call into JavaScript that slices the file and copies the
  slice into .NET: the file took 1.1-1.2 s to copy in the reader's 256 KiB reads, 0.4-0.5 s in 4 MiB
  ones. A read does not overlap the work that follows it: it completes only once the thread is free.
- **The per-batch checkpoint** costs nothing measurable (0.3% of CoreCLR's samples); in the scratch
  page, rendering the progress after every slice read within the noise of not rendering it.

**The changes**, each committed alone, with what it was measured to do (CoreCLR | the browser):

| # | Change | CoreCLR | Browser |
|---|---|---|---|
| 1 | The tokenizer compares sixteen bytes at a time, counts line breaks in quotes on the same pass, appends fields inline | tokenizer alone 144 → 102 ms | tokenizer alone 1,168 → 425 ms; read 10.1 → 8.1 s |
| 2 | A batch of 1,024 records is cut first, then read column by column, each column in one loop | 700 → 653 ms (within noise) | 8.5 → 6.4 s |
| 3 | A column appends a batch's values at once, the segment looked at once a run | 677 → 654 ms | 6.5 → 5.6 s |
| 4 | A number written the common way (a one-byte point and separator, eighteen digits) is read by a short path; anything else by `NumberText.Parse` | Notional alone 173 → 143 ms, Quantity 147 → 120 ms | Notional 1,077 → 800 ms; read 5.5 → 4.6 s |
| 5 | A Boolean's ASCII spellings are matched on an ASCII field's bytes | Confirmed alone 129 → 118 ms | 944 → 695 ms |
| 6 | The blank check compares lengths before bytes | unchanged | read 4.42 → 4.27 s |
| 7 | In UTF-8, texts are told apart by their bytes: a miss is a new text, appended without a lookup, and the dictionary is indexed by text when first asked | 595 → 537 ms; Id alone 336 → 266 ms | Id alone 1,694 → 1,465 ms; read 4.45 → 4.08 s |
| 8 | A Text key is indexed a slot per code; the indexer reads each chunk's keys from the segment's array | the key 33 → 7 ms | the key 323 → 13 ms; read 4.21 → 3.56 s |
| 9 | The helpers every field passes through are marked to be inlined, which the interpreter honours | unchanged | read 3.70 → 3.04 s |
| 10 | The stream is read 4 MiB at a time, no more than a stream of known length needs | unchanged | through `InputFile`, 4.5-4.7 → 3.6-3.7 s; the page 6.3 → 4.0 s with 9 |
| 11 | A text not seen before is hashed and probed once | Id alone 259 → 249 ms | Id alone 1,433 → 1,332 ms |
| 12 | Fields that end in one block of sixteen bytes take their stops from it | tokenizer alone 100 → 73 ms | tokenizer alone 408 → 355 ms |

What is read, refused and reported is unchanged: every test that was green stays green, and each
change that opened an edge has tests of its own, which also pass against the base build (below).

**Measured and not kept**, each within the noise or worse:

- Dates of a fixed layout read at known offsets: TradeDate alone 167 → 172 ms, 978 → 939 ms.
- A date column remembering the clock value of bytes it has read, as text remembers codes: a date's
  ten bytes miss the cache's packed key, and 145 → 191 ms, 900 → 1,012 ms.
- Dates trimmed as the numbers are, and one compiled format read directly: 147 → 152 ms, 865 → 811
  ms against a control that moved 7%.

**Found on the way, and fixed:** a column that marked a Blank early sized its bits to the room it had
then, and sealing took as many words as the rows. A CSV whose Integer, Decimal, Double, Date or
Boolean column held a Blank in its first 256 rows, then enough rows to grow, failed with an
`ArgumentOutOfRangeException` instead of loading, on the base build too; so did a
`SnapshotColumnsBuilder` used the same way.

**A trade made by change 7:** a UTF-8 CSV's dictionary is indexed by text only once asked, by
`TextDictionary.TryGetCode` or the first Change Batch, once for every version that shares it. For the
million Ids that first lookup costs about 74 ms on CoreCLR and 0.23-0.28 s in the browser, which the
read no longer pays. A Snapshot read from a CSV and then kept live pays it with its first batch.

**A proposal, not made:** the browser's yield between slices is `Task.Delay(1)`, as ticket 01
specifies, documented on `SnapshotLoadOptions.Yield`, and shared by ExPivot's `PivotSlicing`.
Measured: 4.3-4.4 ms a yield, painting about two frames a slice; `Task.Yield()` costs 0.4-0.6 ms and
paints one frame a slice. Over a read of about 100 slices that is about 0.4 s, a tenth of PV-21's
target. It changes a default the family shares, so it is the user's to decide.

**What is left**, at the head: on CoreCLR, the Id column about 100 ms (a million strings, and a cache
entry each), finding the fields about 75 ms, each other column 15-45 ms. In the browser, the read's work
is about 2.9 s, the yields about 0.4 s, the file's reads about 0.4 s. The read's long tasks (108-221
ms, as before) were not traced; the young and full collections of the million strings are the likely
source.

**Tests added:** `CsvBoundaryTests` (fields of every length across blocks and buffers, short fields
in one block, line breaks in quotes at every offset, the three faults at every offset, very long
fields), `CsvBatchTests` (which refusal a batch throws, across batches; an early Blank in every kind),
`NumberTextTests` (the short path against `Parse`, and 5,000 random numbers), `CsvShortPathTests`
(Booleans against .NET's comparison, compiled dates against `TryParseExact`, dates that repeat and
not), `CsvTextTests` (100,000 distinct texts, a Change Batch on a CSV's Snapshot, lookups from eight
threads), `CsvKeyTests` (a key carried twice or Blank, wherever it falls, from a CSV and from
columns). The CSV-level ones pass against the base build too; those of internal parts it lacks do not
run there.

**Runs:** `dotnet build ExGrid.slnx` with no warning; `dotnet test ExGrid.slnx`, all 11 suites green
(5,546 passed, 8 skipped); `pivot-csv.spec.mjs` on both hosts, 12 of 12 each, the console empty.
