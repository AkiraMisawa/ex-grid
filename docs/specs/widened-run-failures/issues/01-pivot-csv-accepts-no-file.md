# 01: ExPivot's CSV page accepts no file under the widened run

Status: needs-triage
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

- [ ] Find where the file is lost: the input's change, the stream, or the page's handler.
- [ ] Fix it in the product, or where the test reads if the page is right.
- [ ] The six tests pass 20 runs each under the widened conditions.
