# Windows Excel observations for ExSheet's next functions

This run answers the user's request of 2026-10-03: investigate **every Observe function** in
`docs/specs/exsheet-functions/spec.md`, plus the Decide functions **LET, RAND and RANDBETWEEN**,
on the real Windows Excel, and publish the research on a dedicated branch.

The baseline is `origin/main` at `c3ddb7df5476e550763659760d49736d879c9296`. There are 24 Observe
functions and three additional functions. The governing admission rule is ADR-0047; LET's grammar
and random functions' determinism still require decisions. ADR-0124's volatile recalculation and
ADR-0125's arrays are relevant. This directory records observations and implementation questions;
it does not admit a function, revise an ADR, or claim that an engine implementation has passed.

See [research.md](research.md) for findings and the decisions still needed, and
[case-index.md](case-index.md) for a row for each case. The JSON files retain typed fixtures,
Excel's output and every recorded state, including exact numeric bits and random samples.

## Reproduce on Windows

Requires desktop Excel 365 with Formula2, Windows PowerShell 5.1 or later, and Python 3 for the
independent evidence audit. Copy this directory to a Windows-accessible location. The PowerShell
script creates its own Excel application; it never attaches to a user's instance. Each case has a
fresh workbook, cloned from an unchanged fixture template or newly created. A template contains
only fixture cells, never a check Formula. Fixture Values are read back in every case, including
clones. A 40-case SUMIF pilot was also run with independently populated workbooks; its complete
input readbacks and outputs matched the cloned-fixture pilot exactly. Those pilot records are
retained as `pilot-independent-results.json` and `pilot-template-results.json`. No workbook is
saved. The script closes its workbooks and quits its own Excel.

```powershell
$repositoryPath = 'C:\src\ex-grid' # The baseline checkout; adjust this path.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\probe.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\probe.ps1 -Cases .\followup-cases.json -Out .\followup-results.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\probe.ps1 -Cases .\repeat-cases.json -Out .\repeat-results.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\probe.ps1 -Cases .\additional-cases.json -Out .\additional-results.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\keyboard-probe.ps1 -Oracle "$repositoryPath\tests\ExSheet.Engine.Tests\ExcelOracle\oracle.ps1"
python3 .\summarize.py --repo "$repositoryPath"
```

Run the commands sequentially. `-Function SUMIF` narrows a diagnostic run; write it to a different
`-Out` so that it does not replace the complete evidence. The audit expects the complete runs. The keyboard wrapper uses the unchanged Oracle from the
baseline checkout; pass its path when copying only this evidence directory. It is specific to the
en-GB English Excel UI, and does not change the machine's regional format. The hash audit permits
Git's LF/CRLF checkout difference; it rejects other source/input changes.
A probe exception aborts the run, records the exception and script location, and gives a nonzero
exit. A returned Excel Error Value and a COM refusal of an invalid Formula are recorded separately
from infrastructure failures. A refusal does not establish the UI's dialog text or keyboard behaviour.

## Method and limits

- The machine stays **en-GB**; regional settings are not changed. Formula2 accepts invariant
  English syntax with comma arguments and dot decimals. Culture-sensitive criteria observations
  therefore apply to this environment; other cultures were not observed.
- Fixtures are explicit `Value2` numbers, text or booleans, or Formula2 formulas. Numeric strings
  are stored as Text, never converted into numbers. Blank cells are cleared. The recorded fixture
  readback proves these distinctions. Numbers and text use the same unmodified Excel type system
  that the engine has to reproduce; format-inference behaviour is outside this run. LET grammar
  is additionally asked by real SendInput in `keyboard-probe.ps1`, because Formula2 can bypass UI name restrictions.
- PowerShell 5.1 caches COM setter types. The script uses the existing oracle's IDispatch reflection
  technique for Value2 and NumberFormat. Trial runs that hit this binder problem, or a rejected
  bulk-array assignment, or the default hashtable equating distinct Unicode fixture text, were
  discarded; they are not part of the observations. The fixture is read as one rectangle, but written through the tested per-cell setters.
- Excel's 1900 date system is selected explicitly; iterative circular-reference calculation is off.
  Cases run with manual calculation and an explicit Calculate, except the named automatic/manual
  lifecycle steps. Each numeric output has both round-trip decimal text and the IEEE 754 bits.
  Use those fields: PowerShell 5.1's JSON numeric spelling alone can lose a low bit.
- Each financial result is a measured Excel output, not an inferred iteration algorithm. A small
  residual or a mathematically correct root is insufficient for the catalogue's 15-significant-digit
  promise. Multiple roots and failed guesses remain important acceptance cases.
- Finite random samples do not prove a distribution, generator, seed, independence, or all possible
  outputs. The record establishes the samples and observed recalculation/coercion behaviour only.
- This is a function research run, not a browser or component change. No product source or existing
  engine corpus is changed. The evidence audit checks scope, inputs, bits, repeated outputs and
  random/recalculation invariants; a .NET build or a browser run would not verify these observations.
- No numbered Windows instruction, ADR, ticket or Sheet Document version is allocated in this run.
  The existing numbering reservations are untouched.
