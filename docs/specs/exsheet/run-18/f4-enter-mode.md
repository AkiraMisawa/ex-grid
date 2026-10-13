# The eighteenth Windows run: F4 and Excel's Enter mode

Questions gathered from `claude/run17cf-f4-enter-mode` (the seventeenth run's Part C and the readings of
2026-10-12, R1) for the next Windows run, to be folded into its spec when it is written.

Each is asked of Excel 16 on Windows, with the Selection, the Name Box, the text and the caret or
selection in it read through UI Automation after each key, and no key sent to read them:

1. **F2 in the Formula Bar, then F4.** `=A1+B2` typed into the bar, F2 (Enter mode), F4: a caret at
   the end, or `$B$2` selected?
2. **Which caret selects.** In the bar: `=A1+B2`, ←, → (the caret back at the end), F4 — selected, or
   a caret? `=A1+B2`, ←, ←, ←, `0` typed (`=A10+B2`, the caret after `A10`), F4 — selected, or a
   caret? A press into the bar's text right after `A1` of an existing `=A1+B2`, F4?
3. **Shift+Home while pointing at a range.** In D10, `=`, ↓, Shift+↓ (`=D11:D12`), Shift+Home: does
   the outline become `A11:D12`, `A11:D11`, or something else?
4. **F4 in Edit mode on a Reference with a Sheet qualifier.** `=Sheet2!A1` with F2 first, the caret
   after `A1`, F4: does the selection start at `Sheet2!` or at `$A$1`?
5. **The Primary Modifier's arrows where a Reference can go.** In D10, `=` then Ctrl+↓: does Excel
   point (and where), commit, or do nothing? And Ctrl+Shift+↓?
6. **The grey ground right after `=`.** In D10, `=`, ↓, and `=`, ↓, ↓: is the pointed Reference on the
   grey ground, and what does UI Automation give as the bar's selection? (The eighth run's case 31
   saw no grey and an empty selection; R1 (vi) pictured the grey.)
7. **The directions R1 (vii) did not ask.** `abc` typed into D10, each from a fresh entry: Shift+↑,
   Ctrl+←, Ctrl+↑, Ctrl+↓, Ctrl+Shift+←, Ctrl+Shift+↑ and Ctrl+Shift+↓ — committed, and the
   Selection moved or extended as with no edit open? (Built by generalisation of →, ↓ and ←.)
8. **`=` then Shift+Home, with no key between.** R1 read it with a Ctrl+C before Shift+Home and a
   Ctrl+C after it (`A10:D10`); the text, its selection and the mode were not read: `=A10:D10`, in
   Point?
9. **Chords held behind F2, on Windows and macOS** (ExGrid, ED-2, ADR-0010; added 2026-10-13). A held
   chord in an edit is now applied as the field would apply it, read from the field's value and
   selection. `features.spec.mjs`'s two ED-2 tests ("in Caret Ctrl+← and Ctrl+→ move the caret by a
   word …" on both hosts, and "with a 150 ms round trip, End, Ctrl+←, Shift+End …" on the Server
   host) have run only in the container's Chromium on Linux. On Windows, in Chrome and Edge: do both
   pass, with a word move forward landing at the next word's start? On macOS (at rc, on the user's
   Mac): Option+←/→ move by a word and ⌘+←/→ go to the text's ends, held or not, as the field does.
