# 05: Culture, number formats, dates, and the versioned Sheet Document

Status: ready-for-agent

**What to build:** A Sheet has a declared culture that decides how typed constants are read and how Values show.
Constants are recorded parsed. Dates are serials in Excel's 1900 system, including its
29 February 1900. Excel's number format codes and horizontal alignment are settable per cell.
Values show at most 15 significant digits, and `####` when they do not fit. The Sheet Document
carries a version and the culture, and a reader refuses a version it does not know.

**Blocked by:** 02

- [ ] `1,234.5` under `en-US` and `2026/9/26` under `ja-JP` become a number and a date (ADR-0048)
- [ ] A document saved under `en-US` and opened under `de-DE` shows the same numbers (ADR-0048)
- [ ] Date serials match Excel's around 1900-02-29 (ADR-0047)
- [ ] Format codes for numbers, percent, thousands and dates render as Excel's under the culture
- [ ] A number too wide for its column is `####` (ADR-0016)
- [ ] An unknown document version is refused, not guessed at (ADR-0048)

## Comments
