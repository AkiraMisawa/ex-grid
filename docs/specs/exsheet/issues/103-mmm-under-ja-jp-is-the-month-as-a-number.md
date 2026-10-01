# 103: `mmm` under ja-JP is the month as a number

Status: ready-for-agent

**What to build:** the fourteenth Windows run's cases 10 and 11. ADR-0071, "What the fourteenth Windows
run settled", `mmm` under ja-JP:

- **Under ja-JP, `mmm` shows the month as a number with no leading zero, in every code.** 5 January
  2026 shows `05-1-26` in `dd-mmm-yy`, `5-1-26` in `d-mmm-yy`, `1 5, 2026` in `mmm d, yyyy`, and
  `2026/1/05` in `yyyy/mmm/dd`. `mmmm` shows `1月`. These are Windows' month names; .NET's `1月` for
  `mmm` is ICU's. Today only built-in 15 shows the number.
- **A code typed into Format Cells is a built-in only when it spells that built-in's code under the
  Sheet's culture.** Under ja-JP `dd-mmm-yy` is built-in 15; `d-mmm-yy` is a code of its own.

**Blocked by:** None (can start immediately)

- [ ] **The four codes and `mmmm` above show as the run read them under ja-JP**, on Linux (ICU) and on
      Windows alike. Do it as `NumberFormat.AbbreviatedMonthNamesOf` did `Sept`: one place, named for
      the platform data it replaces.
- [ ] **en-GB and en-US are unchanged** (`05-Jan-26`, `5-Jan-26`).
- [ ] **Typed into Format Cells under ja-JP:** `dd-mmm-yy` is recorded as built-in 15, and `d-mmm-yy`
      as its own code. The same rule holds for the other built-ins ExSheet localises.
- [ ] **Layer 1/2**, and the Linux check in Docker (`mcr.microsoft.com/dotnet/sdk:10.0`) as for `Sept`.

## Comments
