# What to verify on Windows, fifth run

Status: done — run at 3fa16a9 on 2026-09-29 (`verification/2026-09-29-windows-excel-5/`,
`verification/2026-09-29-windows-5/`). What it settled is in ADR-0012 and ADR-0033. *(Set from the
records on 2026-10-10.)*

For the Claude Code session on the Windows desktop of the earlier runs. The fourth run's method,
tools and advance authorisation still apply: read [`verify-on-windows-4.md`](verify-on-windows-4.md)
first. Reuse the scripts in `verification/2026-09-29-windows-4/` and
`verification/2026-09-29-windows-excel-4/`, the equality run's `equality.ps1`, and `oracle.ps1`.
**Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
`docs/definition-of-done.md`.

## Setup

- Fetch `claude/exsheet-start-8cx3v1`. Branch **`claude/exsheet-windows-verify-5`** from its tip,
  and record that tip as the verified commit of every part. If the tip moves during the run, do not
  merge it in.
- Build, then run layers 1–2 in WSL. Record the counts.
- Run `npm ci` afresh in the Windows copy of `tests/ExGrid.Browser`.
- **The user has authorised this run in advance**, as for the fourth: real keys and mouse to Excel
  and the browsers, and `Set-Culture` for as long as the oracle needs. Restore the format afterwards
  and check it byte for byte against an export taken first. **Do not stop to ask.** Say "starting"
  before the first input and "finished" after the last.
- **Correction to the equality run.** A case that names no culture is an en-US case. The oracle
  blocks it under any other regional format, so switch to en-US for Part A.

## Part A — the ordering operators (ADR-0047, "Settled by the equality run")

Comparisons now read both sides at 15 significant digits. Ordering has not been observed. Run
`oracle.ps1 -Id ARITH-146,…,ARITH-152` under en-US, **through COM and with `-Keys`**, then
`-Update` for the same ids.

The new cases are ARITH-150 `=1+4.4E-15>1` (the engine says FALSE), ARITH-151 `=9+3E-14>9` (TRUE)
and ARITH-152 `=1+4.8E-15<=1` (TRUE). ARITH-146..149 are asked again as a check.

Results go to `verification/<date>-windows-excel-5/results.md`.

## Part B — what changed in the browser, by eye and by hand

On both hosts and both browsers, at the display's 150%:

1. **The grid's scrollbar** (ADR-0029, third correction). Open `/wide` and `/features`, both with
   the built-in Chrome. The grid now draws its own 12px bar with a grey thumb. Record whether the
   thumb is visible at rest, and whether it can be dragged. Take one screenshot per browser. The
   fourth run found the gutter blank.
2. **Copy and paste after an edit** (fixed in `b65b211`). In `/sheet`, type `5` in E1 and press
   Enter. Then click B2, press Ctrl+C, click F6 and press Ctrl+V. Record whether F6 shows B2's
   value. Do the same after Tab, and after Escape, in place of the Enter.
3. **Plain text pasted over a range** (ADR-0014, amended). Copy `=A1` from Notepad, select B2:C3
   in `/sheet`, and press Ctrl+V. The value should land in B2 alone, and the Selection should become
   B2. Close only the Notepad tab you opened.

## Part C — layer 3, both browsers, both hosts, at 150%

The whole suite on both hosts, as in the fourth run, with `chrome-150`. Record every failure with
its criterion ID, and rerun each failure three times.

Then run these with `--repeat-each=10` on the host and project each failed on in the fourth run.
Record the count, and for each failure the trace's last frame and the received value:

- **DC-19/DC-34**, the mud Chrome case, on Server `chrome`. The fourth run failed it 5 of 10. It was
  fixed as a product race (a press in the text ahead of the core's caret placement).
- **WR-7**, the Drawer toggle, on WebAssembly and Server, `chrome` and `msedge`. It failed in every
  full suite, and was fixed as a test measuring during the Drawer's slide.
- **`sheet-vs-excel` item 23** on Server `chrome`. Fixed as a click made before the row deletion had
  landed.
- **WR-6** on Server `chrome` and `msedge`. Not changed, so this is measured only.
- **FN-6a** on WebAssembly `chrome`. Fixed as a poll that waited on a cleared attribute.

## Finishing

Commit everything to `claude/exsheet-windows-verify-5` and push. The last message lists every
disagreement and failure, each with the ticket, ADR or criterion it belongs to. It proposes nothing
on the user's behalf.
