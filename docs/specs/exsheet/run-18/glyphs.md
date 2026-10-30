# The eighteenth Windows run: FN-12e's glyph widths and paintings

Gathered from `claude/run17gh-glyphs` (the seventeenth run's Part G and the Japanese-display records of 2026-10-12),
to be folded into the eighteenth run's spec. This branch changes the estimate's tables
(`dotnet/src/ExGrid/Columns/DefaultGlyphWidths.cs`, `dotnet/src/ExGrid.MudBlazor/RobotoGlyphTable.cs`) and `tests/GlyphWidths/measure.mjs`
after the seventeenth run's commit `8436e45e`, all within FN-12e's indexed paths (`verification/by-hand.json`), so both
halves of the row are asked again:

1. **The paintings**, on Windows, in Chrome and Edge, on WebAssembly and on the Server host:
   - `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)";
   - `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and
     no more fit (ADR-0016)".
2. **The measurement**, the four commands on the Windows desk under an English display (`tests/GlyphWidths/README.md`,
   "Measuring again"). Under a Japanese display, `system-ui`'s two records again only if the corpus or the tool
   changes before the run; Roboto under a Japanese display is not asked for `0.1.0` (the orchestrator's ruling of
   2026-10-13: a web font's own widths do not follow the display language, and the English records cover them).
3. **Other display languages** are a Known limitation of `0.1.0` (decided with the user on 2026-10-12), not asked.
