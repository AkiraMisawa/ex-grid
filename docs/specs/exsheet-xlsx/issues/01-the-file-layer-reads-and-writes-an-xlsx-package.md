# 01: The file layer reads and writes an `.xlsx` package

Status: ready-for-agent
Roadmap: Data — `ExGrid.Data.Xlsx` reads and writes the cells of an `.xlsx` file as a stream, with no dependency.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §1, §2 and §9.

- [ ] A new shipped package `ExGrid.Data.Xlsx` (refs `ExGrid.Data` only), packed and smoke-tested
- [ ] Reads the workbook part, the worksheets in order with their names and the one shown, shared strings, and each cell's place, type, value and Formula text, streamed
- [ ] Reads the styles part as far as ADR-0330 §4 carries (Number Formats, fonts, fills, borders, alignment) and the theme's colours
- [ ] Writes the same back
- [ ] Refuses by name: not a zip, `.xlsm`/`.xlsb`/`.xls`, an encrypted file, a DTD, beyond the size or cell-count limit
- [ ] The default limits set from a measurement recorded in the ticket
- [ ] Layer 1 tests, named for ADR-0330; Change Note

**Blocked by:** None (can start immediately).
