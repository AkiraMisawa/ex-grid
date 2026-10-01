# 97: A typed date operand reads in the culture, never invariant first

Status: ready-for-agent

**What to build:** ADR-0023's note of 2026-10-01, a quiet fault ticket 96 found. A date the user types in the
condition form is still read invariant first. Under en-GB, `05/01/2026` becomes 1 May, which is quietly wrong
(principle 1). Reopened dates are fine because they use the ISO form.

**Blocked by:** None (can start immediately)

- [ ] **`ReadOperand` reads a date as ticket 96 reads a number:**
  - ticket 94's ISO forms exactly;
  - anything else in the culture the form shows dates in, alone;
  - a text that reads two ways, or not at all, refused by name with the same `OperandRefusal` words.
- [ ] **Both Chromes' panels**, as for numbers.
- [ ] **Layer 1 and 2** under en-US, en-GB, de-DE and ja-JP, named after ADR-0023 and principle 1. Cover:
  - `05/01/2026` under en-GB is 5 January;
  - the same text under en-US is 1 May;
  - a two-digit year;
  - a time with a date.
