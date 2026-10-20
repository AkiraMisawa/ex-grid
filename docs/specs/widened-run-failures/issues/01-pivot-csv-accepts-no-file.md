# 01: ExPivot's CSV page accepts no file under the widened run

Status: done
Roadmap: ExPivot — A CSV file chosen on a slow connection is always read, refused or offered a Schema.

**Seen:** `pivot-csv.spec.mjs`, the six ADR-0064 tests at lines 112, 136, 167, 220, 252 and 285, under
both Chromes, on the Server host with Chrome and Edge, shard 2/4, in both runs; and :252 (MudBlazor) once
in 920 runs of the Hunt on the branch. They fail on what the page never shows:

- :112: `#csv-refusal` expected "Row 3, column 'Quantity': '12.5' is not an integer (line 4).",
  received "".
- :136: `#csv-status` expected "Reading trades-large.csv under the declared Schema…", received
  "Choose a file, or a sample.".
- :167, :220, :252, :285: the `#csv-suggestion` heading ("The Schema suggested for …") is not found.

**Inferred:** no file was ever taken by the page. The status still reads its first text.

- [x] Find where the file is lost: the input's change, the stream, or the page's handler.
- [x] Fix it in the product, or where the test reads if the page is right.
- [x] The six tests pass 20 runs each under the widened conditions.

**Found (2026-10-20):** the input's change, before anything heard it. Blazor's `InputFile` attaches
its script to the element in its first `OnAfterRenderAsync`, which on the Server host runs only once
the browser has acknowledged the render: a round trip after the input is drawn. `setInputFiles` does
not wait for anything, so under a 150 ms round trip a file was sometimes set on an input that nothing
listened to yet, and the change was lost with nothing said. Read on the element as the test chose:
`_blazorInputFileNextFileId` was still `undefined` once in eight runs. Not the product's: ExPivot
reads whatever stream it is handed.

**Fixed:** the page's two inputs are `AttachedInputFile`, an `InputFile` that says when its script is
attached, and stay disabled until then; the spec waits for them to be enabled before choosing a file.
