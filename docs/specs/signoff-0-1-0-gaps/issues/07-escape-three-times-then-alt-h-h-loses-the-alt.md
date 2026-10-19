# 07: Escape three times, then Alt, H, H, typed at once, can lose the Alt

Status: done

**Why:** a red that passes on a re-run is a defect (AGENTS.md, layer 3). CI's run 39166277639 on PR #176
failed `sheet-toolbar.spec.mjs`'s "Escape three times in a list a KeyTip opened, then Alt, H, H, all typed
at once, opens the next list, the only one (builtin)" on the WebAssembly host in Chrome: no list opened,
and the cell held an edit reading `hh`. The Alt's release never started the KeyTips, so both letters
were gated against the grid. The trace's snapshots show the Home row's letters after the first Escape and
none after the second, where the rows' letters were expected. The Mac's run 18 reported a failure of the
same shape under the MudBlazor Chrome in Edge. The test passed about 250 times locally on Linux:
alone, with the CPU throttled sixfold, within its `describe` block, and with its whole file three times.
The race is in product code that 0.1.0 shipped (ADR-0100, ADR-0050 item 16), not in the test.

**What:** find the ordering that ends the KeyTips at the second Escape, or that loses the held Alt
afterwards, and make the outcome the one the keys have when typed slowly (spine rule 6). Start from the
trace kept in that run's `layer3-records-chrome-4` artifact. One suspect is a focus taken back to the
grid's root after the band already holds the keyboard. That would end the KeyTips through
`OnFocusOut`.

**Done:** the Alt went down while the Escapes before it were held by the grid's key gate. They took the
keyboard to the toolbar and back to the Sheet, and only then was the Alt released. The key gate forgot
an Alt still down when DOM focus left the grid, even while its press was still held. It also never
noted one that the toolbar heard on the keyboard's way back. Either way, the release was dropped as no
lone Alt's (`ex-grid.js`, `onFocusLeft` and the drain). Both now pair the release with its press.
`sheet-toolbar.spec.mjs`'s ticket 07 test presses the Alt in the same burst as the Escapes and releases
it once the keyboard is back. It failed before the fix under both Chromes.
