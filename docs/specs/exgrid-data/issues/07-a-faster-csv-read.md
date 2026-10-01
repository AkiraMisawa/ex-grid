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

- [ ] The profile, and each change with its before and after, recorded in this ticket
- [ ] DA-17's CSV row measured again on CoreCLR and in a published WebAssembly build, and recorded
  beside the first measurement

## Comments
